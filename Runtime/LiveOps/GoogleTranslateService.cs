using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Сервис перевода текста через бесплатный Google Translate API.
    /// Кэширует результаты определения языка и перевода.
    /// </summary>
    public static class GoogleTranslateService
    {
        private const string BaseUrl = "https://translate.googleapis.com/translate_a/single";
        private const int    MaxDetectLength = 500;
        private const int    TimeoutSeconds  = 5;

        private static readonly Dictionary<string, string> _detectCache    = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _translateCache = new Dictionary<string, string>();

        /// <summary>
        /// Определяет язык текста. Возвращает ISO-код (например "ru", "en", "de").
        /// </summary>
        public static async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "en";

            string key = text.GetHashCode().ToString();
            if (_detectCache.TryGetValue(key, out var cached))
                return cached;

            string truncated = text.Length > MaxDetectLength ? text.Substring(0, MaxDetectLength) : text;
            string encoded   = UnityWebRequest.EscapeURL(truncated);
            string url       = $"{BaseUrl}?client=gtx&sl=auto&tl=en&dt=t&q={encoded}";

            string response = await SendRequestAsync(url);
            if (string.IsNullOrEmpty(response))
                return "en";

            string lang = ParseDetectedLanguage(response);
            if (!string.IsNullOrEmpty(lang))
                _detectCache[key] = lang;

            return lang ?? "en";
        }

        /// <summary>
        /// Переводит текст с sourceLang на targetLang.
        /// При ошибке возвращает исходный текст.
        /// </summary>
        public static async Task<string> TranslateAsync(string text, string sourceLang, string targetLang)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (sourceLang == targetLang)
                return text;

            string key = $"{sourceLang}_{targetLang}_{text.GetHashCode()}";
            if (_translateCache.TryGetValue(key, out var cached))
                return cached;

            string encoded = UnityWebRequest.EscapeURL(text);
            string url     = $"{BaseUrl}?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={encoded}";

            string response = await SendRequestAsync(url);
            if (string.IsNullOrEmpty(response))
                return text;

            string translated = ParseTranslatedText(response);
            if (string.IsNullOrEmpty(translated))
                return text;

            _translateCache[key] = translated;
            return translated;
        }

        /// <summary>
        /// Отправляет GET-запрос и возвращает тело ответа или null при ошибке.
        /// </summary>
        private static async Task<string> SendRequestAsync(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = TimeoutSeconds;
                var op = req.SendWebRequest();

                while (!op.isDone)
                    await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[GoogleTranslateService] Request failed: {req.error}");
                    return null;
                }

                return req.downloadHandler?.text;
            }
        }

        /// <summary>
        /// Извлекает код языка из ответа Google Translate.
        /// Формат: [[["translated","original",null,null,10]],null,"ru"]
        /// Код языка — последний строковый элемент на корневом уровне.
        /// </summary>
        private static string ParseDetectedLanguage(string response)
        {
            // Ищем последнее вхождение паттерна ,"xx"] или ,"xxx"] в конце ответа.
            // Язык — это последняя строка в кавычках перед закрывающей ].
            int lastBracket = response.LastIndexOf(']');
            if (lastBracket < 0)
                return null;

            // Идём назад от конца, ищем последнюю строку в кавычках.
            int endQuote = response.LastIndexOf('"', lastBracket);
            if (endQuote < 0)
                return null;

            int startQuote = response.LastIndexOf('"', endQuote - 1);
            if (startQuote < 0)
                return null;

            string lang = response.Substring(startQuote + 1, endQuote - startQuote - 1);

            // Проверяем что это похоже на ISO-код языка (2-5 букв).
            if (lang.Length >= 2 && lang.Length <= 5)
                return lang;

            return null;
        }

        /// <summary>
        /// Извлекает переведённый текст из ответа Google Translate.
        /// Формат: [[["translated text","original text", ...],...],...]
        /// Переведённый текст — первая строка в первом вложенном массиве.
        /// При наличии нескольких сегментов — склеивает их.
        /// </summary>
        private static string ParseTranslatedText(string response)
        {
            // Ответ может содержать несколько сегментов перевода:
            // [[["seg1","orig1",...],["seg2","orig2",...]],...]
            // Нужно собрать все первые элементы из вложенных массивов.

            if (string.IsNullOrEmpty(response) || response.Length < 6)
                return null;

            var result = new System.Text.StringBuilder();

            // Пропускаем первые два символа "[[", затем ищем каждый сегмент.
            int i = 0;

            // Находим начало внешнего массива [[
            if (response[0] != '[' || response[1] != '[')
                return null;

            i = 1; // на первом внутреннем [

            while (i < response.Length)
            {
                // Ищем начало сегмента [
                if (response[i] == '[')
                {
                    // Внутри сегмента ищем первую строку в кавычках.
                    int segStart = i + 1;
                    if (segStart < response.Length && response[segStart] == '"')
                    {
                        string extracted = ExtractQuotedString(response, segStart);
                        if (extracted != null)
                            result.Append(extracted);
                    }

                    // Пропускаем до конца этого сегмента.
                    i = SkipArray(response, i);
                    if (i < 0) break;
                }
                else if (response[i] == ']')
                {
                    // Конец массива сегментов.
                    break;
                }
                else
                {
                    i++;
                }
            }

            return result.Length > 0 ? result.ToString() : null;
        }

        /// <summary>
        /// Извлекает строку в кавычках начиная с позиции открывающей кавычки.
        /// Обрабатывает экранированные символы.
        /// </summary>
        private static string ExtractQuotedString(string s, int startIndex)
        {
            if (startIndex >= s.Length || s[startIndex] != '"')
                return null;

            var sb = new System.Text.StringBuilder();
            int i  = startIndex + 1;

            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'u':
                            if (i + 5 < s.Length &&
                                int.TryParse(s.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 4; // +2 for \u, +4 for hex digits, but loop adds 1
                            }
                            break;
                        default:   sb.Append(next); break;
                    }
                    i += 2;
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Пропускает массив [...], возвращая индекс символа после закрывающей ].
        /// </summary>
        private static int SkipArray(string s, int startIndex)
        {
            if (startIndex >= s.Length || s[startIndex] != '[')
                return -1;

            int depth = 0;
            bool inString = false;

            for (int i = startIndex; i < s.Length; i++)
            {
                char c = s[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        i++; // skip escaped char
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
            }

            return -1;
        }
    }
}
