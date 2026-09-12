// LiveOpsSystem.Players.cs — группы игрока, персональные оверрайды (читы,
// флаги), одноразовые выдачи и благодарности для титров. Всё это ставится
// в дашборде LiveOps на странице «Игроки», без обновления билда.
//
// Контракт (см. Documentation~/LiveOps_ServerContract.md):
//   POST /api/player/state           {project, playerId}
//        → {groups: [..], overrides: [{k, v}], grants: [{id, type, key, amount, payload, note}]}
//   POST /api/player/grants/confirm  {ids: [..]}
//   GET  /api/player/credits?project= → {names: [..]}
//
// Опрашивается при старте, при уточнении id (Steam) и при каждом
// периодическом FetchAsync. Группы и оверрайды кэшируются для оффлайна;
// оверрайды ложатся ПОВЕРХ A/B-варианта и уходят подписчикам через тот же
// VariantChanged — игре не нужно ничего нового, чтобы читы заработали.
//
// Выдачи: игра регистрирует обработчик на тип (RegisterGrantHandler), тот
// применяет выдачу и возвращает true — система подтверждает её серверу, и
// больше она не приходит. Обработчик вернул false (сейв ещё не загружен) —
// попробуем в следующий опрос или по DispatchPendingGrants(). Тип без
// обработчика — висит в PendingGrants, пока билд его не поймёт.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ProtoSystem.LiveOps
{
    /// <summary>Одноразовая выдача из дашборда: ресурс, ачивка — что угодно.</summary>
    [Serializable]
    public class LiveOpsGrant
    {
        public string id;
        /// <summary>Тип — под него игра регистрирует обработчик ("resource", "achievement"…).</summary>
        public string type;
        /// <summary>Что именно: id ресурса, ачивки… Смысл задаёт игра.</summary>
        public string key;
        public float amount;
        /// <summary>Произвольный JSON строкой (может быть пустым).</summary>
        public string payload;
        public string note;
    }

    public partial class LiveOpsSystem
    {
        private const string GROUPS_PREF_KEY           = "ProtoSystem.Players.Groups";
        private const string PLAYER_OVERRIDES_PREF_KEY = "ProtoSystem.Players.Overrides";
        private const string APPLIED_GRANTS_PREF_KEY   = "ProtoSystem.Players.AppliedGrants";
        private const string THANKS_PREF_KEY           = "ProtoSystem.Players.Thanks";
        private const int    APPLIED_GRANTS_KEEP       = 200;

        private readonly List<string>              _groups          = new();
        private readonly Dictionary<string, float> _playerOverrides = new();
        private readonly List<LiveOpsGrant>        _pendingGrants   = new();
        private readonly Dictionary<string, Func<LiveOpsGrant, bool>> _grantHandlers = new();
        /// <summary>Применённые локально, но ещё не подтверждённые серверу (или уже — до чистки).</summary>
        private readonly List<string>    _appliedGrantIds = new();
        private readonly HashSet<string> _confirmInFlight = new();
        private List<string> _thanks;
        private bool _playerStateCacheLoaded;

        // ── Группы ──────────────────────────────────────────────

        /// <summary>Группы игрока из дашборда («developers», «testers»…). Пусто — обычный игрок.</summary>
        public IReadOnlyList<string> Groups => _groups;

        public bool HasGroup(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _groups.Count; i++)
                if (string.Equals(_groups[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Состав групп изменился (после ответа сервера).</summary>
        public event Action<IReadOnlyList<string>> GroupsChanged;

        // ── Выдачи ──────────────────────────────────────────────

        /// <summary>Выдачи, которые ещё никто не применил (нет обработчика или он вернул false).</summary>
        public IReadOnlyList<LiveOpsGrant> PendingGrants => _pendingGrants;

        /// <summary>Пришла новая выдача (до попытки применить) — для уведомлений в UI.</summary>
        public event Action<LiveOpsGrant> GrantReceived;

        /// <summary>Выдача применена обработчиком и подтверждена серверу.</summary>
        public event Action<LiveOpsGrant> GrantApplied;

        /// <summary>
        /// Зарегистрировать обработчик выдач типа <paramref name="type"/>.
        /// Вернуть true = применено и записано в сейв (система подтвердит серверу),
        /// false = не сейчас (повтор при следующем опросе / DispatchPendingGrants).
        /// </summary>
        public void RegisterGrantHandler(string type, Func<LiveOpsGrant, bool> handler)
        {
            if (string.IsNullOrEmpty(type) || handler == null) return;
            _grantHandlers[type] = handler;
            DispatchPendingGrants();
        }

        /// <summary>
        /// Попробовать применить всё, что ждёт (зовите после загрузки сейва —
        /// обработчики, вернувшие false до того, получат второй шанс).
        /// </summary>
        public void DispatchPendingGrants()
        {
            if (_pendingGrants.Count == 0) return;
            var toConfirm = new List<string>();

            for (int i = _pendingGrants.Count - 1; i >= 0; i--)
            {
                var g = _pendingGrants[i];
                if (!_grantHandlers.TryGetValue(g.type ?? "", out var handler)) continue;

                bool applied;
                try { applied = handler(g); }
                catch (Exception ex)
                {
                    ProtoLogger.LogWarning(SystemId, $"Grant handler '{g.type}' упал: {ex.Message}");
                    applied = false;
                }
                if (!applied) continue;

                _pendingGrants.RemoveAt(i);
                RememberApplied(g.id);
                toConfirm.Add(g.id);
                ProtoLogger.LogRuntime(SystemId, $"Выдача применена: {g.type}:{g.key} × {g.amount} ({g.id})");
                try { GrantApplied?.Invoke(g); }
                catch (Exception ex) { ProtoLogger.LogWarning(SystemId, $"GrantApplied handler: {ex.Message}"); }
            }

            if (toConfirm.Count > 0) _ = ConfirmGrantsAsync(toConfirm);
        }

        /// <summary>
        /// Подтвердить выдачу вручную (если игра применила её сама, минуя обработчик).
        /// </summary>
        public Task<bool> ConfirmGrantAsync(string grantId)
        {
            if (string.IsNullOrEmpty(grantId)) return Task.FromResult(false);
            _pendingGrants.RemoveAll(g => g.id == grantId);
            RememberApplied(grantId);
            return ConfirmGrantsAsync(new List<string> { grantId });
        }

        // ── Благодарности ───────────────────────────────────────

        /// <summary>Последний известный список благодарностей (кэш), может быть null до первого запроса.</summary>
        public IReadOnlyList<string> Thanks => _thanks;

        /// <summary>
        /// Имена игроков для раздела «Благодарности» в титрах. Ходит на сервер;
        /// при ошибке отдаёт кэш прошлого ответа (или пустой список).
        /// </summary>
        public async Task<IReadOnlyList<string>> FetchThanksAsync()
        {
            if (_thanks == null) LoadCachedThanks();

            if (config == null || string.IsNullOrEmpty(config.serverUrl)) return _thanks ?? new List<string>();
            try
            {
                string url = config.serverUrl.TrimEnd('/') + "/api/player/credits?project="
                           + UnityWebRequest.EscapeURL(config.projectId);
                string json = await PlayersHttpAsync("GET", url, null);
                if (json == null) return _thanks ?? new List<string>();

                var resp = JsonUtility.FromJson<CreditsResponse>(json);
                _thanks = new List<string>(resp?.names ?? Array.Empty<string>());
                PlayerPrefs.SetString(THANKS_PREF_KEY, JsonUtility.ToJson(new StringList { items = _thanks.ToArray() }));
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"credits fetch failed: {ex.Message}");
            }
            return _thanks ?? new List<string>();
        }

        // ── Опрос состояния игрока ──────────────────────────────

        private async Task FetchPlayerStateAsync()
        {
            if (config == null || string.IsNullOrEmpty(config.serverUrl) || string.IsNullOrEmpty(_playerId)) return;
            LoadCachedPlayerState();

            try
            {
                string url = config.serverUrl.TrimEnd('/') + "/api/player/state";
                string body = "{\"project\":\"" + EscapeJson(config.projectId) + "\",\"playerId\":\"" + EscapeJson(_playerId) + "\"}";
                string json = await PlayersHttpAsync("POST", url, body);
                if (json == null) return;

                var resp = JsonUtility.FromJson<PlayerStateResponse>(json);
                if (resp == null) return;

                ApplyGroups(resp.groups ?? Array.Empty<string>());
                ApplyPlayerOverrides(resp.overrides, persist: true);
                ApplyGrants(resp.grants);
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"player state fetch failed: {ex.Message}");
            }
        }

        private void ApplyGroups(string[] groups)
        {
            bool changed = groups.Length != _groups.Count;
            if (!changed)
                for (int i = 0; i < groups.Length; i++)
                    if (groups[i] != _groups[i]) { changed = true; break; }

            _groups.Clear();
            _groups.AddRange(groups);
            PlayerPrefs.SetString(GROUPS_PREF_KEY, JsonUtility.ToJson(new StringList { items = groups }));

            if (!changed) return;
            ProtoLogger.LogRuntime(SystemId, _groups.Count > 0
                ? $"Группы игрока: {string.Join(", ", _groups)}"
                : "Группы игрока: нет");
            try { GroupsChanged?.Invoke(_groups); }
            catch (Exception ex) { ProtoLogger.LogWarning(SystemId, $"GroupsChanged handler: {ex.Message}"); }
        }

        /// <summary>
        /// Оверрайды игрока: поверх A/B. Изменились — дёргаем VariantChanged
        /// с текущим вариантом, подписчики перечитают снапшот (тот же путь,
        /// что у A/B — игре ничего дописывать не нужно).
        /// </summary>
        private void ApplyPlayerOverrides(AbOverride[] pairs, bool persist)
        {
            var next = new Dictionary<string, float>();
            if (pairs != null)
                foreach (var p in pairs)
                    if (p != null && !string.IsNullOrEmpty(p.k)) next[p.k] = p.v;

            bool changed = next.Count != _playerOverrides.Count;
            if (!changed)
                foreach (var kv in next)
                    if (!_playerOverrides.TryGetValue(kv.Key, out float old) || !Mathf.Approximately(old, kv.Value)) { changed = true; break; }

            _playerOverrides.Clear();
            foreach (var kv in next) _playerOverrides[kv.Key] = kv.Value;

            if (persist)
                PlayerPrefs.SetString(PLAYER_OVERRIDES_PREF_KEY,
                    JsonUtility.ToJson(new AbOverrideList { items = pairs ?? Array.Empty<AbOverride>() }));

            if (!changed) return;
            if (_playerOverrides.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var kv in _playerOverrides) sb.Append(sb.Length > 0 ? "; " : "").Append(kv.Key).Append('=').Append(kv.Value);
                ProtoLogger.LogRuntime(SystemId, $"Оверрайды игрока: {sb}");
            }
            ApplyVariant(Variant);
        }

        private void ApplyGrants(LiveOpsGrant[] grants)
        {
            if (grants == null) return;
            bool any = false;
            foreach (var g in grants)
            {
                if (g == null || string.IsNullOrEmpty(g.id)) continue;
                if (_appliedGrantIds.Contains(g.id))
                {
                    // Применили раньше, но подтверждение не дошло — повторяем его, не выдачу
                    if (!_confirmInFlight.Contains(g.id)) _ = ConfirmGrantsAsync(new List<string> { g.id });
                    continue;
                }
                if (_pendingGrants.Exists(x => x.id == g.id)) continue;

                _pendingGrants.Add(g);
                any = true;
                ProtoLogger.LogRuntime(SystemId, $"Пришла выдача: {g.type}:{g.key} × {g.amount}" +
                    (string.IsNullOrEmpty(g.note) ? "" : $" ({g.note})"));
                try { GrantReceived?.Invoke(g); }
                catch (Exception ex) { ProtoLogger.LogWarning(SystemId, $"GrantReceived handler: {ex.Message}"); }
            }
            if (any) DispatchPendingGrants();
        }

        private async Task<bool> ConfirmGrantsAsync(List<string> ids)
        {
            if (ids == null || ids.Count == 0 || config == null || string.IsNullOrEmpty(config.serverUrl)) return false;
            foreach (var id in ids) _confirmInFlight.Add(id);
            try
            {
                var sb = new StringBuilder("{\"ids\":[");
                for (int i = 0; i < ids.Count; i++)
                    sb.Append(i > 0 ? ",\"" : "\"").Append(EscapeJson(ids[i])).Append('"');
                sb.Append("]}");

                string url = config.serverUrl.TrimEnd('/') + "/api/player/grants/confirm";
                string json = await PlayersHttpAsync("POST", url, sb.ToString());
                if (json == null) return false;

                // Сервер подтвердил — локальную память об этих id можно отпустить
                foreach (var id in ids) _appliedGrantIds.Remove(id);
                SaveAppliedGrants();
                return true;
            }
            catch (Exception ex)
            {
                ProtoLogger.LogWarning(SystemId, $"grant confirm failed: {ex.Message}");
                return false;
            }
            finally
            {
                foreach (var id in ids) _confirmInFlight.Remove(id);
            }
        }

        // ── Кэш ─────────────────────────────────────────────────

        private void LoadCachedPlayerState()
        {
            if (_playerStateCacheLoaded) return;
            _playerStateCacheLoaded = true;
            try
            {
                var groups = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(GROUPS_PREF_KEY, ""));
                if (groups?.items != null) { _groups.Clear(); _groups.AddRange(groups.items); }

                var ov = JsonUtility.FromJson<AbOverrideList>(PlayerPrefs.GetString(PLAYER_OVERRIDES_PREF_KEY, ""));
                if (ov?.items != null)
                {
                    _playerOverrides.Clear();
                    foreach (var p in ov.items)
                        if (p != null && !string.IsNullOrEmpty(p.k)) _playerOverrides[p.k] = p.v;
                }

                var applied = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(APPLIED_GRANTS_PREF_KEY, ""));
                if (applied?.items != null) { _appliedGrantIds.Clear(); _appliedGrantIds.AddRange(applied.items); }
            }
            catch (Exception) { }
        }

        private void LoadCachedThanks()
        {
            try
            {
                var list = JsonUtility.FromJson<StringList>(PlayerPrefs.GetString(THANKS_PREF_KEY, ""));
                if (list?.items != null) _thanks = new List<string>(list.items);
            }
            catch (Exception) { }
        }

        private void RememberApplied(string id)
        {
            if (_appliedGrantIds.Contains(id)) return;
            _appliedGrantIds.Add(id);
            while (_appliedGrantIds.Count > APPLIED_GRANTS_KEEP) _appliedGrantIds.RemoveAt(0);
            SaveAppliedGrants();
        }

        private void SaveAppliedGrants()
            => PlayerPrefs.SetString(APPLIED_GRANTS_PREF_KEY, JsonUtility.ToJson(new StringList { items = _appliedGrantIds.ToArray() }));

        // ── HTTP ────────────────────────────────────────────────

        /// <summary>Тело ответа при 2xx, иначе null (ошибка уже в логе).</summary>
        private async Task<string> PlayersHttpAsync(string method, string url, string body)
        {
            using var req = new UnityWebRequest(url, method);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.Max(2, Mathf.RoundToInt(config.requestTimeoutSeconds));

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                LiveOpsLog.Info($"[LiveOps] {method} {url}: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }

        private static string EscapeJson(string s)
            => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class PlayerStateResponse
        {
            public string[] groups;
            public AbOverride[] overrides;
            public LiveOpsGrant[] grants;
        }

        [Serializable] private class CreditsResponse { public string[] names; }
        [Serializable] private class StringList { public string[] items; }
    }
}
