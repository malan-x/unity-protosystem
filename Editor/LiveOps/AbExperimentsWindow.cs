using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ProtoSystem.LiveOps;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Editor.LiveOps
{
    /// <summary>
    /// Окно A/B-экспериментов баланса: та же коллекция и те же роуты, что у
    /// веб-дашборда (/api/ab/experiments) — один источник правды, правь где
    /// удобнее. Сервер и проект берутся из LiveOpsConfig, доступ — логин
    /// superuser'а PocketBase (токен хранится в EditorPrefs, пароль — нет).
    ///
    /// Формат: сервер отдаёт ?flat=1 — оверрайды и счётчики массивами пар,
    /// потому что JsonUtility не умеет словари.
    /// </summary>
    public class AbExperimentsWindow : EditorWindow
    {
        private const string TOKEN_KEY = "ProtoSystem.LiveOps.SuToken";
        private const string EMAIL_KEY = "ProtoSystem.LiveOps.SuEmail";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // ── Модели (зеркало ответа сервера, flat-формат) ──

        [Serializable] public class KV { public string k; public float v; }
        [Serializable] public class Count { public string id; public int n; }

        [Serializable]
        public class Variant
        {
            public string id = "2";
            public int weight;          // корзины из 10000 (2000 = 20%)
            public int quota;
            public float fillChance = 1f;
            public KV[] overrides = Array.Empty<KV>();
        }

        [Serializable]
        public class Experiment
        {
            public string id;
            public string name;
            public string note;
            public bool active;
            public Variant[] variants = Array.Empty<Variant>();
            public Count[] counts = Array.Empty<Count>();
        }

        [Serializable] private class ListResponse { public Experiment[] experiments; }
        [Serializable] private class AuthResponse { public string token; }

        // ── Состояние ──

        private LiveOpsConfig _config;
        private string _email, _password = "", _token;
        private string _status = "";
        private bool _busy;
        private List<Experiment> _experiments = new List<Experiment>();
        private Vector2 _scroll;

        // Форма редактирования. _editingId == null — форма скрыта
        private string _editingId;
        private string _formName = "", _formNote = "";
        private bool _formActive;
        private List<FormVariant> _formVariants = new List<FormVariant>();

        private class FormVariant
        {
            public string id = "2";
            public bool isQuota;
            public string value = "";      // % игроков или человек — по режиму
            public string chance = "";     // набор, % (только квота)
            public string overrides = "";  // «ключ=значение; …»
        }

        [MenuItem("ProtoSystem/LiveOps/A-B Эксперименты", false, 400)]
        public static void Open()
        {
            var w = GetWindow<AbExperimentsWindow>("A/B Эксперименты");
            w.minSize = new Vector2(560, 400);
        }

        private void OnEnable()
        {
            _token = EditorPrefs.GetString(TOKEN_KEY, "");
            _email = EditorPrefs.GetString(EMAIL_KEY, "");
            FindConfig();
            if (IsLoggedIn && _config != null) _ = Reload();
        }

        private bool IsLoggedIn => !string.IsNullOrEmpty(_token);
        private string ServerUrl => _config != null ? _config.serverUrl.TrimEnd('/') : "";

        private void FindConfig()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LiveOpsConfig"))
            {
                _config = AssetDatabase.LoadAssetAtPath<LiveOpsConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (_config != null) return;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("LiveOpsConfig не найден в проекте — без него неизвестны сервер и проект.", MessageType.Warning);
                if (GUILayout.Button("Искать снова")) FindConfig();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{ServerUrl}  ·  проект: {_config.projectId}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (IsLoggedIn && GUILayout.Button("Выйти", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    _token = "";
                    EditorPrefs.DeleteKey(TOKEN_KEY);
                }
            }

            if (!IsLoggedIn) { DrawLogin(); DrawStatus(); return; }

            using (new EditorGUI.DisabledScope(_busy))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Обновить", GUILayout.Width(90))) _ = Reload();
                    if (_editingId == null && GUILayout.Button("Новый эксперимент", GUILayout.Width(150)))
                        StartEdit(null);
                    GUILayout.FlexibleSpace();
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (_editingId != null) DrawForm();
                foreach (var x in _experiments) DrawExperiment(x);
                if (_experiments.Count == 0 && _editingId == null)
                    EditorGUILayout.HelpBox("Экспериментов нет. «Новый эксперимент» — создать.", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
            DrawStatus();
        }

        private void DrawLogin()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Вход (superuser PocketBase)", EditorStyles.boldLabel);
            _email = EditorGUILayout.TextField("Email", _email);
            _password = EditorGUILayout.PasswordField("Пароль", _password);
            using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_email) || string.IsNullOrEmpty(_password)))
                if (GUILayout.Button("Войти", GUILayout.Width(120))) _ = Login();
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _status.StartsWith("Ошибка") ? MessageType.Error : MessageType.Info);
        }

        private void DrawExperiment(Experiment x)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var title = x.active ? $"● {x.name}  [ACTIVE]" : x.name;
                    var style = new GUIStyle(EditorStyles.boldLabel);
                    if (x.active) style.normal.textColor = new Color(0.35f, 0.85f, 0.45f);
                    GUILayout.Label(title, style);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(x.active ? "Выключить" : "Включить", GUILayout.Width(90)))
                        _ = SetActive(x, !x.active);
                    if (GUILayout.Button("Изменить", GUILayout.Width(80))) StartEdit(x);
                    if (GUILayout.Button("Удалить", GUILayout.Width(70)) &&
                        EditorUtility.DisplayDialog("Удалить эксперимент?",
                            $"«{x.name}» будет удалён. Назначения игроков останутся в истории.", "Удалить", "Отмена"))
                        _ = Delete(x);
                }
                if (!string.IsNullOrEmpty(x.note))
                    GUILayout.Label(x.note, EditorStyles.miniLabel);

                GUILayout.Label($"вариант 1 — контроль, все неназначенные · назначено {CountOf(x, "1")}", EditorStyles.miniLabel);
                foreach (var v in x.variants)
                {
                    string size = v.quota > 0
                        ? $"квота {CountOf(x, v.id)} / {v.quota}" + (v.fillChance < 1f ? $" · набор {Mathf.RoundToInt(v.fillChance * 100)}%" : "")
                        : $"вес {v.weight / 100f:0.#}% игроков · назначено {CountOf(x, v.id)}";
                    string ov = OverridesToText(v.overrides);
                    GUILayout.Label($"вариант {v.id} — {size}  ·  {(string.IsNullOrEmpty(ov) ? "без оверрайдов (играет как «1»)" : ov)}",
                                    EditorStyles.miniLabel);
                }
            }
        }

        private void DrawForm()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUILayout.Label(_editingId == "" ? "Новый эксперимент" : $"Изменить: {_formName}", EditorStyles.boldLabel);
                _formName = EditorGUILayout.TextField("Имя", _formName);
                _formNote = EditorGUILayout.TextField("Заметка", _formNote);
                _formActive = EditorGUILayout.Toggle("Активировать сразу", _formActive);

                EditorGUILayout.Space(4);
                GUILayout.Label("Варианты («1» не задаётся — это контроль)", EditorStyles.miniBoldLabel);

                int remove = -1;
                for (int i = 0; i < _formVariants.Count; i++)
                {
                    var fv = _formVariants[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("вариант", GUILayout.Width(52));
                        fv.id = EditorGUILayout.TextField(fv.id, GUILayout.Width(36));
                        fv.isQuota = EditorGUILayout.Popup(fv.isQuota ? 1 : 0,
                            new[] { "вес, % игроков", "квота, человек" }, GUILayout.Width(120)) == 1;
                        fv.value = EditorGUILayout.TextField(fv.value, GUILayout.Width(56));
                        if (fv.isQuota)
                        {
                            GUILayout.Label(new GUIContent("набор,%",
                                "Шанс взять нового игрока в группу; меньше 100 — набор растянется по времени"),
                                GUILayout.Width(52));
                            fv.chance = EditorGUILayout.TextField(fv.chance, GUILayout.Width(40));
                        }
                        if (GUILayout.Button("✕", GUILayout.Width(24))) remove = i;
                    }
                    fv.overrides = EditorGUILayout.TextField(
                        new GUIContent("  оверрайды", "Что вариант меняет в игре: «ключ=значение; …», ключи — из белого списка клиента"),
                        fv.overrides);
                }
                if (remove >= 0) _formVariants.RemoveAt(remove);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ вариант", GUILayout.Width(90)))
                        _formVariants.Add(new FormVariant { id = NextFreeId() });
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Отмена", GUILayout.Width(80))) _editingId = null;
                    if (GUILayout.Button("Сохранить", GUILayout.Width(100))) _ = Save();
                }
            }
            EditorGUILayout.Space(6);
        }

        // ─────────────────────────────────────────────────────────────
        // Логика формы
        // ─────────────────────────────────────────────────────────────

        private void StartEdit(Experiment x)
        {
            _editingId = x?.id ?? "";
            _formName = x?.name ?? "";
            _formNote = x?.note ?? "";
            _formActive = x?.active ?? false;
            _formVariants.Clear();
            if (x != null)
                foreach (var v in x.variants)
                    _formVariants.Add(new FormVariant
                    {
                        id = v.id,
                        isQuota = v.quota > 0,
                        value = v.quota > 0 ? v.quota.ToString() : (v.weight / 100f).ToString("0.##"),
                        chance = v.quota > 0 && v.fillChance < 1f ? Mathf.RoundToInt(v.fillChance * 100).ToString() : "",
                        overrides = OverridesToText(v.overrides),
                    });
            if (_formVariants.Count == 0) _formVariants.Add(new FormVariant());
        }

        private string NextFreeId()
        {
            var used = new HashSet<string> { "1" };
            foreach (var fv in _formVariants) used.Add(fv.id?.Trim());
            for (int n = 2; ; n++)
                if (!used.Contains(n.ToString())) return n.ToString();
        }

        private static string OverridesToText(KV[] pairs)
        {
            if (pairs == null || pairs.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var p in pairs)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(p.k).Append('=').Append(p.v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static KV[] OverridesFromText(string text)
        {
            var list = new List<KV>();
            foreach (var part in (text ?? "").Split(';'))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var k = part.Substring(0, eq).Trim();
                var vs = part.Substring(eq + 1).Trim();
                if (k.Length > 0 && float.TryParse(vs, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                    list.Add(new KV { k = k, v = v });
            }
            return list.ToArray();
        }

        private static int CountOf(Experiment x, string id)
        {
            foreach (var c in x.counts)
                if (c.id == id) return c.n;
            return 0;
        }

        // ─────────────────────────────────────────────────────────────
        // Сеть
        // ─────────────────────────────────────────────────────────────

        private async Task Login()
        {
            _busy = true; _status = "Вход…"; Repaint();
            try
            {
                var body = $"{{\"identity\":{Quote(_email)},\"password\":{Quote(_password)}}}";
                var resp = await Http.PostAsync($"{ServerUrl}/api/collections/_superusers/auth-with-password",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {text}");
                _token = JsonUtility.FromJson<AuthResponse>(text)?.token ?? "";
                if (string.IsNullOrEmpty(_token)) throw new Exception("сервер не вернул токен");
                EditorPrefs.SetString(TOKEN_KEY, _token);
                EditorPrefs.SetString(EMAIL_KEY, _email);
                _password = "";
                _status = "";
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка входа: {ex.Message}"; }
            finally { _busy = false; Repaint(); }
        }

        private async Task Reload()
        {
            _busy = true; _status = ""; Repaint();
            try
            {
                var text = await Send(HttpMethod.Get,
                    $"/api/ab/experiments?project={Uri.EscapeDataString(_config.projectId)}&flat=1", null);
                var parsed = JsonUtility.FromJson<ListResponse>(text);
                _experiments = new List<Experiment>(parsed?.experiments ?? Array.Empty<Experiment>());
            }
            catch (Exception ex) { _status = $"Ошибка: {ex.Message}"; }
            finally { _busy = false; Repaint(); }
        }

        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_formName)) { _status = "Ошибка: имя эксперимента обязательно"; return; }

            var sb = new StringBuilder();
            var seen = new HashSet<string>();
            foreach (var fv in _formVariants)
            {
                var id = fv.id?.Trim() ?? "";
                if (!float.TryParse(fv.value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value) || value <= 0)
                    continue;
                if (id == "" ) { _status = "Ошибка: у варианта не заполнено имя"; return; }
                if (id == "1") { _status = "Ошибка: имя «1» занято контрольной группой"; return; }
                if (!seen.Add(id)) { _status = "Ошибка: имена вариантов повторяются"; return; }

                float chancePct = 0;
                float.TryParse(fv.chance, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out chancePct);

                if (sb.Length > 0) sb.Append(',');
                sb.Append('{').Append("\"id\":").Append(Quote(id));
                if (fv.isQuota)
                {
                    sb.Append(",\"quota\":").Append(Mathf.RoundToInt(value));
                    sb.Append(",\"fillChance\":").Append(
                        (chancePct > 0 && chancePct < 100 ? chancePct / 100f : 1f)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                    sb.Append(",\"weight\":").Append(Mathf.RoundToInt(value * 100)); // % → корзины из 10000

                sb.Append(",\"overrides\":[");
                var pairs = OverridesFromText(fv.overrides);
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"k\":").Append(Quote(pairs[i].k)).Append(",\"v\":")
                      .Append(pairs[i].v.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('}');
                }
                sb.Append("]}");
            }
            if (sb.Length == 0) { _status = "Ошибка: нужен хотя бы один вариант с именем и размером"; return; }

            var body = new StringBuilder("{");
            if (!string.IsNullOrEmpty(_editingId)) body.Append("\"id\":").Append(Quote(_editingId)).Append(',');
            body.Append("\"project\":").Append(Quote(_config.projectId))
                .Append(",\"name\":").Append(Quote(_formName.Trim()))
                .Append(",\"note\":").Append(Quote(_formNote?.Trim() ?? ""))
                .Append(",\"active\":").Append(_formActive ? "true" : "false")
                .Append(",\"variants\":[").Append(sb).Append("]}");

            _busy = true; Repaint();
            try
            {
                await Send(HttpMethod.Post, "/api/ab/experiments", body.ToString());
                _editingId = null;
                _status = "";
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка сохранения: {ex.Message}"; _busy = false; Repaint(); }
        }

        private async Task SetActive(Experiment x, bool active)
        {
            // Сохраняем эксперимент как есть, меняя только active — сервер сам
            // погасит остальные активные этого проекта
            StartEdit(x);
            _formActive = active;
            await Save();
        }

        private async Task Delete(Experiment x)
        {
            _busy = true; Repaint();
            try
            {
                await Send(HttpMethod.Post, "/api/ab/experiments/delete", $"{{\"id\":{Quote(x.id)}}}");
                await Reload();
            }
            catch (Exception ex) { _status = $"Ошибка удаления: {ex.Message}"; _busy = false; Repaint(); }
        }

        private async Task<string> Send(HttpMethod method, string path, string jsonBody)
        {
            var req = new HttpRequestMessage(method, ServerUrl + path);
            req.Headers.TryAddWithoutValidation("Authorization", _token);
            if (jsonBody != null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var resp = await Http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _token = "";
                EditorPrefs.DeleteKey(TOKEN_KEY);
                throw new Exception("токен протух — войдите заново");
            }
            if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {text}");
            return text;
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            return sb.Append('"').ToString();
        }
    }
}
