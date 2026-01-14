// Packages/com.protosystem.core/Runtime/Publishing/Configs/ItchConfig.cs
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Конфигурация itch.io для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "ItchConfig", menuName = "ProtoSystem/Publishing/Itch.io Config")]
    public class ItchConfig : PlatformConfigBase
    {
        public override string PlatformId => "itch";
        public override string DisplayName => "itch.io";

        [Header("itch.io Project")]
        [Tooltip("Username на itch.io")]
        public string username;
        
        [Tooltip("Название проекта (slug)")]
        public string projectName;
        
        [Tooltip("Полный путь: username/projectName")]
        public string FullPath => $"{username}/{projectName}";

        [Header("Butler")]
        [Tooltip("Путь к Butler CLI")]
        public string butlerPath;

        [Header("Каналы")]
        [Tooltip("Канал для Windows")]
        public string windowsChannel = "windows";
        
        [Tooltip("Канал для macOS")]
        public string macChannel = "mac";
        
        [Tooltip("Канал для Linux")]
        public string linuxChannel = "linux";

        [Header("Настройки")]
        [Tooltip("Версионировать загрузки")]
        public bool pushWithVersion = true;

        public override bool Validate(out string error)
        {
            // TODO: Реализовать валидацию когда будет готова интеграция
            error = "itch.io integration not yet implemented";
            return false;
        }

        public override string GetStatusText()
        {
            return "Not Implemented";
        }

        public override Color GetStatusColor()
        {
            return new Color(0.6f, 0.6f, 0.6f);
        }
    }
}
