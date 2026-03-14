// Packages/com.protosystem.core/Runtime/LiveOps/PocketBaseHttpLiveOpsProvider.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// LiveOps-провайдер для бэкенда на PocketBase.
    ///
    /// Использование в проекте:
    /// <code>
    /// liveOpsConfig.SetProvider(new PocketBaseHttpLiveOpsProvider(
    ///     baseUrl:   "https://api.mygame.com",
    ///     projectId: "my-game",
    ///     playerId:  SteamFriends.GetPersonaName()
    /// ));
    /// </code>
    ///
    /// Ожидаемые коллекции PocketBase:
    ///   ratings       — project_id, version, score, player_id
    ///   announcements — project_id, title (json), body (json), url
    ///   messages      — project_id, title (json), body (json), type, expires_at
    ///   polls         — project_id, question (json), poll_type, expires_at
    ///   poll_votes    — poll_id, option_id, player_id
    ///   devlog        — project_id, focus (json), title (json), description (json)
    ///   goals         — project_id, description (json), unit (json), current, goal
    ///   content_order — project_id, order (json: [{"type":"poll","id":"..."}, ...])
    ///   panel_config  — project_id, (json-поля конфига виджетов)
    ///
    /// Локализуемые поля хранятся как JSON: {"ru":"Текст","en":"Text","de":"Text",...}
    /// Поддерживается обратная совместимость с legacy полями _ru/_en.
    ///
    /// ВАЖНО: фильтрация по project_id выполняется на клиенте.
    /// Новые версии PocketBase возвращают 400 при передаче filter через query params.
    /// </summary>
    public class PocketBaseHttpLiveOpsProvider : ILiveOpsProvider
    {
        private readonly string _baseUrl;
        private readonly string _projectId;
        private readonly string _playerId;
        private readonly float  _timeoutSeconds;

        public PocketBaseHttpLiveOpsProvider(
            string baseUrl,
            string projectId,
            string playerId       = null,
            float  timeoutSeconds = 10f)
        {
            _baseUrl        = baseUrl.TrimEnd('/');
            _projectId      = projectId;
            _playerId       = playerId;
            _timeoutSeconds = timeoutSeconds;
        }

        // ── ILiveOpsProvider ──────────────────────────────────────────────────

        public async Task<List<LiveOpsMessage>> FetchMessagesAsync()
        {
            var items = await FetchCollection<PbMessage>("messages");
            if (items == null) return null;

            var result = new List<LiveOpsMessage>();
            foreach (var r in items)
            {
                if (r.project_id != _projectId) continue;
                result.Add(new LiveOpsMessage
                {
                    id          = r.id,
                    title       = MakeLocalized(r.title, r.title_ru, r.title_en),
                    body        = MakeLocalized(r.body,  r.body_ru,  r.body_en),
                    type        = r.type,
                    publishedAt = r.created,
                    expiresAt   = r.expires_at,
                });
            }
            return result;
        }

        public async Task<List<LiveOpsPoll>> FetchPollsAsync()
        {
            // 1. Получаем активные опросы
            var items = await FetchCollection<PbPoll>("polls");
            if (items == null) return null;

            var result = new List<LiveOpsPoll>();
            foreach (var r in items)
            {
                if (r.project_id != _projectId) continue;
                if (!r.is_active) continue;

                var poll = new LiveOpsPoll
                {
                    id       = r.id,
                    question = MakeLocalized(r.question),
                    pollType = r.poll_type ?? "single",
                    options  = ParsePollOptions(r.options),
                    expiresAt = r.expires_at,
                };

                // 2. Проверяем голосовал ли текущий игрок
                if (!string.IsNullOrEmpty(_playerId))
                {
                    var voteJson = await GetAsync(
                        $"{_baseUrl}/api/collections/poll_votes/records?filter=poll_id='{r.id}'%26%26player_id='{_playerId}'");
                    if (voteJson != null)
                    {
                        var voteWrapper = JsonUtility.FromJson<PbListResponse<PbPollVote>>(
                            FlattenNestedJsonObjects(voteJson));
                        if (voteWrapper?.items != null && voteWrapper.items.Length > 0)
                        {
                            var vote = voteWrapper.items[0];
                            _cachedVoteRecordIds[r.id] = vote.id;
                            poll.userVote = ParseStringArray(vote.option_ids);
                            if (poll.userVote != null)
                                foreach (var opt in poll.options)
                                    opt.selected = System.Array.Exists(poll.userVote, v => v == opt.id);
                        }
                    }
                }

                // 3. Получаем результаты (серверная агрегация)
                var resultsJson = await GetAsync(
                    $"{_baseUrl}/api/polls/results?poll_id={r.id}&project_id={_projectId}");
                if (resultsJson != null)
                    ApplyPollResults(poll, resultsJson);

                result.Add(poll);
            }
            return result;
        }

        /// <summary>Кеш id записей голосов для PATCH при переголосовании.</summary>
        private readonly Dictionary<string, string> _cachedVoteRecordIds = new();

        public async Task<bool> SubmitPollAnswerAsync(LiveOpsPollAnswer answer)
        {
            // Проверяем есть ли уже запись голоса
            bool hasExistingVote = _cachedVoteRecordIds.TryGetValue(answer.pollId, out var voteRecordId);

            if (!hasExistingVote)
            {
                // Ищем существующий голос
                var voteJson = await GetAsync(
                    $"{_baseUrl}/api/collections/poll_votes/records?filter=poll_id='{answer.pollId}'%26%26player_id='{answer.playerId}'");
                if (voteJson != null)
                {
                    var voteWrapper = JsonUtility.FromJson<PbListResponse<PbPollVote>>(
                        FlattenNestedJsonObjects(voteJson));
                    if (voteWrapper?.items != null && voteWrapper.items.Length > 0)
                    {
                        voteRecordId = voteWrapper.items[0].id;
                        hasExistingVote = true;
                    }
                }
            }

            bool ok;
            if (hasExistingVote)
            {
                // PATCH — переголосовать
                var body = $"{{\"option_ids\":{ToJsonArray(answer.optionIds)}}}";
                ok = await PatchAsync($"/api/collections/poll_votes/records/{voteRecordId}", body);
            }
            else
            {
                // POST — новый голос
                var body = $"{{\"project_id\":\"{_projectId}\",\"poll_id\":\"{answer.pollId}\",\"player_id\":\"{answer.playerId}\",\"option_ids\":{ToJsonArray(answer.optionIds)}}}";
                ok = await PostAsync("/api/collections/poll_votes/records", body);
            }

            if (ok && !string.IsNullOrEmpty(voteRecordId))
                _cachedVoteRecordIds[answer.pollId] = voteRecordId;

            return ok;
        }

        public Task<bool> SendEventAsync(LiveOpsEvent evt) =>
            Task.FromResult(false);

        public async Task<bool> SubmitFeedbackAsync(LiveOpsFeedback feedback)
        {
            var body = $"{{\"project_id\":\"{EscapeJson(_projectId)}\",\"player_id\":\"{EscapeJson(feedback.playerId)}\",\"lang\":\"{EscapeJson(feedback.lang)}\",\"category\":\"{EscapeJson(feedback.category)}\",\"game_version\":\"{EscapeJson(feedback.gameVersion)}\",\"message\":\"{EscapeJson(feedback.message)}\",\"tag\":\"{EscapeJson(feedback.tag)}\",\"timestamp\":\"{EscapeJson(feedback.timestamp)}\"}}";
            return await PostAsync("/api/collections/messages/records", body);
        }

        public async Task<LiveOpsPanelConfig> FetchPanelConfigAsync() =>
            // panel_config не реализован — возвращаем конфиг по умолчанию (все виджеты включены)
            await Task.FromResult<LiveOpsPanelConfig>(null);

        public async Task<List<LiveOpsAnnouncement>> FetchAnnouncementsAsync()
        {
            var items = await FetchCollection<PbAnnouncement>("announcements");
            if (items == null) return null;

            var result = new List<LiveOpsAnnouncement>();
            foreach (var r in items)
            {
                if (r.project_id != _projectId) continue;
                result.Add(new LiveOpsAnnouncement
                {
                    id          = r.id,
                    title       = MakeLocalized(r.title, r.title_ru, r.title_en),
                    body        = MakeLocalized(r.body,  r.body_ru,  r.body_en),
                    url         = r.url,
                    publishedAt = r.created,
                });
            }
            return result;
        }

        public async Task<LiveOpsDevLog> FetchDevLogAsync()
        {
            var items = await FetchCollection<PbDevLog>("devlog");
            if (items == null) return null;

            foreach (var r in items)
            {
                if (r.project_id != _projectId) continue;
                if (!r.is_active) continue;
                return new LiveOpsDevLog
                {
                    id          = r.id,
                    focus       = MakeLocalized(r.focus),
                    title       = MakeLocalized(r.title),
                    description = MakeLocalized(r.description),
                    items       = ParseDevLogItems(r.items),
                    updatedAt   = r.updated,
                };
            }
            return null;
        }

        public async Task<LiveOpsMilestoneData> FetchMilestoneAsync()
        {
            var items = await FetchCollection<PbMilestone>("goals");
            if (items == null)
            {
                Debug.LogWarning($"[PocketBaseProvider] FetchMilestone: коллекция milestones не загружена (null)");
                return null;
            }

            Debug.Log($"[PocketBaseProvider] FetchMilestone: {items.Count} записей, ищем project_id='{_projectId}'");

            foreach (var r in items)
            {
                Debug.Log($"[PB DEBUG] goal record: id='{r.id}' project_id='{r.project_id}' title='{r.title}' desc='{r.description}' unit='{r.unit}' current={r.current} goal={r.target}");
                if (r.project_id != _projectId)
                {
                    Debug.Log($"[PocketBaseProvider] FetchMilestone: пропуск записи project_id='{r.project_id}'");
                    continue;
                }
                var desc = MakeLocalized(r.description, r.description_ru, r.description_en);
                Debug.Log($"[PocketBaseProvider] FetchMilestone: найдена цель '{desc.Get("en")}' {r.current}/{r.target}");
                return new LiveOpsMilestoneData
                {
                    title       = MakeLocalized(r.title),
                    description = desc,
                    current     = r.current,
                    goal        = r.target,
                    unit        = MakeLocalized(r.unit, r.unit_ru, r.unit_en),
                    updatedAt   = r.updated,
                };
            }

            Debug.LogWarning($"[PocketBaseProvider] FetchMilestone: нет записей с project_id='{_projectId}'");
            return null;
        }

        public async Task<LiveOpsContentOrder> FetchContentOrderAsync()
        {
            var items = await FetchCollection<PbContentOrder>("content_order");
            if (items == null) return null;

            foreach (var r in items)
            {
                if (r.project_id != _projectId) continue;
                var order = new LiveOpsContentOrder();
                order.order = ParseContentOrderEntries(r.order);
                return order;
            }
            return null;
        }

        public async Task<List<LiveOpsConversationItem>> FetchMyMessagesAsync(string playerId)
        {
            // Безопасный хук — не открывает коллекцию на чтение
            var url = $"{_baseUrl}/api/messages/my?player_id={UnityWebRequest.EscapeURL(playerId)}&project_id={UnityWebRequest.EscapeURL(_projectId)}";
            Debug.Log($"[PB] FetchMyMessages GET {url}");
            var json = await GetAsync(url);
            Debug.Log($"[PB] FetchMyMessages response: {(json != null ? json.Substring(0, System.Math.Min(json.Length, 500)) : "NULL")}");
            if (json == null) return null;

            json = FlattenNestedJsonObjects(json);
            try
            {
                var wrapper = JsonUtility.FromJson<PbMyMessagesResponse>(json);
                if (wrapper?.items == null) return null;

                var result = new List<LiveOpsConversationItem>();
                foreach (var r in wrapper.items)
                {
                    var item = new LiveOpsConversationItem
                    {
                        id           = r.id,
                        message      = r.message,
                        reply        = r.reply ?? "",
                        reply_status = r.reply_status ?? "",
                        category     = r.category,
                        timestamp    = !string.IsNullOrEmpty(r.timestamp) ? r.timestamp : r.created,
                    };
                    // Парсим локализованный ответ
                    if (!string.IsNullOrEmpty(r.reply_localized))
                        item.replyLocalized = MakeLocalized(r.reply_localized);
                    result.Add(item);
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PocketBaseProvider] Parse error (messages/my): {ex.Message}");
                return null;
            }
        }

        public async Task<int> ConfirmRepliesAsync(string[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;
            var body = JsonUtility.ToJson(new PbConfirmRequest { ids = ids });
            var ok = await PostAsync("/api/messages/confirm", body);
            if (!ok) return 0;
            // Ответ: {"updated": N} — но PostAsync возвращает bool, парсим из последнего response
            return ids.Length; // сервер подтвердит кол-во, но мы считаем все отправленные
        }

        public async Task<LiveOpsRatingData> FetchRatingAsync(string version)
        {
            var items = await FetchCollection<PbRating>("ratings");
            if (items == null) return null;

            return CalcRating(items, _projectId, version, _playerId);
        }

        public async Task<LiveOpsRatingResult> SubmitRatingAsync(LiveOpsRatingSubmit submit)
        {
            // Всегда вставляем новую запись — так хранится история изменения оценок
            var body = $"{{\"project_id\":\"{_projectId}\",\"version\":\"{submit.version}\",\"score\":{submit.score},\"player_id\":\"{submit.playerId}\"}}";
            var ok = await PostAsync("/api/collections/ratings/records", body);
            if (!ok) return null;

            // Пересчитываем avg/count по свежим данным
            var updated = await FetchCollection<PbRating>("ratings");
            if (updated == null) return null;

            var data = CalcRating(updated, _projectId, submit.version, submit.playerId);
            return new LiveOpsRatingResult { ok = true, avg = data.avg, count = data.count };
        }

        // ── PocketBase helpers ────────────────────────────────────────────────

        /// <summary>
        /// Загружает все записи коллекции (до 200).
        /// Фильтрация по project_id — на клиенте.
        /// </summary>
        private async Task<List<T>> FetchCollection<T>(string collection)
        {
            var url = $"{_baseUrl}/api/collections/{collection}/records?perPage=200";
            var json = await GetAsync(url);
            if (json == null) return null;

            Debug.Log($"[PB DEBUG] {collection} RAW ({json.Length} chars):\n{json}");

            // JsonUtility не может десериализовать вложенные JSON-объекты в string поля.
            // PocketBase json-поля возвращаются как объекты: "title":{"ru":"...","en":"..."}.
            // Конвертируем их в escaped-строки: "title":"{\"ru\":\"...\",\"en\":\"...\"}".
            json = FlattenNestedJsonObjects(json);

            Debug.Log($"[PB DEBUG] {collection} FLATTENED ({json.Length} chars):\n{json}");

            try
            {
                var wrapper = JsonUtility.FromJson<PbListResponse<T>>(json);
                if (wrapper == null)
                    Debug.LogWarning($"[PB DEBUG] {collection}: JsonUtility returned null wrapper");
                else if (wrapper.items == null)
                    Debug.LogWarning($"[PB DEBUG] {collection}: wrapper.items is null");
                else
                    Debug.Log($"[PB DEBUG] {collection}: parsed {wrapper.items.Length} items");
                return wrapper?.items != null ? new List<T>(wrapper.items) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PocketBaseProvider] Parse error ({collection}): {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Находит паттерны "key":{...} и заменяет на "key":"{escaped...}"
        /// чтобы JsonUtility мог десериализовать вложенные объекты в string поля.
        /// </summary>
        private static string FlattenNestedJsonObjects(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length + 128);
            int i = 0;
            while (i < json.Length)
            {
                // Строки — копируем целиком, чтобы ':' внутри строк не ломали логику
                if (json[i] == '"')
                {
                    sb.Append(json[i]); i++;
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\') { sb.Append(json[i]); i++; }
                        sb.Append(json[i]); i++;
                    }
                    if (i < json.Length) { sb.Append(json[i]); i++; }
                    continue;
                }

                // Паттерн ": {" — вложенный JSON-объект → экранировать в строку
                if (json[i] == ':')
                {
                    sb.Append(json[i]); i++;
                    // Пропуск пробелов после ':'
                    while (i < json.Length && (json[i] == ' ' || json[i] == '\t'))
                    {
                        sb.Append(json[i]); i++;
                    }

                    if (i < json.Length && json[i] == '[')
                    {
                        // Массив после ':' — если это top-level "items", не трогаем,
                        // пусть посимвольная обработка пройдёт внутрь и экранирует вложенные объекты.
                        // Если это JSON-поле записи (напр. devlog items) — экранируем целиком.
                        if (IsTopLevelItemsArray(json, i))
                        {
                            // Просто добавим '[' и продолжим — содержимое обработается посимвольно
                            sb.Append(json[i]); i++;
                        }
                        else
                        {
                            // Массив-поле записи → экранируем в строку целиком
                            int depth = 0, start = i;
                            do
                            {
                                if (json[i] == '[') depth++;
                                else if (json[i] == ']') depth--;
                                else if (json[i] == '"')
                                {
                                    i++;
                                    while (i < json.Length && json[i] != '"')
                                    {
                                        if (json[i] == '\\') i++;
                                        i++;
                                    }
                                }
                                i++;
                            } while (i < json.Length && depth > 0);
                            var nested = json.Substring(start, i - start);
                            sb.Append('"');
                            sb.Append(nested.Replace("\\", "\\\\").Replace("\"", "\\\""));
                            sb.Append('"');
                        }
                    }
                    else if (i < json.Length && json[i] == '{')
                    {
                        // Вложенный объект → экранируем в строку
                        int depth = 0, start = i;
                        do
                        {
                            if (json[i] == '{') depth++;
                            else if (json[i] == '}') depth--;
                            else if (json[i] == '"')
                            {
                                i++;
                                while (i < json.Length && json[i] != '"')
                                {
                                    if (json[i] == '\\') i++;
                                    i++;
                                }
                            }
                            i++;
                        } while (i < json.Length && depth > 0);
                        var nested = json.Substring(start, i - start);
                        sb.Append('"');
                        sb.Append(nested.Replace("\\", "\\\\").Replace("\"", "\\\""));
                        sb.Append('"');
                    }
                    // Не добавляем символ — continue на следующую итерацию
                    continue;
                }

                sb.Append(json[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Проверяет, что массив по позиции start — это "items":[...] обёртки PocketBase,
        /// а не JSON-поле записи. Ищем "items" перед позицией start.
        /// Верхнеуровневый items находится на глубине 1 (внутри корневого {}).
        /// </summary>
        private static bool IsTopLevelItemsArray(string json, int arrayStart)
        {
            // Ищем назад от arrayStart: пропускаем пробелы, ':', пробелы, должно быть "items"
            int p = arrayStart - 1;
            while (p >= 0 && (json[p] == ' ' || json[p] == '\t')) p--;
            if (p < 0 || json[p] != ':') return false;
            p--;
            while (p >= 0 && (json[p] == ' ' || json[p] == '\t')) p--;
            if (p < 0 || json[p] != '"') return false;
            // Читаем ключ назад
            p--;
            int keyEnd = p;
            while (p >= 0 && json[p] != '"') p--;
            if (p < 0) return false;
            var key = json.Substring(p + 1, keyEnd - p);
            if (key != "items") return false;

            // Проверяем глубину: считаем '{' и '}' от начала до p
            int depth = 0;
            for (int k = 0; k < p; k++)
            {
                if (json[k] == '{') depth++;
                else if (json[k] == '}') depth--;
                else if (json[k] == '"')
                {
                    k++;
                    while (k < p && json[k] != '"')
                    {
                        if (json[k] == '\\') k++;
                        k++;
                    }
                }
            }
            return depth == 1; // Внутри корневого {} = глубина 1
        }

        private async Task<string> GetAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);
            await SendAsync(req);
            if (req.isNetworkError || req.isHttpError)
            {
                Debug.LogWarning($"[PocketBaseProvider] GET {url} → {req.responseCode} {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }

        private async Task<bool> PostAsync(string path, string jsonBody)
        {
            var url       = $"{_baseUrl}{path}";
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);
            Debug.Log($"[PB] POST {url}  body={jsonBody}");
            await SendAsync(req);
            Debug.Log($"[PB] POST ← {req.responseCode}  error='{req.error}'  response='{req.downloadHandler?.text}'");
            if (req.isNetworkError || req.isHttpError) return false;
            return true;
        }

        private async Task<bool> PatchAsync(string path, string jsonBody)
        {
            var url       = $"{_baseUrl}{path}";
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            using var req = new UnityWebRequest(url, "PATCH");
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);
            Debug.Log($"[PB] PATCH {url}  body={jsonBody}");  // PATCH log
            await SendAsync(req);
            Debug.Log($"[PB] PATCH ← {req.responseCode}  networkError={req.isNetworkError}  httpError={req.isHttpError}  error='{req.error}'  body='{req.downloadHandler?.text}'");
            if (req.isNetworkError || req.isHttpError) return false;
            return true;
        }

        private static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        /// <summary>Экранирует строку для вставки в JSON-значение.</summary>
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        // ── PocketBase DTOs ───────────────────────────────────────────────────

        /// <summary>
        /// avg и count считаются по последней оценке каждого уникального игрока.
        /// Так отражается текущее мнение, а история оценок хранится в БД.
        /// </summary>
        private static LiveOpsRatingData CalcRating(List<PbRating> items, string projectId, string version, string playerId)
        {
            // latest[player_id] = последняя запись (самая новая created)
            var latest = new System.Collections.Generic.Dictionary<string, PbRating>();
            foreach (var r in items)
            {
                if (r.project_id != projectId) continue;
                if (r.version != version) continue;
                if (!latest.TryGetValue(r.player_id, out var prev) ||
                    string.Compare(r.created, prev.created, StringComparison.Ordinal) > 0)
                    latest[r.player_id] = r;
            }

            if (latest.Count == 0)
                return new LiveOpsRatingData { version = version, avg = 0f, count = 0 };

            float sum = 0; int userVote = 0;
            foreach (var r in latest.Values)
            {
                sum += r.score;
                if (r.player_id == playerId) userVote = r.score;
            }

            return new LiveOpsRatingData
            {
                version  = version,
                avg      = sum / latest.Count,
                count    = latest.Count,
                userVote = userVote,
            };
        }

        /// <summary>
        /// Создаёт LocalizedString из JSON-поля (приоритет) или из legacy _ru/_en полей.
        /// JSON-поле: {"ru":"Текст","en":"Text","de":"Text",...} — любое количество языков.
        /// </summary>
        private static LocalizedString MakeLocalized(string json, string legacyRu = null, string legacyEn = null)
        {
            // Приоритет: JSON-поле со всеми переводами
            if (!string.IsNullOrEmpty(json) && json.TrimStart().StartsWith("{"))
                return LocalizedString.FromJson(json);

            // Fallback: legacy _ru/_en поля
            var ls = new LocalizedString();
            if (!string.IsNullOrEmpty(json)) ls.translations["en"] = json; // plain string = en
            if (!string.IsNullOrEmpty(legacyRu)) ls.translations["ru"] = legacyRu;
            if (!string.IsNullOrEmpty(legacyEn)) ls.translations["en"] = legacyEn;
            return ls;
        }

        [Serializable] private class PbListResponse<T> { public T[] items; }

        [Serializable]
        private class PbRating
        {
            public string id;
            public string project_id;
            public string version;
            public int    score;
            public string player_id;
            public string created;
        }

        [Serializable]
        private class PbMyMessagesResponse
        {
            public PbMyMessageItem[] items;
        }

        [Serializable]
        private class PbMyMessageItem
        {
            public string id;
            public string message;
            public string reply;
            public string reply_localized; // JSON: {"ru":"...","en":"..."}
            public string reply_status;
            public string category;
            public string timestamp;
            public string created;
        }

        [Serializable]
        private class PbConfirmRequest
        {
            public string[] ids;
        }

        [Serializable]
        private class PbConfirmResponse
        {
            public int updated;
        }

        [Serializable]
        private class PbAnnouncement
        {
            public string id;
            public string project_id;
            public string title;
            public string body;
            public string url;
            public string created;
            // Legacy
            public string title_ru;
            public string title_en;
            public string body_ru;
            public string body_en;
        }

        [Serializable]
        private class PbMessage
        {
            public string id;
            public string project_id;
            public string title;
            public string body;
            public string type;
            public string expires_at;
            public string created;
            // Legacy
            public string title_ru;
            public string title_en;
            public string body_ru;
            public string body_en;
        }

        [Serializable]
        private class PbMilestone
        {
            public string id;
            public string project_id;
            // JSON-поля: {"ru":"...","en":"...","de":"...",...}
            public string title;
            public string description;
            public string unit;
            public int    current;
            public int    target;    // На сервере поле называется "target"
            public string updated;
            // Legacy _ru/_en (обратная совместимость)
            public string description_ru;
            public string description_en;
            public string unit_ru;
            public string unit_en;
        }

        [Serializable]
        private class PbContentOrder
        {
            public string id;
            public string project_id;
            public string order;  // JSON-массив: [{"type":"poll","id":"..."}, ...]
        }

        [Serializable]
        private class PbDevLog
        {
            public string id;
            public string project_id;
            public string focus;
            public string title;
            public string description;
            public string items;       // JSON-массив: [{"label":{...},"done":true}, ...]
            public bool   is_active;
            public string updated;
        }

        /// <summary>
        /// Парсит JSON-массив items девлога.
        /// Формат: [{"label":{"ru":"...","en":"..."},"done":true}, ...]
        /// JsonUtility не справляется с вложенными объектами внутри массива,
        /// поэтому парсим вручную.
        /// </summary>
        private static LiveOpsDevLogItem[] ParseDevLogItems(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<LiveOpsDevLogItem>();
            json = json.Trim();
            if (json.Length < 2 || json[0] != '[') return Array.Empty<LiveOpsDevLogItem>();

            var result = new List<LiveOpsDevLogItem>();
            int i = 1; // skip '['
            while (i < json.Length)
            {
                // Пропуск пробелов/запятых
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t'))
                    i++;
                if (i >= json.Length || json[i] == ']') break;

                if (json[i] == '{')
                {
                    // Извлекаем объект целиком
                    int depth = 0, start = i;
                    do
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') depth--;
                        else if (json[i] == '"')
                        {
                            i++;
                            while (i < json.Length && json[i] != '"')
                            {
                                if (json[i] == '\\') i++;
                                i++;
                            }
                        }
                        i++;
                    } while (i <= json.Length && depth > 0);

                    var obj = json.Substring(start, i - start);
                    result.Add(ParseDevLogItem(obj));
                }
                else i++;
            }
            return result.ToArray();
        }

        /// <summary>Парсит один элемент девлога: {"name":{...},"status":"done"/"wip"/"todo"}</summary>
        private static LiveOpsDevLogItem ParseDevLogItem(string obj)
        {
            var item = new LiveOpsDevLogItem();

            // Ищем "name": {...}
            item.name = ExtractLocalizedField(obj, "name");

            // Ищем "status": "done"/"wip"/"todo"
            item.status = ExtractStringField(obj, "status") ?? "todo";

            return item;
        }

        /// <summary>Извлекает вложенный JSON-объект по имени поля и парсит как LocalizedString.</summary>
        private static LocalizedString ExtractLocalizedField(string obj, string fieldName)
        {
            int idx = obj.IndexOf($"\"{fieldName}\"", StringComparison.Ordinal);
            if (idx < 0) return new LocalizedString();

            int colonIdx = obj.IndexOf(':', idx + fieldName.Length + 2);
            if (colonIdx < 0) return new LocalizedString();

            int s = colonIdx + 1;
            while (s < obj.Length && obj[s] == ' ') s++;
            if (s >= obj.Length || obj[s] != '{') return new LocalizedString();

            int depth = 0, start = s;
            do
            {
                if (obj[s] == '{') depth++;
                else if (obj[s] == '}') depth--;
                else if (obj[s] == '"')
                {
                    s++;
                    while (s < obj.Length && obj[s] != '"')
                    {
                        if (obj[s] == '\\') s++;
                        s++;
                    }
                }
                s++;
            } while (s <= obj.Length && depth > 0);
            return LocalizedString.FromJson(obj.Substring(start, s - start));
        }

        /// <summary>Извлекает строковое значение поля из JSON-объекта.</summary>
        private static string ExtractStringField(string obj, string fieldName)
        {
            int idx = obj.IndexOf($"\"{fieldName}\"", StringComparison.Ordinal);
            if (idx < 0) return null;

            int colonIdx = obj.IndexOf(':', idx + fieldName.Length + 2);
            if (colonIdx < 0) return null;

            int s = colonIdx + 1;
            while (s < obj.Length && obj[s] == ' ') s++;
            if (s >= obj.Length || obj[s] != '"') return null;

            s++; // skip opening "
            int start = s;
            while (s < obj.Length && obj[s] != '"')
            {
                if (obj[s] == '\\') s++;
                s++;
            }
            return obj.Substring(start, s - start);
        }

        // ── Poll DTOs & helpers ─────────────────────────────────────────────

        [Serializable]
        private class PbPoll
        {
            public string id;
            public string project_id;
            public string question;     // JSON: {"ru":"...","en":"..."}
            public string poll_type;    // "single" / "multi"
            public string options;      // JSON-массив: [{"id":"...","text":{...}}, ...]
            public string expires_at;
            public bool   is_active;
        }

        [Serializable]
        private class PbPollVote
        {
            public string id;
            public string poll_id;
            public string player_id;
            public string option_ids;   // JSON-массив: ["opt_1","opt_2"]
        }

        /// <summary>Парсит JSON-массив опций опроса: [{"id":"...","text":{...}}, ...]</summary>
        private static LiveOpsPollOption[] ParsePollOptions(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<LiveOpsPollOption>();
            json = json.Trim();
            if (json.Length < 2 || json[0] != '[') return Array.Empty<LiveOpsPollOption>();

            var result = new List<LiveOpsPollOption>();
            int i = 1;
            while (i < json.Length)
            {
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t'))
                    i++;
                if (i >= json.Length || json[i] == ']') break;

                if (json[i] == '{')
                {
                    int depth = 0, start = i;
                    do
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') depth--;
                        else if (json[i] == '"')
                        {
                            i++;
                            while (i < json.Length && json[i] != '"')
                            {
                                if (json[i] == '\\') i++;
                                i++;
                            }
                        }
                        i++;
                    } while (i <= json.Length && depth > 0);

                    var obj = json.Substring(start, i - start);
                    var opt = new LiveOpsPollOption
                    {
                        id    = ExtractStringField(obj, "id"),
                        label = ExtractLocalizedField(obj, "text"),
                    };
                    result.Add(opt);
                }
                else i++;
            }
            return result.ToArray();
        }

        /// <summary>Парсит JSON-массив строк: ["a","b","c"]</summary>
        private static string[] ParseStringArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            json = json.Trim();
            if (json.Length < 2 || json[0] != '[') return null;

            var result = new List<string>();
            int i = 1;
            while (i < json.Length)
            {
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t'))
                    i++;
                if (i >= json.Length || json[i] == ']') break;

                if (json[i] == '"')
                {
                    i++;
                    int start = i;
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\') i++;
                        i++;
                    }
                    result.Add(json.Substring(start, i - start));
                    if (i < json.Length) i++;
                }
                else i++;
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>Формирует JSON-массив строк: ["a","b"]</summary>
        private static string ToJsonArray(string[] arr)
        {
            if (arr == null || arr.Length == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(arr[i]).Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Парсит JSON-массив записей порядка контента: [{"type":"poll","id":"..."}, ...]</summary>
        private static LiveOpsContentOrderEntry[] ParseContentOrderEntries(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<LiveOpsContentOrderEntry>();
            json = json.Trim();
            if (json.Length < 2 || json[0] != '[') return Array.Empty<LiveOpsContentOrderEntry>();

            var result = new List<LiveOpsContentOrderEntry>();
            int i = 1;
            while (i < json.Length)
            {
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t'))
                    i++;
                if (i >= json.Length || json[i] == ']') break;

                if (json[i] == '{')
                {
                    int depth = 0, start = i;
                    do
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') depth--;
                        else if (json[i] == '"')
                        {
                            i++;
                            while (i < json.Length && json[i] != '"')
                            {
                                if (json[i] == '\\') i++;
                                i++;
                            }
                        }
                        i++;
                    } while (i <= json.Length && depth > 0);

                    var obj = json.Substring(start, i - start);
                    var entry = new LiveOpsContentOrderEntry
                    {
                        type = ExtractStringField(obj, "type"),
                        id   = ExtractStringField(obj, "id"),
                    };
                    if (!string.IsNullOrEmpty(entry.type))
                        result.Add(entry);
                }
                else i++;
            }
            return result.ToArray();
        }

        /// <summary>Применяет результаты серверной агрегации к опросу.</summary>
        private static void ApplyPollResults(LiveOpsPoll poll, string json)
        {
            // Ответ: {"poll_id":"...","total_voters":42,"options":[{"id":"...","votes":18,"percent":43},...]}
            // Парсим вручную — JsonUtility не справится с массивом объектов
            var totalVotersStr = ExtractStringField(json, "total_voters");
            // total_voters — число, не строка. Используем другой подход.
            int totalIdx = json.IndexOf("\"total_voters\"", StringComparison.Ordinal);
            if (totalIdx >= 0)
            {
                int colonIdx = json.IndexOf(':', totalIdx + 14);
                if (colonIdx >= 0)
                {
                    int s = colonIdx + 1;
                    while (s < json.Length && json[s] == ' ') s++;
                    int numStart = s;
                    while (s < json.Length && json[s] >= '0' && json[s] <= '9') s++;
                    if (s > numStart && int.TryParse(json.Substring(numStart, s - numStart), out int total))
                        poll.votesTotal = total;
                }
            }

            // Парсим options массив
            int optIdx = json.IndexOf("\"options\"", StringComparison.Ordinal);
            if (optIdx < 0) return;
            int optColon = json.IndexOf(':', optIdx + 9);
            if (optColon < 0) return;
            int arrStart = json.IndexOf('[', optColon);
            if (arrStart < 0) return;

            // Для каждого option в результатах обновляем votes
            int pos = arrStart + 1;
            while (pos < json.Length)
            {
                while (pos < json.Length && json[pos] != '{' && json[pos] != ']') pos++;
                if (pos >= json.Length || json[pos] == ']') break;

                int depth = 0, objStart = pos;
                do
                {
                    if (json[pos] == '{') depth++;
                    else if (json[pos] == '}') depth--;
                    else if (json[pos] == '"')
                    {
                        pos++;
                        while (pos < json.Length && json[pos] != '"')
                        {
                            if (json[pos] == '\\') pos++;
                            pos++;
                        }
                    }
                    pos++;
                } while (pos <= json.Length && depth > 0);

                var resObj = json.Substring(objStart, pos - objStart);
                var resId  = ExtractStringField(resObj, "id");
                if (resId == null) continue;

                // Извлекаем votes (число)
                int votesIdx = resObj.IndexOf("\"votes\"", StringComparison.Ordinal);
                if (votesIdx >= 0)
                {
                    int vc = resObj.IndexOf(':', votesIdx + 7);
                    if (vc >= 0)
                    {
                        int vs = vc + 1;
                        while (vs < resObj.Length && resObj[vs] == ' ') vs++;
                        int ns = vs;
                        while (vs < resObj.Length && resObj[vs] >= '0' && resObj[vs] <= '9') vs++;
                        if (vs > ns && int.TryParse(resObj.Substring(ns, vs - ns), out int votes))
                        {
                            foreach (var opt in poll.options)
                                if (opt.id == resId) opt.votes = votes;
                        }
                    }
                }
            }
        }
    }
}
