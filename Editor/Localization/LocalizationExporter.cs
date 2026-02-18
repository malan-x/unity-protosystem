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
using UnityEngine.Localization.Settings;
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
            
            var sourceLocale = FindLocale(sourceLanguage);
            var targetLocale = FindLocale(targetLanguage);
            
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
        private static Locale FindLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                if (locale.Identifier.Code == code)
                    return locale;
            return null;
        }
        
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
            
            var targetLocale = FindLocale(exportData.targetLanguage);
            if (targetLocale == null)
            {
                result.errorMessages.Add($"Target locale not found: {exportData.targetLanguage}. Create it first.");
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
            
            foreach (var entry in exportData.entries)
            {
                if (string.IsNullOrEmpty(entry.key) || string.IsNullOrEmpty(entry.translation))
                {
                    result.skipped++;
                    continue;
                }
                
                var existing = table.GetEntry(entry.key);
                if (existing != null && !string.IsNullOrEmpty(existing.LocalizedValue) && !overwriteExisting)
                {
                    result.skipped++;
                    continue;
                }
                
                table.AddEntry(entry.key, entry.translation);
                result.imported++;
            }
            
            EditorUtility.SetDirty(table);
            
            // Сохранить SharedTableData тоже
            if (table.SharedData != null)
                EditorUtility.SetDirty(table.SharedData);
            
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[LocalizationImporter] Imported {result.imported}, " +
                      $"skipped {result.skipped}, errors {result.errors}");
            
            return result;
            #endif
        }
        
        #if PROTO_HAS_LOCALIZATION
        private static Locale FindLocale(string code)
        {
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                if (locale.Identifier.Code == code)
                    return locale;
            return null;
        }
        
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
}
