// Packages/com.protosystem.core/Runtime/UI/Core/UISceneInitializerBase.cs
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Базовый класс для инициализаторов UI сцены.
    /// Наследуйте от него для создания кастомной логики инициализации.
    /// </summary>
    public abstract class UISceneInitializerBase : MonoBehaviour, IUISceneInitializer
    {
        [Header("Startup Configuration")]
        [SerializeField] protected string startWindowId;
        [SerializeField] protected List<string> startupWindows = new List<string>();

        [Header("Additional Transitions")]
        [SerializeField] protected List<TransitionEntry> additionalTransitions = new List<TransitionEntry>();

        /// <summary>ID стартового окна</summary>
        public virtual string StartWindowId => startWindowId;

        /// <summary>Порядок окон при старте</summary>
        public virtual IEnumerable<string> StartupWindowOrder => startupWindows;

        /// <summary>
        /// Основная инициализация. Переопределите для кастомной логики.
        /// </summary>
        public virtual void Initialize(UISystem uiSystem)
        {
            // Открываем окна в порядке startupWindows
            foreach (var windowId in startupWindows)
            {
                if (!string.IsNullOrEmpty(windowId))
                {
                    var result = uiSystem.Navigator.Open(windowId);
                    if (result != NavigationResult.Success)
                    {
                        ProtoLogger.Log("UISystem", LogCategory.Runtime, LogLevel.Warnings, $"Failed to open '{windowId}': {result}");
                    }
                }
            }

            // Если есть стартовое окно и его нет в списке — открываем
            if (!string.IsNullOrEmpty(startWindowId) && !startupWindows.Contains(startWindowId))
            {
                uiSystem.Navigator.Open(startWindowId);
            }
        }

        /// <summary>
        /// Дополнительные переходы для графа
        /// </summary>
        public virtual IEnumerable<UITransitionDefinition> GetAdditionalTransitions()
        {
            foreach (var entry in additionalTransitions)
            {
                yield return new UITransitionDefinition(
                    entry.fromWindowId,
                    entry.toWindowId,
                    entry.trigger,
                    entry.animation
                );
            }
        }

        /// <summary>
        /// Запись о переходе для Inspector
        /// </summary>
        [System.Serializable]
        public class TransitionEntry
        {
            public string fromWindowId;
            public string toWindowId;
            public string trigger;
            public TransitionAnimation animation = TransitionAnimation.Fade;
        }
    }
}
