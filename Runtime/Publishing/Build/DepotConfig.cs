// Packages/com.protosystem.core/Runtime/Publishing/Build/DepotConfig.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Конфигурация одного депо
    /// </summary>
    [Serializable]
    public class DepotEntry
    {
        [Tooltip("ID депо (для Steam это числовой ID)")]
        public string depotId;
        
        [Tooltip("Название для отображения")]
        public string displayName = "Windows x64";
        
        [Tooltip("Целевая платформа Unity")]
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        
        [Tooltip("Относительный путь к билду")]
        public string buildPath = "Builds/Windows";
        
        [Tooltip("Паттерны файлов для включения (через запятую, пусто = все)")]
        public string includePatterns = "";
        
        [Tooltip("Паттерны файлов для исключения (через запятую)")]
        public string excludePatterns = "*.pdb, *.log";
        
        [Tooltip("Активно ли это депо")]
        public bool enabled = true;

        /// <summary>
        /// Получить массив паттернов включения
        /// </summary>
        public string[] GetIncludePatterns()
        {
            if (string.IsNullOrWhiteSpace(includePatterns)) return new string[0];
            return includePatterns.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        }

        /// <summary>
        /// Получить массив паттернов исключения
        /// </summary>
        public string[] GetExcludePatterns()
        {
            if (string.IsNullOrWhiteSpace(excludePatterns)) return new string[0];
            return excludePatterns.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        }
    }

    /// <summary>
    /// Целевая платформа сборки
    /// </summary>
    public enum BuildTarget
    {
        StandaloneWindows,
        StandaloneWindows64,
        StandaloneOSX,
        StandaloneLinux64,
        iOS,
        Android,
        WebGL
    }

    /// <summary>
    /// Конфигурация депо для платформы
    /// </summary>
    [CreateAssetMenu(fileName = "DepotConfig", menuName = "ProtoSystem/Publishing/Depot Config")]
    public class DepotConfig : ScriptableObject
    {
        [Header("Депо")]
        [Tooltip("Список настроенных депо")]
        public List<DepotEntry> depots = new List<DepotEntry>();

        [Header("Общие настройки")]
        [Tooltip("Базовый путь для билдов")]
        public string baseBuildPath = "Builds";

        /// <summary>
        /// Получить депо по ID
        /// </summary>
        public DepotEntry GetDepot(string depotId)
        {
            return depots.Find(d => d.depotId == depotId);
        }

        /// <summary>
        /// Получить депо для платформы
        /// </summary>
        public DepotEntry GetDepotForPlatform(BuildTarget target)
        {
            return depots.Find(d => d.buildTarget == target && d.enabled);
        }

        /// <summary>
        /// Получить все активные депо
        /// </summary>
        public List<DepotEntry> GetEnabledDepots()
        {
            return depots.FindAll(d => d.enabled);
        }

        /// <summary>
        /// Создать конфиг с настройками по умолчанию для Steam
        /// </summary>
        public static DepotConfig CreateDefaultSteam(string appId)
        {
            var config = CreateInstance<DepotConfig>();
            
            long baseId = long.Parse(appId);
            
            config.depots = new List<DepotEntry>
            {
                new DepotEntry
                {
                    depotId = (baseId + 1).ToString(),
                    displayName = "Windows x64",
                    buildTarget = BuildTarget.StandaloneWindows64,
                    buildPath = "Builds/Windows",
                    enabled = true
                },
                new DepotEntry
                {
                    depotId = (baseId + 2).ToString(),
                    displayName = "macOS",
                    buildTarget = BuildTarget.StandaloneOSX,
                    buildPath = "Builds/macOS",
                    enabled = false
                },
                new DepotEntry
                {
                    depotId = (baseId + 3).ToString(),
                    displayName = "Linux",
                    buildTarget = BuildTarget.StandaloneLinux64,
                    buildPath = "Builds/Linux",
                    enabled = false
                }
            };
            
            return config;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Преобразовать BuildTarget в UnityEditor.BuildTarget (Editor-only)
        /// </summary>
        public static UnityEditor.BuildTarget ToUnityBuildTarget(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows => UnityEditor.BuildTarget.StandaloneWindows,
                BuildTarget.StandaloneWindows64 => UnityEditor.BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneOSX => UnityEditor.BuildTarget.StandaloneOSX,
                BuildTarget.StandaloneLinux64 => UnityEditor.BuildTarget.StandaloneLinux64,
                BuildTarget.iOS => UnityEditor.BuildTarget.iOS,
                BuildTarget.Android => UnityEditor.BuildTarget.Android,
                BuildTarget.WebGL => UnityEditor.BuildTarget.WebGL,
                _ => UnityEditor.BuildTarget.StandaloneWindows64
            };
        }
#endif
    }
}
