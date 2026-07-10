// Packages/com.protosystem.core/Editor/Localization/ClaudeTranslationRunner.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Полный цикл AI-перевода одной кнопкой:
    /// Export → headless-запуск Claude Code (CLI) → Import + валидация.
    ///
    /// Проектно-независим: пути и языки приходят из вызывающего кода,
    /// промпт встроенный, но если в проекте есть скилл .claude/skills/localize/SKILL.md —
    /// используется команда /localize (проект может переопределить контракт перевода).
    /// </summary>
    public static class ClaudeTranslationRunner
    {
        public class RunOptions
        {
            public string[] tables;
            public string sourceLanguage;
            public string[] targetLanguages;
            public bool onlyMissing = true;
            public bool overwriteExisting;
            public string exportFolder;   // относительно корня проекта или абсолютный
            public string importFolder;
            public StringMetadataDatabase metadata;
            public LocalizationConfig config;
            /// <summary>CLI-команда, по умолчанию "claude".</summary>
            public string cliCommand = "claude";
        }

        public class ImportSummary
        {
            public int files;
            public int imported;
            public int skipped;
            public int errors;
            public int validationErrors;
            public int validationWarnings;
        }

        public static bool IsRunning { get; private set; }
        public static string Status { get; private set; } = "";
        public static ImportSummary LastSummary { get; private set; }

        /// <summary>Путь проектного скилла с контрактом перевода.</summary>
        public const string SkillPath = ".claude/skills/localize/SKILL.md";
        public static bool SkillExists =>
            File.Exists(SkillPath) || File.Exists(".claude/commands/localize.md");

        private static readonly ConcurrentQueue<string> _logQueue = new();
        private static readonly List<string> _log = new();
        private static System.Diagnostics.Process _process;
        private static List<string> _exportedFiles;
        private static RunOptions _options;
        private static bool _processExited;
        private static Action<int> _onExit;

        /// <summary>Последние строки лога для отображения в окне.</summary>
        public static IReadOnlyList<string> Log => _log;

        public static void Run(RunOptions options)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[ClaudeTranslation] Уже запущен.");
                return;
            }

            _options = options;
            _log.Clear();
            LastSummary = null;

            // ── 1. Export ──
            Status = "Экспорт таблиц...";
            _exportedFiles = new List<string>();

            foreach (var table in options.tables)
            {
                foreach (var lang in options.targetLanguages)
                {
                    if (lang == options.sourceLanguage) continue;

                    string fileName = $"{table}_{options.sourceLanguage}_to_{lang}.json";
                    string fullPath = Path.Combine(options.exportFolder, fileName);

                    var result = LocalizationExporter.Export(
                        table, options.sourceLanguage, lang,
                        options.metadata, options.config, fullPath, options.onlyMissing);

                    if (result != null)
                        _exportedFiles.Add(fileName);
                }
            }

            if (_exportedFiles.Count == 0)
            {
                Status = "Нечего переводить — все строки уже переведены.";
                Debug.Log("[ClaudeTranslation] Нет записей для экспорта.");
                return;
            }

            AppendLog($"Экспортировано файлов: {_exportedFiles.Count}");

            // ── 2. Claude CLI ──
            if (!Directory.Exists(options.importFolder))
                Directory.CreateDirectory(options.importFolder);

            string prompt = BuildPrompt(options, _exportedFiles);
            LaunchClaude(options.cliCommand, prompt, "Claude переводит...", exitCode =>
            {
                if (exitCode != 0)
                {
                    Status = $"Claude завершился с кодом {exitCode} — импорт пропущен.";
                    Debug.LogError($"[ClaudeTranslation] {Status}\n{string.Join("\n", _log)}");
                    return;
                }
                AppendLog("Перевод завершён, импортирую...");
                DoImport();
            });
        }

        /// <summary>
        /// Сгенерировать проектный скилл /localize: headless-Claude изучает проект
        /// (доки, исходные строки таблиц) и пишет SKILL.md с тоном и глоссарием.
        /// </summary>
        public static void GenerateSkill(RunOptions options)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[ClaudeTranslation] Уже запущен.");
                return;
            }

            _options = options;
            _log.Clear();
            LastSummary = null;

            // Экспортируем таблицы на исходном языке (без фильтра missing) —
            // это материал для глоссария.
            _exportedFiles = new List<string>();
            foreach (var table in options.tables)
            {
                foreach (var lang in options.targetLanguages)
                {
                    if (lang == options.sourceLanguage) continue;
                    string fileName = $"{table}_{options.sourceLanguage}_to_{lang}.json";
                    string fullPath = Path.Combine(options.exportFolder, fileName);
                    if (LocalizationExporter.Export(table, options.sourceLanguage, lang,
                            options.metadata, options.config, fullPath, onlyMissing: false) != null)
                        _exportedFiles.Add(fileName);
                    break; // одного целевого языка достаточно — нужны только source-строки
                }
            }

            string prompt = BuildSkillGenPrompt(options);
            LaunchClaude(options.cliCommand, prompt, "Claude изучает проект и пишет скилл...", exitCode =>
            {
                AssetDatabase.Refresh();
                if (exitCode == 0 && File.Exists(SkillPath))
                {
                    Status = $"Скилл создан: {SkillPath}";
                    Debug.Log($"[ClaudeTranslation] {Status}");
                    EditorUtility.RevealInFinder(SkillPath);
                }
                else
                {
                    Status = $"Генерация скилла не удалась (код {exitCode}).";
                    Debug.LogError($"[ClaudeTranslation] {Status}\n{string.Join("\n", _log)}");
                }
            });
        }

        public static void Cancel()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClaudeTranslation] Cancel: {e.Message}");
            }
        }

        // ──────────────── Prompt ────────────────

        private static string BuildPrompt(RunOptions options, List<string> files)
        {
            string exportAbs = Path.GetFullPath(options.exportFolder).Replace('\\', '/');
            string importAbs = Path.GetFullPath(options.importFolder).Replace('\\', '/');
            string fileList = string.Join(", ", files);

            // Если проект определяет собственный скилл — используем его.
            if (SkillExists)
            {
                return $"/localize export=\"{exportAbs}\" import=\"{importAbs}\" files=\"{fileList}\"";
            }

            // Встроенный контракт перевода (проектно-независимый).
            return
                "You are running headless inside a Unity project to translate localization files.\n" +
                $"Export folder: {exportAbs}\n" +
                $"Import folder: {importAbs}\n" +
                $"Files to process: {fileList}\n" +
                "For each file (named <Table>_<src>_to_<tgt>.json):\n" +
                "1. Read it. Each entry has: key, source, context, maxLength, tags, pluralForm, translation.\n" +
                "2. Fill ONLY the empty \"translation\" fields with a translation of \"source\" " +
                "from the source language to the target language (see file name and header fields).\n" +
                "   - Preserve placeholders in curly braces, e.g. {enemy}, exactly as-is.\n" +
                "   - Preserve TMP rich-text tags (<b>, <color=...>, <size=...>) and \\n line breaks.\n" +
                "   - If maxLength > 0, the translation must not exceed it.\n" +
                "   - Use \"context\" to disambiguate; never translate \"key\" or \"context\".\n" +
                $"   - Match the tone of the game \"{Application.productName}\"; keep UI strings terse.\n" +
                "3. Write the result to the Import folder with the SAME file name, " +
                "preserving the full JSON structure and all fields. UTF-8, valid JSON.\n" +
                "Do not modify files in the Export folder. Do not touch any other files.\n" +
                "When done, print one line per file: <filename> — <N> translated.";
        }

        private static string BuildSkillGenPrompt(RunOptions options)
        {
            string exportAbs = Path.GetFullPath(options.exportFolder).Replace('\\', '/');
            string exportRel = options.exportFolder.Replace('\\', '/');
            string importRel = options.importFolder.Replace('\\', '/');
            string targets = string.Join(", ", options.targetLanguages);

            return
                "You are running headless inside a Unity project. Create a Claude Code skill file at " +
                $"{SkillPath} defining the translation contract for this game's localization pipeline.\n" +
                "Research the project FIRST:\n" +
                $"- Game name: \"{Application.productName}\".\n" +
                "- Read the project README and design docs (root *.md, Assets/**/Docs/*.md) to understand " +
                "the game's setting, genre and tone.\n" +
                $"- Read the localization export files in {exportAbs} — the \"source\" fields contain all " +
                "game strings. From them, extract 8-15 recurring game-specific terms for a consistency glossary.\n" +
                $"Languages: source \"{options.sourceLanguage}\", targets: {targets}.\n" +
                "The skill file structure (write it in Russian — the team works in Russian):\n" +
                "- YAML frontmatter: name: localize; description: one line.\n" +
                $"- Arguments: export=, import=, files= with defaults \"{exportRel}\" and \"{importRel}\".\n" +
                "- Task: for each JSON file named <Table>_<src>_to_<tgt>.json read entries " +
                "(key, source, context, maxLength, tags, pluralForm, translation), fill ONLY empty " +
                "\"translation\" fields, write to the Import folder with the SAME file name preserving " +
                "the full JSON structure, UTF-8, valid JSON.\n" +
                "- Translation rules: preserve {placeholders} exactly; preserve TMP rich-text tags " +
                "(<b>, <color=...>) and \\n; respect maxLength; never translate key/context; use context " +
                "to disambiguate; honor pluralForm.\n" +
                "- Tone section: describe the game's tone based on your research; UI strings must stay terse.\n" +
                "- Glossary section: the terms you extracted, with a note to translate them consistently " +
                "across all files and existing translations.\n" +
                "- Completion: print one line per file \"<filename> - <N> translated\" plus a total.\n" +
                "Create parent folders as needed. Do not modify any other files. " +
                "When done, print: SKILL CREATED.";
        }

        // ──────────────── Process ────────────────

        private static void LaunchClaude(string cliCommand, string prompt,
            string statusWhileRunning, Action<int> onExit)
        {
            Status = statusWhileRunning;
            _onExit = onExit;
            AppendLog("Запуск Claude Code (headless)...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Промпт передаём через stdin — никакого экранирования аргументов.
            string args = "-p --permission-mode acceptEdits";
            #if UNITY_EDITOR_WIN
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/c {cliCommand} {args}";
            #else
            psi.FileName = "/bin/bash";
            psi.Arguments = $"-lc \"{cliCommand} {args}\"";
            #endif

            _process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

            _process.OutputDataReceived += (_, e) => { if (e.Data != null) _logQueue.Enqueue(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _logQueue.Enqueue("[err] " + e.Data); };
            _process.Exited += (_, _) => { _processExited = true; };

            try
            {
                _process.Start();
            }
            catch (Exception e)
            {
                Status = $"Не удалось запустить '{cliCommand}': {e.Message}";
                Debug.LogError($"[ClaudeTranslation] {Status}");
                _process = null;
                return;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            var stdin = _process.StandardInput;
            stdin.Write(prompt);
            stdin.Close();

            IsRunning = true;
            _processExited = false;
            EditorApplication.update += Pump;
        }

        /// <summary>Главный поток: перекачка лога и завершение.</summary>
        private static void Pump()
        {
            while (_logQueue.TryDequeue(out var line))
                AppendLog(line);

            if (!_processExited) return;

            EditorApplication.update -= Pump;
            IsRunning = false;

            int exitCode = -1;
            try { exitCode = _process.ExitCode; } catch { /* killed */ }
            _process.Dispose();
            _process = null;

            var callback = _onExit;
            _onExit = null;
            callback?.Invoke(exitCode);
        }

        // ──────────────── Import + Validate ────────────────

        private static void DoImport()
        {
            AssetDatabase.Refresh();

            var summary = new ImportSummary();

            foreach (var fileName in _exportedFiles)
            {
                string path = Path.Combine(_options.importFolder, fileName);
                if (!File.Exists(path))
                {
                    AppendLog($"⚠ Нет файла перевода: {fileName}");
                    summary.errors++;
                    continue;
                }

                // Валидация до импорта
                var validation = LocalizationValidator.Validate(path, _options.metadata);
                summary.validationErrors += validation.Count(r => r.type == ValidationResult.ValidationType.Error);
                summary.validationWarnings += validation.Count(r => r.type == ValidationResult.ValidationType.Warning);

                foreach (var r in validation.Where(r => r.type != ValidationResult.ValidationType.OK))
                    AppendLog($"  {(r.type == ValidationResult.ValidationType.Error ? "❌" : "⚠")} [{r.key}] {r.message}");

                var result = LocalizationImporter.Import(path, _options.overwriteExisting);
                summary.files++;
                summary.imported += result.imported;
                summary.skipped += result.skipped;
                summary.errors += result.errors;

                AppendLog($"✓ {fileName}: +{result.imported}, skip {result.skipped}");
            }

            LastSummary = summary;
            Status = $"Готово: файлов {summary.files}, импортировано {summary.imported}, " +
                     $"пропущено {summary.skipped}, ошибок {summary.errors} " +
                     $"(валидация: {summary.validationErrors} err / {summary.validationWarnings} warn)";

            Debug.Log($"[ClaudeTranslation] {Status}");
        }

        private static void AppendLog(string line)
        {
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
        }
    }
}
