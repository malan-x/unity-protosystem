// Packages/com.protosystem.core/Editor/Capture/Recorder/RecorderBootstrap.cs
// Компилируется ТОЛЬКО при наличии com.unity.recorder
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor
{
    [InitializeOnLoad]
    internal static class RecorderBootstrap
    {
        private static int _retryCount;

        static RecorderBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Debug.Log("[RecorderBootstrap] EnteredPlayMode — ожидаю CaptureSystem.Instance...");
                _retryCount = 0;
                EditorApplication.update += PollAndRegister;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= PollAndRegister;
            }
        }

        private static void PollAndRegister()
        {
            var system = CaptureSystem.Instance;
            if (system != null)
            {
                system.SetRecorderBridge(new RecorderBridge());
                EditorApplication.update -= PollAndRegister;
                Debug.Log($"[RecorderBootstrap] RecorderBridge зарегистрирован (попытка {_retryCount})");
                return;
            }

            // Без лимита попыток: у больших проектов инициализация систем занимает
            // десятки секунд, и жёсткие 5 сек оставляли мост не привязанным.
            // Поллинг снимается при ExitingPlayMode.
            _retryCount++;
        }
    }
}
