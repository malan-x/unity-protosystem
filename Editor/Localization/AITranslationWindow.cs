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
        private bool[] _targetLanguageToggles;
        private bool _onlyMissing = true;
        private string _exportPath;

        // Import
        private string _importFolder;
        private string[] _importFiles = System.Array.Empty<string>();
        private bool[] _importFileToggles = System.Array.Empty<bool>();
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

                _targetLanguageToggles = new bool[_languageCodes.Length];
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = i != _sourceLanguageIdx;
            }
            else
            {
                _languageCodes = new[] { "ru", "en" };
                _languageNames = new[] { "Русский (ru)", "English (en)" };
                _targetLanguageToggles = new[] { false, true };
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
            _importFolder = Directory.Exists(importPath) ? importPath : basePath;
            _validatePath = _importFolder;
            RefreshImportFiles();
            
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

            // Язык-источник
            _sourceLanguageIdx = EditorGUILayout.Popup("Source Language",
                _sourceLanguageIdx, _languageNames);

            // Целевые языки — галочки
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Target Languages", EditorStyles.boldLabel);

            if (_targetLanguageToggles == null || _targetLanguageToggles.Length != _languageCodes.Length)
            {
                _targetLanguageToggles = new bool[_languageCodes.Length];
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = i != _sourceLanguageIdx;
            }

            // Кнопки Выбрать все / Снять все
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
            {
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = i != _sourceLanguageIdx;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
            {
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            for (int i = 0; i < _languageCodes.Length; i++)
            {
                if (i == _sourceLanguageIdx) continue;
                _targetLanguageToggles[i] = EditorGUILayout.ToggleLeft(_languageNames[i], _targetLanguageToggles[i]);
            }
            EditorGUI.indentLevel--;

            bool anyTarget = false;
            for (int i = 0; i < _targetLanguageToggles.Length; i++)
            {
                if (i != _sourceLanguageIdx && _targetLanguageToggles[i])
                { anyTarget = true; break; }
            }

            if (!anyTarget)
                EditorGUILayout.HelpBox("Выберите хотя бы один целевой язык.", MessageType.Warning);

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

            GUI.enabled = anyTarget;
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

            // Папка с файлами
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _importFolder = EditorGUILayout.TextField("Import Folder", _importFolder);
            if (EditorGUI.EndChangeCheck())
                RefreshImportFiles();

            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Import Folder", _importFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _importFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _importFolder = path;
                    RefreshImportFiles();
                }
            }

            if (GUILayout.Button("↻", GUILayout.Width(25)))
                RefreshImportFiles();

            EditorGUILayout.EndHorizontal();

            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);

            EditorGUILayout.Space(5);

            // Список файлов
            if (_importFiles.Length == 0)
            {
                EditorGUILayout.HelpBox("JSON файлы не найдены в указанной папке.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"JSON файлы ({_importFiles.Length}):", EditorStyles.boldLabel);

                // Кнопки выбора
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
                {
                    for (int i = 0; i < _importFileToggles.Length; i++)
                        _importFileToggles[i] = true;
                }
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
                {
                    for (int i = 0; i < _importFileToggles.Length; i++)
                        _importFileToggles[i] = false;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                for (int i = 0; i < _importFiles.Length; i++)
                {
                    _importFileToggles[i] = EditorGUILayout.ToggleLeft(
                        Path.GetFileName(_importFiles[i]), _importFileToggles[i]);
                }
                EditorGUI.indentLevel--;
            }

            bool anySelected = _importFileToggles.Any(t => t);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = anySelected;
            if (GUILayout.Button("✅ Validate First", GUILayout.Height(30)))
            {
                DoValidateSelected();
            }
            if (GUILayout.Button("📥 Import", GUILayout.Height(30)))
            {
                DoImport();
            }
            GUI.enabled = true;
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
            var exported = new List<string>();

            for (int i = 0; i < _languageCodes.Length; i++)
            {
                if (i == _sourceLanguageIdx || !_targetLanguageToggles[i]) continue;

                string target = _languageCodes[i];
                string fileName = $"{_exportTable}_{source}_to_{target}.json";
                string fullPath = Path.Combine(_exportPath, fileName);

                var result = LocalizationExporter.Export(
                    _exportTable, source, target, _metadata, _config, fullPath, _onlyMissing);

                if (result != null)
                    exported.Add(Path.GetFileName(result));
            }

            if (exported.Count > 0)
            {
                EditorUtility.DisplayDialog("Export Complete",
                    $"Экспортировано файлов: {exported.Count}\n\n" +
                    string.Join("\n", exported) +
                    "\n\nОткройте файлы и передайте AI для перевода.", "OK");

                EditorUtility.RevealInFinder(_exportPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Export", "Нет записей для экспорта.", "OK");
            }
        }

        private void DoImport()
        {
            var selectedFiles = GetSelectedImportFiles();
            if (selectedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Не выбраны файлы для импорта.", "OK");
                return;
            }

            int totalImported = 0, totalSkipped = 0, totalErrors = 0;
            var allMessages = new List<string>();

            foreach (var file in selectedFiles)
            {
                var result = LocalizationImporter.Import(file, _overwriteExisting);
                totalImported += result.imported;
                totalSkipped += result.skipped;
                totalErrors += result.errors;

                if (result.errorMessages.Count > 0)
                    allMessages.Add($"[{Path.GetFileName(file)}]\n  " +
                        string.Join("\n  ", result.errorMessages));
            }

            string msg = $"Files: {selectedFiles.Count}\nImported: {totalImported}\n" +
                         $"Skipped: {totalSkipped}\nErrors: {totalErrors}";
            if (allMessages.Count > 0)
                msg += "\n\n" + string.Join("\n", allMessages);

            EditorUtility.DisplayDialog("Import Result", msg, "OK");
        }

        private void DoValidateSelected()
        {
            var selectedFiles = GetSelectedImportFiles();
            _validationResults = new List<ValidationResult>();

            foreach (var file in selectedFiles)
            {
                var results = LocalizationValidator.Validate(file, _metadata);
                foreach (var r in results)
                    r.message = $"[{Path.GetFileName(file)}] {r.message}";
                _validationResults.AddRange(results);
            }

            _currentTab = Tab.Validate;
        }

        private List<string> GetSelectedImportFiles()
        {
            var files = new List<string>();
            for (int i = 0; i < _importFiles.Length; i++)
            {
                if (_importFileToggles[i])
                    files.Add(_importFiles[i]);
            }
            return files;
        }

        private void RefreshImportFiles()
        {
            if (!string.IsNullOrEmpty(_importFolder) && Directory.Exists(_importFolder))
            {
                _importFiles = Directory.GetFiles(_importFolder, "*.json")
                    .OrderBy(f => Path.GetFileName(f))
                    .ToArray();
                _importFileToggles = new bool[_importFiles.Length];
                // По умолчанию все выделены
                for (int i = 0; i < _importFileToggles.Length; i++)
                    _importFileToggles[i] = true;
            }
            else
            {
                _importFiles = System.Array.Empty<string>();
                _importFileToggles = System.Array.Empty<bool>();
            }
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
