// Packages/com.protosystem.core/Runtime/Localization/LocalizationConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ProtoSystem
{
    /// <summary>
    /// Конфигурация системы локализации ProtoSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationConfig", menuName = "ProtoSystem/Localization Config")]
    public class LocalizationConfig : ScriptableObject
    {
        [Header("Languages")]
        [Tooltip("Язык по умолчанию (код ISO 639-1)")]
        public string defaultLanguage = "ru";
        
        [Tooltip("Fallback язык если перевод не найден")]
        public string fallbackLanguage = "en";
        
        [Tooltip("Автоопределение языка системы при первом запуске")]
        public bool autoDetectSystemLanguage = true;
        
        public List<LanguageEntry> supportedLanguages = new()
        {
            new() { code = "ru", displayName = "Русский", isSource = true },
            new() { code = "en", displayName = "English" },
        };
        
        [Header("Tables")]
        [Tooltip("Таблица по умолчанию для Loc.Get(key)")]
        public string defaultStringTable = "UI";
        
        [Tooltip("Таблицы для предзагрузки при старте")]
        public List<string> preloadTables = new() { "UI", "Game" };
        
        [Header("Behavior")]
        [Tooltip("Логировать отсутствующие ключи")]
        public bool logMissingKeys = true;
        
        [Tooltip("Формат отображения отсутствующих ключей")]
        public string missingKeyFormat = "[{0}]";
        
        [Header("AI Export")]
        [Tooltip("Путь для экспорта JSON (относительно Assets/)")]
        public string exportPath = "Localization/Export";
        
        [Tooltip("Включать контекст в экспорт")]
        public bool includeContext = true;
        
        [Tooltip("Включать ограничение длины в экспорт")]
        public bool includeMaxLength = true;
    }
    
    /// <summary>
    /// Запись о поддерживаемом языке.
    /// </summary>
    [Serializable]
    public class LanguageEntry
    {
        [Tooltip("Код языка ISO 639-1 (ru, en, de, ja)")]
        public string code;
        
        [Tooltip("Отображаемое имя")]
        public string displayName;
        
        [Tooltip("Исходный язык (для AI-экспорта)")]
        public bool isSource;
        
        [Tooltip("Шрифт для языка (опционально, для CJK)")]
        public TMP_FontAsset font;
    }
}
