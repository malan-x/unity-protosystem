// Packages/com.protosystem.core/Runtime/Publishing/Configs/PublishingConfig.cs
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Главный конфиг публикации - объединяет все настройки
    /// </summary>
    [CreateAssetMenu(fileName = "PublishingConfig", menuName = "ProtoSystem/Publishing/Main Config")]
    public class PublishingConfig : ScriptableObject
    {
        [Header("Платформы")]
        [Tooltip("Конфигурация Steam")]
        public SteamConfig steamConfig;
        
        [Tooltip("Конфигурация itch.io")]
        public ItchConfig itchConfig;
        
        [Tooltip("Конфигурация Epic Games Store")]
        public EpicConfig epicConfig;
        
        [Tooltip("Конфигурация GOG")]
        public GOGConfig gogConfig;

        [Header("Патчноуты")]
        [Tooltip("Данные патчноутов")]
        public PatchNotesData patchNotesData;

        [Header("Сборка")]
        [Tooltip("Настройки логирования")]
        public BuildLogSettings buildLogSettings = new BuildLogSettings();
        
        [Tooltip("Путь для билдов по умолчанию")]
        public string defaultBuildPath = "Builds";

        [Header("Git")]
        [Tooltip("Интеграция с Git")]
        public bool gitIntegration = true;
        
        [Tooltip("Создавать тег при публикации")]
        public bool createGitTag = true;
        
        [Tooltip("Пушить тег в remote")]
        public bool pushGitTag = true;
        
        [Tooltip("Формат тега (поддерживает {version})")]
        public string gitTagFormat = "v{version}";

        /// <summary>
        /// Получить конфиг платформы по ID
        /// </summary>
        public PlatformConfigBase GetPlatformConfig(string platformId)
        {
            return platformId switch
            {
                "steam" => steamConfig,
                "itch" => itchConfig,
                "epic" => epicConfig,
                "gog" => gogConfig,
                _ => null
            };
        }

        /// <summary>
        /// Получить все настроенные платформы
        /// </summary>
        public PlatformConfigBase[] GetAllPlatforms()
        {
            return new PlatformConfigBase[] { steamConfig, itchConfig, epicConfig, gogConfig };
        }

        /// <summary>
        /// Получить все включенные и валидные платформы
        /// </summary>
        public System.Collections.Generic.List<PlatformConfigBase> GetEnabledPlatforms()
        {
            var result = new System.Collections.Generic.List<PlatformConfigBase>();
            
            foreach (var platform in GetAllPlatforms())
            {
                if (platform != null && platform.enabled && platform.Validate(out _))
                {
                    result.Add(platform);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Сформировать Git тег для версии
        /// </summary>
        public string GetGitTag(string version)
        {
            return gitTagFormat.Replace("{version}", version);
        }
    }
}
