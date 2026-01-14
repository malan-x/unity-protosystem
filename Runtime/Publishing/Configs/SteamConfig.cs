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
    /// Конфигурация Steam для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "SteamConfig", menuName = "ProtoSystem/Publishing/Steam Config")]
    public class SteamConfig : PlatformConfigBase
    {
        public override string PlatformId => "steam";
        public override string DisplayName => "Steam";

        [Header("Steam App")]
        [Tooltip("App ID из Steamworks")]
        public string appId;
        
        [Tooltip("Название приложения (для отображения)")]
        public string appName;

        [Header("Депо")]
        [Tooltip("Конфигурация депо")]
        public DepotConfig depotConfig;

        [Header("Ветки")]
        [Tooltip("Доступные ветки")]
        public List<SteamBranch> branches = new List<SteamBranch>
        {
            new SteamBranch { name = "default", description = "Public release" },
            new SteamBranch { name = "beta", description = "Beta testing", isPrivate = false },
            new SteamBranch { name = "internal", description = "Internal testing", isPrivate = true }
        };
        
        [Tooltip("Ветка по умолчанию для публикации")]
        public string defaultBranch = "default";

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
        /// Валидация конфигурации
        /// </summary>
        public override bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(appId))
            {
                error = "App ID not set";
                return false;
            }

            if (!long.TryParse(appId, out _))
            {
                error = "App ID must be a number";
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

            if (depotConfig == null)
            {
                error = "Depot config not set";
                return false;
            }

            if (depotConfig.GetEnabledDepots().Count == 0)
            {
                error = "No enabled depots";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Получить путь к VDF файлу
        /// </summary>
        public string GetVdfPath()
        {
            var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            return System.IO.Path.Combine(projectPath, "SteamUpload", $"app_build_{appId}.vdf");
        }

        /// <summary>
        /// Создать конфиг по умолчанию
        /// </summary>
        public static SteamConfig CreateDefault()
        {
            var config = CreateInstance<SteamConfig>();
            config.branches = new List<SteamBranch>
            {
                new SteamBranch { name = "default", description = "Public release" },
                new SteamBranch { name = "beta", description = "Beta testing" },
                new SteamBranch { name = "internal", description = "Internal testing", isPrivate = true }
            };
            return config;
        }
    }
}
