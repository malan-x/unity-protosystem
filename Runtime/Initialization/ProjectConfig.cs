using UnityEngine;

namespace ProtoSystem
{
    /// <summary>
    /// Конфигурация проекта (namespace, версия и т.д.)
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectConfig", menuName = "ProtoSystem/Project Config")]
    public class ProjectConfig : ScriptableObject
    {
        [Tooltip("Основной namespace проекта")]
        public string projectNamespace = "MyGame";
        
        /// <summary>
        /// Получить ProjectConfig из Resources
        /// </summary>
        public static ProjectConfig Load()
        {
            var config = Resources.Load<ProjectConfig>("ProjectConfig");
            if (config == null)
            {
                Debug.LogWarning("ProjectConfig not found in Resources. Using default namespace.");
                config = CreateInstance<ProjectConfig>();
            }
            return config;
        }
    }
}
