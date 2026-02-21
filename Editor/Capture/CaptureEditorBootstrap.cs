// Packages/com.protosystem.core/Editor/Capture/CaptureEditorBootstrap.cs
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.Editor
{
    [InitializeOnLoad]
    internal static class CaptureEditorBootstrap
    {
        private static int _retryCount;

        static CaptureEditorBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Debug.Log("[CaptureBootstrap] EnteredPlayMode — ожидаю CaptureSystem.Instance...");
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
                system.SetReplayEncoder(ReplayEncoder.Encode);
                EditorApplication.update -= PollAndRegister;
                Debug.Log($"[CaptureBootstrap] ReplayEncoder зарегистрирован (попытка {_retryCount})");
                return;
            }

            _retryCount++;
            if (_retryCount > 300)
            {
                EditorApplication.update -= PollAndRegister;
                Debug.LogWarning("[CaptureBootstrap] CaptureSystem.Instance не найден за 5 сек — ReplayEncoder НЕ зарегистрирован");
            }
        }
    }
}
