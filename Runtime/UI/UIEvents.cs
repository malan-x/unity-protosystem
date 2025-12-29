// Packages/com.protosystem.core/Runtime/UI/UIEvents.cs
namespace ProtoSystem
{
    /// <summary>
    /// События UI системы для EventBus
    /// Номера начинаются с 10200
    /// </summary>
    public static partial class EventBus
    {
        public static partial class UI
        {
            // Навигация (10200-10209)
            /// <summary>Окно открыто. Data: WindowEventData</summary>
            public const int WindowOpened = 10200;
            /// <summary>Окно закрыто. Data: WindowEventData</summary>
            public const int WindowClosed = 10201;
            /// <summary>Окно получило фокус. Data: WindowEventData</summary>
            public const int WindowFocused = 10202;
            /// <summary>Окно потеряло фокус. Data: WindowEventData</summary>
            public const int WindowBlurred = 10203;
            /// <summary>Навигация выполнена. Data: NavigationEventData</summary>
            public const int NavigationCompleted = 10204;
            /// <summary>Навигация отклонена. Data: NavigationEventData</summary>
            public const int NavigationFailed = 10205;
            /// <summary>Нажата кнопка Back. Data: null</summary>
            public const int BackPressed = 10206;

            // Диалоги (10210-10219)
            /// <summary>Диалог показан. Data: DialogEventData</summary>
            public const int DialogShown = 10210;
            /// <summary>Диалог закрыт. Data: DialogEventData</summary>
            public const int DialogClosed = 10211;
            /// <summary>Диалог подтверждён. Data: DialogEventData</summary>
            public const int DialogConfirmed = 10212;
            /// <summary>Диалог отменён. Data: DialogEventData</summary>
            public const int DialogCancelled = 10213;

            // Toast/Notifications (10220-10229)
            /// <summary>Toast показан. Data: ToastEventData</summary>
            public const int ToastShown = 10220;
            /// <summary>Toast скрыт. Data: ToastEventData</summary>
            public const int ToastHidden = 10221;

            // Tooltip (10230-10239)
            /// <summary>Tooltip показан. Data: TooltipEventData</summary>
            public const int TooltipShown = 10230;
            /// <summary>Tooltip скрыт. Data: TooltipEventData</summary>
            public const int TooltipHidden = 10231;

            // Progress (10240-10249)
            /// <summary>Progress показан. Data: ProgressEventData</summary>
            public const int ProgressShown = 10240;
            /// <summary>Progress обновлён. Data: ProgressEventData</summary>
            public const int ProgressUpdated = 10241;
            /// <summary>Progress скрыт. Data: ProgressEventData</summary>
            public const int ProgressHidden = 10242;
        }
    }
}

namespace ProtoSystem.UI
{
    /// <summary>
    /// Данные события окна
    /// </summary>
    public struct WindowEventData
    {
        public string WindowId;
        public WindowType Type;
        public WindowLayer Layer;
    }

    /// <summary>
    /// Данные события навигации
    /// </summary>
    public struct NavigationEventData
    {
        public string FromWindowId;
        public string ToWindowId;
        public string Trigger;
        public NavigationResult Result;
    }

    /// <summary>
    /// Данные события диалога
    /// </summary>
    public struct DialogEventData
    {
        public string DialogId;
        public string Message;
        public bool Confirmed;
        public string InputValue; // Для Input диалогов
        public int SelectedIndex; // Для Choice диалогов
    }

    /// <summary>
    /// Данные события Toast
    /// </summary>
    public struct ToastEventData
    {
        public string ToastId;
        public string Message;
        public float Duration;
    }

    /// <summary>
    /// Данные события Tooltip
    /// </summary>
    public struct TooltipEventData
    {
        public string TooltipId;
        public string Text;
    }

    /// <summary>
    /// Данные события Progress
    /// </summary>
    public struct ProgressEventData
    {
        public string ProgressId;
        public float Value;      // 0-1
        public string Message;
        public bool IsIndeterminate;
    }
}
