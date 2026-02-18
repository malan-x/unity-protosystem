// Packages/com.protosystem.core/Runtime/Screenshot/ScreenshotConfig.cs
using UnityEngine;
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

    /// <summary>
    /// Конфигурация системы скриншотов
    /// </summary>
    [CreateAssetMenu(fileName = "ScreenshotConfig", menuName = "ProtoSystem/Screenshot Config")]
    public class ScreenshotConfig : ScriptableObject
    {
        [Header("Capture")]
        [Tooltip("Множитель разрешения (1 = нативное, 2 = x2, 4 = x4)")]
        [Range(1, 4)]
        public int superSampling = 1;

        [Tooltip("Формат сохранения")]
        public ScreenshotFormat format = ScreenshotFormat.PNG;

        [Tooltip("Качество JPG (1-100). Игнорируется для PNG")]
        [Range(1, 100)]
        public int jpgQuality = 90;

        [Header("Hotkeys")]
        [Tooltip("Скриншот с UI")]
#if PROTO_HAS_INPUT_SYSTEM
        public Key hotkeyWithUI = Key.F12;
#else
        public KeyCode hotkeyWithUI = KeyCode.F12;
#endif

        [Tooltip("Модификатор для скриншота без UI (модификатор + основная клавиша)")]
        public KeyModifier cleanModifier = KeyModifier.Shift;

        [Header("Output")]
        [Tooltip("Подпапка в persistentDataPath")]
        public string subfolder = "Screenshots";

        [Tooltip("Копировать в буфер обмена")]
        public bool copyToClipboard = true;

        [Header("Feedback")]
        [Tooltip("Воспроизводить звук при снятии")]
        public bool playSound = true;

        [Tooltip("ID звука из SoundLibrary")]
        public string soundId = "ui_success";
    }
}
