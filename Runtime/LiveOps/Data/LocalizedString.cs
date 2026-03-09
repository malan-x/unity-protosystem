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
    }
}
