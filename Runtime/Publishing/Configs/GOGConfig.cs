// Packages/com.protosystem.core/Runtime/Publishing/Configs/GOGConfig.cs
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Конфигурация GOG Galaxy для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "GOGConfig", menuName = "ProtoSystem/Publishing/GOG Config")]
    public class GOGConfig : PlatformConfigBase
    {
        public override string PlatformId => "gog";
        public override string DisplayName => "GOG Galaxy";

        [Header("GOG Product")]
        [Tooltip("Product ID")]
        public string productId;
        
        [Tooltip("Название продукта")]
        public string productName;

        [Header("GOG Pipeline Builder")]
        [Tooltip("Путь к GOG Pipeline Builder")]
        public string pipelineBuilderPath;
        
        [Tooltip("Путь к конфигу проекта (.json)")]
        public string projectConfigPath;

        [Header("Credentials")]
        [Tooltip("Username для GOG")]
        public string username;

        [Header("Ветки")]
        [Tooltip("Ветка по умолчанию")]
        public string defaultBranch = "default";

        public override bool Validate(out string error)
        {
            // TODO: Реализовать валидацию когда будет готова интеграция
            error = "GOG Galaxy integration not yet implemented";
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
