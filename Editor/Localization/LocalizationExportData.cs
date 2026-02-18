// Packages/com.protosystem.core/Editor/Localization/LocalizationExportData.cs
using System;
using System.Collections.Generic;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// JSON-формат экспорта для AI-перевода.
    /// </summary>
    [Serializable]
    public class LocalizationExportData
    {
        public string sourceLanguage;
        public string targetLanguage;
        public string table;
        public string exported;
        public string projectName;
        public string instructions;
        public List<ExportEntry> entries = new();
    }
    
    [Serializable]
    public class ExportEntry
    {
        public string key;
        public string source;
        public string context;
        public int maxLength;
        public List<string> tags;
        public string pluralForm;
        
        // Для импорта
        public string translation;
    }
    
    /// <summary>
    /// JSON-формат импорта переводов.
    /// </summary>
    [Serializable]
    public class LocalizationImportData
    {
        public string sourceLanguage;
        public string targetLanguage;
        public string table;
        public List<ImportEntry> entries = new();
    }
    
    [Serializable]
    public class ImportEntry
    {
        public string key;
        public string translation;
    }
    
    /// <summary>
    /// Результат валидации перевода.
    /// </summary>
    [Serializable]
    public class ValidationResult
    {
        public string key;
        public ValidationType type;
        public string message;
        
        public enum ValidationType
        {
            OK,
            Warning,
            Error
        }
    }
}
