// Packages/com.protosystem.core/Runtime/UI/Services/VirtualKeyboard.cs
using System;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Точка доступа к виртуальной клавиатуре.
    ///
    /// При наличии Steamworks.NET (define STEAMWORKS_NET) встроенный провайдер
    /// регистрируется автоматически через [RuntimeInitializeOnLoadMethod].
    ///
    /// Для кастомных платформ:
    /// <code>
    /// VirtualKeyboard.Register(new MyKeyboardProvider());
    /// </code>
    /// </summary>
    public static class VirtualKeyboard
    {
        private static IVirtualKeyboardProvider _provider;

        /// <summary>Зарегистрировать провайдер. Последний зарегистрированный побеждает.</summary>
        public static void Register(IVirtualKeyboardProvider provider)
        {
            _provider = provider;
            Debug.Log($"[VirtualKeyboard] Registered: {provider?.GetType().Name}");
        }

        /// <summary>Нужна ли виртуальная клавиатура прямо сейчас?</summary>
        public static bool IsNeeded => _provider != null && _provider.IsNeeded;

        /// <summary>
        /// Показать виртуальную клавиатуру если она нужна.
        /// Возвращает true если клавиатура была показана.
        /// </summary>
        public static bool TryShow(string currentText, int maxLength, Action<string> onResult)
        {
            if (_provider == null || !_provider.IsNeeded)
                return false;

            _provider.Show(currentText, maxLength, onResult);
            return true;
        }
    }
}
