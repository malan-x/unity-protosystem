using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Клиент Meshy API (облако): image-to-3D, при пустом референсе — text-to-3D
    /// (preview → refine). Ключ — только в EditorPrefs. Кредиты Meshy дают полные
    /// коммерческие права на выходы.
    /// </summary>
    public static class MeshyClient
    {
        private const string Base = "https://api.meshy.ai";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

        public static async Task<ModelGenResult> GenerateAsync(ModelGenRequest req,
            string apiKey, Action<float, string> progress, CancellationToken ct)
        {
            var result = new ModelGenResult { Request = req };
            if (string.IsNullOrEmpty(apiKey))
            {
                result.Error = "Не задан API-ключ Meshy (Project Settings ▸ AI Model Tools).";
                return result;
            }
            try
            {
                bool hasImage = !string.IsNullOrEmpty(req.RefImagePath) && File.Exists(req.RefImagePath);
                string taskUrl, taskId;

                if (hasImage)
                {
                    progress?.Invoke(0.03f, "Meshy: image-to-3D");
                    string b64 = Convert.ToBase64String(File.ReadAllBytes(req.RefImagePath));
                    string body = "{\"image_url\":\"data:image/png;base64," + b64 + "\"," +
                                  "\"enable_pbr\":true,\"should_remesh\":true,\"should_texture\":true," +
                                  "\"target_polycount\":" + Math.Max(10000, req.MaxFaces) + "}";
                    taskId = await PostTask(Base + "/openapi/v1/image-to-3d", body, apiKey, ct);
                    taskUrl = Base + "/openapi/v1/image-to-3d/" + taskId;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(req.Prompt))
                    { result.Error = "Для Meshy нужен референс или текстовый промпт."; return result; }

                    progress?.Invoke(0.03f, "Meshy: text-to-3D (preview)");
                    string body = "{\"mode\":\"preview\",\"prompt\":" + Json(req.Prompt) +
                                  ",\"art_style\":\"realistic\",\"target_polycount\":" + Math.Max(10000, req.MaxFaces) + "}";
                    string previewId = await PostTask(Base + "/openapi/v2/text-to-3d", body, apiKey, ct);
                    await PollTask(Base + "/openapi/v2/text-to-3d/" + previewId, apiKey,
                        (p, s) => progress?.Invoke(0.05f + p * 0.35f, "preview: " + s), ct);

                    progress?.Invoke(0.45f, "Meshy: refine (текстуры)");
                    string refineBody = "{\"mode\":\"refine\",\"preview_task_id\":\"" + previewId + "\",\"enable_pbr\":true}";
                    taskId = await PostTask(Base + "/openapi/v2/text-to-3d", refineBody, apiKey, ct);
                    taskUrl = Base + "/openapi/v2/text-to-3d/" + taskId;
                }

                string finalJson = await PollTask(taskUrl, apiKey,
                    (p, s) => progress?.Invoke(0.45f + p * 0.45f, s), ct);

                string glbUrl = ExtractGlbUrl(finalJson);
                if (string.IsNullOrEmpty(glbUrl)) { result.Error = "Meshy не вернул ссылку на GLB."; return result; }

                progress?.Invoke(0.93f, "скачивание GLB");
                using (var dl = await Http.GetAsync(glbUrl, ct))
                {
                    dl.EnsureSuccessStatusCode();
                    result.GlbBytes = await dl.Content.ReadAsByteArrayAsync();
                }
                return result;
            }
            catch (OperationCanceledException) { result.Error = "Отменено."; return result; }
            catch (Exception e) { result.Error = Explain(e.Message); return result; }
        }

        /// <summary>
        /// Риггинг модели (гуманоид, лицом к +Z, с текстурами): GLB как Data URI.
        /// Возвращает id риг-задачи (для анимаций) и GLB с walk-анимацией.
        /// </summary>
        public static async Task<(string rigTaskId, byte[] walkGlb, string error)> RigAsync(
            byte[] glbBytes, string apiKey, float heightMeters,
            Action<float, string> progress, CancellationToken ct)
        {
            try
            {
                progress?.Invoke(0.02f, "Meshy: риггинг");
                string b64 = Convert.ToBase64String(glbBytes);
                string body = "{\"model_url\":\"data:model/gltf-binary;base64," + b64 + "\"," +
                              "\"height_meters\":" + heightMeters.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                string id = await PostTask(Base + "/openapi/v1/rigging", body, apiKey, ct);
                string json = await PollTask(Base + "/openapi/v1/rigging/" + id, apiKey,
                    (p, s) => progress?.Invoke(0.05f + p * 0.8f, "риггинг: " + s), ct);

                byte[] walk = null;
                string walkUrl = ExtractUrlAfter(json, "\"animated_glb_url\"") ?? ExtractUrlAfter(json, "\"walking_glb_url\"");
                if (walkUrl != null)
                {
                    progress?.Invoke(0.9f, "скачивание walk GLB");
                    using var dl = await Http.GetAsync(walkUrl, ct);
                    if (dl.IsSuccessStatusCode) walk = await dl.Content.ReadAsByteArrayAsync();
                }
                return (id, walk, null);
            }
            catch (OperationCanceledException) { return (null, null, "Отменено."); }
            catch (Exception e) { return (null, null, Explain(e.Message)); }
        }

        /// <summary>Применить анимацию из библиотеки Meshy к риг-задаче. Возвращает анимированный GLB.</summary>
        public static async Task<(byte[] glb, string error)> AnimateAsync(
            string rigTaskId, int actionId, string apiKey,
            Action<float, string> progress, CancellationToken ct)
        {
            try
            {
                progress?.Invoke(0.02f, $"Meshy: анимация #{actionId}");
                string body = "{\"rig_task_id\":\"" + rigTaskId + "\",\"action_id\":" + actionId + "}";
                string id = await PostTask(Base + "/openapi/v1/animations", body, apiKey, ct);
                string json = await PollTask(Base + "/openapi/v1/animations/" + id, apiKey,
                    (p, s) => progress?.Invoke(0.05f + p * 0.85f, $"анимация #{actionId}: " + s), ct);
                string url = ExtractUrlAfter(json, "\"animation_glb_url\"");
                if (url == null) return (null, "Meshy не вернул animation_glb_url.");
                progress?.Invoke(0.93f, "скачивание GLB");
                using var dl = await Http.GetAsync(url, ct);
                dl.EnsureSuccessStatusCode();
                return (await dl.Content.ReadAsByteArrayAsync(), null);
            }
            catch (OperationCanceledException) { return (null, "Отменено."); }
            catch (Exception e) { return (null, Explain(e.Message)); }
        }

        private static string ExtractUrlAfter(string json, string key)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return null;
            int q1 = json.IndexOf('"', json.IndexOf(':', i) + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 <= q1) return null;
            string url = json.Substring(q1 + 1, q2 - q1 - 1).Replace("\\u0026", "&").Replace("\\/", "/");
            return url.StartsWith("http") ? url : null;
        }

        /// <summary>Баланс кредитов (для тулбара студии). null — не удалось.</summary>
        public static async Task<int?> GetCreditsAsync(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return null;
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Get, Base + "/openapi/v1/balance");
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var resp = await Http.SendAsync(msg, cts.Token);
                string body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode) return null;
                int i = body.IndexOf("\"balance\"", StringComparison.Ordinal);
                if (i < 0) return null;
                i = body.IndexOf(':', i) + 1;
                int end = i;
                while (end < body.Length && (char.IsDigit(body[end]) || body[end] == ' ')) end++;
                return int.TryParse(body[i..end].Trim(), out int v) ? v : null;
            }
            catch { return null; }
        }

        private static async Task<string> PostTask(string url, string body, string apiKey, CancellationToken ct)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, url);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            msg.Content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await Http.SendAsync(msg, ct);
            string respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception((int)resp.StatusCode + ": " + respBody);
            string id = Hy3DComfyClient.ExtractJsonString(respBody, "result");
            if (string.IsNullOrEmpty(id)) throw new Exception("Meshy не вернул id задачи: " + respBody);
            return id;
        }

        private static async Task<string> PollTask(string url, string apiKey,
            Action<float, string> progress, CancellationToken ct)
        {
            var started = DateTime.UtcNow;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(5000, ct);
                using var msg = new HttpRequestMessage(HttpMethod.Get, url);
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var resp = await Http.SendAsync(msg, ct);
                string body = await resp.Content.ReadAsStringAsync();
                string status = Hy3DComfyClient.ExtractJsonString(body, "status") ?? "";
                switch (status)
                {
                    case "SUCCEEDED": return body;
                    case "FAILED":
                    case "CANCELED":
                        throw new Exception("Meshy: задача " + status + " — " +
                            (Hy3DComfyClient.ExtractJsonString(body, "task_error") ?? body));
                }
                int prog = 0;
                int i = body.IndexOf("\"progress\"", StringComparison.Ordinal);
                if (i >= 0) { i = body.IndexOf(':', i) + 1; int e2 = i; while (e2 < body.Length && (char.IsDigit(body[e2]) || body[e2] == ' ')) e2++; int.TryParse(body[i..e2].Trim(), out prog); }
                progress?.Invoke(prog / 100f, status + " " + prog + "%");
                if ((DateTime.UtcNow - started).TotalMinutes > 25) throw new Exception("Meshy: таймаут 25 мин.");
            }
        }

        private static string ExtractGlbUrl(string json)
        {
            int i = json.IndexOf("\"model_urls\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int g = json.IndexOf("\"glb\"", i, StringComparison.Ordinal);
            if (g < 0) return null;
            int q1 = json.IndexOf('"', json.IndexOf(':', g) + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            return q1 > 0 && q2 > q1 ? json.Substring(q1 + 1, q2 - q1 - 1).Replace("\\u0026", "&").Replace("\\/", "/") : null;
        }

        private static string Json(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        private static string Explain(string raw)
        {
            if (raw.Contains("401")) return "Meshy: неверный API-ключ (401). " + raw;
            if (raw.Contains("402")) return "Meshy: кончились кредиты (402). " + raw;
            if (raw.Contains("429")) return "Meshy: превышен лимит запросов (429), подожди минуту. " + raw;
            return "Meshy: " + raw;
        }
    }
}
