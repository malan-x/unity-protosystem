// Packages/com.protosystem.core/Runtime/UI/Core/IUISceneInitializer.cs
using System.Collections.Generic;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Интерфейс для сценарного инициализатора UI.
    /// Определяет какие окна открывать, в каком порядке, и как они связаны.
    /// Разные сцены могут использовать разные инициализаторы.
    /// </summary>
    public interface IUISceneInitializer
    {
        /// <summary>
        /// Вызывается после инициализации UISystem.
        /// Здесь можно открыть стартовые окна, настроить связи.
        /// </summary>
        void Initialize(UISystem uiSystem);

        /// <summary>
        /// Дополнительные переходы, которые нельзя определить из атрибутов.
        /// Например, динамические переходы в зависимости от состояния игры.
        /// </summary>
        IEnumerable<UITransitionDefinition> GetAdditionalTransitions();

        /// <summary>
        /// ID окна для открытия при старте (может быть null).
        /// </summary>
        string StartWindowId { get; }

        /// <summary>
        /// Порядок открытия окон при старте (например, сначала HUD, потом Overlay).
        /// </summary>
        IEnumerable<string> StartupWindowOrder { get; }
    }

    /// <summary>
    /// Определение перехода для построения графа
    /// </summary>
    [System.Serializable]
    public struct UITransitionDefinition
    {
        public string fromWindowId;
        public string toWindowId;
        public string trigger;
        public TransitionAnimation animation;

        public UITransitionDefinition(string from, string to, string trigger, TransitionAnimation anim = TransitionAnimation.Fade)
        {
            this.fromWindowId = from;
            this.toWindowId = to;
            this.trigger = trigger;
            this.animation = anim;
        }
    }
}
