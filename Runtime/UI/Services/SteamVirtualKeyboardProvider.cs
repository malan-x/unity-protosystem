// Packages/com.protosystem.core/Runtime/UI/Services/SteamVirtualKeyboardProvider.cs
//
// Автоматически активируется при наличии Steamworks.NET в проекте.
// Define STEAMWORKS_NET добавляется пакетом com.rlabrecque.steamworks.net
// или вручную в Player Settings → Scripting Define Symbols.

#if STEAMWORKS_NET
using System;
using Steamworks;
using UnityEngine;

namespace ProtoSystem.UI
{
    /// <summary>
    /// Виртуальная клавиатура Steam (Game Input overlay).
    /// Работает на Steam Deck и при запуске Steam в Big Picture Mode.
    /// </summary>
    public class SteamVirtualKeyboardProvider : IVirtualKeyboardProvider
    {
        private Action<string> _pendingCallback;
        private Callback<GamepadTextInputDismissed_t> _callbackHandle;

        public bool IsNeeded
        {
            get
            {
                if (!SteamManager.Initialized) return false;

                // Steam Deck — всегда нужна
                if (SteamUtils.IsSteamRunningOnSteamDeck()) return true;

                // Big Picture Mode — геймпад без клавиатуры
                if (SteamUtils.IsSteamInBigPictureMode()) return true;

                return false;
            }
        }

        public void Show(string currentText, int maxLength, Action<string> onResult)
        {
            if (!SteamManager.Initialized)
            {
                onResult?.Invoke(null);
                return;
            }

            _pendingCallback = onResult;

            // Подписка на результат (если ещё не подписаны)
            _callbackHandle ??= Callback<GamepadTextInputDismissed_t>.Create(OnGamepadTextDismissed);

            bool shown = SteamUtils.ShowGamepadTextInput(
                EGamepadTextInputMode.k_EGamepadTextInputModeNormal,
                EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine,
                "", // description
                (uint)maxLength,
                currentText ?? "");

            if (!shown)
            {
                Debug.LogWarning("[SteamVirtualKeyboard] ShowGamepadTextInput returned false");
                _pendingCallback = null;
                onResult?.Invoke(null);
            }
        }

        private void OnGamepadTextDismissed(GamepadTextInputDismissed_t result)
        {
            var cb = _pendingCallback;
            _pendingCallback = null;

            if (!result.m_bSubmitted)
            {
                cb?.Invoke(null);
                return;
            }

            if (SteamUtils.GetEnteredGamepadTextInput(out string text, result.m_unSubmittedText + 1))
                cb?.Invoke(text);
            else
                cb?.Invoke(null);
        }

        /// <summary>
        /// Автоматическая регистрация при старте игры.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoRegister()
        {
            if (!SteamManager.Initialized) return;

            var provider = new SteamVirtualKeyboardProvider();
            VirtualKeyboard.Register(provider);

            if (provider.IsNeeded)
                Debug.Log("[SteamVirtualKeyboard] Auto-registered (Steam Deck / Big Picture detected)");
        }
    }
}
#endif
