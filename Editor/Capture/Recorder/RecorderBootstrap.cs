// Packages/com.protosystem.core/Editor/Capture/Recorder/RecorderBootstrap.cs
// Компилируется ТОЛЬКО при наличии com.unity.recorder
using UnityEditor;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Автоматическая регистрация RecorderBridge при входе в Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class RecorderBootstrap
    {
        static RecorderBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += RegisterBridge;
            }
        }

        private static void RegisterBridge()
        {
            var system = CaptureSystem.Instance;
            if (system == null) return;

            system.SetRecorderBridge(new RecorderBridge());
        }
    }
}
