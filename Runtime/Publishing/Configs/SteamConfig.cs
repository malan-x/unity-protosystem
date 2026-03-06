// Packages/com.protosystem.core/Runtime/Publishing/Configs/SteamConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Ветка Steam для публикации
    /// </summary>
    [Serializable]
    public class SteamBranch
    {
        [Tooltip("Название ветки")]
        public string name = "default";

        [Tooltip("Описание")]
        public string description = "Public release branch";

        [Tooltip("Требует пароль для доступа")]
        public bool isPrivate = false;
    }

    /// <summary>
    /// Тип целевого приложения Steam
    /// </summary>
    public enum SteamBuildTargetType
    {
        Main,
        Playtest,
        Demo
    }

    /// <summary>
    /// Целевое приложение Steam (Main / Playtest / Demo) с собственным App ID, депотами и ветками
    /// </summary>
    [Serializable]
    public class SteamAppTarget
    {
        public SteamBuildTargetType targetType = SteamBuildTargetType.Main;

        [Tooltip("App ID из Steamworks")]
        public string appId;

        [Tooltip("Название приложения (для отображения)")]
        public string appName;

        [Tooltip("Конфигурация депо")]
        public DepotConfig depotConfig;

        [Tooltip("Доступные ветки")]
        public List<SteamBranch> branches = new List<SteamBranch>
        {
            new SteamBranch { name = "default", description = "Public release" }
        };

        [Tooltip("Ветка по умолчанию для публикации")]
        public string defaultBranch = "default";

        [Tooltip("Активен ли этот target")]
        public bool enabled = true;

        public string ShortName => targetType switch
        {
            SteamBuildTargetType.Main => "Main",
            SteamBuildTargetType.Playtest => "PT",
            SteamBuildTargetType.Demo => "Demo",
            _ => targetType.ToString()
        };

        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(appId))
            {
                error = $"{targetType}: App ID not set";
                return false;
            }

            if (!long.TryParse(appId, out _))
            {
                error = $"{targetType}: App ID must be a number";
                return false;
            }

            if (depotConfig == null)
            {
                error = $"{targetType}: Depot config not set";
                return false;
            }

            if (depotConfig.GetEnabledDepots().Count == 0)
            {
                error = $"{targetType}: No enabled depots";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Конфигурация Steam для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "SteamConfig", menuName = "ProtoSystem/Publishing/Steam Config")]
    public class SteamConfig : PlatformConfigBase
    {
        public override string PlatformId => "steam";
        public override string DisplayName => "Steam";

        [Header("Build Targets")]
        [Tooltip("Целевые приложения (Main, Playtest, Demo)")]
        public List<SteamAppTarget> buildTargets = new List<SteamAppTarget>();

        [Tooltip("Активный build target")]
        public SteamBuildTargetType activeBuildTarget = SteamBuildTargetType.Main;

        // Legacy fields for migration
        [HideInInspector, SerializeField] internal string appId;
        [HideInInspector, SerializeField] internal string appName;
        [HideInInspector, SerializeField] internal DepotConfig depotConfig;
        [HideInInspector, SerializeField] internal List<SteamBranch> branches;
        [HideInInspector, SerializeField] internal string defaultBranch;

        [Header("Аккаунт")]
        [Tooltip("Логин Steam (пароль хранится отдельно в SecureCredentials)")]
        public string username;

        [Tooltip("Использовать Steam Guard через файл")]
        public bool useSteamGuardFile = false;

        [Header("Пути SDK")]
        [Tooltip("Путь к SteamCMD")]
        public string steamCmdPath;

        [Tooltip("Путь к Steamworks SDK (опционально)")]
        public string steamworksSdkPath;

        [Header("Настройки загрузки")]
        [Tooltip("Автоматически установить live после успешной загрузки")]
        public bool autoSetLive = false;

        [Tooltip("Описание билда в Steamworks")]
        public string buildDescription = "Uploaded from Unity";

        [Tooltip("Превью режим (не загружает на сервер)")]
        public bool previewMode = false;

        [Header("Steam Web API")]
        [Tooltip("Публиковать новости через Web API")]
        public bool publishNews = false;

        [Tooltip("App ID для новостей (обычно = appId)")]
        public string newsAppId;

        /// <summary>
        /// Текущий активный target
        /// </summary>
        public SteamAppTarget ActiveTarget
        {
            get
            {
                MigrateIfNeeded();
                return buildTargets.Find(t => t.targetType == activeBuildTarget && t.enabled)
                    ?? buildTargets.Find(t => t.enabled)
                    ?? (buildTargets.Count > 0 ? buildTargets[0] : null);
            }
        }

        /// <summary>
        /// Получить все включённые targets
        /// </summary>
        public List<SteamAppTarget> GetEnabledTargets()
        {
            MigrateIfNeeded();
            return buildTargets.FindAll(t => t.enabled);
        }

        /// <summary>
        /// Получить target по типу
        /// </summary>
        public SteamAppTarget GetTarget(SteamBuildTargetType type)
        {
            MigrateIfNeeded();
            return buildTargets.Find(t => t.targetType == type);
        }

        /// <summary>
        /// Миграция старых полей в buildTargets
        /// </summary>
        public void MigrateIfNeeded()
        {
            if (buildTargets.Count > 0) return;
            if (string.IsNullOrEmpty(appId) && depotConfig == null) return;

            buildTargets.Add(new SteamAppTarget
            {
                targetType = SteamBuildTargetType.Main,
                appId = appId ?? "",
                appName = appName ?? "",
                depotConfig = depotConfig,
                branches = branches ?? new List<SteamBranch>
                {
                    new SteamBranch { name = "default", description = "Public release" },
                    new SteamBranch { name = "beta", description = "Beta testing" },
                    new SteamBranch { name = "internal", description = "Internal testing", isPrivate = true }
                },
                defaultBranch = defaultBranch ?? "default",
                enabled = true
            });

            activeBuildTarget = SteamBuildTargetType.Main;
        }

        /// <summary>
        /// Валидация конфигурации
        /// </summary>
        public override bool Validate(out string error)
        {
            MigrateIfNeeded();

            var target = ActiveTarget;
            if (target == null)
            {
                error = "No build targets configured";
                return false;
            }

            if (string.IsNullOrEmpty(steamCmdPath))
            {
                error = "SteamCMD path not set";
                return false;
            }

            if (!System.IO.File.Exists(steamCmdPath))
            {
                error = "SteamCMD not found at specified path";
                return false;
            }

            if (string.IsNullOrEmpty(username))
            {
                error = "Username not set";
                return false;
            }

            return target.Validate(out error);
        }

        /// <summary>
        /// Получить путь к VDF файлу для активного target
        /// </summary>
        public string GetVdfPath()
        {
            var target = ActiveTarget;
            var id = target?.appId ?? "0";
            var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            return System.IO.Path.Combine(projectPath, "SteamUpload", $"app_build_{id}.vdf");
        }

        /// <summary>
        /// Создать конфиг по умолчанию
        /// </summary>
        public static SteamConfig CreateDefault()
        {
            var config = CreateInstance<SteamConfig>();
            config.buildTargets = new List<SteamAppTarget>
            {
                new SteamAppTarget
                {
                    targetType = SteamBuildTargetType.Main,
                    branches = new List<SteamBranch>
                    {
                        new SteamBranch { name = "default", description = "Public release" },
                        new SteamBranch { name = "beta", description = "Beta testing" },
                        new SteamBranch { name = "internal", description = "Internal testing", isPrivate = true }
                    },
                    enabled = true
                }
            };
            return config;
        }
    }
}
