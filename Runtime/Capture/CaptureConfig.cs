// Packages/com.protosystem.core/Runtime/Capture/CaptureConfig.cs
using UnityEngine;
using UnityEngine.Serialization;
#if PROTO_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProtoSystem
{
    public enum KeyModifier
    {
        Shift,
        Ctrl,
        Alt
    }

    public enum ScreenshotFormat
    {
        PNG,
        JPG
    }

#if UNITY_EDITOR
    public enum VideoRecordingMode
    {
        Manual,
        ReplayBuffer
    }

    public enum RecordingState
    {
        Idle,
        Recording,
        ReplayBuffering,
        Encoding
    }
#endif

    /// <summary>
    /// Конфигурация системы захвата (скриншоты и видео)
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "ProtoSystem", null, "ScreenshotConfig")]
    [CreateAssetMenu(fileName = "CaptureConfig", menuName = "ProtoSystem/Capture Config")]
    public class CaptureConfig : ScriptableObject
    {
        // ─── Screenshots ───

        [Header("Screenshot — Capture")]
        [Tooltip("Множитель разрешения (1 = нативное, 2 = x2, 4 = x4)")]
        [Range(1, 4)]
        public int superSampling = 1;

        [Tooltip("Формат сохранения")]
        public ScreenshotFormat format = ScreenshotFormat.PNG;

        [Tooltip("Качество JPG (1-100). Игнорируется для PNG")]
        [Range(1, 100)]
        public int jpgQuality = 90;

        [Header("Screenshot — Hotkeys")]
        [Tooltip("Скриншот с UI")]
#if PROTO_HAS_INPUT_SYSTEM
        public Key hotkeyWithUI = Key.F12;
#else
        public KeyCode hotkeyWithUI = KeyCode.F12;
#endif

        [Tooltip("Модификатор для скриншота без UI (модификатор + основная клавиша)")]
        public KeyModifier cleanModifier = KeyModifier.Shift;

        [Header("Screenshot — Output")]
        [Tooltip("Подпапка в persistentDataPath")]
        public string subfolder = "Screenshots";

        [Tooltip("Копировать в буфер обмена")]
        public bool copyToClipboard = true;

        [Header("Screenshot — Feedback")]
        [Tooltip("Воспроизводить звук при снятии")]
        public bool playSound = true;

        [Tooltip("ID звука из SoundLibrary")]
        public string soundId = "ui_success";

        // ─── Video (Editor Only) ───

#if UNITY_EDITOR
        [Header("Video — General")]
        [Tooltip("Режим видеозаписи: ручной старт/стоп или replay buffer")]
        public VideoRecordingMode videoMode = VideoRecordingMode.Manual;

        [Tooltip("FPS видеозаписи")]
        [Range(15, 60)]
        public int videoFps = 30;

        [Tooltip("Подпапка для видео в persistentDataPath")]
        public string videoSubfolder = "Videos";

        [Header("Video — Quality")]
        [Tooltip("Масштаб разрешения видео (1 = нативное)")]
        [Range(0.25f, 1f)]
        public float videoResolutionScale = 1f;

        [Header("Video — Replay Buffer")]
        [Tooltip("Длительность replay buffer в секундах")]
        [Range(5, 120)]
        public int replayBufferSeconds = 30;

        [Tooltip("Качество JPEG для кадров replay buffer (50-100)")]
        [Range(50, 100)]
        public int replayFrameQuality = 75;

        [Header("Video — Hotkeys")]
        [Tooltip("Клавиша старт/стоп записи (с модификатором)")]
#if PROTO_HAS_INPUT_SYSTEM
        public Key videoRecordToggle = Key.F9;
        [Tooltip("Клавиша сохранения replay buffer (с модификатором)")]
        public Key replaySaveKey = Key.F8;
#else
        public KeyCode videoRecordToggle = KeyCode.F9;
        [Tooltip("Клавиша сохранения replay buffer (с модификатором)")]
        public KeyCode replaySaveKey = KeyCode.F8;
#endif
        [Tooltip("Модификатор для видео-хоткеев")]
        public KeyModifier videoModifier = KeyModifier.Ctrl;

        /// <summary>
        /// Исправляет дефолтные значения для полей, добавленных после создания ассета.
        /// </summary>
        private void OnValidate()
        {
#if PROTO_HAS_INPUT_SYSTEM
            if (videoRecordToggle == Key.None) videoRecordToggle = Key.F9;
            if (replaySaveKey == Key.None) replaySaveKey = Key.F8;
#else
            if (videoRecordToggle == KeyCode.None) videoRecordToggle = KeyCode.F9;
            if (replaySaveKey == KeyCode.None) replaySaveKey = KeyCode.F8;
#endif
            if (videoFps == 0) videoFps = 30;
            if (replayBufferSeconds == 0) replayBufferSeconds = 30;
            if (replayFrameQuality == 0) replayFrameQuality = 75;
            if (string.IsNullOrEmpty(videoSubfolder)) videoSubfolder = "Videos";
        }
#endif
    }
}
