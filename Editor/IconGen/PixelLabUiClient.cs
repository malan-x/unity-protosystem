using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Клиент PixelLab UI Creator (api.pixellab.ai/v2): генерация пиксель-арт
    /// UI-элементов (панели, кнопки, глифы) по текстовому описанию. Асинхронный
    /// job-флоу: create-ui-asset → поллинг ui-assets/{id} → скачивание image_url.
    ///
    /// Самодостаточный (модуль IconGen не зависит от проекта): ключ читается из
    /// гитигнорного LocalOverrides/pixellab.json — тот же файл, что у студии мобов.
    /// </summary>
    public static class PixelLabUiClient
    {
        private const string BaseUrl = "https://api.pixellab.ai/v2";

        private static HttpClient _http;
        private static HttpClient _download; // без Bearer: подписанные URL хранилища отвергают чужой Authorization
        private static string _cachedKey;

        public struct Request
        {
            public string Description;
            public int Width;        // 192–688
            public int Height;       // 192–688
            public string Palette;   // словами («rust brown, dark gold»), пусто — свободная
            public string[] Elements; // button/icon_button/toolbar/tab/panel/window/health_bar…
            public int Seed;
        }

        public static bool HasKey => !string.IsNullOrEmpty(LoadKey());

        public static string KeyPath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "LocalOverrides", "pixellab.json"));

        private static string LoadKey()
        {
            if (_cachedKey != null) return _cachedKey;
            try
            {
                _cachedKey = File.Exists(KeyPath)
                    ? JObject.Parse(File.ReadAllText(KeyPath))["api_key"]?.ToString() ?? ""
                    : "";
            }
            catch (Exception e)
            {
                Debug.LogError($"[PixelLabUI] Не удалось прочитать {KeyPath}: {e.Message}");
                _cachedKey = "";
            }
            return _cachedKey;
        }

        private static HttpClient Http
        {
            get
            {
                if (_http == null)
                {
                    _http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                    _http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", LoadKey());
                }
                return _http;
            }
        }

        public static void Reset()
        {
            _http?.Dispose();
            _http = null;
            _download?.Dispose();
            _download = null;
            _cachedKey = null;
        }

        /// <summary>
        /// Полный цикл генерации UI-элемента: запуск (с ожиданием свободных слотов),
        /// поллинг до готовности, скачивание PNG. Бросает исключение при ошибке —
        /// AiIconGenerator показывает его в консоли как обычный сбой генерации.
        /// </summary>
        public static async Task<byte[]> GenerateAsync(Request r)
        {
            var body = new JObject
            {
                ["description"] = r.Description,
                ["image_size"] = new JObject
                {
                    ["width"] = Mathf.Clamp(r.Width, 192, 688),
                    ["height"] = Mathf.Clamp(r.Height, 192, 688),
                },
                ["no_background"] = true,
            };
            if (!string.IsNullOrEmpty(r.Palette))
                body["color_palette"] = r.Palette;
            if (r.Elements != null && r.Elements.Length > 0)
                body["elements"] = new JArray(r.Elements);
            if (r.Seed > 0)
                body["seed"] = r.Seed;

            // Запуск: занятые слоты аккаунта (429) — ждём и пробуем снова
            JObject created = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var resp = await Http.PostAsync(BaseUrl + "/create-ui-asset",
                    new StringContent(body.ToString(), Encoding.UTF8, "application/json"));
                string text = await resp.Content.ReadAsStringAsync();

                if ((int)resp.StatusCode == 429)
                {
                    await Task.Delay(10000);
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"PixelLab UI {(int)resp.StatusCode}: {text}");

                created = JObject.Parse(text);
                break;
            }
            if (created == null)
                throw new Exception("PixelLab UI: не дождались свободных слотов (429)");

            string assetId = created["ui_asset_id"]?.ToString();
            if (string.IsNullOrEmpty(assetId))
                throw new Exception("PixelLab UI: в ответе нет ui_asset_id");

            // Поллинг готовности
            string imageUrl = null;
            var deadline = DateTime.UtcNow.AddMinutes(6);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(4000);

                var resp = await Http.GetAsync($"{BaseUrl}/ui-assets/{assetId}");
                string text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"PixelLab UI {(int)resp.StatusCode}: {text}");

                var detail = JObject.Parse(text);
                string status = detail["status"]?.ToString();
                if (status == "completed")
                {
                    imageUrl = detail["image_url"]?.ToString();
                    break;
                }
                if (status == "failed")
                    throw new Exception("PixelLab UI: генерация упала — " +
                        (detail["error"]?.ToString() ?? text));
            }
            if (string.IsNullOrEmpty(imageUrl))
                throw new Exception("PixelLab UI: таймаут ожидания генерации (6 мин)");

            _download ??= new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var img = await _download.GetAsync(imageUrl);
            if (!img.IsSuccessStatusCode)
                throw new Exception($"PixelLab UI {(int)img.StatusCode}: не скачался {imageUrl}");
            return await img.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Отдельная пиксель-арт картинка через pixflux (create-image-pixflux) —
        /// правильный инструмент для иконок/глифов (UI Creator всегда рисует панель).
        /// Синхронный эндпоинт: ответ сразу содержит base64-картинку. Размеры 32–400.
        /// Палитра словами дописывается в описание.
        /// </summary>
        public static async Task<byte[]> GenerateImageAsync(Request r)
        {
            string description = r.Description ?? "";
            if (!string.IsNullOrEmpty(r.Palette))
                description += ", palette: " + r.Palette;

            var body = new JObject
            {
                ["description"] = description,
                ["image_size"] = new JObject
                {
                    ["width"] = Mathf.Clamp(r.Width, 32, 400),
                    ["height"] = Mathf.Clamp(r.Height, 32, 400),
                },
                ["no_background"] = true,
            };
            if (r.Seed > 0)
                body["seed"] = r.Seed;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var resp = await Http.PostAsync(BaseUrl + "/create-image-pixflux",
                    new StringContent(body.ToString(), Encoding.UTF8, "application/json"));
                string text = await resp.Content.ReadAsStringAsync();

                if ((int)resp.StatusCode == 429)
                {
                    await Task.Delay(10000);
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"PixelLab pixflux {(int)resp.StatusCode}: {text}");

                string b64 = JObject.Parse(text)["image"]?["base64"]?.ToString();
                if (string.IsNullOrEmpty(b64))
                    throw new Exception("PixelLab pixflux: в ответе нет image.base64");

                // Может прийти data-URI — отрезаем префикс
                int comma = b64.IndexOf(',');
                if (b64.StartsWith("data:") && comma > 0)
                    b64 = b64.Substring(comma + 1);
                return Convert.FromBase64String(b64);
            }
            throw new Exception("PixelLab pixflux: не дождались свободных слотов (429)");
        }
    }
}
