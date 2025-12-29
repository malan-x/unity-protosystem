// Packages/com.protosystem.core/Runtime/SceneFlow/SceneFlowConfig.cs
using UnityEngine;

namespace ProtoSystem.SceneFlow
{
    /// <summary>
    /// Конфигурация системы загрузки сцен
    /// </summary>
    [CreateAssetMenu(fileName = "SceneFlowConfig", menuName = "ProtoSystem/SceneFlow/Config")]
    public class SceneFlowConfig : ScriptableObject
    {
        [Header("Loading Screen")]
        [Tooltip("Использовать loading screen")]
        public bool useLoadingScreen = true;
        
        [Tooltip("Минимальное время показа loading screen")]
        public float minimumLoadingTime = 0.5f;
        
        [Tooltip("Префаб loading screen")]
        public GameObject loadingScreenPrefab;

        [Header("Transitions")]
        [Tooltip("Переход по умолчанию")]
        public TransitionType defaultTransition = TransitionType.Fade;
        
        [Tooltip("Длительность перехода")]
        public float transitionDuration = 0.3f;
        
        [Tooltip("Цвет fade overlay")]
        public Color fadeColor = Color.black;

        [Header("Scene Mapping")]
        [Tooltip("Маппинг логических имён на реальные сцены")]
        public SceneMapping[] sceneMappings;

        /// <summary>
        /// Получить реальное имя сцены по логическому
        /// </summary>
        public string GetSceneName(string logicalName)
        {
            if (sceneMappings == null) return logicalName;
            
            foreach (var mapping in sceneMappings)
            {
                if (mapping.logicalName == logicalName)
                    return mapping.sceneName;
            }
            
            return logicalName;
        }

        public static SceneFlowConfig CreateDefault()
        {
            return CreateInstance<SceneFlowConfig>();
        }
    }

    /// <summary>
    /// Маппинг логического имени сцены на реальное
    /// </summary>
    [System.Serializable]
    public class SceneMapping
    {
        [Tooltip("Логическое имя (для кода)")]
        public string logicalName;
        
        [Tooltip("Реальное имя сцены")]
        public string sceneName;
        
        [Tooltip("Описание")]
        public string description;
    }
}
