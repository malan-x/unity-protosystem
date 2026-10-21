using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Клиент локального ComfyUI (адрес в AiToolsSettings.ComfyServer) для генерации иконок.
    /// Только редактор, только транспорт — стиль/промпт задаёт вызывающий (IconStylePreset).
    ///
    /// Почему именно так:
    /// — Чистый SDXL БЕЗ LayerDiffuse: инъекция в conv-слои ради прозрачности корёжила формы.
    ///   Генерим иконку на фоне, прозрачность делает rembg постобработкой.
    /// — Иконочная LoRA (опционально): базовый чекпоинт можно усилить LoRA. Файл — в models/loras,
    ///   имя/сила/триггер — в AiToolsSettings.
    ///
    /// Сервер поднимается вручную (start.bat в папке ComfyUI). Вызовы асинхронные, не морозят редактор.
    /// </summary>
    public static class ComfyUIClient
    {
        private static string DefaultServer => AiToolsSettings.ComfyServer;
        private static string Checkpoint => AiToolsSettings.Checkpoint;
        private static string LoraName => AiToolsSettings.LoraName;
        private static float LoraStrength => AiToolsSettings.LoraStrength;
        private static string LoraTrigger => AiToolsSettings.LoraTrigger;

        /// <summary>Добавлять ли триггер-слово LoRA в начало промпта (используется пресетом).</summary>
        public static bool UseLora => !string.IsNullOrWhiteSpace(LoraName);
        public static string LoraTriggerPrefix => UseLora ? LoraTrigger.Trim() + ", " : "";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

        public struct IconRequest
        {
            public string Positive;
            public string Negative;
            public int Seed;
            public int Size;       // ширина рендера; 1024 — нативный размер SDXL
            public int Height;     // высота рендера; 0 — квадрат (= Size). Для превью 1024×640
            public int Steps;

            public int EffHeight => Height > 0 ? Height : Size;
            public float Cfg;
            public string Sampler;    // напр. dpmpp_2m
            public string Scheduler;  // напр. karras

            // Оверрайды модели для воспроизводимости. Пусто — берётся глобальная настройка.
            // Нужны, чтобы перегенерировать старый вариант ТОЙ ЖЕ моделью, даже если дефолт сменился.
            public string CheckpointOverride;
            public string LoraOverride;         // "" — явно без LoRA; null — глобальная настройка
            public float LoraStrengthOverride;
            public bool HasModelOverride;       // true — использовать поля выше, а не глобальные

            // Референсы стиля (IPAdapter): абсолютные пути к PNG-эталонам на диске. Пусто — выключен.
            // Несколько картинок батчатся, CLIP-эмбеддинги усредняются — общий стиль без композиции.
            // Загружаются на сервер перед генерацией; работает на SDXL, на Flux игнорируется.
            public string[] StyleRefFiles;
            public float StyleRefWeight;

            // Режим IPAdapter: пусто — "style transfer" (переносит стиль, не композицию;
            // для выравнивания набора это то, что нужно). Альтернатива — "standard".
            public string StyleRefMode;

            public bool HasStyleRef => StyleRefFiles != null && StyleRefFiles.Length > 0;

            // Пиксель-арт: квантовать итог в сетку N px по ширине (0 — нет).
            // Пост-обработка на нашей стороне (SaveResult), в граф ComfyUI не попадает.
            public int PixelGrid;

            // Обрезать прозрачные поля перед квантованием: объект занимает весь кадр,
            // при мелкой сетке на него приходится больше пикселей. false — не трогать.
            public bool PixelTrim;

            // Ограничить палитру N цветов (median-cut) после квантования — «честный»
            // пиксель-look и чёткие плашки в мелком размере. 0 — без ограничения.
            public int PixelColors;

            // img2img-стилизация: абсолютный путь к исходной картинке. Пусто — генерация с нуля.
            // Композиция сохраняется, стиль перерисовывается промптом. Denoise — сила перерисовки:
            // 0.4 — лёгкая доводка, 0.6 — смена стиля, 0.8 — почти с нуля. 0 — дефолт 0.6.
            public string InitImageFile;
            public float Denoise;

            public string EffCheckpoint => HasModelOverride && !string.IsNullOrEmpty(CheckpointOverride)
                ? CheckpointOverride : Checkpoint;
            public string EffLora => HasModelOverride ? (LoraOverride ?? "") : LoraName;
            public float EffLoraStrength => HasModelOverride ? LoraStrengthOverride : LoraStrength;
        }

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
        public static Task<List<string>> GetCheckpointsAsync(string server = null)
            => GetNodeChoicesAsync("CheckpointLoaderSimple", "ckpt_name", server);

        /// <summary>LoRA, доступные на сервере (models/loras).</summary>
        public static Task<List<string>> GetLorasAsync(string server = null)
            => GetNodeChoicesAsync("LoraLoader", "lora_name", server);

        /// <summary>
        /// Варианты значения входа ноды из /object_info: ComfyUI отдаёт список допустимых
        /// файлов моделей как первый элемент описания входа — {"ckpt_name":[["a.safetensors",…],{…}]}.
        /// </summary>
        private static async Task<List<string>> GetNodeChoicesAsync(string nodeClass, string field, string server)
        {
            server ??= DefaultServer;
            string json = await GetAsync($"{server}/object_info/{nodeClass}");
            return ParseFirstStringArray(json, field);
        }

        /// <summary>Сгенерировать одну иконку. Возвращает PNG (RGBA) или бросает с понятным текстом.</summary>
        public static async Task<byte[]> GenerateAsync(IconRequest request, string server = null)
        {
            server ??= DefaultServer;
            string clientId = Guid.NewGuid().ToString("N");

            // Референсы стиля: сперва закинуть эталоны на сервер (в input), потом сослаться в графе
            List<string> styleRefNames = null;
            if (request.HasStyleRef)
            {
                styleRefNames = new List<string>();
                for (int i = 0; i < request.StyleRefFiles.Length; i++)
                    if (!string.IsNullOrEmpty(request.StyleRefFiles[i]) &&
                        System.IO.File.Exists(request.StyleRefFiles[i]))
                        styleRefNames.Add(await UploadImageAsync(
                            request.StyleRefFiles[i], server, $"protoicon_style_ref_{i}.png"));
            }

            // img2img: исходник тоже уезжает на сервер
            string initImageName = null;
            if (!string.IsNullOrEmpty(request.InitImageFile) && System.IO.File.Exists(request.InitImageFile))
                initImageName = await UploadImageAsync(request.InitImageFile, server, "protoicon_init.png");

            string workflow = BuildWorkflow(request, styleRefNames, initImageName);
            string body = $"{{\"prompt\":{workflow},\"client_id\":\"{clientId}\"}}";

            var queued = await PostJsonAsync($"{server}/prompt", body);
            string promptId = ExtractJsonString(queued, "prompt_id");
            if (string.IsNullOrEmpty(promptId))
                throw new Exception($"ComfyUI отклонил задачу: {Trim(queued)}");

            // Опрос истории до готовности. Сервер не пушит — только polling.
            var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(9);
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

            throw new Exception("ComfyUI: таймаут ожидания результата (9 мин).");
        }

        /// <summary>
        /// Граф ComfyUI как JSON. Держим строкой намеренно: у нас нет типов нод в C#,
        /// а граф фиксирован — плейсхолдеры только для промптов и параметров сэмплера.
        ///
        /// Чистый SDXL БЕЗ LayerDiffuse: инъекция в conv-слои ради прозрачности корёжила формы.
        /// Генерим полноценную иконку на фоне (dpmpp_2m/karras — заметно чище euler), фон
        /// убирает rembg постобработкой. Опциональная LoRA — нода "L" между чекпоинтом и остальным.
        /// </summary>
        private static string BuildWorkflow(IconRequest r, List<string> styleRefNames = null,
                                            string initImageName = null)
        {
            string pos = EscapeJson(r.Positive);
            string neg = EscapeJson(r.Negative);
            string cfg = r.Cfg.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string sampler = string.IsNullOrEmpty(r.Sampler) ? "dpmpp_2m" : r.Sampler;
            string scheduler = string.IsNullOrEmpty(r.Scheduler) ? "karras" : r.Scheduler;

            // Модель/LoRA — эффективные (из запроса, если задан оверрайд, иначе глобальные).
            string checkpoint = r.EffCheckpoint;
            string lora = r.EffLora;
            float loraStr = r.EffLoraStrength;
            bool useLora = !string.IsNullOrWhiteSpace(lora);

            string modelSrc = useLora ? "\"L\",0" : "\"1\",0";
            string clipSrc = useLora ? "\"L\",1" : "\"1\",1";

            string loraNode = useLora
                ? "\"L\":{\"class_type\":\"LoraLoader\",\"inputs\":{\"model\":[\"1\",0],\"clip\":[\"1\",1]," +
                  "\"lora_name\":\"" + EscapeJson(lora) + "\"," +
                  "\"strength_model\":" + loraStr.ToString(ic) + "," +
                  "\"strength_clip\":" + loraStr.ToString(ic) + "}},"
                : "";

            // Референсы стиля (IPAdapter plus, только SDXL): встраиваются МЕЖДУ моделью и сэмплером,
            // все генерации тянутся к эталонам — лечит стилевой разнобой между иконками набора.
            // Несколько эталонов сшиваются цепочкой ImageBatch: эмбеддинги усредняются, композиции
            // отдельных иконок гасят друг друга, остаётся общий стиль.
            string ipaNodes = "";
            if (styleRefNames != null && styleRefNames.Count > 0)
            {
                float w = r.StyleRefWeight <= 0f ? 0.5f : r.StyleRefWeight;

                var sbNodes = new StringBuilder();
                for (int i = 0; i < styleRefNames.Count; i++)
                    sbNodes.Append("\"RI" + i + "\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":\"" +
                                   EscapeJson(styleRefNames[i]) + "\"}},");

                string imgSrc = "\"RI0\",0";
                for (int i = 1; i < styleRefNames.Count; i++)
                {
                    sbNodes.Append("\"BA" + i + "\":{\"class_type\":\"ImageBatch\",\"inputs\":{" +
                                   "\"image1\":[" + imgSrc + "],\"image2\":[\"RI" + i + "\",0]}},");
                    imgSrc = "\"BA" + i + "\",0";
                }

                sbNodes.Append("\"U\":{\"class_type\":\"IPAdapterUnifiedLoader\",\"inputs\":{\"model\":[" + modelSrc + "]," +
                               "\"preset\":\"PLUS (high strength)\"}},");
                string weightType = string.IsNullOrEmpty(r.StyleRefMode) ? "style transfer" : r.StyleRefMode;
                sbNodes.Append("\"IPA\":{\"class_type\":\"IPAdapter\",\"inputs\":{\"model\":[\"U\",0],\"ipadapter\":[\"U\",1]," +
                               "\"image\":[" + imgSrc + "],\"weight\":" + w.ToString(ic) +
                               ",\"start_at\":0.0,\"end_at\":1.0,\"weight_type\":\"" + EscapeJson(weightType) + "\"}},");
                ipaNodes = sbNodes.ToString();
                modelSrc = "\"IPA\",0";
            }

            // img2img: латент из исходной картинки (масштабируем до размера рендера — хранёные
            // иконки бывают 256px, SDXL в таком разрешении не рисует) + пониженный denoise.
            string latentSrc = "\"5\",0";
            string denoise = "1.0";
            string initNodes = "";
            if (!string.IsNullOrEmpty(initImageName))
            {
                float dn = r.Denoise <= 0f ? 0.6f : (r.Denoise > 1f ? 1f : r.Denoise);
                denoise = dn.ToString(ic);
                initNodes =
                    "\"II\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":\"" + EscapeJson(initImageName) + "\"}}," +
                    "\"IS\":{\"class_type\":\"ImageScale\",\"inputs\":{\"image\":[\"II\",0]," +
                        "\"upscale_method\":\"lanczos\",\"width\":" + r.Size + ",\"height\":" + r.EffHeight +
                        ",\"crop\":\"disabled\"}}," +
                    "\"VE\":{\"class_type\":\"VAEEncode\",\"inputs\":{\"pixels\":[\"IS\",0],\"vae\":[\"1\",2]}},";
                latentSrc = "\"VE\",0";
            }

            return "{" +
                "\"1\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"" + EscapeJson(checkpoint) + "\"}}," +
                loraNode +
                ipaNodes +
                initNodes +
                "\"3\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[" + clipSrc + "],\"text\":\"" + pos + "\"}}," +
                "\"4\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[" + clipSrc + "],\"text\":\"" + neg + "\"}}," +
                "\"5\":{\"class_type\":\"EmptyLatentImage\",\"inputs\":{\"width\":" + r.Size +
                    ",\"height\":" + r.EffHeight + ",\"batch_size\":1}}," +
                "\"6\":{\"class_type\":\"KSampler\",\"inputs\":{\"model\":[" + modelSrc + "],\"positive\":[\"3\",0]," +
                    "\"negative\":[\"4\",0],\"latent_image\":[" + latentSrc + "],\"seed\":" + r.Seed +
                    ",\"steps\":" + r.Steps + ",\"cfg\":" + cfg +
                    ",\"sampler_name\":\"" + sampler + "\",\"scheduler\":\"" + scheduler +
                    "\",\"denoise\":" + denoise + "}}," +
                "\"7\":{\"class_type\":\"VAEDecode\",\"inputs\":{\"samples\":[\"6\",0],\"vae\":[\"1\",2]}}," +
                "\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"7\",0]," +
                    "\"filename_prefix\":\"lc_icon\"}}" +
            "}";
        }

        /// <summary>
        /// Загрузить картинку на сервер (в input) и вернуть имя, под которым её видит LoadImage.
        /// overwrite=true: референсы перезаливаются при каждой генерации — это дёшево.
        /// </summary>
        public static async Task<string> UploadImageAsync(string absolutePath, string server = null,
                                                          string fileName = null)
        {
            server ??= DefaultServer;
            byte[] bytes = System.IO.File.ReadAllBytes(absolutePath);
            fileName ??= "protoicon_style_ref.png";

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "image", fileName);
            form.Add(new StringContent("true"), "overwrite");

            var resp = await Http.PostAsync($"{server}/upload/image", form);
            string text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"ComfyUI /upload/image {(int)resp.StatusCode}: {Trim(text)}");

            return ExtractJsonString(text, "name") ?? fileName;
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

            // пропускаем пробелы и открывающую кавычку
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

            // Возможна двойная вложенность: [["a","b"], {...}] — заходим во внутренний массив
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
                    if (c == ']') break;   // конец внутреннего массива — дальше опции, не имена
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
                    .Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
        }

        private static string Trim(string s)
            => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        #endregion
    }
}
