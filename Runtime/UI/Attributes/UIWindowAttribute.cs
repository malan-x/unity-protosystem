// Packages/com.protosystem.core/Runtime/UI/Attributes/UIWindowAttribute.cs
using System;

namespace ProtoSystem.UI
{
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
        
        /// <summary>Ставить игру на паузу при открытии</summary>
        public bool PauseGame { get; set; }
        
        /// <summary>Скрывать окна ниже</summary>
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
