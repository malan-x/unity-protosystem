// Packages/com.protosystem.core/Runtime/UI/Attributes/UIWindowAttribute.cs
using System;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Режим курсора для окна
    /// </summary>
    public enum WindowCursorMode
    {
        /// <summary>Наследовать от предыдущего окна (не менять)</summary>
        Inherit = 0,
        /// <summary>Показать и разблокировать курсор</summary>
        Visible = 1,
        /// <summary>Скрыть и заблокировать в центре экрана</summary>
        Locked = 2,
        /// <summary>Видим, но ограничен границами окна</summary>
        Confined = 3
    }

    /// <summary>
    /// Атрибут для декларации окна UI.
    /// Позволяет Editor-сканеру собрать граф переходов до запуска.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UIWindowAttribute : Attribute
    {
        /// <summary>Уникальный ID окна</summary>
        public string WindowId { get; }
        
        /// <summary>Тип окна</summary>
        public WindowType Type { get; }
        
        /// <summary>Слой отображения</summary>
        public WindowLayer Layer { get; }
        
        /// <summary>
        /// Уровень иерархии окна.
        /// При открытии окна уровня N все окна уровня ≤ N закрываются.
        /// Level 0 = базовые окна (MainMenu, GameHUD) - взаимоисключающие.
        /// Level 1+ = вложенные окна (Settings внутри меню).
        /// Overlay и Modal игнорируют Level.
        /// </summary>
        public int Level { get; set; } = 0;
        
        /// <summary>Ставить игру на паузу при открытии</summary>
        public bool PauseGame { get; set; } = false;
        
        /// <summary>Режим курсора при открытии окна</summary>
        public WindowCursorMode CursorMode { get; set; } = WindowCursorMode.Visible;
        
        /// <summary>Скрывать окна ниже (deprecated, используйте Level)</summary>
        public bool HideBelow { get; set; } = true;
        
        /// <summary>Разрешить закрытие кнопкой Back/Escape</summary>
        public bool AllowBack { get; set; } = true;

        public UIWindowAttribute(string windowId, WindowType type = WindowType.Normal, WindowLayer layer = WindowLayer.Windows)
        {
            WindowId = windowId;
            Type = type;
            Layer = layer;
        }
    }

    /// <summary>
    /// Атрибут для декларации перехода из окна.
    /// Можно указать несколько на одном классе.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class UITransitionAttribute : Attribute
    {
        /// <summary>Имя триггера для Navigate()</summary>
        public string Trigger { get; }
        
        /// <summary>ID целевого окна</summary>
        public string ToWindowId { get; }
        
        /// <summary>Анимация перехода</summary>
        public TransitionAnimation Animation { get; set; } = TransitionAnimation.Fade;

        public UITransitionAttribute(string trigger, string toWindowId)
        {
            Trigger = trigger;
            ToWindowId = toWindowId;
        }
    }

    /// <summary>
    /// Атрибут для декларации глобального перехода (доступен из любого окна).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class UIGlobalTransitionAttribute : Attribute
    {
        /// <summary>Имя триггера</summary>
        public string Trigger { get; }
        
        /// <summary>ID целевого окна</summary>
        public string ToWindowId { get; }
        
        /// <summary>Анимация перехода</summary>
        public TransitionAnimation Animation { get; set; } = TransitionAnimation.Fade;

        public UIGlobalTransitionAttribute(string trigger, string toWindowId)
        {
            Trigger = trigger;
            ToWindowId = toWindowId;
        }
    }
}
