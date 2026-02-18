// Packages/com.protosystem.core/Runtime/Localization/StringMetadata.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Метаданные для строки локализации.
    /// Используются при AI-экспорте для контекста перевода.
    /// </summary>
    [Serializable]
    public class StringMetadata
    {
        [Tooltip("Ключ строки в таблице")]
        public string key;
        
        [Tooltip("Таблица (пустая = defaultStringTable)")]
        public string table;
        
        [Tooltip("Контекст для переводчика/AI")]
        [TextArea(1, 3)]
        public string context;
        
        [Tooltip("Максимальная длина перевода (0 = без ограничения)")]
        public int maxLength;
        
        [Tooltip("Теги для группировки (ui, gameplay, tutorial)")]
        public List<string> tags = new();
        
        [Tooltip("Форма множественного числа (one/few/other или пусто)")]
        public string pluralForm;
    }
    
    /// <summary>
    /// База метаданных строк локализации.
    /// Хранит контекст, ограничения длины и теги для AI-экспорта.
    /// </summary>
    [CreateAssetMenu(fileName = "StringMetadataDatabase", 
        menuName = "ProtoSystem/Localization/String Metadata Database")]
    public class StringMetadataDatabase : ScriptableObject
    {
        public List<StringMetadata> entries = new();
        
        // Кеш для быстрого поиска
        [NonSerialized] private Dictionary<string, StringMetadata> _cache;
        
        /// <summary>Найти метаданные по ключу (таблица по умолчанию).</summary>
        public StringMetadata Find(string key)
        {
            EnsureCache();
            _cache.TryGetValue(key, out var meta);
            return meta;
        }
        
        /// <summary>Найти метаданные по таблице и ключу.</summary>
        public StringMetadata Find(string table, string key)
        {
            EnsureCache();
            _cache.TryGetValue($"{table}:{key}", out var meta);
            if (meta != null) return meta;
            _cache.TryGetValue(key, out meta);
            return meta;
        }
        
        /// <summary>Добавить или обновить метаданные.</summary>
        public void Set(StringMetadata meta)
        {
            var existing = string.IsNullOrEmpty(meta.table) 
                ? Find(meta.key) 
                : Find(meta.table, meta.key);
            
            if (existing != null)
            {
                existing.context = meta.context;
                existing.maxLength = meta.maxLength;
                existing.tags = meta.tags;
                existing.pluralForm = meta.pluralForm;
            }
            else
            {
                entries.Add(meta);
            }
            
            _cache = null; // Инвалидировать кеш
        }
        
        /// <summary>Перестроить кеш.</summary>
        public void InvalidateCache() => _cache = null;
        
        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, StringMetadata>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.table))
                    _cache[$"{entry.table}:{entry.key}"] = entry;
                _cache[entry.key] = entry;
            }
        }
    }
}
