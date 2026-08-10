using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProtoSystem.AudioGen.Editor
{
    /// <summary>Движок генерации: какая модель и какой граф ComfyUI собирается.</summary>
    public enum AudioEngine
    {
        /// <summary>Stable Audio Open — SFX, эмбиенты, короткие лупы (до ~47 с). Промпт + негатив.</summary>
        StableAudio = 0,

        /// <summary>ACE-Step — музыка/стингеры (теги + лирика, "[inst]" — инструментал).</summary>
        AceStep = 1,

        /// <summary>ElevenLabs Sound Effects — облако: качество выше, нативные лупы, нужен API-ключ.</summary>
        ElevenLabs = 2,

        /// <summary>ElevenLabs TTS — озвучка реплик (нужны права Text to Speech + Voices Read у ключа).</summary>
        ElevenLabsTts = 3,

        /// <summary>Qwen3-TTS — ЛОКАЛЬНАЯ озвучка (qwentts.cpp + GGUF): бесплатные итерации, offline.</summary>
        QwenTts = 4,
    }

    /// <summary>
    /// Клиент локального ComfyUI (тот же сервер, что у арт-студии) для генерации звука.
    /// Только редактор, только транспорт — стиль/промпт задаёт вызывающий (AudioStylePreset).
    ///
    /// Сервер отдаёт результат как FLAC (WAV ComfyUI не пишет) — конвертация в WAV для
    /// Unity-импорта лежит на вызывающем (AudioConvert, ffmpeg).
    /// Зеркало ComfyUIClient из ProtoSystem.IconGen: тот же polling /history, тот же мини-парсер.
    /// </summary>
    public static class ComfyAudioClient
    {
        private static string DefaultServer => AudioAiSettings.ComfyServer;

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };

        public struct AudioRequest
        {
            public AudioEngine Engine;

            /// <summary>StableAudio: позитивный промпт. AceStep: строка тегов (жанр, настроение, инструменты).</summary>
            public string Positive;

            /// <summary>Негатив (только StableAudio; AceStep использует ConditioningZeroOut).</summary>
            public string Negative;

            /// <summary>Лирика AceStep. Пусто → "[inst]" (инструментал). StableAudio игнорирует.</summary>
            public string Lyrics;
            public float LyricsStrength;   // 0 — дефолт 1.0

            public float Seconds;          // длительность результата
            public int Seed;
            public int Steps;
            public float Cfg;
            public string Sampler;         // пусто — дефолт движка
            public string Scheduler;

            /// <summary>Луп (нативно поддерживает только ElevenLabs; локальным движкам — подсказка промптом).</summary>
            public bool Loop;

            /// <summary>ElevenLabs: строгость следования промпту 0..1 (0 — дефолт 0.3).</summary>
            public float PromptInfluence;

            // ── TTS (ElevenLabsTts / QwenTts): Positive = произносимый текст ──
            public string VoiceId;
            public string TtsModelId;      // пусто — eleven_multilingual_v2
            public float TtsStability;     // 0 — дефолт 0.45
            public float TtsSimilarity;    // 0 — дефолт 0.75

            /// <summary>QwenTts: язык синтеза («Russian», «English»). Lyrics = описание голоса (voice design).</summary>
            public string TtsLanguage;

            /// <summary>ffmpeg-фильтр постобработки (рация и т.п.). Применяется до трима тишины.</summary>
            public string PostFilter;

            /// <summary>Целевая интегральная громкость LUFS: loudnorm последним фильтром. 0 — без нормализации.</summary>
            public float TargetLufs;

            // Оверрайд чекпоинта для воспроизводимости рецептов. Пусто — глобальная настройка движка.
            public string CheckpointOverride;
            public bool HasModelOverride;

            public string EffCheckpoint => Engine == AudioEngine.QwenTts
                ? "qwen3-tts"
                : Engine == AudioEngine.ElevenLabsTts
                ? "elevenlabs-tts"
                : Engine == AudioEngine.ElevenLabs
                ? "elevenlabs"
                : HasModelOverride && !string.IsNullOrEmpty(CheckpointOverride)
                    ? CheckpointOverride
                    : (Engine == AudioEngine.AceStep ? AudioAiSettings.MusicCheckpoint : AudioAiSettings.SfxCheckpoint);
        }

        /// <summary>Чекпоинт принадлежит ACE-Step? По нему выбирается граф — движки несовместимы.</summary>
        public static bool IsAceCheckpoint(string name)
            => !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("ace");

        /// <summary>Жив ли сервер (быстрая проверка перед пачкой генераций).</summary>
        public static async Task<bool> IsOnlineAsync(string server = null)
        {
            server ??= DefaultServer;
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                var resp = await Http.GetAsync($"{server}/system_stats", cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>Чекпоинты, доступные на сервере (models/checkpoints) — для выпадающих списков в UI.</summary>
        public static async Task<List<string>> GetCheckpointsAsync(string server = null)
        {
            server ??= DefaultServer;
            string json = await GetAsync($"{server}/object_info/CheckpointLoaderSimple");
            return ParseFirstStringArray(json, "ckpt_name");
        }

        /// <summary>Сгенерировать один клип. Возвращает FLAC-байты или бросает с понятным текстом.</summary>
        public static async Task<byte[]> GenerateAsync(AudioRequest request, string server = null)
        {
            server ??= DefaultServer;
            string clientId = Guid.NewGuid().ToString("N");

            string workflow = BuildWorkflow(request);
            string body = $"{{\"prompt\":{workflow},\"client_id\":\"{clientId}\"}}";

            var queued = await PostJsonAsync($"{server}/prompt", body);
            string promptId = ExtractJsonString(queued, "prompt_id");
            if (string.IsNullOrEmpty(promptId))
                throw new Exception($"ComfyUI отклонил задачу: {Trim(queued)}");

            // Опрос истории до готовности. Сервер не пушит — только polling.
            // Таймаут щедрый: первый прогон грузит модель с диска (минуты), ACE на 2 мин трека тоже небыстр.
            var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(12);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(1500);
                string history = await GetAsync($"{server}/history/{promptId}");
                if (string.IsNullOrEmpty(history) || history.Trim() == "{}") continue;

                if (history.Contains("\"status_str\": \"error\"") || history.Contains("\"status_str\":\"error\""))
                    throw new Exception($"ComfyUI: ошибка выполнения графа. {ExtractError(history)}");

                string filename = ExtractJsonString(history, "filename");
                if (string.IsNullOrEmpty(filename)) continue;

                string subfolder = ExtractJsonString(history, "subfolder") ?? "";
                return await GetBytesAsync(
                    $"{server}/view?filename={Uri.EscapeDataString(filename)}" +
                    $"&subfolder={Uri.EscapeDataString(subfolder)}&type=output");
            }

            throw new Exception("ComfyUI: таймаут ожидания результата (12 мин).");
        }

        /// <summary>
        /// Граф ComfyUI как JSON. Держим строкой намеренно (как в арт-студии): типов нод в C#
        /// нет, граф фиксирован — плейсхолдеры только для промптов и параметров сэмплера.
        /// Обе модели — all-in-one чекпоинты: CheckpointLoaderSimple отдаёт MODEL+CLIP+VAE.
        /// </summary>
        private static string BuildWorkflow(AudioRequest r)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string ckpt = EscapeJson(r.EffCheckpoint);
            float seconds = r.Seconds <= 0f ? 5f : r.Seconds;
            string sec = seconds.ToString(ic);
            string cfg = r.Cfg.ToString(ic);

            string loader =
                "\"1\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"" + ckpt + "\"}},";
            string tail =
                "\"7\":{\"class_type\":\"VAEDecodeAudio\",\"inputs\":{\"samples\":[\"6\",0],\"vae\":[\"1\",2]}}," +
                "\"9\":{\"class_type\":\"SaveAudio\",\"inputs\":{\"audio\":[\"7\",0]," +
                    "\"filename_prefix\":\"lc_audio\"}}";

            if (r.Engine == AudioEngine.AceStep)
            {
                // ACE-Step: теги+лирика через свой энкодер, негатива нет — ConditioningZeroOut,
                // ModelSamplingSD3 shift 5.0 — из официального воркфлоу Comfy.
                string tags = EscapeJson(r.Positive);
                string lyrics = EscapeJson(string.IsNullOrWhiteSpace(r.Lyrics) ? "[inst]" : r.Lyrics);
                float lyrStr = r.LyricsStrength <= 0f ? 1f : r.LyricsStrength;
                string sampler = string.IsNullOrEmpty(r.Sampler) ? "euler" : r.Sampler;
                string scheduler = string.IsNullOrEmpty(r.Scheduler) ? "simple" : r.Scheduler;

                return "{" + loader +
                    "\"M\":{\"class_type\":\"ModelSamplingSD3\",\"inputs\":{\"model\":[\"1\",0],\"shift\":5.0}}," +
                    "\"3\":{\"class_type\":\"TextEncodeAceStepAudio\",\"inputs\":{\"clip\":[\"1\",1]," +
                        "\"tags\":\"" + tags + "\",\"lyrics\":\"" + lyrics + "\"," +
                        "\"lyrics_strength\":" + lyrStr.ToString(ic) + "}}," +
                    "\"4\":{\"class_type\":\"ConditioningZeroOut\",\"inputs\":{\"conditioning\":[\"3\",0]}}," +
                    "\"5\":{\"class_type\":\"EmptyAceStepLatentAudio\",\"inputs\":{\"seconds\":" + sec +
                        ",\"batch_size\":1}}," +
                    "\"6\":{\"class_type\":\"KSampler\",\"inputs\":{\"model\":[\"M\",0],\"positive\":[\"3\",0]," +
                        "\"negative\":[\"4\",0],\"latent_image\":[\"5\",0],\"seed\":" + r.Seed +
                        ",\"steps\":" + r.Steps + ",\"cfg\":" + cfg +
                        ",\"sampler_name\":\"" + sampler + "\",\"scheduler\":\"" + scheduler +
                        "\",\"denoise\":1.0}}," +
                    tail + "}";
            }
            else
            {
                // Stable Audio Open: T5-энкодер идёт ОТДЕЛЬНЫМ файлом (в чекпоинте-репаке его
                // нет — проверено: «checkpoint does not contain a valid clip») → CLIPLoader.
                // Дальше отметка длительности в кондишенинге (ConditioningStableAudio) + латент.
                string pos = EscapeJson(r.Positive);
                string neg = EscapeJson(r.Negative);
                string t5 = EscapeJson(AudioAiSettings.SfxTextEncoder);
                string sampler = string.IsNullOrEmpty(r.Sampler) ? "dpmpp_3m_sde_gpu" : r.Sampler;
                string scheduler = string.IsNullOrEmpty(r.Scheduler) ? "exponential" : r.Scheduler;

                return "{" + loader +
                    "\"2\":{\"class_type\":\"CLIPLoader\",\"inputs\":{\"clip_name\":\"" + t5 +
                        "\",\"type\":\"stable_audio\",\"device\":\"default\"}}," +
                    "\"3\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"2\",0],\"text\":\"" + pos + "\"}}," +
                    "\"4\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"2\",0],\"text\":\"" + neg + "\"}}," +
                    "\"C\":{\"class_type\":\"ConditioningStableAudio\",\"inputs\":{\"positive\":[\"3\",0]," +
                        "\"negative\":[\"4\",0],\"seconds_start\":0.0,\"seconds_total\":" + sec + "}}," +
                    "\"5\":{\"class_type\":\"EmptyLatentAudio\",\"inputs\":{\"seconds\":" + sec +
                        ",\"batch_size\":1}}," +
                    "\"6\":{\"class_type\":\"KSampler\",\"inputs\":{\"model\":[\"1\",0],\"positive\":[\"C\",0]," +
                        "\"negative\":[\"C\",1],\"latent_image\":[\"5\",0],\"seed\":" + r.Seed +
                        ",\"steps\":" + r.Steps + ",\"cfg\":" + cfg +
                        ",\"sampler_name\":\"" + sampler + "\",\"scheduler\":\"" + scheduler +
                        "\",\"denoise\":1.0}}," +
                    tail + "}";
            }
        }

        #region HTTP

        private static async Task<string> PostJsonAsync(string url, string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(url, content);
            string text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ComfyUI {(int)resp.StatusCode}: {Trim(text)}");
            return text;
        }

        private static async Task<string> GetAsync(string url)
        {
            var resp = await Http.GetAsync(url);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }

        private static async Task<byte[]> GetBytesAsync(string url)
        {
            var resp = await Http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ComfyUI /view {(int)resp.StatusCode}");
            return await resp.Content.ReadAsByteArrayAsync();
        }

        #endregion

        #region Мини-парсер JSON (нам нужны 2-3 поля, тащить зависимость незачем)

        /// <summary>Первое значение строкового поля "key": "value" в сыром JSON.</summary>
        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return null;

            int start = i + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            if (start >= json.Length || json[start] != '"') return null;
            start++;

            var sb = new StringBuilder();
            for (int p = start; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length) { sb.Append(json[++p]); continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Строки первого вложенного массива значения "key": [["a","b",…], …]. Читаем только
        /// первый внутренний массив — дальше идёт словарь опций (tooltip и т.п.), он не нужен.
        /// </summary>
        private static List<string> ParseFirstStringArray(string json, string key)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(json)) return result;

            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return result;
            i = json.IndexOf('[', i + token.Length);
            if (i < 0) return result;

            int p = i + 1;
            while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
            if (p < json.Length && json[p] == '[') p++;

            var sb = new StringBuilder();
            bool inString = false;
            for (; p < json.Length; p++)
            {
                char c = json[p];
                if (inString)
                {
                    if (c == '\\' && p + 1 < json.Length) { sb.Append(json[++p]); continue; }
                    if (c == '"') { result.Add(sb.ToString()); sb.Clear(); inString = false; continue; }
                    sb.Append(c);
                }
                else
                {
                    if (c == '"') { inString = true; continue; }
                    if (c == ']') break;
                }
            }
            return result;
        }

        private static string ExtractError(string history)
        {
            string msg = ExtractJsonString(history, "exception_message");
            return string.IsNullOrEmpty(msg) ? "см. консоль ComfyUI" : msg;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", " ").Replace("\t", " ");
        }

        private static string Trim(string s)
            => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        #endregion
    }
}
