// Packages/com.protosystem.core/Runtime/Localization/LocalizationSystem.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if PROTO_HAS_LOCALIZATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace ProtoSystem
{
    /// <summary>
    /// Система локализации ProtoSystem.
    /// Wrapper поверх Unity Localization с простым API и EventBus интеграцией.
    /// Без Unity Localization работает в fallback-режиме (возвращает ключи/fallback).
    /// </summary>
    [ProtoSystemComponent("Localization", "Система локализации с поддержкой AI-перевода",
        "Core", "🌐", 50)]
    public class LocalizationSystem : InitializableSystemBase, IResettable
    {
        #region InitializableSystemBase
        
        public override string SystemId => "localization";
        public override string DisplayName => "Localization System";
        public override string Description => "Управление переводами, смена языков, интеграция с Unity Localization.";
        
        #endregion
        
        #region Serialized
        
        [Header("Configuration")]
        [SerializeField, InlineConfig] private LocalizationConfig config;
        
        #endregion
        
        #region State
        
        private string _currentLanguage;
        private bool _isReady;
        private bool _isFallbackMode;
        private List<string> _availableLanguages = new();
        
        #if PROTO_HAS_LOCALIZATION
        private Dictionary<string, StringTable> _loadedTables = new();
        #endif

        /// <summary>Рантайм-ключи, добавленные через Set(). table → key → value.</summary>
        private readonly Dictionary<string, Dictionary<string, string>> _runtimeEntries = new();

        #endregion
        
        #region Properties
        
        public bool IsReady => _isReady;
        public string CurrentLanguage => _currentLanguage;
        public IReadOnlyList<string> AvailableLanguages => _availableLanguages;
        public LocalizationConfig Config => config;
        
        #endregion
        
        #region Initialization
        
        protected override void InitEvents() { }
        
        public override async Task<bool> InitializeAsync()
        {
            ReportProgress(0.1f);
            
            if (config == null)
            {
                config = Resources.Load<LocalizationConfig>("LocalizationConfig");
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<LocalizationConfig>();
                    LogWarning("LocalizationConfig not found, using defaults");
                }
            }
            
            Loc.Register(this);
            ReportProgress(0.3f);
            
            #if PROTO_HAS_LOCALIZATION
            await InitializeUnityLocalization();
            #else
            _isFallbackMode = true;
            foreach (var lang in config.supportedLanguages)
                _availableLanguages.Add(lang.code);
            _currentLanguage = DetermineLanguage();
            LogWarning("Unity Localization not installed. Fallback mode (keys only).");
            await Task.CompletedTask;
            #endif
            
            ReportProgress(0.9f);
            
            _isReady = true;
            EventBus.Publish(EventBus.Localization.Ready, null);
            
            LogInit($"Localization ready. Language: {_currentLanguage}, " +
                $"Available: {string.Join(", ", _availableLanguages)}");
            
            ReportProgress(1f);
            return true;
        }
        
        #if PROTO_HAS_LOCALIZATION
        
        /// <summary>
        /// Проверяет готовность Unity Localization к работе:
        /// Addressables собраны + Locale-ассеты существуют.
        /// Вызывать ДО LocalizationSettings.InitializationOperation!
        /// </summary>
        private bool CanUseUnityLocalization(out string reason)
        {
            #if UNITY_EDITOR
            // 1. Проверка catalog (реальный билд Addressables)
            string aaPath = "Library/com.unity.addressables/aa/Windows";
            if (!System.IO.Directory.Exists(aaPath) || 
                System.IO.Directory.GetFiles(aaPath, "catalog*").Length == 0)
            {
                reason = "Addressables not built. Build via: Window → Asset Management → Addressables → Groups → Build";
                return false;
            }
            
            // 2. Проверка Locale-ассетов (без триггера Addressables)
            var localeGuids = UnityEditor.AssetDatabase.FindAssets("t:UnityEngine.Localization.Locale");
            if (localeGuids.Length == 0)
            {
                reason = "No Locale assets found. Create via: Window → Asset Management → Localization Tables → New Locale";
                return false;
            }
            
            // 3. Проверка что Locale'и включены в Addressables билд
            // Читаем catalog и ищем GUID'ы locale-ассетов
            var catalogFiles = System.IO.Directory.GetFiles(aaPath, "catalog*");
            string catalogContent = null;
            foreach (var cf in catalogFiles)
            {
                if (cf.EndsWith(".json")) { catalogContent = System.IO.File.ReadAllText(cf); break; }
            }
            if (catalogContent != null)
            {
                bool anyLocaleInCatalog = false;
                foreach (var guid in localeGuids)
                {
                    if (catalogContent.Contains(guid))
                    {
                        anyLocaleInCatalog = true;
                        break;
                    }
                }
                if (!anyLocaleInCatalog)
                {
                    reason = "Locale assets not in Addressables build. Add Locales to Localization Settings, then rebuild Addressables.";
                    return false;
                }
            }
            
            reason = null;
            return true;
            #else
            reason = null;
            return true;
            #endif
        }
        
        private async Task InitializeUnityLocalization()
        {
            // Проверяем готовность ДО обращения к Unity Localization
            if (!CanUseUnityLocalization(out string reason))
            {
                #if UNITY_EDITOR
                // В Editor даём шанс инициализироваться напрямую через AssetDatabase,
                // даже если Addressables catalog не собран.
                LogWarning($"Unity Localization pre-check failed: {reason}. Trying direct initialization in Editor.");
                #else
                LogWarning($"Unity Localization unavailable: {reason}. Using fallback.");
                FallbackToConfig();
                return;
                #endif
            }
            
            try
            {
                var initOp = LocalizationSettings.InitializationOperation;
                while (!initOp.IsDone)
                    await Task.Yield();
                
                if (initOp.Status != AsyncOperationStatus.Succeeded)
                {
                    LogWarning("Unity Localization init failed. Falling back to config.");
                    FallbackToConfig();
                    return;
                }
                
                var locales = LocalizationSettings.AvailableLocales?.Locales;
                if (locales == null || locales.Count == 0)
                {
                    LogWarning("No Locales available. Falling back to config.");
                    FallbackToConfig();
                    return;
                }
                
                _availableLanguages.Clear();
                foreach (var locale in locales)
                    _availableLanguages.Add(locale.Identifier.Code);
                
                string targetLang = DetermineLanguage();
                var targetLocale = FindLocale(targetLang);
                if (targetLocale != null)
                {
                    LocalizationSettings.SelectedLocale = targetLocale;
                    _currentLanguage = targetLang;
                }
                else
                {
                    _currentLanguage = LocalizationSettings.SelectedLocale?.Identifier.Code 
                        ?? config.defaultLanguage;
                }
                
                LocalizationSettings.SelectedLocaleChanged += OnUnityLocaleChanged;
                
                foreach (var tableName in config.preloadTables)
                {
                    try { await PreloadTable(tableName); }
                    catch (System.Exception e) { LogWarning($"Failed to preload table '{tableName}': {e.Message}"); }
                }
            }
            catch (System.Exception e)
            {
                LogWarning($"Unity Localization init exception: {e.Message}. Falling back to config.");
                FallbackToConfig();
            }
        }
        
        private void FallbackToConfig()
        {
            _isFallbackMode = true;
            _availableLanguages.Clear();
            foreach (var lang in config.supportedLanguages)
                _availableLanguages.Add(lang.code);
            _currentLanguage = DetermineLanguage();
        }
        
        private async Task PreloadTable(string tableName)
        {
            try
            {
                var op = LocalizationSettings.StringDatabase.GetTableAsync(tableName);
                while (!op.IsDone)
                    await Task.Yield();
                
                if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                {
                    _loadedTables[tableName] = op.Result;
                    EventBus.Publish(EventBus.Localization.TableLoaded, tableName);
                    LogInit($"Table preloaded: {tableName}");
                }
                else
                {
                    LogWarning($"Failed to preload table: {tableName}");
                }
            }
            catch (Exception e)
            {
                LogWarning($"Error preloading table '{tableName}': {e.Message}");
            }
        }
        
        private Locale FindLocale(string code)
        {
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                if (locale.Identifier.Code == code)
                    return locale;
            return null;
        }
        
        private async void OnUnityLocaleChanged(Locale newLocale)
        {
            if (newLocale == null) return;
            string newLang = newLocale.Identifier.Code;
            if (newLang == _currentLanguage) return;
            
            var prevLang = _currentLanguage;
            _currentLanguage = newLang;
            _loadedTables.Clear();
            
            // Дождаться загрузки таблиц нового языка перед оповещением UI
            foreach (var tableName in config.preloadTables)
            {
                try { await PreloadTable(tableName); }
                catch (System.Exception e) { LogWarning($"Failed to reload table '{tableName}': {e.Message}"); }
            }
            
            EventBus.Publish(EventBus.Localization.LanguageChanged, new LocaleChangedData
            {
                PreviousLanguage = prevLang,
                NewLanguage = newLang
            });
            LogRuntime($"Language changed: {prevLang} → {newLang}");
        }
        #endif
        
        private const string LANGUAGE_PREF_KEY = "ProtoSystem.Language";
        
        private string DetermineLanguage()
        {
            // 1. Сохранённый выбор пользователя
            string saved = PlayerPrefs.GetString(LANGUAGE_PREF_KEY, "");
            if (!string.IsNullOrEmpty(saved) && _availableLanguages.Contains(saved))
                return saved;
            
            // 2. Авто-определение по системе
            if (config.autoDetectSystemLanguage)
            {
                string sysLang = Application.systemLanguage.ToISOCode();
                if (_availableLanguages.Contains(sysLang))
                    return sysLang;
            }
            return config.defaultLanguage;
        }
        
        #endregion
        
        #region Get API (default table)
        
        public string Get(string key)
        {
            return GetInternal(config.defaultStringTable, key, FormatMissing(key));
        }
        
        public string Get(string key, string fallback)
        {
            return GetInternal(config.defaultStringTable, key, fallback);
        }
        
        public string GetWithArgs(string key, (string name, object value)[] args)
        {
            return GetWithArgsInternal(config.defaultStringTable, key, args);
        }
        
        #endregion
        
        #region From API (explicit table)
        
        public string From(string table, string key)
        {
            return GetInternal(table, key, FormatMissing(key));
        }
        
        public string From(string table, string key, string fallback)
        {
            return GetInternal(table, key, fallback);
        }
        
        public string FromWithArgs(string table, string key, (string name, object value)[] args)
        {
            return GetWithArgsInternal(table, key, args);
        }
        
        #endregion
        
        #region Plural API
        
        public string GetPlural(string keyPrefix, int count)
        {
            return GetPluralInternal(config.defaultStringTable, keyPrefix, count);
        }
        
        public string GetPlural(string table, string keyPrefix, int count)
        {
            return GetPluralInternal(table, keyPrefix, count);
        }
        
        private string GetPluralInternal(string table, string keyPrefix, int count)
        {
            string suffix = PluralRules.GetSuffix(_currentLanguage, count);
            string fullKey = $"{keyPrefix}.{suffix}";
            
            string result = GetInternal(table, fullKey, null);
            
            if (result == null && suffix != "other")
                result = GetInternal(table, $"{keyPrefix}.other", null);
            
            if (result == null)
            {
                if (config.logMissingKeys)
                    LogWarning($"Missing plural: {table}:{keyPrefix} (count={count})");
                return FormatMissing(fullKey);
            }
            
            return result.Replace("{count}", count.ToString());
        }
        
        #endregion
        
        #region Has API
        
        public bool Has(string key) => Has(config.defaultStringTable, key);
        
        public bool Has(string table, string key)
        {
            if (_runtimeEntries.TryGetValue(table, out var rtDict) && rtDict.ContainsKey(key))
                return true;

            #if PROTO_HAS_LOCALIZATION
            try
            {
                if (_loadedTables.TryGetValue(table, out var t))
                    return t.GetEntry(key) != null;
                return false;
            }
            catch { return false; }
            #else
            return false;
            #endif
        }
        
        #endregion

        #region Runtime Keys

        /// <summary>
        /// Добавить/перезаписать ключ локализации в рантайме.
        /// Рантайм-ключи имеют приоритет над загруженными таблицами.
        /// При смене языка LocalizeTMP автоматически вызовет UpdateText(),
        /// поэтому серверные данные нужно перезаписывать при смене языка.
        /// </summary>
        public void Set(string key, string value)
            => Set(config.defaultStringTable, key, value);

        public void Set(string table, string key, string value)
        {
            if (!_runtimeEntries.TryGetValue(table, out var dict))
            {
                dict = new Dictionary<string, string>();
                _runtimeEntries[table] = dict;
            }
            dict[key] = value;
        }

        #endregion

        #region Language API

        public void SetLanguage(string languageCode)
        {
            if (languageCode == _currentLanguage) return;
            if (!_availableLanguages.Contains(languageCode))
            {
                LogWarning($"Language not available: {languageCode}");
                return;
            }
            
            // Сохраняем выбор пользователя
            PlayerPrefs.SetString(LANGUAGE_PREF_KEY, languageCode);
            PlayerPrefs.Save();
            
            #if PROTO_HAS_LOCALIZATION
            if (!_isFallbackMode)
            {
                var locale = FindLocale(languageCode);
                if (locale != null)
                {
                    LocalizationSettings.SelectedLocale = locale;
                    return; // OnUnityLocaleChanged опубликует событие
                }
            }
            #endif
            
            // Fallback: публикуем событие напрямую
            var prev = _currentLanguage;
            _currentLanguage = languageCode;
            EventBus.Publish(EventBus.Localization.LanguageChanged, new LocaleChangedData
            {
                PreviousLanguage = prev,
                NewLanguage = languageCode
            });
            LogRuntime($"Language changed: {prev} → {languageCode}");
        }
        
        #endregion
        
        #region IResettable
        
        public void ResetState(object resetData = null) { }
        
        #endregion
        
        #region Internal
        
        private string GetInternal(string tableName, string key, string fallback)
        {
            // Рантайм-ключи — наивысший приоритет
            if (_runtimeEntries.TryGetValue(tableName, out var rtDict) &&
                rtDict.TryGetValue(key, out var rtValue))
                return rtValue;

            #if PROTO_HAS_LOCALIZATION
            try
            {
                var entry = GetTableEntry(tableName, key);
                if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                    return entry.LocalizedValue;
            }
            catch { /* fallback */ }
            #endif

            if (fallback != null) return fallback;
            
            if (config.logMissingKeys)
                LogWarning($"Missing key: {tableName}:{key}");
            
            return FormatMissing(key);
        }
        
        private string GetWithArgsInternal(string tableName, string key, 
            (string name, object value)[] args)
        {
            #if PROTO_HAS_LOCALIZATION
            try
            {
                var entry = GetTableEntry(tableName, key);
                if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                {
                    string result = entry.LocalizedValue;
                    foreach (var (name, value) in args)
                        result = result.Replace($"{{{name}}}", value?.ToString() ?? "");
                    return result;
                }
            }
            catch { /* fallback */ }
            #endif
            
            if (config.logMissingKeys)
                LogWarning($"Missing key: {tableName}:{key}");
            
            return FormatMissing(key);
        }
        
        #if PROTO_HAS_LOCALIZATION
        private StringTableEntry GetTableEntry(string tableName, string key)
        {
            // Из кеша
            if (_loadedTables.TryGetValue(tableName, out var table))
                return table.GetEntry(key);
            
            // Синхронная загрузка таблицы
            var op = LocalizationSettings.StringDatabase.GetTableAsync(tableName);
            if (op.IsDone && op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
            {
                _loadedTables[tableName] = op.Result;
                return op.Result.GetEntry(key);
            }
            
            return null;
        }
        #endif
        
        private string FormatMissing(string key)
        {
            return string.Format(config.missingKeyFormat, key);
        }
        
        private void Cleanup()
        {
            Loc.Unregister(this);
            
            #if PROTO_HAS_LOCALIZATION
            LocalizationSettings.SelectedLocaleChanged -= OnUnityLocaleChanged;
            _loadedTables.Clear();
            #endif
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            Cleanup();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Расширение для конвертации SystemLanguage в ISO код.
    /// </summary>
    public static class SystemLanguageExtensions
    {
        public static string ToISOCode(this SystemLanguage lang)
        {
            return lang switch
            {
                SystemLanguage.Russian => "ru",
                SystemLanguage.English => "en",
                SystemLanguage.German => "de",
                SystemLanguage.French => "fr",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Italian => "it",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.ChineseSimplified => "zh",
                SystemLanguage.ChineseTraditional => "zh-TW",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Danish => "da",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Thai => "th",
                SystemLanguage.Vietnamese => "vi",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Ukrainian => "uk",
                _ => "en"
            };
        }
    }
}
