// Packages/com.protosystem.core/Runtime/Publishing/Configs/EpicConfig.cs
using UnityEngine;

namespace ProtoSystem.Publishing
{
    /// <summary>
    /// Конфигурация Epic Games Store для публикации
    /// </summary>
    [CreateAssetMenu(fileName = "EpicConfig", menuName = "ProtoSystem/Publishing/Epic Games Config")]
    public class EpicConfig : PlatformConfigBase
    {
        public override string PlatformId => "epic";
        public override string DisplayName => "Epic Games Store";

        [Header("Epic Product")]
        [Tooltip("Product ID")]
        public string productId;
        
        [Tooltip("Artifact ID")]
        public string artifactId;
        
        [Tooltip("Organization ID")]
        public string organizationId;

        [Header("Build Patch Tool")]
        [Tooltip("Путь к BuildPatchTool")]
        public string buildPatchToolPath;
        
        [Tooltip("Cloud Directory")]
        public string cloudDir;

        [Header("Credentials")]
        [Tooltip("Client ID")]
        public string clientId;

        public override bool Validate(out string error)
        {
            // TODO: Реализовать валидацию когда будет готова интеграция
            error = "Epic Games Store integration not yet implemented";
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
