using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>
    /// Облачный движок SFX: ElevenLabs Sound Effects API. Один endpoint
    /// (/v1/sound-generation), ключ в заголовке xi-api-key (AudioAiSettings.ElevenLabsApiKey,
    /// только EditorPrefs — в git не попадает). Запасные ключи (ElevenLabsReserveKeys) вступают по
    /// очереди, когда у текущего кончились кредиты. Отдаёт MP3 44.1к — дальше общий конвейер
    /// ffmpeg → WAV. Нативные duration_seconds (0.5–30) и loop — трюки локальных движков
    /// (генерация с запасом, промпт «seamless loop») здесь не нужны.
    /// </summary>
    public static class ElevenLabsClient
    {
        private const string Endpoint =
            "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

        public static bool HasKey => AudioAiSettings.ElevenLabsApiKeys.Count > 0;

        // ── Несколько ключей: кончились кредиты у текущего — тот же запрос уходит со следующим ──

        private static int _activeKey;

        /// <summary>Номер ключа, которым сейчас идут запросы (0 — основной), и сколько их всего.</summary>
        public static int ActiveKeyIndex
        {
            get
            {
                int n = AudioAiSettings.ElevenLabsApiKeys.Count;
                return n == 0 ? 0 : _activeKey % n;
            }
        }

        public static int KeyCount => AudioAiSettings.ElevenLabsApiKeys.Count;

        /// <summary>
        /// Ошибка «этот ключ больше не годится» — кредиты/квота кончились, оплата, ключ погашен.
        /// На ней берём следующий ключ; на прочих (голос не найден, плохой текст, 429) — ключ ни при чём.
        /// </summary>
        private static bool IsKeyExhausted(int status, string err)
        {
            if (status == 402) return true;
            if (string.IsNullOrEmpty(err)) return false;
            return err.Contains("quota_exceeded") || err.Contains("payment_issue") ||
                   err.Contains("payment_required") || err.Contains("invalid_api_key") ||
                   err.Contains("detected_unusual_activity");
        }

        /// <summary>
        /// Отправить запрос, перебирая ключи с текущего по кругу. build — каждый раз новый
        /// HttpRequestMessage (повторно отправлять один и тот же нельзя). Удачный ключ становится текущим.
        /// </summary>
        private static async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> build, string what)
        {
            var keys = AudioAiSettings.ElevenLabsApiKeys;
            if (keys.Count == 0)
                throw new Exception("ElevenLabs: не задан API-ключ (Project Settings ▸ AI Audio Tools).");

            int start = _activeKey % keys.Count;
            string lastErr = null;
            for (int tried = 0; tried < keys.Count; tried++)
            {
                int idx = (start + tried) % keys.Count;
                var msg = build();
                msg.Headers.Add("xi-api-key", keys[idx]);
                HttpResponseMessage resp;
                try { resp = await Http.SendAsync(msg); }
                finally { msg.Dispose(); }

                if (resp.IsSuccessStatusCode)
                {
                    if (idx != _activeKey % keys.Count)
                    {
                        UnityEngine.Debug.Log($"[ElevenLabs] работаю ключом {idx + 1} из {keys.Count}");
                        _activeKey = idx;
                        InvalidateCredits();
                        InvalidateVoices();
                    }
                    return resp;
                }

                string err = await resp.Content.ReadAsStringAsync();
                int status = (int)resp.StatusCode;
                resp.Dispose();
                string where = keys.Count > 1 ? $" (ключ {idx + 1} из {keys.Count})" : "";
                if (!IsKeyExhausted(status, err))
                    throw new Exception($"{what} {status}{where}: {Explain(err, idx)}");

                lastErr = $"{status}: {Explain(err, idx)}";
                if (keys.Count > 1 && tried < keys.Count - 1)
                    UnityEngine.Debug.LogWarning($"[ElevenLabs] ключ {idx + 1} из {keys.Count}: {Explain(err, idx)} — беру следующий");
            }
            throw new Exception(keys.Count > 1
                ? $"{what}: ни один из {keys.Count} ключей не подошёл — последний ответ {lastErr}"
                : $"{what} {lastErr}");
        }

        /// <summary>Сгенерировать SFX. Возвращает MP3-байты или бросает с понятным текстом.</summary>
        public static async Task<byte[]> GenerateAsync(ComfyAudioClient.AudioRequest request)
        {
            if (!HasKey)
                throw new Exception("ElevenLabs: не задан API-ключ (Project Settings ▸ AI Audio Tools).");

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            float seconds = UnityEngine.Mathf.Clamp(request.Seconds <= 0f ? 2f : request.Seconds, 0.5f, 30f);
            float influence = request.PromptInfluence <= 0f ? 0.3f : UnityEngine.Mathf.Clamp01(request.PromptInfluence);

            string body = "{" +
                "\"text\":\"" + EscapeJson(request.Positive) + "\"," +
                "\"duration_seconds\":" + seconds.ToString(ic) + "," +
                "\"prompt_influence\":" + influence.ToString(ic) +
                (request.Loop ? ",\"loop\":true" : "") +
            "}";

            using var resp = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }, "ElevenLabs");
            return await resp.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Озвучить текст (TTS). request.Positive — произносимый текст, VoiceId обязателен.
        /// seed передаётся — best-effort воспроизводимость рецептов (TTS не полностью детерминирован).
        /// Возвращает MP3-байты.
        /// </summary>
        public static async Task<byte[]> TextToSpeechAsync(ComfyAudioClient.AudioRequest request)
        {
            if (!HasKey)
                throw new Exception("ElevenLabs: не задан API-ключ (Project Settings ▸ AI Audio Tools).");
            if (string.IsNullOrEmpty(request.VoiceId))
                throw new Exception("ElevenLabs TTS: не выбран голос (voiceId в стиле).");
            if (string.IsNullOrWhiteSpace(request.Positive))
                throw new Exception("ElevenLabs TTS: пустой текст реплики.");

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string model = string.IsNullOrEmpty(request.TtsModelId) ? "eleven_multilingual_v2" : request.TtsModelId;
            float stability = request.TtsStability <= 0f ? 0.45f : UnityEngine.Mathf.Clamp01(request.TtsStability);
            float similarity = request.TtsSimilarity <= 0f ? 0.75f : UnityEngine.Mathf.Clamp01(request.TtsSimilarity);

            // У eleven_v3 свой формат настроек: stability только 0.0/0.5/1.0
            // (creative/natural/robust), similarity_boost не принимается.
            string settings = model.StartsWith("eleven_v3")
                ? "\"voice_settings\":{\"stability\":" +
                  (stability < 0.25f ? "0.0" : stability > 0.75f ? "1.0" : "0.5") + "}"
                : "\"voice_settings\":{\"stability\":" + stability.ToString(ic) +
                  ",\"similarity_boost\":" + similarity.ToString(ic) + "}";

            string url = "https://api.elevenlabs.io/v1/text-to-speech/" +
                Uri.EscapeDataString(request.VoiceId) + "?output_format=mp3_44100_128";
            string body = "{" +
                "\"text\":\"" + EscapeJson(request.Positive) + "\"," +
                "\"model_id\":\"" + EscapeJson(model) + "\"," +
                "\"seed\":" + Math.Abs(request.Seed) + "," +
                settings +
            "}";

            using var resp = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }, "ElevenLabs TTS");
            return await resp.Content.ReadAsByteArrayAsync();
        }

        // ── Список голосов аккаунта (для дропдауна в студии) ──

        private static List<(string id, string name)> _voicesCache;

        public static void InvalidateVoices() => _voicesCache = null;

        /// <summary>Голоса аккаунта (нужно право Voices Read). Кэшируется до перезапуска.</summary>
        public static async Task<List<(string id, string name)>> GetVoicesAsync()
        {
            if (_voicesCache != null) return _voicesCache;
            if (!HasKey) return new List<(string, string)>();

            // Голоса — того аккаунта, чьим ключом сейчас идут запросы
            using var msg = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/voices");
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKeys[ActiveKeyIndex]);
            var resp = await Http.SendAsync(msg);
            if (!resp.IsSuccessStatusCode) return new List<(string, string)>();

            string json = await resp.Content.ReadAsStringAsync();
            _voicesCache = ParseVoices(json);
            return _voicesCache;
        }

        /// <summary>
        /// Мини-парсер списка голосов: пары voice_id/name идут в объектах массива voices.
        /// Полный JSON-парсер не тащим (паттерн проекта).
        /// </summary>
        private static List<(string id, string name)> ParseVoices(string json)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(json)) return result;

            int pos = 0;
            while (true)
            {
                int idKey = json.IndexOf("\"voice_id\"", pos, StringComparison.Ordinal);
                if (idKey < 0) break;
                string id = ExtractStringAfter(json, idKey);
                int nameKey = json.IndexOf("\"name\"", idKey, StringComparison.Ordinal);
                string name = nameKey < 0 ? id : ExtractStringAfter(json, nameKey);
                if (!string.IsNullOrEmpty(id)) result.Add((id, name ?? id));
                pos = idKey + 10;
            }
            return result;
        }

        private static string ExtractStringAfter(string json, int keyIndex)
        {
            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0) return null;
            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            var sb = new StringBuilder();
            for (int p = start + 1; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length) { sb.Append(json[++p]); continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Баланс кредитов (нужно право User Read у ключа) ──

        public struct Credits
        {
            public bool Ok;
            public bool NoPermission;
            public long Used;
            public long Limit;
            public long Remaining => Limit - Used;
            /// <summary>Чей это остаток: номер текущего ключа (0 — основной) из KeyCount.</summary>
            public int KeyIndex;
            public int KeyCount;
        }

        private static Credits _creditsCache;
        private static double _creditsFetchedAt = -999999;

        public static void InvalidateCredits() => _creditsFetchedAt = -999999;

        /// <summary>Остаток кредитов подписки. Кэш 30 с — дёргать можно свободно.</summary>
        public static async Task<Credits> GetCreditsAsync()
        {
            if (UnityEditor.EditorApplication.timeSinceStartup - _creditsFetchedAt < 30.0)
                return _creditsCache;
            _creditsFetchedAt = UnityEditor.EditorApplication.timeSinceStartup;

            if (!HasKey) return _creditsCache = new Credits();

            // Остаток текущего ключа: запасные вступают, только когда он кончится
            int keyIndex = ActiveKeyIndex, keyCount = KeyCount;
            using var msg = new HttpRequestMessage(HttpMethod.Get,
                "https://api.elevenlabs.io/v1/user/subscription");
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKeys[keyIndex]);
            try
            {
                var resp = await Http.SendAsync(msg);
                string json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return _creditsCache = new Credits
                    {
                        NoPermission = json.Contains("missing_permissions") || json.Contains("user_read"),
                        KeyIndex = keyIndex,
                        KeyCount = keyCount,
                    };

                return _creditsCache = new Credits
                {
                    Ok = true,
                    Used = ExtractJsonNumber(json, "character_count"),
                    Limit = ExtractJsonNumber(json, "character_limit"),
                    KeyIndex = keyIndex,
                    KeyCount = keyCount,
                };
            }
            catch { return _creditsCache = new Credits(); }
        }

        private static long ExtractJsonNumber(string json, string key)
        {
            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return 0;
            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return 0;
            int p = i + 1;
            while (p < json.Length && (json[p] == ' ' || json[p] == '\t')) p++;
            long value = 0;
            bool any = false;
            for (; p < json.Length && char.IsDigit(json[p]); p++)
            {
                value = value * 10 + (json[p] - '0');
                any = true;
            }
            return any ? value : 0;
        }

        /// <summary>Человеческое объяснение типовых ошибок API вместо сырого JSON.</summary>
        private static string Explain(string errorJson, int keyIndex = 0)
        {
            if (string.IsNullOrEmpty(errorJson)) return "пустой ответ";
            if (errorJson.Contains("payment_issue") || errorJson.Contains("payment_required"))
                return "проблема с оплатой подписки — завершите платёж на elevenlabs.io";
            if (errorJson.Contains("quota_exceeded"))
                return "кредиты кончились (лимит ключа или тарифа)";
            if (errorJson.Contains("invalid_api_key") || errorJson.Contains("missing_permissions"))
                return "ключ неверный или без нужного права (Sound Effects / Text to Speech)";
            if (errorJson.Contains("voice_not_found"))
                return keyIndex > 0
                    ? "голос не найден у аккаунта запасного ключа — голоса из библиотеки добавьте и в этот аккаунт"
                    : "голос не найден — выберите голос заново в стиле";
            return errorJson.Length > 200 ? errorJson.Substring(0, 200) + "…" : errorJson;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
        }
    }
}
