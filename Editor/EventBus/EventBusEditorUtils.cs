using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Утилиты для работы с проектным файлом событий EventIds в редакторе
    /// </summary>
    public static class EventBusEditorUtils
    {
        private const string EDITOR_PREFS_KEY_BASE = "ProtoSystem_EventIdsFilePath_";
        private const string EVENT_IDS_FILE_PATTERN = "EventIds.*.cs";

        /// <summary>
        /// Данные о проектном EventBus файле
        /// </summary>
        public class EventBusFileInfo
        {
            public bool Exists;
            public string FilePath;
            public string Namespace;
            public int EventCount;
            public int CategoryCount;
            public List<EventCategoryInfo> Categories = new List<EventCategoryInfo>();
        }

        /// <summary>
        /// Информация о категории событий
        /// </summary>
        public class EventCategoryInfo
        {
            public string Name;
            public int EventCount;
            public List<string> Events = new List<string>();
        }

        /// <summary>
        /// Получает информацию о проектном EventBus файле
        /// </summary>
        public static EventBusFileInfo GetProjectEventBusInfo()
        {
            var info = new EventBusFileInfo();

            // Ключ специфичный для проекта
            string prefsKey = EDITOR_PREFS_KEY_BASE + Application.dataPath.GetHashCode();

            // Пробуем получить путь из EditorPrefs
            string cachedPath = EditorPrefs.GetString(prefsKey, "");

            if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath) && IsPathInProject(cachedPath))
            {
                // Кэшированный путь валиден и в проекте
                info.FilePath = cachedPath;
                info.Exists = true;
                ParseEventBusFile(info);
                return info;
            }

            // Ищем файл в проекте
            string foundPath = FindEventBusFile();

            if (!string.IsNullOrEmpty(foundPath))
            {
                info.FilePath = foundPath;
                info.Exists = true;

                // Сохраняем путь в EditorPrefs для этого проекта
                EditorPrefs.SetString(prefsKey, foundPath);

                ParseEventBusFile(info);
            }
            else
            {
                info.Exists = false;
            }

            return info;
        }

        /// <summary>
        /// Проверяет, находится ли путь в текущем проекте
        /// </summary>
        private static bool IsPathInProject(string path)
        {
            return path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ищет файл EventIds.*.cs
        /// </summary>
        private static string FindEventBusFile()
        {
            // Сначала ищем в Assets/KM/Scripts/Events/
            string eventsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "KM", "Scripts", "Events"));
            if (Directory.Exists(eventsPath))
            {
                var eventFiles = Directory.GetFiles(eventsPath, EVENT_IDS_FILE_PATTERN, SearchOption.TopDirectoryOnly)
                    .ToList();
                
                if (eventFiles.Count > 0)
                    return eventFiles.First();
            }

            // Затем ищем в любом месте Assets
            string assetsPath = Application.dataPath;
            var assetFiles = Directory.GetFiles(assetsPath, EVENT_IDS_FILE_PATTERN, SearchOption.AllDirectories)
                .Where(f => !f.Contains("Packages"))
                .ToList();

            return assetFiles.FirstOrDefault();
        }

        /// <summary>
        /// Парсит файл EventIds для получения информации
        /// </summary>
        private static void ParseEventBusFile(EventBusFileInfo info)
        {
            if (string.IsNullOrEmpty(info.FilePath) || !File.Exists(info.FilePath))
                return;

            try
            {
                string content = File.ReadAllText(info.FilePath);

                // Извлекаем namespace из имени файла (EventIds.KM.cs -> KM)
                string fileName = Path.GetFileNameWithoutExtension(info.FilePath);
                var nameParts = fileName.Split('.');
                if (nameParts.Length >= 2)
                {
                    info.Namespace = nameParts[1];
                }

                // Считаем категории (public static class XXX) - без partial
                var categoryMatches = Regex.Matches(content, @"public\s+static\s+class\s+(\w+)");
                var categoryDict = new Dictionary<string, EventCategoryInfo>();

                foreach (Match match in categoryMatches)
                {
                    string categoryName = match.Groups[1].Value;
                    // Пропускаем главный класс Evt
                    if (categoryName == "Evt" || categoryName == "EventIds")
                        continue;
                    if (!categoryDict.ContainsKey(categoryName))
                    {
                        categoryDict[categoryName] = new EventCategoryInfo { Name = categoryName };
                    }
                }

                // Считаем события (public const int XXX = NNN;)
                var eventMatches = Regex.Matches(content, @"public\s+const\s+int\s+(\w+)\s*=\s*(\d+);");
                info.EventCount = eventMatches.Count;

                // Более сложный парсинг для привязки событий к категориям
                var lines = content.Split('\n');
                EventCategoryInfo currentCat = null;

                foreach (var line in lines)
                {
                    var catMatch = Regex.Match(line, @"public\s+static\s+class\s+(\w+)");
                    if (catMatch.Success)
                    {
                        string catName = catMatch.Groups[1].Value;
                        if (categoryDict.TryGetValue(catName, out var cat))
                        {
                            currentCat = cat;
                        }
                    }

                    var eventMatch = Regex.Match(line, @"public\s+const\s+int\s+(\w+)\s*=\s*(\d+);");
                    if (eventMatch.Success && currentCat != null)
                    {
                        currentCat.Events.Add(eventMatch.Groups[1].Value);
                        currentCat.EventCount++;
                    }
                }

                info.Categories = categoryDict.Values.Where(c => c.EventCount > 0).ToList();
                info.CategoryCount = info.Categories.Count;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка парсинга EventIds файла: {ex.Message}");
            }
        }

        /// <summary>
        /// Создает новый файл EventIds для проекта
        /// </summary>
        public static string CreateEventBusFile(string projectNamespace)
        {
            if (string.IsNullOrEmpty(projectNamespace))
            {
                Debug.LogError("Namespace не может быть пустым");
                return null;
            }

            // Ключ специфичный для проекта
            string prefsKey = EDITOR_PREFS_KEY_BASE + Application.dataPath.GetHashCode();

            // Путь: Assets/KM/Scripts/Events/EventIds.<Namespace>.cs
            string eventsDir = Path.Combine(Application.dataPath, "KM", "Scripts", "Events");
            string fileName = $"EventIds.{projectNamespace}.cs";
            string filePath = Path.Combine(eventsDir, fileName);

            // Создаем директорию если нужно
            if (!Directory.Exists(eventsDir))
            {
                Directory.CreateDirectory(eventsDir);
            }

            // Проверяем, не существует ли уже файл
            if (File.Exists(filePath))
            {
                Debug.LogWarning($"Файл уже существует: {filePath}");
                EditorPrefs.SetString(prefsKey, filePath);
                return filePath;
            }

            // Генерируем содержимое файла
            string content = GenerateEventBusTemplate(projectNamespace);

            // Записываем файл
            File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);

            // Сохраняем путь в EditorPrefs
            EditorPrefs.SetString(prefsKey, filePath);

            // Обновляем AssetDatabase
            AssetDatabase.Refresh();

            Debug.Log($"✅ Создан файл EventIds: {filePath}");

            return filePath;
        }

        /// <summary>
        /// Генерирует шаблон EventIds файла
        /// </summary>
        private static string GenerateEventBusTemplate(string projectNamespace)
        {
            return $@"// События проекта {projectNamespace}
// Использование: EventBus.Publish(Evt.Категория.Событие, data);
// Не забудьте добавить: using {projectNamespace}; using static ProtoSystem.EventBus;

namespace {projectNamespace}
{{
    /// <summary>
    /// Короткий алиас для ID событий проекта {projectNamespace}
    /// </summary>
    public static class Evt
    {{
        // ═══════════════════════════════════════════════════════════════════
        // События проекта {projectNamespace}
        // ═══════════════════════════════════════════════════════════════════

        // Пример категории событий:
        // public static class МояКатегория
        // {{
        //     public const int Событие_1 = 10001;
        //     public const int Событие_2 = 10002;
        // }}

        // Добавляйте свои категории и события ниже:

    }}
}}
";
        }

        /// <summary>
        /// Открывает файл EventIds в редакторе кода
        /// </summary>
        public static void OpenEventBusFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"Файл не найден: {filePath}");
                return;
            }

            // Конвертируем в путь относительно Assets
            string assetPath = filePath;
            if (filePath.Contains(Application.dataPath))
            {
                assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);
            }

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                // Fallback - открываем через систему
                System.Diagnostics.Process.Start(filePath);
            }
        }

        /// <summary>
        /// Сбрасывает кэш пути к EventIds файлу
        /// </summary>
        public static void ResetCache()
        {
            string prefsKey = EDITOR_PREFS_KEY_BASE + Application.dataPath.GetHashCode();
            EditorPrefs.DeleteKey(prefsKey);
            Debug.Log("Кэш пути EventIds сброшен для текущего проекта");
        }

        /// <summary>
        /// Экспортирует информацию о событиях для MCP
        /// </summary>
        public static string ExportEventsForMCP(EventBusFileInfo info)
        {
            if (info == null || !info.Exists)
                return null;

            var exportData = new EventBusExportData
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                projectNamespace = info.Namespace,
                filePath = info.FilePath,
                totalEvents = info.EventCount,
                totalCategories = info.CategoryCount,
                categories = info.Categories.Select(c => new CategoryExportInfo
                {
                    name = c.Name,
                    eventCount = c.EventCount,
                    events = c.Events
                }).ToList()
            };

            string json = JsonUtility.ToJson(exportData, true);

            // Сохраняем в файл
            string mcpDir = Path.Combine(Application.dataPath, "MCP");
            if (!Directory.Exists(mcpDir))
            {
                Directory.CreateDirectory(mcpDir);
            }

            string filePath = Path.Combine(mcpDir, "eventbus_info.json");
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            Debug.Log($"📤 EventIds экспортирован для MCP: {filePath}");
            return filePath;
        }
    }

    #region MCP Export Data Classes

    [Serializable]
    public class EventBusExportData
    {
        public string timestamp;
        public string projectNamespace;
        public string filePath;
        public int totalEvents;
        public int totalCategories;
        public List<CategoryExportInfo> categories;
    }

    [Serializable]
    public class CategoryExportInfo
    {
        public string name;
        public int eventCount;
        public List<string> events;
    }

    #endregion
}
