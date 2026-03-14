// Packages/com.protosystem.core/Runtime/LiveOps/Data/LocalizedString.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoSystem.LiveOps
{
    /// <summary>
    /// Локализованная строка. Хранит переводы по коду языка (ISO 639-1).
    ///
    /// JSON-формат с сервера:
    /// <code>
    /// { "ru": "Текст", "en": "Text", "de": "Text" }
    /// </code>
    ///
    /// Использование:
    /// <code>
    /// string text = message.title.Get(liveOpsSystem.Language);
    /// </code>
    /// </summary>
    [Serializable]
    public class LocalizedString
    {
        public Dictionary<string, string> translations = new();

        /// <summary>
        /// Получить перевод. Порядок: langCode → fallback ("en") → первый доступный.
        /// </summary>
        public string Get(string langCode, string fallback = "en")
        {
            if (translations == null || translations.Count == 0) return string.Empty;
            if (translations.TryGetValue(langCode, out var val)) return val;
            if (translations.TryGetValue(fallback, out var fb)) return fb;
            return translations.Values.First();
        }

        /// <summary>Создать из одиночной строки без локализации (en-only).</summary>
        public static LocalizedString FromRaw(string value) =>
            new() { translations = new Dictionary<string, string> { { "en", value } } };

        /// <summary>
        /// Парсит плоский JSON-объект {"lang":"value", ...} в LocalizedString.
        /// Поддерживает любое количество языков.
        /// </summary>
        public static LocalizedString FromJson(string json)
        {
            var ls = new LocalizedString();
            if (string.IsNullOrEmpty(json)) return ls;

            // Простой парсер для {"key":"value", ...} без вложенности
            json = json.Trim();
            if (json.Length < 2 || json[0] != '{') return ls;

            int i = 1;
            while (i < json.Length)
            {
                // Пропуск пробелов/запятых
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t'))
                    i++;
                if (i >= json.Length || json[i] == '}') break;

                var key = ParseJsonString(json, ref i);
                if (key == null) break;

                // Пропуск до ':'
                while (i < json.Length && json[i] != ':') i++;
                i++; // skip ':'

                // Пропуск пробелов
                while (i < json.Length && json[i] == ' ') i++;

                var val = ParseJsonString(json, ref i);
                if (val == null) break;

                ls.translations[key] = val;
            }
            return ls;
        }

        private static string ParseJsonString(string json, ref int i)
        {
            while (i < json.Length && json[i] != '"') i++;
            if (i >= json.Length) return null;
            i++; // skip opening "

            var start = i;
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\') i++; // skip escaped char
                i++;
            }
            if (i >= json.Length) return null;

            var result = json.Substring(start, i - start)
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\");
            i++; // skip closing "
            return result;
        }

        /// <summary>
        /// Зарегистрировать перевод для текущего языка как рантайм-ключ в Loc.
        /// LocalizeTMP с этим ключом покажет нужный перевод.
        /// Вызывать повторно при смене языка.
        /// </summary>
        public void RegisterInLoc(string key, string lang)
        {
            var value = Get(lang);
            if (!string.IsNullOrEmpty(value))
                Loc.Set(key, value);
        }
    }
}
