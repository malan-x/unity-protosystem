// Packages/com.protosystem.core/Editor/Localization/LocalizationSetupWizard.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

#if PROTO_HAS_LOCALIZATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Wizard для первоначальной настройки системы локализации.
    /// Создаёт LocalizationConfig, Locale'и, String Tables, билдит Addressables.
    /// 
    /// Меню: ProtoSystem → Localization → Setup Wizard
    /// </summary>
    public class LocalizationSetupWizard : EditorWindow
    {
        private string _configPath;
        private string _defaultLang = "ru";
        private string _fallbackLang = "en";
        private bool _autoDetect = true;
        private bool _pathInitialized;
        private Vector2 _scrollPos;
        private LocalizationConfig _config;
        private bool _languagesFoldout;

        /// <summary>
        /// Каталог всех доступных языков (код, нативное имя, русское название).
        /// Отсортирован по русскому названию.
        /// </summary>
        private static readonly (string code, string nativeName, string label)[] LanguageCatalog =
        {
            ("en",     "English",              "Английский"),
            ("ar",     "العربية",               "Арабский"),
            ("bg",     "Български",             "Болгарский"),
            ("hu",     "Magyar",               "Венгерский"),
            ("vi",     "Tiếng Việt",           "Вьетнамский"),
            ("el",     "Ελληνικά",             "Греческий"),
            ("da",     "Dansk",                "Датский"),
            ("id",     "Bahasa Indonesia",     "Индонезийский"),
            ("es",     "Español (España)",     "Испанский — Испания"),
            ("es-419", "Español (LATAM)",      "Испанский — Лат. Америка"),
            ("it",     "Italiano",             "Итальянский"),
            ("zh",     "简体中文",               "Китайский (упр.)"),
            ("zh-TW",  "繁體中文",               "Китайский (трад.)"),
            ("ko",     "한국어",                 "Корейский"),
            ("de",     "Deutsch",              "Немецкий"),
            ("nl",     "Nederlands",           "Нидерландский"),
            ("no",     "Norsk",                "Норвежский"),
            ("pl",     "Polski",               "Польский"),
            ("pt-BR",  "Português (Brasil)",   "Португальский — Бразилия"),
            ("pt-PT",  "Português (Portugal)", "Португальский — Португалия"),
            ("ro",     "Română",               "Румынский"),
            ("ru",     "Русский",              "Русский"),
            ("th",     "ไทย",                   "Тайский"),
            ("tr",     "Türkçe",               "Турецкий"),
            ("uk",     "Українська",            "Украинский"),
            ("fi",     "Suomi",                "Финский"),
            ("fr",     "Français",             "Французский"),
            ("cs",     "Čeština",              "Чешский"),
            ("sv",     "Svenska",              "Шведский"),
            ("ja",     "日本語",                 "Японский"),
        };
        
        [MenuItem("ProtoSystem/Localization/Setup Wizard", false, 500)]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationSetupWizard>("Localization Setup");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }
        
        [MenuItem("ProtoSystem/Localization/Create Config", false, 501)]
        public static void CreateConfigQuick()
        {
            string path = GetDefaultConfigPath();
            CreateConfig(path, "ru", "en", true);
        }
        
        private void OnEnable()
        {
            InitializeDefaultPath();
            TryFindConfig();
        }
        
        private void InitializeDefaultPath()
        {
            if (_pathInitialized) return;
            _configPath = GetDefaultConfigPath();
            _pathInitialized = true;
        }

        private void TryFindConfig()
        {
            if (_config != null) return;

            string fullPath = $"{_configPath}/LocalizationConfig.asset";
            _config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(fullPath);
            if (_config != null) return;

            _config = Resources.Load<LocalizationConfig>("LocalizationConfig");
            if (_config != null) return;

            var guids = AssetDatabase.FindAssets("t:LocalizationConfig");
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        
        private static string GetDefaultConfigPath()
        {
            var projectConfig = Resources.Load<ProjectConfig>("ProjectConfig");
            if (projectConfig != null && !string.IsNullOrEmpty(projectConfig.projectNamespace))
                return $"Assets/{projectConfig.projectNamespace}/Settings/Localization";
            return "Assets/Settings/Localization";
        }
        
        private void OnGUI()
        {
            ProtoEditorStyles.Header("🌐 ProtoLocalization Setup",
                "Установка зависимостей, конфиг, языки и создание String Tables.");

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawDependencyStatus();
            EditorGUILayout.Space(10);
            
            DrawConfigSection();
            EditorGUILayout.Space(10);

            DrawLanguagesSection();
            EditorGUILayout.Space(10);

            #if PROTO_HAS_LOCALIZATION
            DrawLocalizationSetup();
            EditorGUILayout.Space(10);
            #endif
            
            DrawActionButtons();
            
            EditorGUILayout.EndScrollView();
        }
        
        // ═══════════════════════════════════════════════════════════
        // Status
        // ═══════════════════════════════════════════════════════════
        
        private void DrawDependencyStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Статус", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            #if PROTO_HAS_LOCALIZATION
            DrawCheck("Unity Localization", true, "Установлен");
            
            // Localization Settings (через AssetDatabase)
            bool hasSettings = AssetDatabase.FindAssets("t:LocalizationSettings").Length > 0;
            DrawCheck("Localization Settings", hasSettings, 
                hasSettings ? "Найден" : "Не создан");
            
            // Locales
            var editorLocales = LocalizationEditorSettings.GetLocales();
            int localeCount = editorLocales?.Count ?? 0;
            string localeNames = "";
            if (localeCount > 0)
            {
                for (int i = 0; i < editorLocales.Count; i++)
                {
                    if (i > 0) localeNames += ", ";
                    localeNames += editorLocales[i].Identifier.Code;
                }
            }
            DrawCheck("Locale'и", localeCount > 0,
                localeCount > 0 ? $"{localeCount} шт. ({localeNames})" : "Не созданы");
            
            // String Tables
            var tableCollections = LocalizationEditorSettings.GetStringTableCollections();
            int tableCount = 0;
            string tableNames = "";
            foreach (var tc in tableCollections)
            {
                if (tableCount > 0) tableNames += ", ";
                tableNames += tc.TableCollectionName;
                tableCount++;
            }
            DrawCheck("String Tables", tableCount > 0,
                tableCount > 0 ? $"{tableCount} шт. ({tableNames})" : "Не созданы");
            
            // Addressables
            bool addressablesBuilt = IsAddressablesBuilt();
            DrawCheck("Addressables Build", addressablesBuilt,
                addressablesBuilt ? "Собраны" : "Требуется сборка!");
            
            // Config
            string configPath = $"{_configPath}/LocalizationConfig.asset";
            bool hasConfig = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(configPath) != null;
            DrawCheck("LocalizationConfig", hasConfig,
                hasConfig ? "Создан" : "Не создан");
            
            #else
            DrawCheck("Unity Localization", false, "Не установлен");
            EditorGUILayout.HelpBox(
                "Установите через Package Manager: com.unity.localization\n" +
                "Без него система работает в fallback-режиме.",
                MessageType.Warning);
            #endif
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCheck(string label, bool ok, string detail)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = ok ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.4f);
            EditorGUILayout.LabelField(ok ? "  ✓" : "  ✗", GUILayout.Width(25));
            GUI.color = Color.white;
            EditorGUILayout.LabelField(label, GUILayout.Width(140));
            EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        // ═══════════════════════════════════════════════════════════
        // Config Creation
        // ═══════════════════════════════════════════════════════════
        
        private void DrawConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("LocalizationConfig", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUI.BeginChangeCheck();
            _config = (LocalizationConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(LocalizationConfig), false);
            if (EditorGUI.EndChangeCheck() && _config != null)
            {
                _defaultLang = _config.defaultLanguage;
                _fallbackLang = _config.fallbackLanguage;
                _autoDetect = _config.autoDetectSystemLanguage;
                var configAssetPath = AssetDatabase.GetAssetPath(_config);
                if (!string.IsNullOrEmpty(configAssetPath))
                    _configPath = Path.GetDirectoryName(configAssetPath).Replace('\\', '/');
            }

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            _configPath = EditorGUILayout.TextField("Path", _configPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Config Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                    _configPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
            
            _defaultLang = EditorGUILayout.TextField("Default Language", _defaultLang);
            _fallbackLang = EditorGUILayout.TextField("Fallback Language", _fallbackLang);
            _autoDetect = EditorGUILayout.Toggle("Auto Detect", _autoDetect);
            
            EditorGUILayout.Space(3);
            
            string fullPath = $"{_configPath}/LocalizationConfig.asset";
            bool exists = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(fullPath) != null;
            
            if (exists)
                EditorGUILayout.HelpBox("Конфиг уже существует.", MessageType.Info);
            
            if (GUILayout.Button(exists ? "Пересоздать Config" : "Создать Config", GUILayout.Height(25)))
            {
                _config = CreateConfig(_configPath, _defaultLang, _fallbackLang, _autoDetect);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // ═══════════════════════════════════════════════════════════
        // Languages
        // ═══════════════════════════════════════════════════════════

        private void DrawLanguagesSection()
        {
            if (_config == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Сводка: количество и список текущих языков
            int count = _config.supportedLanguages.Count;
            string summary = "";
            for (int i = 0; i < _config.supportedLanguages.Count; i++)
            {
                if (i > 0) summary += ", ";
                var lang = _config.supportedLanguages[i];
                summary += $"{lang.displayName} ({lang.code})";
            }

            EditorGUILayout.LabelField($"Языки в конфиге: {count}", EditorStyles.boldLabel);
            if (count > 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(3);

            _languagesFoldout = EditorGUILayout.Foldout(_languagesFoldout,
                "Выбор языков", true, EditorStyles.foldoutHeader);

            if (_languagesFoldout)
            {
                EditorGUILayout.Space(3);

                var currentCodes = new HashSet<string>();
                foreach (var lang in _config.supportedLanguages)
                    currentCodes.Add(lang.code);

                bool changed = false;
                int half = (LanguageCatalog.Length + 1) / 2;

                for (int i = 0; i < half; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Левый столбец
                    changed |= DrawLanguageToggle(LanguageCatalog[i], currentCodes);

                    // Правый столбец
                    int ri = i + half;
                    if (ri < LanguageCatalog.Length)
                        changed |= DrawLanguageToggle(LanguageCatalog[ri], currentCodes);

                    EditorGUILayout.EndHorizontal();
                }

                if (changed)
                    EditorUtility.SetDirty(_config);

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Выбрать все", GUILayout.Height(20)))
                {
                    foreach (var (code, nativeName, _) in LanguageCatalog)
                    {
                        if (!currentCodes.Contains(code))
                        {
                            _config.supportedLanguages.Add(new LanguageEntry
                            {
                                code = code,
                                displayName = nativeName,
                                isSource = false
                            });
                        }
                    }
                    EditorUtility.SetDirty(_config);
                }
                if (GUILayout.Button("Снять все", GUILayout.Height(20)))
                {
                    _config.supportedLanguages.Clear();
                    EditorUtility.SetDirty(_config);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private bool DrawLanguageToggle(
            (string code, string nativeName, string label) entry,
            HashSet<string> currentCodes)
        {
            bool wasEnabled = currentCodes.Contains(entry.code);
            bool isEnabled = EditorGUILayout.ToggleLeft(
                $"{entry.label} ({entry.nativeName})", wasEnabled);

            if (isEnabled == wasEnabled) return false;

            if (isEnabled)
            {
                _config.supportedLanguages.Add(new LanguageEntry
                {
                    code = entry.code,
                    displayName = entry.nativeName,
                    isSource = false
                });
            }
            else
            {
                _config.supportedLanguages.RemoveAll(l => l.code == entry.code);
            }
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        // Localization Setup (Locales + Tables + Addressables)
        // ═══════════════════════════════════════════════════════════
        
        #if PROTO_HAS_LOCALIZATION
        
        private void DrawLocalizationSetup()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Unity Localization Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            // Full Setup button
            GUI.color = new Color(0.5f, 0.9f, 0.5f);
            if (GUILayout.Button("🚀 Full Setup (всё сразу)", GUILayout.Height(30)))
            {
                RunFullSetup();
            }
            GUI.color = Color.white;
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Или по шагам:", EditorStyles.miniLabel);
            EditorGUILayout.Space(3);
            
            // Step 1: Localization Settings
            bool hasSettings = AssetDatabase.FindAssets("t:LocalizationSettings").Length > 0;
            EditorGUI.BeginDisabledGroup(hasSettings);
            if (GUILayout.Button(hasSettings 
                ? "1. ✓ Localization Settings (создан)" 
                : "1. Создать Localization Settings", GUILayout.Height(23)))
            {
                EnsureLocalizationSettings();
            }
            EditorGUI.EndDisabledGroup();
            
            // Step 2: Locales
            var existingLocales = LocalizationEditorSettings.GetLocales();
            int localeCount = existingLocales?.Count ?? 0;
            if (GUILayout.Button(localeCount > 0 
                ? $"2. ✓ Locale'и ({localeCount} шт.) — добавить недостающие"
                : "2. Создать Locale'и (из конфига)", GUILayout.Height(23)))
            {
                CreateLocalesFromConfig();
            }
            
            // Step 3: String Tables
            if (GUILayout.Button("3. Создать String Tables (из конфига)", GUILayout.Height(23)))
            {
                CreateStringTablesFromConfig();
            }
            
            // Step 4: Build Addressables
            bool addressablesBuilt = IsAddressablesBuilt();
            GUI.color = addressablesBuilt ? Color.white : new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button(addressablesBuilt 
                ? "4. ✓ Пересобрать Addressables"
                : "4. ⚡ Build Addressables (обязательно!)", GUILayout.Height(25)))
            {
                BuildAddressables();
            }
            GUI.color = Color.white;
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Полная настройка: Settings → Locales → Tables → Addressables
        /// </summary>
        private void RunFullSetup()
        {
            EditorUtility.DisplayProgressBar("Localization Setup", "Создание Localization Settings...", 0.1f);
            EnsureLocalizationSettings();
            
            EditorUtility.DisplayProgressBar("Localization Setup", "Создание Locale'ей...", 0.3f);
            int created = CreateLocalesFromConfig();
            
            EditorUtility.DisplayProgressBar("Localization Setup", "Создание String Tables + заполнение UIKeys...", 0.5f);
            int tables = CreateStringTablesFromConfig();
            
            EditorUtility.DisplayProgressBar("Localization Setup", "Сборка Addressables...", 0.8f);
            BuildAddressables();
            
            EditorUtility.ClearProgressBar();
            
            Debug.Log($"[ProtoLocalization] Full Setup завершён: Locale'ей: {created}, таблиц: {tables}");
            
            // Перерисовать окно
            Repaint();
        }
        
        /// <summary>
        /// Создать/найти LocalizationSettings
        /// </summary>
        private static void EnsureLocalizationSettings()
        {
            var guids = AssetDatabase.FindAssets("t:LocalizationSettings");
            if (guids.Length > 0)
            {
                Debug.Log("[ProtoLocalization] Localization Settings уже существует");
                return;
            }
            
            // Создаём через Project Settings (Unity сама создаст при открытии)
            SettingsService.OpenProjectSettings("Project/Localization");
            Debug.Log("[ProtoLocalization] Откройте Project Settings → Localization и нажмите 'Create' если не создался автоматически");
        }
        
        /// <summary>
        /// Создать Locale'и из config.supportedLanguages
        /// </summary>
        private int CreateLocalesFromConfig()
        {
            var config = FindConfig();
            List<string> langsToCreate;
            
            if (config != null && config.supportedLanguages.Count > 0)
            {
                langsToCreate = new List<string>();
                foreach (var lang in config.supportedLanguages)
                    langsToCreate.Add(lang.code);
            }
            else
            {
                langsToCreate = new List<string> { _defaultLang, _fallbackLang };
            }
            
            var existingLocales = LocalizationEditorSettings.GetLocales();
            var existingCodes = new HashSet<string>();
            if (existingLocales != null)
            {
                foreach (var loc in existingLocales)
                    existingCodes.Add(loc.Identifier.Code);
            }
            
            int created = 0;
            string localeFolder = "Assets/Localization/Locales";
            if (!Directory.Exists(localeFolder))
                Directory.CreateDirectory(localeFolder);
            
            foreach (var code in langsToCreate)
            {
                if (existingCodes.Contains(code))
                {
                    Debug.Log($"[ProtoLocalization] Locale '{code}' уже существует, пропускаем");
                    continue;
                }
                
                var locale = Locale.CreateLocale(code);
                string assetPath = $"{localeFolder}/{code}.asset";
                
                AssetDatabase.CreateAsset(locale, assetPath);
                LocalizationEditorSettings.AddLocale(locale);
                
                Debug.Log($"[ProtoLocalization] Создан Locale: {code} → {assetPath}");
                created++;
            }
            
            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            
            // Автозаполнение встроенных ключей из UIKeys
            PopulateTablesFromUIKeys();
            
            return created;
        }
        
        /// <summary>
        /// Заполнить таблицу "UI" ключами и fallback-значениями из UIKeys.
        /// Сканирует UIKeys рефлексией: каждый вложенный класс с Fallback.
        /// </summary>
        private int PopulateTablesFromUIKeys()
        {
            // Найти таблицу UI
            StringTableCollection uiCollection = null;
            foreach (var tc in LocalizationEditorSettings.GetStringTableCollections())
            {
                if (tc.TableCollectionName == "UI")
                {
                    uiCollection = tc;
                    break;
                }
            }
            
            if (uiCollection == null)
            {
                Debug.LogWarning("[ProtoLocalization] Таблица 'UI' не найдена. Сначала создайте String Tables.");
                return 0;
            }
            
            // Собрать все StringTable по locale code
            var tables = new Dictionary<string, StringTable>();
            foreach (var st in uiCollection.StringTables)
            {
                tables[st.LocaleIdentifier.Code] = st;
            }
            
            if (tables.Count == 0)
            {
                Debug.LogWarning("[ProtoLocalization] Нет StringTable'ов в коллекции UI.");
                return 0;
            }
            
            // Сканировать UIKeys рефлексией
            var entries = CollectUIKeysEntries();
            
            int added = 0;
            var dirtyTables = new HashSet<StringTable>();
            
            foreach (var (key, translations) in entries)
            {
                // Создать ключ в SharedData если нет
                var sharedEntry = uiCollection.SharedData.GetEntry(key);
                if (sharedEntry == null)
                {
                    sharedEntry = uiCollection.SharedData.AddKey(key);
                }
                
                // Заполнить каждый locale (встроенные данные пакета — всегда перезапись)
                foreach (var (langCode, value) in translations)
                {
                    if (!tables.TryGetValue(langCode, out var table)) continue;
                    
                    // AddEntry возвращает существующую запись без изменения — явно ставим Value
                    var entry = table.AddEntry(sharedEntry.Id, value);
                    entry.Value = value;
                    dirtyTables.Add(table);
                    added++;
                }
            }
            
            if (dirtyTables.Count > 0)
            {
                foreach (var t in dirtyTables)
                    EditorUtility.SetDirty(t);
                EditorUtility.SetDirty(uiCollection.SharedData);
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log($"[ProtoLocalization] Таблица UI заполнена: {added} значений в {dirtyTables.Count} таблиц(ах)");
            return added;
        }
        
        /// <summary>
        /// Собирает (key, translations) из UIKeys рефлексией.
        /// Сканирует Fallback (русский) и FallbackEn (английский).
        /// Возвращает: key, Dictionary{langCode, value}
        /// </summary>
        private static List<(string key, Dictionary<string, string> translations)> CollectUIKeysEntries()
        {
            var result = new List<(string, Dictionary<string, string>)>();
            var uiKeysType = typeof(UIKeys);
            
            // Маппинг имён классов на коды языков
            var fallbackClassMap = new Dictionary<string, string>
            {
                { "Fallback", "ru" },
                { "FallbackEn", "en" },
            };
            
            foreach (var nestedType in uiKeysType.GetNestedTypes(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (nestedType.Name == "Fallback" || nestedType.Name.StartsWith("Fallback")) continue;
                
                // Собираем все Fallback* классы в словарь: lang -> {fieldName -> value}
                var langFallbacks = new Dictionary<string, Dictionary<string, string>>();
                foreach (var (className, langCode) in fallbackClassMap)
                {
                    var fbType = nestedType.GetNestedType(className,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (fbType == null) continue;
                    
                    var dict = new Dictionary<string, string>();
                    foreach (var field in fbType.GetFields(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        if (field.IsLiteral && field.FieldType == typeof(string))
                            dict[field.Name] = (string)field.GetRawConstantValue();
                    }
                    langFallbacks[langCode] = dict;
                }
                
                if (langFallbacks.Count == 0) continue;
                
                // Собираем ключи
                foreach (var field in nestedType.GetFields(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                    
                    string key = (string)field.GetRawConstantValue();
                    var translations = new Dictionary<string, string>();
                    
                    foreach (var (langCode, dict) in langFallbacks)
                    {
                        if (dict.TryGetValue(field.Name, out var val))
                            translations[langCode] = val;
                    }
                    
                    result.Add((key, translations));
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Создать String Table Collections из config.preloadTables
        /// </summary>
        private int CreateStringTablesFromConfig()
        {
            var config = FindConfig();
            List<string> tableNames;
            
            if (config != null && config.preloadTables.Count > 0)
            {
                tableNames = new List<string>(config.preloadTables);
            }
            else
            {
                tableNames = new List<string> { "UI", "Game" };
            }
            
            // Существующие таблицы
            var existingTables = new HashSet<string>();
            foreach (var tc in LocalizationEditorSettings.GetStringTableCollections())
                existingTables.Add(tc.TableCollectionName);
            
            // Существующие Locale'и
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning("[ProtoLocalization] Нет Locale'ей! Сначала создайте Locale'и.");
                return 0;
            }
            
            string tablesFolder = "Assets/Localization/Tables";
            if (!Directory.Exists(tablesFolder))
                Directory.CreateDirectory(tablesFolder);
            
            int created = 0;
            
            foreach (var tableName in tableNames)
            {
                if (existingTables.Contains(tableName))
                {
                    Debug.Log($"[ProtoLocalization] Table '{tableName}' уже существует, пропускаем");
                    continue;
                }
                
                var collection = LocalizationEditorSettings.CreateStringTableCollection(
                    tableName, $"{tablesFolder}/{tableName}");
                
                if (collection != null)
                {
                    Debug.Log($"[ProtoLocalization] Создана String Table: {tableName}");
                    created++;
                }
                else
                {
                    Debug.LogWarning($"[ProtoLocalization] Не удалось создать таблицу: {tableName}");
                }
            }
            
            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            
            return created;
        }
        
        /// <summary>
        /// Найти LocalizationConfig
        /// </summary>
        private LocalizationConfig FindConfig()
        {
            string fullPath = $"{_configPath}/LocalizationConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(fullPath);
            if (config != null) return config;
            
            // Поиск в Resources
            config = Resources.Load<LocalizationConfig>("LocalizationConfig");
            if (config != null) return config;
            
            // Глобальный поиск
            var guids = AssetDatabase.FindAssets("t:LocalizationConfig");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<LocalizationConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            
            return null;
        }
        
        #endif
        
        // ═══════════════════════════════════════════════════════════
        // Action Buttons
        // ═══════════════════════════════════════════════════════════
        
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Быстрые действия", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            #if PROTO_HAS_LOCALIZATION
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("📋 Localization Tables", GUILayout.Height(25)))
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Localization Tables");
            
            if (GUILayout.Button("📦 Addressables Groups", GUILayout.Height(25)))
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("⚙ Project Settings", GUILayout.Height(25)))
                SettingsService.OpenProjectSettings("Project/Localization");
            
            if (GUILayout.Button("🔄 AI Translation", GUILayout.Height(25)))
                AITranslationWindow.ShowWindow();
            
            EditorGUILayout.EndHorizontal();
            
            #else
            EditorGUILayout.HelpBox(
                "Установите com.unity.localization для доступа к действиям.",
                MessageType.Info);
            
            if (GUILayout.Button("Открыть Package Manager", GUILayout.Height(25)))
                UnityEditor.PackageManager.UI.Window.Open("com.unity.localization");
            #endif
            
            EditorGUILayout.EndVertical();
        }
        
        // ═══════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════
        
        private static bool IsAddressablesBuilt()
        {
            string aaPath = "Library/com.unity.addressables/aa/Windows";
            if (!Directory.Exists(aaPath)) return false;
            return Directory.GetFiles(aaPath, "catalog*").Length > 0;
        }
        
        #if PROTO_HAS_LOCALIZATION
        private static void BuildAddressables()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    settings = AddressableAssetSettings.Create(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                        AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                        true, true);
                    AddressableAssetSettingsDefaultObject.Settings = settings;
                    AssetDatabase.SaveAssets();
                }
                
                EditorUtility.DisplayProgressBar("Building Addressables", "Please wait...", 0.5f);
                AddressableAssetSettings.BuildPlayerContent();
                EditorUtility.ClearProgressBar();
                Debug.Log("[ProtoLocalization] Addressables built successfully!");
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[ProtoLocalization] Addressables build failed: {e.Message}");
                EditorUtility.DisplayDialog("Build Failed",
                    $"Addressables build failed:\n{e.Message}", "OK");
            }
        }
        #endif
        
        private static LocalizationConfig CreateConfig(string path, string defaultLang,
            string fallbackLang, bool autoDetect)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            var config = ScriptableObject.CreateInstance<LocalizationConfig>();
            config.defaultLanguage = defaultLang;
            config.fallbackLanguage = fallbackLang;
            config.autoDetectSystemLanguage = autoDetect;
            
            string assetPath = $"{path}/LocalizationConfig.asset";
            
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(assetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);
            
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"[ProtoLocalization] Config created: {assetPath}");
            return config;
        }
    }
}
