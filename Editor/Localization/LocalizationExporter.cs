// Packages/com.protosystem.core/Editor/Localization/LocalizationExporter.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

#if PROTO_HAS_LOCALIZATION
using UnityEditor.Localization;
using UnityEngine.Localization;
// LocalizationSettings не используется — поиск локалей через EditorLocaleHelper
using UnityEngine.Localization.Tables;
#endif

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Экспорт строк локализации в JSON для AI-перевода.
    /// Объединяет данные из StringTable + StringMetadataDatabase.
    /// </summary>
    public static class LocalizationExporter
    {
        /// <summary>
        /// Экспортировать таблицу в JSON файл.
        /// </summary>
        public static string Export(
            string tableName,
            string sourceLanguage,
            string targetLanguage,
            StringMetadataDatabase metadata,
            LocalizationConfig config,
            string outputPath,
            bool onlyMissing = false,
            List<string> filterTags = null)
        {
            #if !PROTO_HAS_LOCALIZATION
            Debug.LogError("[LocalizationExporter] Unity Localization not installed");
            return null;
            #else

            var sourceLocale = EditorLocaleHelper.FindLocale(sourceLanguage);
            var targetLocale = EditorLocaleHelper.FindLocale(targetLanguage);
            
            if (sourceLocale == null)
            {
                Debug.LogError($"[LocalizationExporter] Source locale not found: {sourceLanguage}");
                return null;
            }
            
            // Загрузить таблицу для исходного языка
            var sourceTable = GetStringTable(tableName, sourceLocale);
            if (sourceTable == null)
            {
                Debug.LogError($"[LocalizationExporter] Source table not found: {tableName}");
                return null;
            }
            
            // Загрузить таблицу для целевого языка (для проверки уже переведённых)
            StringTable targetTable = null;
            if (targetLocale != null && onlyMissing)
                targetTable = GetStringTable(tableName, targetLocale);
            
            var exportData = new LocalizationExportData
            {
                sourceLanguage = sourceLanguage,
                targetLanguage = targetLanguage,
                table = tableName,
                exported = System.DateTime.UtcNow.ToString("O"),
                projectName = Application.productName,
                instructions = BuildInstructions(sourceLanguage, targetLanguage, tableName)
            };
            
            foreach (var entry in sourceTable.Values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                
                string sourceText = entry.LocalizedValue;
                if (string.IsNullOrEmpty(sourceText)) continue;
                
                // Фильтр: только отсутствующие переводы
                if (onlyMissing && targetTable != null)
                {
                    var targetEntry = targetTable.GetEntry(entry.Key);
                    if (targetEntry != null && !string.IsNullOrEmpty(targetEntry.LocalizedValue))
                        continue;
                }
                
                // Метаданные
                var meta = metadata?.Find(tableName, entry.Key);
                
                // Фильтр по тегам
                if (filterTags != null && filterTags.Count > 0 && meta != null)
                {
                    if (meta.tags == null || !meta.tags.Any(t => filterTags.Contains(t)))
                        continue;
                }
                
                var exportEntry = new ExportEntry
                {
                    key = entry.Key,
                    source = sourceText,
                    context = config.includeContext ? meta?.context : null,
                    maxLength = config.includeMaxLength ? (meta?.maxLength ?? 0) : 0,
                    tags = meta?.tags,
                    pluralForm = meta?.pluralForm
                };
                
                exportData.entries.Add(exportEntry);
            }
            
            if (exportData.entries.Count == 0)
            {
                Debug.LogWarning("[LocalizationExporter] No entries to export");
                return null;
            }
            
            // Сохранить JSON
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            string json = JsonUtility.ToJson(exportData, true);
            File.WriteAllText(outputPath, json, System.Text.Encoding.UTF8);
            
            AssetDatabase.Refresh();
            Debug.Log($"[LocalizationExporter] Exported {exportData.entries.Count} entries → {outputPath}");
            
            return outputPath;
            #endif
        }
        
        private static string BuildInstructions(string source, string target, string table)
        {
            return $"Translate from {source} to {target}. " +
                   $"Table: {table}. " +
                   "Preserve {{variables}} in curly braces exactly as-is. " +
                   "Respect maxLength if > 0. " +
                   "Fill 'translation' field for each entry. " +
                   "Keep the same key. " +
                   "Context field helps understand meaning — do not translate it.";
        }
        
        #if PROTO_HAS_LOCALIZATION
        private static StringTable GetStringTable(string tableName, Locale locale)
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            foreach (var collection in collections)
            {
                if (collection.TableCollectionName == tableName)
                    return collection.GetTable(locale.Identifier) as StringTable;
            }
            return null;
        }
        #endif
    }
    
    /// <summary>
    /// Импорт переводов из JSON в StringTable.
    /// </summary>
    public static class LocalizationImporter
    {
        /// <summary>
        /// Результат импорта.
        /// </summary>
        public struct ImportResult
        {
            public int imported;
            public int skipped;
            public int errors;
            public List<string> errorMessages;
        }
        
        /// <summary>
        /// Импортировать переводы из JSON файла.
        /// </summary>
        public static ImportResult Import(string jsonPath, bool overwriteExisting = false)
        {
            var result = new ImportResult { errorMessages = new List<string>() };
            
            #if !PROTO_HAS_LOCALIZATION
            result.errorMessages.Add("Unity Localization not installed");
            result.errors = 1;
            return result;
            #else
            
            if (!File.Exists(jsonPath))
            {
                result.errorMessages.Add($"File not found: {jsonPath}");
                result.errors = 1;
                return result;
            }
            
            string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            
            // Поддерживаем оба формата
            LocalizationExportData exportData = null;
            try
            {
                exportData = JsonUtility.FromJson<LocalizationExportData>(json);
            }
            catch
            {
                result.errorMessages.Add("Failed to parse JSON");
                result.errors = 1;
                return result;
            }
            
            if (exportData == null || exportData.entries == null || exportData.entries.Count == 0)
            {
                result.errorMessages.Add("No entries in JSON");
                result.errors = 1;
                return result;
            }
            
            var targetLocale = EditorLocaleHelper.FindLocale(exportData.targetLanguage);
            if (targetLocale == null)
            {
                var available = EditorLocaleHelper.GetAvailableCodes();
                result.errorMessages.Add(
                    $"Target locale not found: '{exportData.targetLanguage}'. " +
                    $"Available: [{string.Join(", ", available)}]. " +
                    "Create locale first via Setup Wizard or rebuild Addressables.");
                result.errors = 1;
                return result;
            }
            
            var table = GetOrCreateStringTable(exportData.table, targetLocale);
            if (table == null)
            {
                result.errorMessages.Add($"Could not get/create table: {exportData.table}");
                result.errors = 1;
                return result;
            }
            
            int skippedEmpty = 0;
            int skippedExisting = 0;

            foreach (var entry in exportData.entries)
            {
                if (string.IsNullOrEmpty(entry.key) || string.IsNullOrEmpty(entry.translation))
                {
                    result.skipped++;
                    skippedEmpty++;
                    continue;
                }

                var existing = table.GetEntry(entry.key);
                if (existing != null && !string.IsNullOrEmpty(existing.LocalizedValue) && !overwriteExisting)
                {
                    result.skipped++;
                    skippedExisting++;
                    continue;
                }

                table.AddEntry(entry.key, entry.translation);
                result.imported++;
            }

            if (skippedEmpty > 0)
                result.errorMessages.Add($"Empty translation: {skippedEmpty} entries");
            if (skippedExisting > 0)
                result.errorMessages.Add($"Already translated (use Overwrite): {skippedExisting} entries");

            EditorUtility.SetDirty(table);

            // Сохранить SharedTableData тоже
            if (table.SharedData != null)
                EditorUtility.SetDirty(table.SharedData);

            AssetDatabase.SaveAssets();

            Debug.Log($"[LocalizationImporter] Target: {exportData.targetLanguage}, " +
                      $"imported: {result.imported}, skipped: {result.skipped} " +
                      $"(empty: {skippedEmpty}, existing: {skippedExisting})");
            
            return result;
            #endif
        }
        
        #if PROTO_HAS_LOCALIZATION
        private static StringTable GetOrCreateStringTable(string tableName, Locale locale)
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            foreach (var collection in collections)
            {
                if (collection.TableCollectionName == tableName)
                {
                    var table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table == null)
                    {
                        // Добавить locale в коллекцию
                        collection.AddNewTable(locale.Identifier);
                        table = collection.GetTable(locale.Identifier) as StringTable;
                    }
                    return table;
                }
            }
            
            Debug.LogError($"Table collection '{tableName}' not found. Create it first.");
            return null;
        }
        #endif
    }
    
    /// <summary>
    /// Валидация переводов.
    /// </summary>
    public static class LocalizationValidator
    {
        private static readonly Regex VariablePattern = new(@"\{(\w+)\}", RegexOptions.Compiled);
        
        /// <summary>
        /// Валидировать переводы из файла.
        /// </summary>
        public static List<ValidationResult> Validate(string jsonPath, StringMetadataDatabase metadata = null)
        {
            var results = new List<ValidationResult>();
            
            if (!File.Exists(jsonPath))
            {
                results.Add(new ValidationResult
                {
                    key = "*",
                    type = ValidationResult.ValidationType.Error,
                    message = $"File not found: {jsonPath}"
                });
                return results;
            }
            
            string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            var data = JsonUtility.FromJson<LocalizationExportData>(json);
            
            if (data?.entries == null)
            {
                results.Add(new ValidationResult
                {
                    key = "*",
                    type = ValidationResult.ValidationType.Error,
                    message = "Invalid JSON or no entries"
                });
                return results;
            }
            
            // Проверка: все переводы пусты → вероятно файл экспорта, не импорта
            int totalEmpty = data.entries.Count(e => string.IsNullOrEmpty(e.translation));
            if (totalEmpty == data.entries.Count)
            {
                results.Add(new ValidationResult
                {
                    key = "*",
                    type = ValidationResult.ValidationType.Error,
                    message = $"All {totalEmpty} translations are empty — this looks like an EXPORT file, not a translated file. " +
                              "Select the file with translations (from Import folder)."
                });
                return results;
            }

            foreach (var entry in data.entries)
            {
                if (string.IsNullOrEmpty(entry.translation))
                {
                    results.Add(new ValidationResult
                    {
                        key = entry.key,
                        type = ValidationResult.ValidationType.Warning,
                        message = "Missing translation"
                    });
                    continue;
                }
                
                // Проверка переменных
                var sourceVars = ExtractVariables(entry.source);
                var transVars = ExtractVariables(entry.translation);
                
                foreach (var v in sourceVars)
                {
                    if (!transVars.Contains(v))
                    {
                        results.Add(new ValidationResult
                        {
                            key = entry.key,
                            type = ValidationResult.ValidationType.Error,
                            message = $"Missing variable {{{v}}} in translation"
                        });
                    }
                }
                
                foreach (var v in transVars)
                {
                    if (!sourceVars.Contains(v))
                    {
                        results.Add(new ValidationResult
                        {
                            key = entry.key,
                            type = ValidationResult.ValidationType.Warning,
                            message = $"Extra variable {{{v}}} in translation (not in source)"
                        });
                    }
                }
                
                // Проверка длины
                int maxLen = entry.maxLength;
                if (maxLen <= 0 && metadata != null)
                    maxLen = metadata.Find(entry.key)?.maxLength ?? 0;
                
                if (maxLen > 0 && entry.translation.Length > maxLen)
                {
                    results.Add(new ValidationResult
                    {
                        key = entry.key,
                        type = ValidationResult.ValidationType.Warning,
                        message = $"Translation too long: {entry.translation.Length}/{maxLen}"
                    });
                }
                
                // Проверка пустой строки или только пробелы
                if (string.IsNullOrWhiteSpace(entry.translation))
                {
                    results.Add(new ValidationResult
                    {
                        key = entry.key,
                        type = ValidationResult.ValidationType.Error,
                        message = "Translation is whitespace-only"
                    });
                }
            }
            
            return results;
        }
        
        private static HashSet<string> ExtractVariables(string text)
        {
            var vars = new HashSet<string>();
            if (string.IsNullOrEmpty(text)) return vars;

            foreach (Match match in VariablePattern.Matches(text))
                vars.Add(match.Groups[1].Value);

            return vars;
        }
    }

    /// <summary>
    /// Поиск Locale через Editor API (не зависит от Addressables-билда).
    /// </summary>
    public static class EditorLocaleHelper
    {
        #if PROTO_HAS_LOCALIZATION
        /// <summary>
        /// Найти Locale по коду через LocalizationEditorSettings.
        /// Поддерживает точное совпадение и fallback по базовому коду (pt-BR → pt).
        /// </summary>
        public static Locale FindLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null) return null;

            // Точное совпадение
            foreach (var locale in locales)
                if (string.Equals(locale.Identifier.Code, code, System.StringComparison.OrdinalIgnoreCase))
                    return locale;

            // Fallback: код содержит подтег (pt-BR → ищем pt)
            int dashIdx = code.IndexOf('-');
            if (dashIdx > 0)
            {
                string baseCode = code.Substring(0, dashIdx);
                foreach (var locale in locales)
                    if (string.Equals(locale.Identifier.Code, baseCode, System.StringComparison.OrdinalIgnoreCase))
                        return locale;
            }

            return null;
        }

        /// <summary>
        /// Список доступных кодов локалей (для диагностики).
        /// </summary>
        public static List<string> GetAvailableCodes()
        {
            var codes = new List<string>();
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales != null)
                foreach (var locale in locales)
                    codes.Add(locale.Identifier.Code);
            return codes;
        }
        #else
        public static object FindLocale(string code) => null;
        public static List<string> GetAvailableCodes() => new List<string>();
        #endif
    }
}
