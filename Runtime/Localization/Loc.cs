// Packages/com.protosystem.core/Runtime/Localization/Loc.cs
using System;
using System.Collections.Generic;

namespace ProtoSystem
{
    /// <summary>
    /// Статический helper для быстрого доступа к локализации.
    /// 
    /// Использование:
    ///   Loc.Get("menu.play")                          → из таблицы по умолчанию
    ///   Loc.Get("menu.play", "PLAY")                  → с fallback
    ///   Loc.From("Items", "sword.name")               → из конкретной таблицы
    ///   Loc.From("Items", "sword.name", "Sword")      → из конкретной таблицы с fallback
    ///   Loc.GetPlural("enemies.killed", 5)             → plural form
    ///   Loc.Ref("Items", "sword.name")                → LocRef для подстановки
    /// </summary>
    public static class Loc
    {
        private static LocalizationSystem _system;
        
        internal static void Register(LocalizationSystem system) => _system = system;
        
        internal static void Unregister(LocalizationSystem system)
        {
            if (_system == system) _system = null;
        }
        
        /// <summary>Система доступна и инициализирована</summary>
        public static bool IsReady => _system != null && _system.IsReady;
        
        /// <summary>Текущий язык (код: "ru", "en")</summary>
        public static string CurrentLanguage => _system?.CurrentLanguage ?? "??";
        
        /// <summary>Список доступных языков</summary>
        public static IReadOnlyList<string> AvailableLanguages => 
            _system?.AvailableLanguages ?? Array.Empty<string>();
        
        // ──────────────────── Get (default table) ────────────────────
        
        /// <summary>Получить перевод из таблицы по умолчанию.</summary>
        public static string Get(string key)
        {
            if (_system != null) return _system.Get(key);
            return $"[{key}]";
        }
        
        /// <summary>Получить перевод с fallback.</summary>
        public static string Get(string key, string fallback)
        {
            if (_system != null) return _system.Get(key, fallback);
            return fallback ?? $"[{key}]";
        }
        
        /// <summary>Получить перевод с подстановкой переменных.</summary>
        public static string Get(string key, params (string name, object value)[] args)
        {
            if (_system != null) return _system.GetWithArgs(key, args);
            return $"[{key}]";
        }
        
        // ──────────────────── From (explicit table) ────────────────────
        
        /// <summary>Получить перевод из конкретной таблицы.</summary>
        public static string From(string table, string key)
        {
            if (_system != null) return _system.From(table, key);
            return $"[{table}:{key}]";
        }
        
        /// <summary>Получить перевод из конкретной таблицы с fallback.</summary>
        public static string From(string table, string key, string fallback)
        {
            if (_system != null) return _system.From(table, key, fallback);
            return fallback ?? $"[{table}:{key}]";
        }
        
        /// <summary>Получить перевод из конкретной таблицы с переменными.</summary>
        public static string From(string table, string key, params (string name, object value)[] args)
        {
            if (_system != null) return _system.FromWithArgs(table, key, args);
            return $"[{table}:{key}]";
        }
        
        // ──────────────────── Plural ────────────────────
        
        /// <summary>Plural из таблицы по умолчанию. Суффикс .one/.few/.other автоматически.</summary>
        public static string GetPlural(string keyPrefix, int count)
        {
            if (_system != null) return _system.GetPlural(keyPrefix, count);
            return $"[{keyPrefix}.other]";
        }
        
        /// <summary>Plural из конкретной таблицы.</summary>
        public static string GetPlural(string table, string keyPrefix, int count)
        {
            if (_system != null) return _system.GetPlural(table, keyPrefix, count);
            return $"[{table}:{keyPrefix}.other]";
        }
        
        // ──────────────────── Ref ────────────────────
        
        /// <summary>Ссылка на локализованный ключ для подстановки в другие строки.</summary>
        public static LocRef Ref(string table, string key) => new LocRef(table, key);
        
        /// <summary>Ссылка из таблицы по умолчанию.</summary>
        public static LocRef Ref(string key) => new LocRef(null, key);
        
        // ──────────────────── Has ────────────────────
        
        /// <summary>Проверить наличие ключа в таблице по умолчанию.</summary>
        public static bool Has(string key) => _system != null && _system.Has(key);
        
        /// <summary>Проверить наличие ключа в конкретной таблице.</summary>
        public static bool Has(string table, string key) => _system != null && _system.Has(table, key);
        
        // ──────────────────── Language ────────────────────
        
        /// <summary>Сменить язык.</summary>
        public static void SetLanguage(string languageCode) => _system?.SetLanguage(languageCode);
    }
    
    /// <summary>
    /// Ссылка на локализованную строку для подстановки в Smart Strings.
    /// При вызове ToString() возвращает текущий перевод.
    /// </summary>
    public struct LocRef
    {
        public readonly string Table;
        public readonly string Key;
        
        public LocRef(string table, string key)
        {
            Table = table;
            Key = key;
        }
        
        public override string ToString()
        {
            return Table != null ? Loc.From(Table, Key) : Loc.Get(Key);
        }
    }
}
