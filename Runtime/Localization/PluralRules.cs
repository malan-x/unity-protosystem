// Packages/com.protosystem.core/Runtime/Localization/PluralRules.cs
namespace ProtoSystem
{
    /// <summary>
    /// Правила множественного числа для разных языков.
    /// Возвращает суффикс ключа: "one", "few", "many", "other".
    /// 
    /// Основано на CLDR plural rules:
    /// https://www.unicode.org/cldr/charts/latest/supplemental/language_plural_rules.html
    /// </summary>
    public static class PluralRules
    {
        /// <summary>
        /// Получить суффикс plural-формы для данного языка и числа.
        /// </summary>
        public static string GetSuffix(string languageCode, int count)
        {
            int abs = count < 0 ? -count : count;
            
            return languageCode switch
            {
                "ru" or "uk" or "be" => GetSlavicSuffix(abs),
                "pl" => GetPolishSuffix(abs),
                "en" or "de" or "es" or "it" or "pt" or "nl" or "sv" or "no" or "da" 
                    => abs == 1 ? "one" : "other",
                "fr" => abs <= 1 ? "one" : "other",
                "ar" => GetArabicSuffix(abs),
                "ja" or "ko" or "zh" or "th" or "vi" => "other", // нет plural forms
                _ => abs == 1 ? "one" : "other", // fallback: English-like
            };
        }
        
        /// <summary>
        /// Славянские языки (русский, украинский, белорусский).
        /// 1 враг, 2-4 врага, 5-20 врагов, 21 враг, 22-24 врага...
        /// </summary>
        private static string GetSlavicSuffix(int n)
        {
            int mod10 = n % 10;
            int mod100 = n % 100;
            
            if (mod10 == 1 && mod100 != 11)
                return "one";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                return "few";
            return "other";
        }
        
        /// <summary>Польский: 1, 2-4, 5-21, 22-24...</summary>
        private static string GetPolishSuffix(int n)
        {
            if (n == 1) return "one";
            int mod10 = n % 10;
            int mod100 = n % 100;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                return "few";
            return "other";
        }
        
        /// <summary>Арабский: 0, 1, 2, 3-10, 11-99, 100+</summary>
        private static string GetArabicSuffix(int n)
        {
            if (n == 0) return "zero";
            if (n == 1) return "one";
            if (n == 2) return "two";
            int mod100 = n % 100;
            if (mod100 >= 3 && mod100 <= 10) return "few";
            if (mod100 >= 11 && mod100 <= 99) return "many";
            return "other";
        }
    }
}
