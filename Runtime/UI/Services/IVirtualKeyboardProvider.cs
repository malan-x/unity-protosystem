// Packages/com.protosystem.core/Runtime/UI/Services/IVirtualKeyboardProvider.cs
using System;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Провайдер виртуальной (экранной) клавиатуры.
    /// Реализуется в проекте-потребителе через платформенный API
    /// (Steamworks, консольный SDK и т.д.).
    ///
    /// Пакет содержит встроенную реализацию для Steamworks.NET —
    /// она активируется автоматически при наличии define STEAMWORKS_NET.
    /// Для других платформ зарегистрируйте свою реализацию:
    /// <code>
    /// VirtualKeyboard.Register(new MyConsoleKeyboardProvider());
    /// </code>
    /// </summary>
    public interface IVirtualKeyboardProvider
    {
        /// <summary>
        /// Нужна ли виртуальная клавиатура прямо сейчас?
        /// Например: Steam Deck, консоль без физической клавы, и т.д.
        /// </summary>
        bool IsNeeded { get; }

        /// <summary>
        /// Показать виртуальную клавиатуру.
        /// </summary>
        /// <param name="currentText">Текущий текст в поле ввода.</param>
        /// <param name="maxLength">Максимальная длина ввода.</param>
        /// <param name="onResult">Коллбэк с результатом. null если пользователь отменил.</param>
        void Show(string currentText, int maxLength, Action<string> onResult);
    }
}
