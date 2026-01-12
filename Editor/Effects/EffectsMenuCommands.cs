using UnityEngine;
using UnityEditor;
using System.IO;
using ProtoSystem.Effects;

namespace ProtoSystem.Effects.Editor
{
    /// <summary>
    /// Меню команды для создания EffectConfig assets
    /// </summary>
    public static class EffectsMenuCommands
    {
        private const string EffectsFolder = "Assets/Settings/Effects";

        [MenuItem("ProtoSystem/Effects/Create/Effect Folder", false, 1)]
        private static void CreateEffectsFolder()
        {
            // Создать основную папку
            if (!AssetDatabase.IsValidFolder(EffectsFolder))
            {
                string parentFolder = Path.GetDirectoryName(EffectsFolder);
                string folderName = Path.GetFileName(EffectsFolder);
                
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }
                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[EffectsMenu] Создана папка: {EffectsFolder}");
            }

            // Создать подпапки
            string containersFolder = $"{EffectsFolder}/Containers";
            if (!AssetDatabase.IsValidFolder(containersFolder))
            {
                AssetDatabase.CreateFolder(EffectsFolder, "Containers");
                Debug.Log($"[EffectsMenu] Создана папка: {containersFolder}");
            }

            string configsFolder = $"{EffectsFolder}/Configs";
            if (!AssetDatabase.IsValidFolder(configsFolder))
            {
                AssetDatabase.CreateFolder(EffectsFolder, "Configs");
                Debug.Log($"[EffectsMenu] Создана папка: {configsFolder}");
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("ProtoSystem/Effects/Create/Effect Container", false, 2)]
        private static void CreateEffectContainer()
        {
            CreateEffectsFolder(); // Убедиться что папка существует

            var container = ScriptableObject.CreateInstance<EffectContainer>();
            container.ContainerName = "New Effect Container";
            container.Description = "Container for related effects";

            string path = $"{EffectsFolder}/Containers/{container.ContainerName}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(container, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Выделить созданный asset
            Selection.activeObject = container;

            Debug.Log($"[EffectsMenu] Создан EffectContainer: {path}");
        }

        [MenuItem("ProtoSystem/Effects/Create/VFX Effect Config", false, 10)]
        private static void CreateVFXEffectConfig()
        {
            CreateEffectConfig("VFX_Effect", EffectConfig.EffectType.VFX);
        }

        [MenuItem("ProtoSystem/Effects/Create/Audio Effect Config", false, 11)]
        private static void CreateAudioEffectConfig()
        {
            CreateEffectConfig("Audio_Effect", EffectConfig.EffectType.Audio);
        }

        [MenuItem("ProtoSystem/Effects/Create/UI Effect Config", false, 12)]
        private static void CreateUIEffectConfig()
        {
            CreateEffectConfig("UI_Effect", EffectConfig.EffectType.UI);
        }

        [MenuItem("ProtoSystem/Effects/Create/Combined Effect Config", false, 13)]
        private static void CreateCombinedEffectConfig()
        {
            CreateEffectConfig("Combined_Effect", EffectConfig.EffectType.Combined);
        }

        [MenuItem("ProtoSystem/Effects/Create/All Example Effects", false, 20)]
        private static void CreateAllExampleEffects()
        {
            CreateVFXEffectConfig();
            CreateAudioEffectConfig();
            CreateUIEffectConfig();
            CreateCombinedEffectConfig();
            Debug.Log("[EffectsMenu] Созданы все примеры эффектов");
        }

        [MenuItem("ProtoSystem/Effects/Tools/Refresh Event Cache", false, 25)]
        private static void RefreshEventCache()
        {
            EventPathDrawer.ResetCache();
            EventPathDrawer.InitializeCache();

            var categories = EventPathDrawer.GetCategories();
            int totalEvents = 0;
            foreach (var category in categories)
            {
                totalEvents += EventPathDrawer.GetEventsInCategory(category).Count;
            }

            string className = EventPathDrawer.FoundEventClassName ?? "(не найден)";
            Debug.Log($"[EffectsMenu] Кеш событий обновлён. Класс: {className}, Категорий: {categories.Length}, Событий: {totalEvents}");

            if (categories.Length > 0)
            {
                Debug.Log($"[EffectsMenu] Категории: {string.Join(", ", categories)}");
            }
        }

        [MenuItem("ProtoSystem/Effects/Tools/Validate Effects Folder", false, 30)]
        private static void ValidateEffectsFolder()
        {
            CreateEffectsFolder();

            string[] effectAssets = AssetDatabase.FindAssets("t:EffectConfig", new[] { EffectsFolder });
            Debug.Log($"[EffectsMenu] Найдено EffectConfig assets: {effectAssets.Length}");

            foreach (string guid in effectAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EffectConfig config = AssetDatabase.LoadAssetAtPath<EffectConfig>(path);
                if (config != null)
                {
                    Debug.Log($"[EffectsMenu] Effect: {config.effectId} - {config.displayName} ({config.effectType})");
                }
            }
        }

        private static void CreateEffectConfig(string baseName, EffectConfig.EffectType effectType)
        {
            CreateEffectsFolder();

            // Найти уникальное имя файла
            string fileName = baseName;
            string fullPath = $"{EffectsFolder}/Configs/{fileName}.asset";
            int counter = 1;
            while (File.Exists(fullPath))
            {
                fileName = $"{baseName}_{counter}";
                fullPath = $"{EffectsFolder}/Configs/{fileName}.asset";
                counter++;
            }

            // Создать EffectConfig
            EffectConfig config = ScriptableObject.CreateInstance<EffectConfig>();
            config.effectId = fileName.ToLower().Replace(" ", "_");
            config.displayName = fileName.Replace("_", " ");
            config.effectType = effectType;

            // Настройки по умолчанию
            switch (effectType)
            {
                case EffectConfig.EffectType.VFX:
                    config.lifetime = 2f;
                    config.scale = Vector3.one;
                    break;
                case EffectConfig.EffectType.Audio:
                    config.volume = 0.8f;
                    config.pitch = 1f;
                    config.spatial = true;
                    break;
                case EffectConfig.EffectType.UI:
                    config.uiDisplayTime = 3f;
                    break;
                case EffectConfig.EffectType.Combined:
                    config.lifetime = 2f;
                    config.volume = 0.8f;
                    config.uiDisplayTime = 3f;
                    break;
            }

            // Сохранить asset
            AssetDatabase.CreateAsset(config, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Выделить созданный asset
            Selection.activeObject = config;

            Debug.Log($"[EffectsMenu] Создан EffectConfig: {fullPath}");
        }
    }
}
