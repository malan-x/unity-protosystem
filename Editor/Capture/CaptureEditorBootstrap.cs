// Packages/com.protosystem.core/Editor/Capture/CaptureEditorBootstrap.cs
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Автоматическая регистрация editor-only зависимостей для CaptureSystem
    /// при входе в Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class CaptureEditorBootstrap
    {
        static CaptureEditorBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Даём кадр на инициализацию систем, потом регистрируем bridge
                EditorApplication.delayCall += RegisterBridges;
            }
        }

        private static void RegisterBridges()
        {
            var system = CaptureSystem.Instance;
            if (system == null) return;

            system.SetRecorderBridge(new RecorderBridge());
            system.SetReplayEncoder(ReplayEncoder.Encode);
        }
    }
}
