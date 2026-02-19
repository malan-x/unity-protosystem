// Packages/com.protosystem.core/Editor/Localization/AITranslationWindow.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

#if PROTO_HAS_LOCALIZATION
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Окно для экспорта/импорта/валидации переводов через AI.
    /// ProtoSystem → Localization → AI Translation
    /// </summary>
    public class AITranslationWindow : EditorWindow
    {
        private enum Tab { Export, Import, Validate }
        
        private Tab _currentTab;
        private Vector2 _scrollPos;
        
        // Config
        private LocalizationConfig _config;
        private StringMetadataDatabase _metadata;
        
        // Export
        private string _exportTable = "UI";
        private int _sourceLanguageIdx;
        private int _targetLanguageIdx = 1;
        private bool _onlyMissing = true;
        private string _exportPath;
        
        // Import
        private string _importPath;
        private bool _overwriteExisting;
        
        // Validate
        private string _validatePath;
        private List<ValidationResult> _validationResults;
        
        // Available
        private string[] _languageCodes;
        private string[] _languageNames;
        private string[] _tableNames;
        private bool _initialized;
        
        [MenuItem("ProtoSystem/Localization/AI Translation", false, 502)]
        public static void ShowWindow()
        {
            var window = GetWindow<AITranslationWindow>("AI Translation");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            // Config
            _config = FindConfig();
            _metadata = FindMetadata();
            
            // Языки из конфига
            if (_config != null && _config.supportedLanguages.Count > 0)
            {
                _languageCodes = _config.supportedLanguages.Select(l => l.code).ToArray();
                _languageNames = _config.supportedLanguages
                    .Select(l => $"{l.displayName} ({l.code})").ToArray();
                
                var sourceIdx = _config.supportedLanguages
                    .FindIndex(l => l.isSource || l.code == _config.defaultLanguage);
                _sourceLanguageIdx = Mathf.Max(0, sourceIdx);
                _targetLanguageIdx = _sourceLanguageIdx == 0 ? 
                    Mathf.Min(1, _languageCodes.Length - 1) : 0;
            }
            else
            {
                _languageCodes = new[] { "ru", "en" };
                _languageNames = new[] { "Русский (ru)", "English (en)" };
            }
            
            // Таблицы
            #if PROTO_HAS_LOCALIZATION
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            _tableNames = collections.Select(c => c.TableCollectionName).ToArray();
            #else
            _tableNames = new[] { "UI", "Game" };
            #endif
            
            if (_tableNames.Length == 0)
                _tableNames = new[] { "UI" };
            
            // Путь по умолчанию
            string basePath = GetDefaultExportPath();
            _exportPath = basePath;
            // Import/Validate по умолчанию смотрят в Import-папку
            string importPath = basePath.Replace("/Export", "/Import");
            _importPath = Directory.Exists(importPath) ? importPath : basePath;
            _validatePath = _importPath;
            
            _initialized = true;
        }
        
        private void OnGUI()
        {
            if (!_initialized) Initialize();
            
            EditorGUILayout.Space(5);
            
            // Табы
            EditorGUILayout.BeginHorizontal();
            DrawTab("📤 Export", Tab.Export);
            DrawTab("📥 Import", Tab.Import);
            DrawTab("✅ Validate", Tab.Validate);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Конфиг
            EditorGUILayout.BeginHorizontal();
            _config = (LocalizationConfig)EditorGUILayout.ObjectField("Config", _config, 
                typeof(LocalizationConfig), false);
            _metadata = (StringMetadataDatabase)EditorGUILayout.ObjectField("Metadata", _metadata, 
                typeof(StringMetadataDatabase), false, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            #if !PROTO_HAS_LOCALIZATION
            EditorGUILayout.HelpBox("Unity Localization не установлен. Экспорт/импорт недоступны.", 
                MessageType.Warning);
            GUI.enabled = false;
            #endif
            
            EditorGUILayout.Space(5);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            switch (_currentTab)
            {
                case Tab.Export: DrawExportTab(); break;
                case Tab.Import: DrawImportTab(); break;
                case Tab.Validate: DrawValidateTab(); break;
            }
            
            EditorGUILayout.EndScrollView();
            
            GUI.enabled = true;
        }
        
        // ──────────────── Export Tab ────────────────
        
        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("Экспорт строк для AI-перевода", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Таблица
            int tableIdx = System.Array.IndexOf(_tableNames, _exportTable);
            if (tableIdx < 0) tableIdx = 0;
            tableIdx = EditorGUILayout.Popup("Table", tableIdx, _tableNames);
            _exportTable = _tableNames[tableIdx];
            
            // Языки
            _sourceLanguageIdx = EditorGUILayout.Popup("Source Language", 
                _sourceLanguageIdx, _languageNames);
            _targetLanguageIdx = EditorGUILayout.Popup("Target Language", 
                _targetLanguageIdx, _languageNames);
            
            if (_sourceLanguageIdx == _targetLanguageIdx)
            {
                EditorGUILayout.HelpBox("Source и Target языки совпадают!", MessageType.Error);
            }
            
            _onlyMissing = EditorGUILayout.Toggle("Only Missing Translations", _onlyMissing);
            
            EditorGUILayout.Space(5);
            
            // Путь
            EditorGUILayout.BeginHorizontal();
            _exportPath = EditorGUILayout.TextField("Export Folder", _exportPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Export folder", _exportPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _exportPath = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _exportPath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            GUI.enabled = _sourceLanguageIdx != _targetLanguageIdx;
            if (GUILayout.Button("📤 Export to JSON", GUILayout.Height(30)))
            {
                DoExport();
            }
            GUI.enabled = true;
        }
        
        // ──────────────── Import Tab ────────────────
        
        private void DrawImportTab()
        {
            EditorGUILayout.LabelField("Импорт переводов из JSON", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            _importPath = EditorGUILayout.TextField("JSON File", _importPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select JSON", _importPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _importPath = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _importPath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("✅ Validate First", GUILayout.Height(30)))
            {
                _validatePath = _importPath;
                _validationResults = LocalizationValidator.Validate(_importPath, _metadata);
                _currentTab = Tab.Validate;
            }
            if (GUILayout.Button("📥 Import", GUILayout.Height(30)))
            {
                DoImport();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        // ──────────────── Validate Tab ────────────────
        
        private void DrawValidateTab()
        {
            EditorGUILayout.LabelField("Валидация переводов", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            _validatePath = EditorGUILayout.TextField("JSON File", _validatePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select JSON", _validatePath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _validatePath = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _validatePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Validate", GUILayout.Height(25)))
            {
                _validationResults = LocalizationValidator.Validate(_validatePath, _metadata);
            }
            
            EditorGUILayout.Space(10);
            
            if (_validationResults != null)
            {
                int errors = _validationResults.Count(r => r.type == ValidationResult.ValidationType.Error);
                int warnings = _validationResults.Count(r => r.type == ValidationResult.ValidationType.Warning);
                
                if (_validationResults.Count == 0)
                {
                    EditorGUILayout.HelpBox("✅ Все проверки пройдены!", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Найдено: {errors} ошибок, {warnings} предупреждений", 
                        errors > 0 ? MessageType.Error : MessageType.Warning);
                }
                
                EditorGUILayout.Space(5);
                
                foreach (var r in _validationResults)
                {
                    var icon = r.type switch
                    {
                        ValidationResult.ValidationType.Error => "❌",
                        ValidationResult.ValidationType.Warning => "⚠️",
                        _ => "✅"
                    };
                    
                    var style = r.type == ValidationResult.ValidationType.Error
                        ? EditorStyles.helpBox : EditorStyles.helpBox;
                    
                    EditorGUILayout.BeginHorizontal(style);
                    EditorGUILayout.LabelField($"{icon} [{r.key}]", GUILayout.Width(200));
                    EditorGUILayout.LabelField(r.message);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        // ──────────────── Actions ────────────────
        
        private void DoExport()
        {
            string source = _languageCodes[_sourceLanguageIdx];
            string target = _languageCodes[_targetLanguageIdx];
            string fileName = $"{_exportTable}_{source}_to_{target}.json";
            string fullPath = Path.Combine(_exportPath, fileName);
            
            var result = LocalizationExporter.Export(
                _exportTable, source, target, _metadata, _config, fullPath, _onlyMissing);
            
            if (result != null)
            {
                EditorUtility.DisplayDialog("Export Complete", 
                    $"Exported to:\n{result}\n\nОткройте файл и передайте AI для перевода.", "OK");
                
                EditorUtility.RevealInFinder(result);
            }
        }
        
        private void DoImport()
        {
            if (string.IsNullOrEmpty(_importPath) || !File.Exists(_importPath))
            {
                EditorUtility.DisplayDialog("Error", "JSON файл не найден", "OK");
                return;
            }
            
            var result = LocalizationImporter.Import(_importPath, _overwriteExisting);
            
            string msg = $"Imported: {result.imported}\nSkipped: {result.skipped}\nErrors: {result.errors}";
            if (result.errorMessages.Count > 0)
                msg += "\n\n" + string.Join("\n", result.errorMessages);
            
            EditorUtility.DisplayDialog("Import Result", msg, "OK");
        }
        
        // ──────────────── Helpers ────────────────
        
        private void DrawTab(string label, Tab tab)
        {
            var style = _currentTab == tab ? EditorStyles.toolbarButton : EditorStyles.toolbarButton;
            
            var oldBg = GUI.backgroundColor;
            if (_currentTab == tab)
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            
            if (GUILayout.Button(label, style))
                _currentTab = tab;
            
            GUI.backgroundColor = oldBg;
        }
        
        private static LocalizationConfig FindConfig()
        {
            var guids = AssetDatabase.FindAssets("t:LocalizationConfig");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<LocalizationConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            return Resources.Load<LocalizationConfig>("LocalizationConfig");
        }
        
        private static StringMetadataDatabase FindMetadata()
        {
            var guids = AssetDatabase.FindAssets("t:StringMetadataDatabase");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<StringMetadataDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }
        
        private static string GetDefaultExportPath()
        {
            var projectConfig = Resources.Load<ProjectConfig>("ProjectConfig");
            if (projectConfig != null && !string.IsNullOrEmpty(projectConfig.projectNamespace))
                return $"Assets/{projectConfig.projectNamespace}/Settings/Localization/Export";
            return "Assets/Settings/Localization/Export";
        }
    }
}
