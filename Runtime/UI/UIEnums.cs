// Packages/com.protosystem.core/Runtime/UI/UIEnums.cs
namespace ProtoSystem.UI
{
    /// <summary>
    /// Тип окна
    /// </summary>
    public enum WindowType
    {
        /// <summary>Обычное окно, заменяет предыдущее</summary>
        Normal,
        
        /// <summary>Модальное окно, блокирует взаимодействие с нижними</summary>
        Modal,
        
        /// <summary>Оверлей, отображается поверх без блокировки</summary>
        Overlay
    }

    /// <summary>
    /// Слой отображения (Z-order)
    /// </summary>
    public enum WindowLayer
    {
        /// <summary>Фоновые элементы</summary>
        Background = 0,
        
        /// <summary>HUD элементы</summary>
        HUD = 100,
        
        /// <summary>Обычные окна (меню)</summary>
        Windows = 200,
        
        /// <summary>Модальные окна</summary>
        Modals = 300,
        
        /// <summary>Тултипы</summary>
        Tooltips = 400,
        
        /// <summary>Уведомления (Toast)</summary>
        Notifications = 500,
        
        /// <summary>Системные (Loading, Fade)</summary>
        System = 1000
    }

    /// <summary>
    /// Анимация перехода
    /// </summary>
    public enum TransitionAnimation
    {
        None,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        Scale,
        Custom
    }

    /// <summary>
    /// Состояние окна
    /// </summary>
    public enum WindowState
    {
        /// <summary>Скрыто</summary>
        Hidden,
        
        /// <summary>Показывается (анимация)</summary>
        Showing,
        
        /// <summary>Видимо и активно</summary>
        Visible,
        
        /// <summary>Видимо, но не в фокусе (есть окно поверх)</summary>
        Blurred,
        
        /// <summary>Скрывается (анимация)</summary>
        Hiding
    }

    /// <summary>
    /// Результат навигации
    /// </summary>
    public enum NavigationResult
    {
        /// <summary>Успешный переход</summary>
        Success,
        
        /// <summary>Переход не разрешён в графе</summary>
        TransitionNotAllowed,
        
        /// <summary>Целевое окно не найдено</summary>
        WindowNotFound,
        
        /// <summary>Триггер не найден</summary>
        TriggerNotFound,
        
        /// <summary>Переход заблокирован модальным окном</summary>
        BlockedByModal,
        
        /// <summary>Уже на этом окне</summary>
        AlreadyOnWindow,
        
        /// <summary>Стек пуст (нельзя Back)</summary>
        StackEmpty
    }
}
