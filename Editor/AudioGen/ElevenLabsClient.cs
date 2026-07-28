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
    /// только EditorPrefs — в git не попадает). Отдаёт MP3 44.1к — дальше общий конвейер
    /// ffmpeg → WAV. Нативные duration_seconds (0.5–30) и loop — трюки локальных движков
    /// (генерация с запасом, промпт «seamless loop») здесь не нужны.
    /// </summary>
    public static class ElevenLabsClient
    {
        private const string Endpoint =
            "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

        public static bool HasKey => !string.IsNullOrEmpty(AudioAiSettings.ElevenLabsApiKey);

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

            using var msg = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKey);
            msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(msg);
            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"ElevenLabs {(int)resp.StatusCode}: {Explain(err)}");
            }
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

            using var msg = new HttpRequestMessage(HttpMethod.Post, url);
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKey);
            msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = await Http.SendAsync(msg);
            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"ElevenLabs TTS {(int)resp.StatusCode}: {Explain(err)}");
            }
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

            using var msg = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/voices");
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKey);
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

            using var msg = new HttpRequestMessage(HttpMethod.Get,
                "https://api.elevenlabs.io/v1/user/subscription");
            msg.Headers.Add("xi-api-key", AudioAiSettings.ElevenLabsApiKey);
            try
            {
                var resp = await Http.SendAsync(msg);
                string json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return _creditsCache = new Credits
                    {
                        NoPermission = json.Contains("missing_permissions") || json.Contains("user_read"),
                    };

                return _creditsCache = new Credits
                {
                    Ok = true,
                    Used = ExtractJsonNumber(json, "character_count"),
                    Limit = ExtractJsonNumber(json, "character_limit"),
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
        private static string Explain(string errorJson)
        {
            if (string.IsNullOrEmpty(errorJson)) return "пустой ответ";
            if (errorJson.Contains("payment_issue"))
                return "проблема с оплатой подписки — завершите платёж на elevenlabs.io";
            if (errorJson.Contains("quota_exceeded"))
                return "кредиты кончились (лимит ключа или тарифа)";
            if (errorJson.Contains("invalid_api_key") || errorJson.Contains("missing_permissions"))
                return "ключ неверный или без нужного права (Sound Effects / Text to Speech)";
            if (errorJson.Contains("voice_not_found"))
                return "голос не найден — выберите голос заново в стиле";
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
