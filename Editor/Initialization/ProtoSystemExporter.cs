#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace ProtoSystem.ProEditor
{
    /// <summary>
    /// Утилита для экспорта ProtoSystem в отдельный пакет
    /// </summary>
    public static class ProtoSystemExporter
    {
        private const string EXPORT_MENU = "ProtoSystem/Export Package";
        private const string PACKAGE_NAME = "ProtoSystem.unitypackage";

        // Список файлов для экспорта
        private static readonly string[] FilesToExport = new string[]
        {
            // EventBus файлы
            "Assets/KM/Scripts/EventBus.cs",
            "Assets/KM/Scripts/IEventBus.cs",
            "Assets/KM/Scripts/MonoEventBus.cs",
            "Assets/KM/Scripts/NetworkEventBus.cs",
            
            // Initializer файлы
            "Assets/KM/Scripts/Core/Initializer/IInitializableSystem.cs",
            "Assets/KM/Scripts/Core/Initializer/InitializationHelper.cs",
            "Assets/KM/Scripts/Core/Initializer/InitializableSystemBase.cs",
            "Assets/KM/Scripts/Core/Initializer/NetworkInitializableSystem.cs",
            "Assets/KM/Scripts/Core/Initializer/SystemProvider.cs",
            "Assets/KM/Scripts/Core/Initializer/SystemEntry.cs",
            // SystemInitializationManager.cs - теперь проект-специфичный, не экспортируется
            
            // Editor файлы
            "Assets/KM/Scripts/Core/Initializer/Editor/SystemHierarchyIcons.cs",
            
            // Примеры
            "Assets/KM/Scripts/Core/Initializer/Examples/ExampleLocalSystem.cs",
            "Assets/KM/Scripts/Core/Initializer/Examples/ExampleNetworkSystem.cs",
            
            // Документация и конфигурация
            "Assets/KM/Scripts/ProtoSystem_README.md",
            "Assets/KM/Scripts/package.json",
            
            // Meta файлы (Unity автоматически добавит их)
        };

        [MenuItem(EXPORT_MENU)]
        public static void ExportProtoSystem()
        {
            // Проверяем существование файлов
            List<string> existingFiles = new List<string>();
            List<string> missingFiles = new List<string>();

            foreach (string file in FilesToExport)
            {
                if (File.Exists(file))
                {
                    existingFiles.Add(file);
                }
                else
                {
                    missingFiles.Add(file);
                }
            }

            // Показываем информацию о файлах
            if (missingFiles.Count > 0)
            {
                string missingList = string.Join("\n", missingFiles);
                Debug.LogWarning($"ProtoSystem Export: Следующие файлы не найдены:\n{missingList}");
            }

            if (existingFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("ProtoSystem Export",
                    "Не найдено файлов для экспорта!", "OK");
                return;
            }

            // Запрашиваем путь для сохранения
            string path = EditorUtility.SaveFilePanel(
                "Экспорт ProtoSystem Package",
                "",
                PACKAGE_NAME,
                "unitypackage");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Экспортируем пакет
            AssetDatabase.ExportPackage(
                existingFiles.ToArray(),
                path,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

            Debug.Log($"ProtoSystem успешно экспортирован в: {path}");
            Debug.Log($"Экспортировано файлов: {existingFiles.Count}");

            // Открываем папку с экспортированным файлом
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("ProtoSystem/Tools/Create Standalone Folder", false, 10)]
        public static void CreateStandaloneFolder()
        {
            // Создаём отдельную папку ProtoSystem в корне Assets
            string targetPath = "Assets/ProtoSystem";

            if (Directory.Exists(targetPath))
            {
                if (!EditorUtility.DisplayDialog("ProtoSystem",
                    $"Папка {targetPath} уже существует. Перезаписать?",
                    "Да", "Отмена"))
                {
                    return;
                }

                Directory.Delete(targetPath, true);
            }

            // Создаём структуру папок
            Directory.CreateDirectory(targetPath);
            Directory.CreateDirectory(Path.Combine(targetPath, "EventBus"));
            Directory.CreateDirectory(Path.Combine(targetPath, "Initializer"));
            Directory.CreateDirectory(Path.Combine(targetPath, "Initializer/Editor"));
            Directory.CreateDirectory(Path.Combine(targetPath, "Initializer/Examples"));
            Directory.CreateDirectory(Path.Combine(targetPath, "Documentation"));

            // Копируем файлы
            CopyProtoSystemFiles(targetPath);

            // Обновляем базу данных ассетов
            AssetDatabase.Refresh();

            Debug.Log($"ProtoSystem скопирован в {targetPath}");
            EditorUtility.DisplayDialog("ProtoSystem",
                $"ProtoSystem успешно скопирован в {targetPath}\n\n" +
                "Теперь вы можете:\n" +
                "1. Удалить оригинальные файлы из KM/Scripts\n" +
                "2. Экспортировать папку ProtoSystem как package\n" +
                "3. Использовать в других проектах",
                "OK");
        }

        private static void CopyProtoSystemFiles(string targetPath)
        {
            // Маппинг исходных файлов на целевые
            Dictionary<string, string> fileMappings = new Dictionary<string, string>()
            {
                // EventBus
                {"KM/Scripts/EventBus.cs", "EventBus/EventBus.cs"},
                {"KM/Scripts/IEventBus.cs", "EventBus/IEventBus.cs"},
                {"KM/Scripts/MonoEventBus.cs", "EventBus/MonoEventBus.cs"},
                {"KM/Scripts/NetworkEventBus.cs", "EventBus/NetworkEventBus.cs"},
                
                // Initializer
                {"KM/Scripts/Core/Initializer/IInitializableSystem.cs", "Initializer/IInitializableSystem.cs"},
                {"KM/Scripts/Core/Initializer/InitializationHelper.cs", "Initializer/InitializationHelper.cs"},
                {"KM/Scripts/Core/Initializer/InitializableSystemBase.cs", "Initializer/InitializableSystemBase.cs"},
                {"KM/Scripts/Core/Initializer/NetworkInitializableSystem.cs", "Initializer/NetworkInitializableSystem.cs"},
                {"KM/Scripts/Core/Initializer/SystemProvider.cs", "Initializer/SystemProvider.cs"},
                {"KM/Scripts/Core/Initializer/SystemEntry.cs", "Initializer/SystemEntry.cs"},
                // {"KM/Scripts/Core/Initializer/SystemInitializationManager.cs", "Initializer/SystemInitializationManager.cs"}, // Удален - теперь проект-специфичный
                
                // Editor
                {"KM/Scripts/Core/Initializer/Editor/SystemHierarchyIcons.cs", "Initializer/Editor/SystemHierarchyIcons.cs"},
                
                // Examples
                {"KM/Scripts/Core/Initializer/Examples/ExampleLocalSystem.cs", "Initializer/Examples/ExampleLocalSystem.cs"},
                {"KM/Scripts/Core/Initializer/Examples/ExampleNetworkSystem.cs", "Initializer/Examples/ExampleNetworkSystem.cs"},
                
                // Documentation
                {"KM/Scripts/ProtoSystem_README.md", "Documentation/README.md"},
                {"KM/Scripts/Core/Initializer/MIGRATION_GUIDE.md", "Documentation/MIGRATION_GUIDE.md"},
            };

            foreach (var mapping in fileMappings)
            {
                string sourcePath = Path.Combine("Assets", mapping.Key);
                string destPath = Path.Combine(targetPath, mapping.Value);

                if (File.Exists(sourcePath))
                {
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(sourcePath, destPath, true);
                    Debug.Log($"Скопирован: {mapping.Key} -> {mapping.Value}");
                }
                else
                {
                    Debug.LogWarning($"Файл не найден: {sourcePath}");
                }
            }

            // Копируем assembly definitions
            string asmdefSource = Path.Combine("Assets", "KM/Scripts/ProtoSystem.asmdef");
            string asmdefDest = Path.Combine(targetPath, "ProtoSystem.asmdef");
            if (File.Exists(asmdefSource))
            {
                File.Copy(asmdefSource, asmdefDest, true);
            }

            // Копируем package.json
            string packageSource = Path.Combine("Assets", "KM/Scripts/package.json");
            string packageDest = Path.Combine(targetPath, "package.json");
            if (File.Exists(packageSource))
            {
                File.Copy(packageSource, packageDest, true);
            }
        }

        [MenuItem("ProtoSystem/About/Show ProtoSystem Info", false, 1000)]
        public static void ShowProtoSystemInfo()
        {
            string info = @"ProtoSystem - Универсальная система для Unity

Компоненты:
• EventBus - система событий
• Initialization System - управление зависимостями

Namespace: ProtoSystem

Использование:
1. В новом проекте импортируйте ProtoSystem.unitypackage
2. Создайте свои события, наследуя EventBus
3. Используйте MonoEventBus или NetworkEventBus для компонентов
4. Наследуйтесь от InitializableSystemBase для систем

Экспорт:
• Tools/ProtoSystem/Export Package - создать .unitypackage
• Tools/ProtoSystem/Create Standalone Folder - копировать в отдельную папку

Документация: см. ProtoSystem_README.md";

            EditorUtility.DisplayDialog("ProtoSystem Info", info, "OK");
        }
    }
}
#endif
