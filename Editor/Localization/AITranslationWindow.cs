// Packages/com.protosystem.core/Editor/Localization/AITranslationWindow.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

#if PROTO_HAS_LOCALIZATION
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Окно для экспорта/импорта/валидации переводов через AI.
    /// ProtoSystem → Localization → AI Translation
    /// </summary>
    public class AITranslationWindow : EditorWindow
    {
        private enum Tab { Export, Import, Validate, Claude }

        private Tab _currentTab = Tab.Export;
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
        private string[] _importFileInfo = System.Array.Empty<string>();
        private bool _overwriteExisting;

        // Validate
        private string _validatePath;
        private List<ValidationResult> _validationResults;

        // Claude (полный цикл)
        private bool[] _claudeTableToggles;

        // Coverage (статистика переводов)
        private bool _coverageFoldout = true;
        private List<CoverageRow> _coverage;

        private struct CoverageRow
        {
            public string table;
            public string lang;
            public int done;
            public int total;
        }

        // Available
        private string[] _languageCodes;
        private string[] _languageNames;
        private string[] _tableNames;
        private bool _initialized;

        [MenuItem("ProtoSystem/Localization/AI Translation", false, 502)]
        public static void ShowWindow()
        {
            var window = GetWindow<AITranslationWindow>("AI Translation");
            window.minSize = new Vector2(460, 480);
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
            ComputeCoverage();

            _initialized = true;
        }

        private void OnGUI()
        {
            if (!_initialized) Initialize();

            LocalizationEditorStyles.Header("🌐 AI Translation",
                "Экспорт строк для перевода, импорт результата и полный цикл через Claude Code.");

            // Табы
            EditorGUILayout.BeginHorizontal();
            if (LocalizationEditorStyles.Tab("📤 Export", _currentTab == Tab.Export)) _currentTab = Tab.Export;
            if (LocalizationEditorStyles.Tab("📥 Import", _currentTab == Tab.Import)) _currentTab = Tab.Import;
            if (LocalizationEditorStyles.Tab("✅ Validate", _currentTab == Tab.Validate)) _currentTab = Tab.Validate;
            if (LocalizationEditorStyles.Tab("🤖 Claude", _currentTab == Tab.Claude)) _currentTab = Tab.Claude;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Конфиг
            EditorGUILayout.BeginHorizontal();
            _config = (LocalizationConfig)EditorGUILayout.ObjectField("Config", _config,
                typeof(LocalizationConfig), false);
            _metadata = (StringMetadataDatabase)EditorGUILayout.ObjectField(_metadata,
                typeof(StringMetadataDatabase), false, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            #if !PROTO_HAS_LOCALIZATION
            EditorGUILayout.HelpBox("Unity Localization не установлен. Экспорт/импорт недоступны.",
                MessageType.Warning);
            GUI.enabled = false;
            #endif

            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab)
            {
                case Tab.Export: DrawExportTab(); break;
                case Tab.Import: DrawImportTab(); break;
                case Tab.Validate: DrawValidateTab(); break;
                case Tab.Claude: DrawClaudeTab(); break;
            }

            // Статистика покрытия — на вкладках Export и Claude
            if (_currentTab == Tab.Export || _currentTab == Tab.Claude)
                DrawCoverage();

            EditorGUILayout.EndScrollView();

            GUI.enabled = true;
        }

        // ──────────────── Coverage ────────────────

        private void DrawCoverage()
        {
            LocalizationEditorStyles.BeginCard();
            EditorGUILayout.BeginHorizontal();
            _coverageFoldout = EditorGUILayout.Foldout(_coverageFoldout,
                "Покрытие переводов", true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↻", GUILayout.Width(25)))
                ComputeCoverage();
            EditorGUILayout.EndHorizontal();

            if (_coverageFoldout && _coverage != null)
            {
                string currentTable = null;
                foreach (var row in _coverage)
                {
                    if (row.table != currentTable)
                    {
                        currentTable = row.table;
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField(currentTable, EditorStyles.miniBoldLabel);
                    }
                    LocalizationEditorStyles.CoverageBar(row.lang, row.done, row.total);
                }

                if (_coverage.Count == 0)
                    EditorGUILayout.LabelField("Нет данных — создайте String Tables.",
                        EditorStyles.miniLabel);
            }
            LocalizationEditorStyles.EndCard();
        }

        private void ComputeCoverage()
        {
            _coverage = new List<CoverageRow>();
            #if PROTO_HAS_LOCALIZATION
            if (_languageCodes == null) return;

            string sourceCode = _languageCodes[Mathf.Clamp(_sourceLanguageIdx, 0, _languageCodes.Length - 1)];
            var sourceLocale = EditorLocaleHelper.FindLocale(sourceCode);
            if (sourceLocale == null) return;

            var collections = LocalizationEditorSettings.GetStringTableCollections();
            foreach (var collection in collections)
            {
                if (collection.GetTable(sourceLocale.Identifier) is not StringTable sourceTable) continue;

                // Ключи с непустым источником
                var sourceKeys = sourceTable.Values
                    .Where(e => e != null && !string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.LocalizedValue))
                    .Select(e => e.Key)
                    .ToList();

                foreach (var code in _languageCodes)
                {
                    if (code == sourceCode) continue;
                    var locale = EditorLocaleHelper.FindLocale(code);
                    var targetTable = locale != null
                        ? collection.GetTable(locale.Identifier) as StringTable : null;

                    int done = 0;
                    if (targetTable != null)
                    {
                        foreach (var key in sourceKeys)
                        {
                            var entry = targetTable.GetEntry(key);
                            if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                                done++;
                        }
                    }

                    _coverage.Add(new CoverageRow
                    {
                        table = collection.TableCollectionName,
                        lang = code,
                        done = done,
                        total = sourceKeys.Count,
                    });
                }
            }
            #endif
        }

        // ──────────────── Export Tab ────────────────

        private void DrawExportTab()
        {
            LocalizationEditorStyles.BeginCard("Что экспортируем");

            // Таблица
            int tableIdx = System.Array.IndexOf(_tableNames, _exportTable);
            if (tableIdx < 0) tableIdx = 0;
            tableIdx = EditorGUILayout.Popup("Table", tableIdx, _tableNames);
            _exportTable = _tableNames[tableIdx];

            // Язык-источник
            _sourceLanguageIdx = EditorGUILayout.Popup("Source Language",
                _sourceLanguageIdx, _languageNames);

            EditorGUILayout.Space(3);
            DrawTargetLanguages();

            LocalizationEditorStyles.EndCard();

            LocalizationEditorStyles.BeginCard("Параметры");
            _onlyMissing = EditorGUILayout.Toggle(
                new GUIContent("Only Missing", "Экспортировать только строки без перевода"), _onlyMissing);
            DrawFolderField("Export Folder", ref _exportPath);
            LocalizationEditorStyles.EndCard();

            bool anyTarget = AnyTargetSelected();
            if (!anyTarget)
                EditorGUILayout.HelpBox("Выберите хотя бы один целевой язык.", MessageType.Warning);

            EditorGUILayout.Space(4);
            if (LocalizationEditorStyles.AccentButton("📤 Export to JSON",
                    LocalizationEditorStyles.Accent, anyTarget))
                DoExport();
        }

        private void DrawTargetLanguages()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Languages", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Все", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = i != _sourceLanguageIdx;
            if (GUILayout.Button("Ничего", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = false;
            EditorGUILayout.EndHorizontal();

            if (_targetLanguageToggles == null || _targetLanguageToggles.Length != _languageCodes.Length)
            {
                _targetLanguageToggles = new bool[_languageCodes.Length];
                for (int i = 0; i < _targetLanguageToggles.Length; i++)
                    _targetLanguageToggles[i] = i != _sourceLanguageIdx;
            }

            // Сетка в две колонки
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _languageCodes.Length; i++)
            {
                if (i == _sourceLanguageIdx) continue;
                _targetLanguageToggles[i] = EditorGUILayout.ToggleLeft(
                    _languageNames[i], _targetLanguageToggles[i], GUILayout.Width(180));
                if (++col % 2 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool AnyTargetSelected()
        {
            for (int i = 0; i < _targetLanguageToggles.Length; i++)
                if (i != _sourceLanguageIdx && _targetLanguageToggles[i]) return true;
            return false;
        }

        private void DrawFolderField(string label, ref string path)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var picked = EditorUtility.OpenFolderPanel(label, path, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    path = picked.StartsWith(Application.dataPath)
                        ? "Assets" + picked.Substring(Application.dataPath.Length)
                        : picked;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Import Tab ────────────────

        private void DrawImportTab()
        {
            LocalizationEditorStyles.BeginCard("Файлы переводов");

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
                    _importFolder = path.StartsWith(Application.dataPath)
                        ? "Assets" + path.Substring(Application.dataPath.Length)
                        : path;
                    RefreshImportFiles();
                }
            }

            if (GUILayout.Button("↻", GUILayout.Width(25)))
                RefreshImportFiles();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Список файлов
            if (_importFiles.Length == 0)
            {
                EditorGUILayout.HelpBox("JSON файлы не найдены в указанной папке.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Найдено файлов: {_importFiles.Length}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Все", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                    for (int i = 0; i < _importFileToggles.Length; i++) _importFileToggles[i] = true;
                if (GUILayout.Button("Ничего", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                    for (int i = 0; i < _importFileToggles.Length; i++) _importFileToggles[i] = false;
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < _importFiles.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    _importFileToggles[i] = EditorGUILayout.ToggleLeft(
                        Path.GetFileName(_importFiles[i]), _importFileToggles[i]);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(_importFileInfo.Length > i ? _importFileInfo[i] : "",
                        EditorStyles.miniLabel, GUILayout.Width(110));
                    EditorGUILayout.EndHorizontal();
                }
            }

            LocalizationEditorStyles.EndCard();

            LocalizationEditorStyles.BeginCard("Параметры");
            _overwriteExisting = EditorGUILayout.Toggle(
                new GUIContent("Overwrite Existing", "Перезаписывать уже существующие переводы"),
                _overwriteExisting);
            LocalizationEditorStyles.EndCard();

            bool anySelected = _importFileToggles.Any(t => t);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (LocalizationEditorStyles.AccentButton("✅ Validate First",
                    LocalizationEditorStyles.Warn, anySelected))
                DoValidateSelected();
            if (LocalizationEditorStyles.AccentButton("📥 Import",
                    LocalizationEditorStyles.Accent, anySelected))
                DoImport();
            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Validate Tab ────────────────

        private void DrawValidateTab()
        {
            LocalizationEditorStyles.BeginCard("Проверка файла переводов");

            EditorGUILayout.BeginHorizontal();
            _validatePath = EditorGUILayout.TextField("JSON File", _validatePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select JSON", _validatePath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    _validatePath = path.StartsWith(Application.dataPath)
                        ? "Assets" + path.Substring(Application.dataPath.Length)
                        : path;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (LocalizationEditorStyles.AccentButton("Validate", LocalizationEditorStyles.Accent))
                _validationResults = LocalizationValidator.Validate(_validatePath, _metadata);

            LocalizationEditorStyles.EndCard();

            if (_validationResults == null) return;

            int errors = _validationResults.Count(r => r.type == ValidationResult.ValidationType.Error);
            int warnings = _validationResults.Count(r => r.type == ValidationResult.ValidationType.Warning);

            LocalizationEditorStyles.BeginCard("Результаты");

            EditorGUILayout.BeginHorizontal();
            if (_validationResults.Count == 0)
            {
                LocalizationEditorStyles.DrawBadge("✓ OK", LocalizationEditorStyles.Ok, 60);
                EditorGUILayout.LabelField("Все проверки пройдены", EditorStyles.miniLabel);
            }
            else
            {
                if (errors > 0)
                    LocalizationEditorStyles.DrawBadge($"{errors} err", LocalizationEditorStyles.Error);
                if (warnings > 0)
                    LocalizationEditorStyles.DrawBadge($"{warnings} warn", LocalizationEditorStyles.Warn);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            foreach (var r in _validationResults)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var color = r.type == ValidationResult.ValidationType.Error
                    ? LocalizationEditorStyles.Error : LocalizationEditorStyles.Warn;
                LocalizationEditorStyles.DrawBadge(
                    r.type == ValidationResult.ValidationType.Error ? "err" : "warn", color, 40);
                EditorGUILayout.LabelField(r.key, EditorStyles.miniBoldLabel, GUILayout.Width(180));
                EditorGUILayout.LabelField(r.message, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            LocalizationEditorStyles.EndCard();
        }

        // ──────────────── Claude Tab ────────────────

        private void DrawClaudeTab()
        {
            LocalizationEditorStyles.BeginCard("Полный цикл: Export → Claude → Import");
            EditorGUILayout.LabelField(
                "Экспортирует недостающие переводы, запускает Claude Code (headless)\n" +
                "и импортирует результат в String Tables с валидацией.",
                EditorStyles.miniLabel);
            LocalizationEditorStyles.EndCard();

            // Таблицы + языки
            LocalizationEditorStyles.BeginCard("Объём перевода");

            EditorGUILayout.LabelField("Tables", EditorStyles.boldLabel);
            if (_claudeTableToggles == null || _claudeTableToggles.Length != _tableNames.Length)
            {
                _claudeTableToggles = new bool[_tableNames.Length];
                for (int i = 0; i < _claudeTableToggles.Length; i++)
                    _claudeTableToggles[i] = true;
            }
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _tableNames.Length; i++)
                _claudeTableToggles[i] = EditorGUILayout.ToggleLeft(
                    _tableNames[i], _claudeTableToggles[i], GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            _sourceLanguageIdx = EditorGUILayout.Popup("Source Language",
                _sourceLanguageIdx, _languageNames);
            DrawTargetLanguages();

            LocalizationEditorStyles.EndCard();

            // Параметры
            LocalizationEditorStyles.BeginCard("Параметры");
            _onlyMissing = EditorGUILayout.Toggle(
                new GUIContent("Only Missing", "Переводить только строки без перевода"), _onlyMissing);
            _overwriteExisting = EditorGUILayout.Toggle(
                new GUIContent("Overwrite Existing", "Перезаписывать существующие переводы при импорте"),
                _overwriteExisting);
            DrawFolderField("Export Folder", ref _exportPath);
            LocalizationEditorStyles.EndCard();

            // Проектный скилл
            DrawSkillCard();

            bool anyTable = _claudeTableToggles.Any(t => t);
            bool anyLang = AnyTargetSelected();

            EditorGUILayout.Space(4);

            if (ClaudeTranslationRunner.IsRunning)
            {
                EditorGUILayout.HelpBox($"⏳ {ClaudeTranslationRunner.Status}", MessageType.Info);
                if (LocalizationEditorStyles.AccentButton("✖ Cancel", LocalizationEditorStyles.Error))
                    ClaudeTranslationRunner.Cancel();
            }
            else
            {
                if (LocalizationEditorStyles.AccentButton("🤖 Translate via Claude",
                        LocalizationEditorStyles.Claude, anyTable && anyLang))
                    RunClaudeCycle();

                DrawClaudeSummary();
            }

            // Лог
            var log = ClaudeTranslationRunner.Log;
            if (log.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Log", EditorStyles.miniBoldLabel);
                var tail = log.Skip(Mathf.Max(0, log.Count - 15));
                EditorGUILayout.LabelField(string.Join("\n", tail), LocalizationEditorStyles.LogBox);
            }
        }

        private void DrawSkillCard()
        {
            LocalizationEditorStyles.BeginCard("Скилл проекта");

            bool skillExists = ClaudeTranslationRunner.SkillExists;

            EditorGUILayout.BeginHorizontal();
            LocalizationEditorStyles.DrawBadge(skillExists ? "✓" : "—",
                skillExists ? LocalizationEditorStyles.Ok : LocalizationEditorStyles.Warn, 24);
            EditorGUILayout.LabelField(skillExists
                    ? ClaudeTranslationRunner.SkillPath
                    : "Скилл /localize не найден — будет использован встроенный промпт.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (!ClaudeTranslationRunner.IsRunning)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(skillExists
                        ? "📝 Перегенерировать (Claude)"
                        : "📝 Создать скилл на основе проекта (Claude)"))
                {
                    if (!skillExists || EditorUtility.DisplayDialog("Перегенерировать скилл?",
                            $"Существующий {ClaudeTranslationRunner.SkillPath} будет переписан " +
                            "(тон и глоссарий соберутся заново из доков и строк проекта).", "Да", "Отмена"))
                    {
                        ClaudeTranslationRunner.GenerateSkill(BuildRunOptions());
                    }
                }
                if (skillExists && GUILayout.Button("Открыть", GUILayout.Width(70)))
                    EditorUtility.OpenWithDefaultApp(ClaudeTranslationRunner.SkillPath);
                EditorGUILayout.EndHorizontal();
            }

            LocalizationEditorStyles.EndCard();
        }

        private void DrawClaudeSummary()
        {
            if (string.IsNullOrEmpty(ClaudeTranslationRunner.Status)) return;

            var summary = ClaudeTranslationRunner.LastSummary;

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            if (summary != null)
            {
                LocalizationEditorStyles.DrawBadge($"+{summary.imported}", LocalizationEditorStyles.Ok);
                if (summary.skipped > 0)
                    LocalizationEditorStyles.DrawBadge($"skip {summary.skipped}", LocalizationEditorStyles.AccentDim);
                if (summary.errors > 0 || summary.validationErrors > 0)
                    LocalizationEditorStyles.DrawBadge(
                        $"err {summary.errors + summary.validationErrors}", LocalizationEditorStyles.Error);
                if (summary.validationWarnings > 0)
                    LocalizationEditorStyles.DrawBadge(
                        $"warn {summary.validationWarnings}", LocalizationEditorStyles.Warn);
                GUILayout.Space(6);
            }
            EditorGUILayout.LabelField(ClaudeTranslationRunner.Status, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void RunClaudeCycle()
        {
            ClaudeTranslationRunner.Run(BuildRunOptions());
        }

        private ClaudeTranslationRunner.RunOptions BuildRunOptions()
        {
            var tables = new List<string>();
            for (int i = 0; i < _tableNames.Length; i++)
                if (_claudeTableToggles != null && i < _claudeTableToggles.Length && _claudeTableToggles[i])
                    tables.Add(_tableNames[i]);

            var targets = new List<string>();
            for (int i = 0; i < _languageCodes.Length; i++)
                if (i != _sourceLanguageIdx && _targetLanguageToggles[i])
                    targets.Add(_languageCodes[i]);

            return new ClaudeTranslationRunner.RunOptions
            {
                tables = tables.ToArray(),
                sourceLanguage = _languageCodes[_sourceLanguageIdx],
                targetLanguages = targets.ToArray(),
                onlyMissing = _onlyMissing,
                overwriteExisting = _overwriteExisting,
                exportFolder = _exportPath,
                importFolder = _exportPath.Replace("/Export", "/Import"),
                metadata = _metadata,
                config = _config,
            };
        }

        private bool _wasRunning;

        private void OnInspectorUpdate()
        {
            // Обновление лога/статуса во время работы Claude
            if (ClaudeTranslationRunner.IsRunning)
            {
                _wasRunning = true;
                Repaint();
            }
            else if (_wasRunning)
            {
                // Цикл завершился — обновить статистику покрытия
                _wasRunning = false;
                ComputeCoverage();
                RefreshImportFiles();
                Repaint();
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
            ComputeCoverage();
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
                _importFileInfo = new string[_importFiles.Length];

                for (int i = 0; i < _importFiles.Length; i++)
                {
                    _importFileToggles[i] = true;
                    _importFileInfo[i] = DescribeImportFile(_importFiles[i]);
                }
            }
            else
            {
                _importFiles = System.Array.Empty<string>();
                _importFileToggles = System.Array.Empty<bool>();
                _importFileInfo = System.Array.Empty<string>();
            }
        }

        /// <summary>Краткая сводка по файлу: сколько переводов заполнено.</summary>
        private static string DescribeImportFile(string path)
        {
            try
            {
                var data = JsonUtility.FromJson<LocalizationExportData>(File.ReadAllText(path));
                if (data?.entries == null || data.entries.Count == 0) return "пусто";
                int filled = data.entries.Count(e => !string.IsNullOrEmpty(e.translation));
                return $"{filled}/{data.entries.Count} переведено";
            }
            catch
            {
                return "не читается";
            }
        }

        // ──────────────── Helpers ────────────────

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
