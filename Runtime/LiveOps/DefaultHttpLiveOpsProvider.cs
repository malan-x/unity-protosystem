// Packages/com.protosystem.core/Runtime/LiveOps/DefaultHttpLiveOpsProvider.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Универсальный HTTP-провайдер LiveOps.
    /// Подставляется автоматически если в LiveOpsConfig задан serverUrl
    /// и провайдер не установлен вручную через config.SetProvider().
    ///
    /// API контракт:
    ///   GET  {serverUrl}/config
    ///   GET  {serverUrl}/polls
    ///   POST {serverUrl}/polls/{id}/vote        body: { "optionIds": [...], "playerId": "..." }
    ///   GET  {serverUrl}/announcements
    ///   GET  {serverUrl}/devlog
    ///   GET  {serverUrl}/ratings?version={v}
    ///   POST {serverUrl}/ratings               body: { "version": "...", "score": 7, "playerId": "..." }
    ///   POST {serverUrl}/messages              body: { "playerId": "...", "message": "...", "category": "...", "tag": "..." }
    ///   POST {serverUrl}/events                body: { ... }
    ///
    /// Заголовки: X-Project-ID, X-Steam-ID (если задан playerId)
    /// </summary>
    public class DefaultHttpLiveOpsProvider : ILiveOpsProvider
    {
        private readonly string _baseUrl;
        private readonly string _projectId;
        private readonly string _playerId;
        private readonly float  _timeoutSeconds;

        public DefaultHttpLiveOpsProvider(string baseUrl, string projectId,
            string playerId = null, float timeoutSeconds = 10f)
        {
            _baseUrl        = baseUrl.TrimEnd('/');
            _projectId      = projectId;
            _playerId       = playerId;
            _timeoutSeconds = timeoutSeconds;
        }

        // ── Health ────────────────────────────────────────────────────────────

        /// <summary>
        /// Проверить доступность сервера (HEAD /config).
        /// Возвращает true если сервер ответил кодом < 500.
        /// </summary>
        public async Task<bool> PingAsync()
        {
            using var req = UnityWebRequest.Head($"{_baseUrl}/config");
            AddHeaders(req);
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);
            await SendAsync(req);
            return !req.isNetworkError && req.responseCode > 0 && req.responseCode < 500;
        }

        // ── Panel Config ──────────────────────────────────────────────────────

        public async Task<LiveOpsPanelConfig> FetchPanelConfigAsync()
        {
            var json = await GetAsync("/config");
            if (json == null) return null;
            try { return JsonUtility.FromJson<LiveOpsPanelConfig>(json); }
            catch { return null; }
        }

        // ── Messages ──────────────────────────────────────────────────────────

        public async Task<List<LiveOpsMessage>> FetchMessagesAsync()
        {
            var json = await GetAsync("/messages");
            return ParseList<LiveOpsMessage>(json, "messages");
        }

        // ── Polls ─────────────────────────────────────────────────────────────

        public async Task<List<LiveOpsPoll>> FetchPollsAsync()
        {
            var json = await GetAsync("/polls");
            return ParseList<LiveOpsPoll>(json, "polls");
        }

        public async Task<bool> SubmitPollAnswerAsync(LiveOpsPollAnswer answer)
        {
            var body = JsonUtility.ToJson(answer);
            return await PostAsync($"/polls/{answer.pollId}/vote", body);
        }

        // ── Announcements ─────────────────────────────────────────────────────

        public async Task<List<LiveOpsAnnouncement>> FetchAnnouncementsAsync()
        {
            var json = await GetAsync("/announcements");
            return ParseList<LiveOpsAnnouncement>(json, "announcements");
        }

        // ── DevLog ────────────────────────────────────────────────────────────

        public async Task<LiveOpsDevLog> FetchDevLogAsync()
        {
            var json = await GetAsync("/devlog");
            if (json == null) return null;
            try { return JsonUtility.FromJson<LiveOpsDevLog>(json); }
            catch { return null; }
        }

        // ── Milestone ──────────────────────────────────────────────────────────

        public async Task<LiveOpsMilestoneData> FetchMilestoneAsync()
        {
            var json = await GetAsync("/milestone");
            if (json == null) return null;
            try { return JsonUtility.FromJson<LiveOpsMilestoneData>(json); }
            catch { return null; }
        }

        // ── Rating ────────────────────────────────────────────────────────────

        public async Task<LiveOpsRatingData> FetchRatingAsync(string version)
        {
            var json = await GetAsync($"/ratings?version={UnityWebRequest.EscapeURL(version)}");
            if (json == null) return null;
            try { return JsonUtility.FromJson<LiveOpsRatingData>(json); }
            catch { return null; }
        }

        public async Task<LiveOpsRatingResult> SubmitRatingAsync(LiveOpsRatingSubmit submit)
        {
            var body = JsonUtility.ToJson(submit);
            var json = await PostAsyncWithResponse("/ratings", body);
            if (json == null) return null;
            try { return JsonUtility.FromJson<LiveOpsRatingResult>(json); }
            catch { return null; }
        }

        // ── Feedback ──────────────────────────────────────────────────────────

        public async Task<bool> SubmitFeedbackAsync(LiveOpsFeedback feedback)
        {
            var body = JsonUtility.ToJson(feedback);
            return await PostAsync("/messages", body);
        }

        // ── Analytics ─────────────────────────────────────────────────────────

        public async Task<bool> SendEventAsync(LiveOpsEvent evt)
        {
            var body = JsonUtility.ToJson(evt);
            return await PostAsync("/events", body);
        }

        // ── HTTP helpers ──────────────────────────────────────────────────────

        private async Task<string> GetAsync(string path)
        {
            using var req = UnityWebRequest.Get($"{_baseUrl}{path}");
            AddHeaders(req);
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);
            await SendAsync(req);
            if (req.isNetworkError || req.isHttpError) return null;
            return req.downloadHandler.text;
        }

        private async Task<bool> PostAsync(string path, string jsonBody)
        {
            return await PostAsyncWithResponse(path, jsonBody) != null;
        }

        private async Task<string> PostAsyncWithResponse(string path, string jsonBody)
        {
            var url = $"{_baseUrl}{path}";
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AddHeaders(req);
            req.timeout = Mathf.CeilToInt(_timeoutSeconds);

            Debug.Log($"[LiveOps HTTP] POST {url}  body={jsonBody}");
            await SendAsync(req);
            Debug.Log($"[LiveOps HTTP] ← {req.responseCode}  networkError={req.isNetworkError}  httpError={req.isHttpError}  error='{req.error}'  body='{req.downloadHandler?.text}'");

            if (req.isNetworkError || req.isHttpError) return null;
            return req.downloadHandler.text;
        }

        private void AddHeaders(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(_projectId))
                req.SetRequestHeader("X-Project-ID", _projectId);
            if (!string.IsNullOrEmpty(_playerId))
                req.SetRequestHeader("X-Steam-ID", _playerId);
        }

        // UnityWebRequest не имеет async/await — оборачиваем через TaskCompletionSource
        private static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        // JsonUtility не поддерживает массивы напрямую — используем враппер
        private static List<T> ParseList<T>(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                // Пробуем как массив через враппер { "items": [...] }
                var wrapped = $"{{\"items\":{json}}}";
                var wrapper = JsonUtility.FromJson<JsonListWrapper<T>>(wrapped);
                if (wrapper?.items != null) return new List<T>(wrapper.items);
            }
            catch { }
            return null;
        }

        [Serializable] private class JsonListWrapper<T> { public T[] items; }
    }
}
