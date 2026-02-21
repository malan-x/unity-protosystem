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

            _retryCount++;
            if (_retryCount > 300)
            {
                EditorApplication.update -= PollAndRegister;
                Debug.LogWarning("[RecorderBootstrap] CaptureSystem.Instance не найден за 5 сек — RecorderBridge НЕ зарегистрирован");
            }
        }
    }
}
