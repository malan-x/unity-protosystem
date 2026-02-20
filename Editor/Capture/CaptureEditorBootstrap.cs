// Packages/com.protosystem.core/Editor/Capture/CaptureEditorBootstrap.cs
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Автоматическая регистрация ReplayEncoder для CaptureSystem при входе в Play Mode.
    /// RecorderBridge регистрируется отдельно из ProtoSystem.Editor.Recorder (если com.unity.recorder установлен).
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
                EditorApplication.delayCall += RegisterEncoder;
            }
        }

        private static void RegisterEncoder()
        {
            var system = CaptureSystem.Instance;
            if (system == null) return;

            system.SetReplayEncoder(ReplayEncoder.Encode);
        }
    }
}
