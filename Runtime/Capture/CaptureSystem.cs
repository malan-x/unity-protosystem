// Packages/com.protosystem.core/Runtime/Capture/CaptureSystem.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
#if PROTO_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProtoSystem
{
    /// <summary>
    /// Система захвата скриншотов и видео.
    /// Скриншоты работают в runtime и editor.
    /// Видеозапись — только в Editor Play Mode.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "ProtoSystem", null, "ScreenshotSystem")]
    [ProtoSystemComponent("Capture", "Захват скриншотов и видео", "Tools", "📸", 200)]
    public class CaptureSystem : InitializableSystemBase
    {
        #region InitializableSystemBase

        public override string SystemId => "capture";
        public override string DisplayName => "Capture System";
        public override string Description =>
            "Захват скриншотов и видео.\n" +
            "● Скриншот с UI: F12\n" +
            "● Скриншот без UI: Shift+F12\n" +
            "● Запись видео (старт/стоп): Ctrl+F9 (требует Unity Recorder)\n" +
            "● Сохранить replay buffer: Ctrl+F8\n" +
            "Видеозапись доступна только в Editor Play Mode.";

        #endregion

        protected override void InitEvents()
        {
            if (config == null || config.eventTriggers == null) return;
            foreach (var trigger in config.eventTriggers)
            {
                if (!trigger.enabled) continue;
                AddEvent(trigger.eventId, _ => OnEventTrigger(trigger));
            }
        }

        #region Serialized Fields

        [Header("Configuration")]
        [SerializeField, InlineConfig] private CaptureConfig config;

        #endregion

        #region State

        private bool _capturing;
        private static CaptureSystem _instance;

#if UNITY_EDITOR
        private RecordingState _recordingState = RecordingState.Idle;
        private ReplayBuffer _replayBuffer;
        private Coroutine _replayCoroutine;

        // Recorder bridge — registered by Editor assembly via SetRecorderBridge()
        private IRecorderBridge _recorderBridge;
        // Replay encoder — registered by Editor assembly via SetReplayEncoder()
        private Func<ReplayBuffer, string, string> _replayEncoder;
#endif

        #endregion

        #region Properties

        public static CaptureSystem Instance => _instance;
        public CaptureConfig Config => config;
        public bool IsCapturing => _capturing;

#if UNITY_EDITOR
        public RecordingState CurrentRecordingState => _recordingState;
#endif

        #endregion

#if UNITY_EDITOR
        #region Editor Bridge Registration

        /// <summary>
        /// Интерфейс для обёртки видеозаписи (реализуется в Editor assembly).
        /// </summary>
        public interface IRecorderBridge : IDisposable
        {
            bool IsRecording { get; }
            void StartRecording(string outputDir, string filename, int fps, float resScale);
            void StopRecording();
        }

        /// <summary>
        /// Регистрация обёртки Unity Recorder (вызывается из Editor assembly).
        /// </summary>
        public void SetRecorderBridge(IRecorderBridge bridge)
        {
            _recorderBridge = bridge;
            Debug.Log($"[CaptureSystem] RecorderBridge зарегистрирован: {bridge?.GetType().Name}");
        }

        /// <summary>
        /// Регистрация энкодера replay buffer (вызывается из Editor assembly).
        /// </summary>
        public void SetReplayEncoder(Func<ReplayBuffer, string, string> encoder)
        {
            _replayEncoder = encoder;
            Debug.Log($"[CaptureSystem] ReplayEncoder зарегистрирован: {encoder?.Method.DeclaringType?.Name}.{encoder?.Method.Name}");
        }

        #endregion
#endif

        #region Initialization

        public override async Task<bool> InitializeAsync()
        {
            _instance = this;
            Debug.Log($"[CaptureSystem] InitializeAsync. Instance set. Config={config != null}");

            if (config == null)
            {
                LogWarning("CaptureConfig не назначен, используются значения по умолчанию");
                config = ScriptableObject.CreateInstance<CaptureConfig>();
            }

            EnsureDirectory();

#if UNITY_EDITOR
            EnsureVideoDirectory();
            Debug.Log($"[CaptureSystem] VideoMode={config.videoMode}, RecorderBridge={_recorderBridge != null}, ReplayEncoder={_replayEncoder != null}");

            if (Application.isPlaying && config.videoMode == VideoRecordingMode.ReplayBuffer)
            {
                StartReplayBuffer();
            }
#endif

            ReportProgress(1f);
            return true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

#if UNITY_EDITOR
            CleanupVideo();
#endif
        }

        #endregion

        #region Update — Hotkey

        private void Update()
        {
            if (config == null) return;

#if PROTO_HAS_INPUT_SYSTEM
            if (Keyboard.current == null) return;

            // Screenshot hotkeys
            if (!_capturing && IsKeyPressed(config.hotkeyWithUI))
            {
                bool mod = IsModifierHeld(config.cleanModifier);
                TakeAndSave(!mod);
            }

#if UNITY_EDITOR
            // Video hotkeys (только в Play Mode)
            if (Application.isPlaying)
            {
                if (IsModifierHeld(config.videoModifier))
                {
                    if (IsKeyPressed(config.videoRecordToggle))
                        ToggleRecording();
                    else if (IsKeyPressed(config.replaySaveKey))
                        SaveReplayBuffer();
                }
            }
#endif

#else
            // Legacy Input
            if (!_capturing && Input.GetKeyDown(config.hotkeyWithUI))
            {
                bool mod = IsModifierHeldLegacy(config.cleanModifier);
                TakeAndSave(!mod);
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                if (IsModifierHeldLegacy(config.videoModifier))
                {
                    if (Input.GetKeyDown(config.videoRecordToggle))
                        ToggleRecording();
                    else if (Input.GetKeyDown(config.replaySaveKey))
                        SaveReplayBuffer();
                }
            }
#endif
#endif
        }

        #endregion

        #region Public API — Screenshots

        /// <summary>
        /// Сделать скриншот и получить Texture2D в callback.
        /// Вызывающий отвечает за Destroy текстуры.
        /// </summary>
        public void Take(bool includeUI, Action<Texture2D> onComplete)
        {
            if (_capturing)
            {
                LogWarning("Скриншот уже выполняется");
                return;
            }
            StartCoroutine(CaptureCoroutine(includeUI, onComplete, saveToFile: false));
        }

        /// <summary>
        /// Сделать скриншот, сохранить на диск и в буфер обмена.
        /// </summary>
        public void TakeAndSave(bool includeUI)
        {
            if (_capturing)
            {
                LogWarning("Скриншот уже выполняется");
                return;
            }
            StartCoroutine(CaptureCoroutine(includeUI, null, saveToFile: true));
        }

        /// <summary>
        /// Сделать скриншот по событию, сохранить с меткой события в имени файла.
        /// </summary>
        public void TakeAndSave(bool includeUI, string eventLabel)
        {
            if (_capturing) return;
            StartCoroutine(CaptureCoroutine(includeUI, null, saveToFile: true, eventLabel: eventLabel));
        }

        /// <summary>
        /// Путь к папке скриншотов
        /// </summary>
        public string GetScreenshotDirectory()
        {
            return Path.Combine(Application.persistentDataPath, config.subfolder);
        }

        #endregion

        #region Public API — Video (Editor Only)

#if UNITY_EDITOR
        /// <summary>
        /// Путь к папке видео
        /// </summary>
        public string GetVideoDirectory()
        {
            return Path.Combine(Application.persistentDataPath, config.videoSubfolder);
        }

        /// <summary>
        /// Начать ручную запись через Unity Recorder.
        /// </summary>
        public void StartRecording()
        {
            if (!Application.isPlaying)
            {
                LogWarning("Видеозапись доступна только в Play Mode");
                return;
            }

            if (_recordingState != RecordingState.Idle && _recordingState != RecordingState.ReplayBuffering)
            {
                LogWarning($"Невозможно начать запись в состоянии {_recordingState}");
                return;
            }

            if (_recorderBridge == null)
            {
                LogWarning("RecorderBridge не зарегистрирован. Unity Recorder недоступен.");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"capture_{timestamp}";

            _recorderBridge.StartRecording(GetVideoDirectory(), filename, config.videoFps, config.videoResolutionScale);
            _recordingState = RecordingState.Recording;

            EventBus.Publish(Evt.Capture.RecordingStarted, null);
            LogRuntime("Запись видео начата");
        }

        /// <summary>
        /// Остановить ручную запись.
        /// </summary>
        public void StopRecording()
        {
            if (_recordingState != RecordingState.Recording)
            {
                LogWarning("Нет активной записи");
                return;
            }

            _recorderBridge?.StopRecording();
            _recordingState = RecordingState.Idle;

            EventBus.Publish(Evt.Capture.RecordingStopped, null);
            LogRuntime("Запись видео остановлена");
        }

        /// <summary>
        /// Переключить старт/стоп записи.
        /// </summary>
        public void ToggleRecording()
        {
            if (_recordingState == RecordingState.Recording)
                StopRecording();
            else
                StartRecording();
        }

        /// <summary>
        /// Начать заполнение replay buffer.
        /// </summary>
        public void StartReplayBuffer()
        {
            if (!Application.isPlaying)
            {
                LogWarning("Replay buffer доступен только в Play Mode");
                return;
            }

            if (_replayCoroutine != null)
            {
                LogWarning("Replay buffer уже запущен");
                return;
            }

            _replayBuffer = new ReplayBuffer(config.videoFps, config.replayBufferSeconds);
            Debug.Log($"[CaptureSystem] ReplayBuffer создан: capacity={_replayBuffer.Capacity}, fps={config.videoFps}, seconds={config.replayBufferSeconds}");
            _replayCoroutine = StartCoroutine(ReplayBufferCoroutine());
            _recordingState = RecordingState.ReplayBuffering;

            LogRuntime($"Replay buffer запущен ({config.replayBufferSeconds} сек, {config.videoFps} fps)");
        }

        /// <summary>
        /// Остановить replay buffer.
        /// </summary>
        public void StopReplayBuffer()
        {
            if (_replayCoroutine != null)
            {
                StopCoroutine(_replayCoroutine);
                _replayCoroutine = null;
            }

            _replayBuffer?.Dispose();
            _replayBuffer = null;

            if (_recordingState == RecordingState.ReplayBuffering)
                _recordingState = RecordingState.Idle;

            LogRuntime("Replay buffer остановлен");
        }

        /// <summary>
        /// Закодировать replay buffer в MP4.
        /// </summary>
        public void SaveReplayBuffer()
        {
            Debug.Log($"[CaptureSystem] SaveReplayBuffer вызван. Buffer={_replayBuffer != null}, Coroutine={_replayCoroutine != null}, Encoder={_replayEncoder != null}, State={_recordingState}");

            if (_replayBuffer == null || _replayCoroutine == null)
            {
                // Автостарт replay buffer при первом нажатии
                StartReplayBuffer();
                LogRuntime("Replay buffer запущен. Подождите несколько секунд и нажмите Ctrl+F8 снова для сохранения.");
                return;
            }

            Debug.Log($"[CaptureSystem] Buffer: Count={_replayBuffer.Count}, Capacity={_replayBuffer.Capacity}, Dims={_replayBuffer.GetFrameDimensions()}, Memory={_replayBuffer.EstimatedMemoryBytes / 1024}KB");

            if (_replayBuffer.Count == 0)
            {
                LogWarning("Replay buffer пуст — подождите несколько секунд");
                return;
            }

            if (_recordingState == RecordingState.Encoding)
            {
                LogWarning("Кодирование уже выполняется");
                return;
            }

            if (_replayEncoder == null)
            {
                LogWarning("ReplayEncoder не зарегистрирован. CaptureEditorBootstrap не сработал?");
                return;
            }

            _recordingState = RecordingState.Encoding;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"replay_{timestamp}.mp4";
            string outputPath = Path.Combine(GetVideoDirectory(), filename);

            EnsureVideoDirectory();

            Debug.Log($"[CaptureSystem] Вызываю encoder: {_replayBuffer.Count} кадров → {outputPath}");
            string result = _replayEncoder(_replayBuffer, outputPath);

            _recordingState = _replayCoroutine != null ? RecordingState.ReplayBuffering : RecordingState.Idle;

            if (result != null)
            {
                EventBus.Publish(Evt.Capture.ReplaySaved, null);
                LogRuntime($"Replay сохранён: {result}");
            }
            else
            {
                Debug.LogWarning("[CaptureSystem] Encoder вернул null — файл не создан");
            }
        }

        private IEnumerator ReplayBufferCoroutine()
        {
            float frameInterval = 1f / config.videoFps;
            float lastCaptureTime = 0f;
            int totalCaptured = 0;

            Debug.Log($"[CaptureSystem] ReplayBufferCoroutine запущена. FPS={config.videoFps}, interval={frameInterval:F4}s, resScale={config.videoResolutionScale}");

            while (true)
            {
                yield return new WaitForEndOfFrame();

                float now = Time.realtimeSinceStartup;
                if (now - lastCaptureTime < frameInterval)
                    continue;

                lastCaptureTime = now;

                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(1);
                if (tex == null)
                {
                    if (totalCaptured == 0)
                        Debug.LogWarning("[CaptureSystem] CaptureScreenshotAsTexture вернул null!");
                    continue;
                }

                // Масштабирование если нужно
                if (config.videoResolutionScale < 1f)
                {
                    int w = (int)(tex.width * config.videoResolutionScale) & ~1;
                    int h = (int)(tex.height * config.videoResolutionScale) & ~1;

                    var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    Destroy(tex);

                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }

                _replayBuffer.PushFrame(tex, config.replayFrameQuality);
                totalCaptured++;

                if (totalCaptured == 1)
                    Debug.Log($"[CaptureSystem] Первый кадр захвачен: {tex.width}x{tex.height}, format={tex.format}");
                else if (totalCaptured % 300 == 0)
                    Debug.Log($"[CaptureSystem] Replay buffer: {totalCaptured} кадров захвачено, buffer count={_replayBuffer.Count}/{_replayBuffer.Capacity}, memory={_replayBuffer.EstimatedMemoryBytes / 1024}KB");

                Destroy(tex);
            }
        }

        private void EnsureVideoDirectory()
        {
            string dir = GetVideoDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private void CleanupVideo()
        {
            StopReplayBuffer();

            _recorderBridge?.Dispose();
            _recorderBridge = null;
            _replayEncoder = null;

            _recordingState = RecordingState.Idle;
        }
#endif

        #endregion

#if PROTO_HAS_INPUT_SYSTEM
        private bool IsKeyPressed(Key key)
        {
            try
            {
                return Keyboard.current[key].wasPressedThisFrame;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private bool IsModifierHeld(KeyModifier mod)
        {
            switch (mod)
            {
                case KeyModifier.Shift: return Keyboard.current.shiftKey.isPressed;
                case KeyModifier.Ctrl:  return Keyboard.current.ctrlKey.isPressed;
                case KeyModifier.Alt:   return Keyboard.current.altKey.isPressed;
                default: return false;
            }
        }
#else
        private bool IsModifierHeldLegacy(KeyModifier mod)
        {
            switch (mod)
            {
                case KeyModifier.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case KeyModifier.Ctrl:  return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case KeyModifier.Alt:   return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                default: return false;
            }
        }
#endif

        #region Event Triggers

        private void OnEventTrigger(CaptureEventTrigger trigger)
        {
            if (_capturing) return;

            bool isEditor = Application.isEditor;
            if (trigger.runtime == TriggerRuntime.EditorOnly && !isEditor) return;
            if (trigger.runtime == TriggerRuntime.BuildOnly && isEditor) return;

            if (trigger.delay > 0f)
                StartCoroutine(DelayedEventCapture(trigger));
            else
                TakeAndSave(trigger.includeUI, trigger.label);
        }

        private IEnumerator DelayedEventCapture(CaptureEventTrigger trigger)
        {
            yield return new WaitForSeconds(trigger.delay);
            TakeAndSave(trigger.includeUI, trigger.label);
        }

        #endregion

        #region Capture Coroutine

        private IEnumerator CaptureCoroutine(bool includeUI, Action<Texture2D> onComplete, bool saveToFile, string eventLabel = null)
        {
            _capturing = true;

            // Отключаем Canvas для режима без UI
            List<Canvas> disabledCanvases = null;
            if (!includeUI)
            {
                disabledCanvases = new List<Canvas>();
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.enabled)
                    {
                        canvas.enabled = false;
                        disabledCanvases.Add(canvas);
                    }
                }
            }

            yield return new WaitForEndOfFrame();

            // Захват
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(config.superSampling);

            // Возвращаем Canvas
            if (disabledCanvases != null)
            {
                foreach (var canvas in disabledCanvases)
                    if (canvas != null) canvas.enabled = true;
            }

            if (tex == null)
            {
                LogError("Не удалось захватить скриншот");
                _capturing = false;
                yield break;
            }

            // Сохранение
            if (saveToFile)
            {
                string path = SaveTexture(tex, eventLabel);
                LogRuntime($"Скриншот сохранён: {path}");
                EventBus.Publish(Evt.Capture.ScreenshotTaken, null);
            }

            // Буфер обмена
            if (config.copyToClipboard)
            {
                ClipboardHelper.CopyTexture(tex);
            }

            // Звук
            if (config.playSound && !string.IsNullOrEmpty(config.soundId))
            {
                try { Sound.SoundManagerSystem.Play(config.soundId); }
                catch { /* SoundManager может быть не инициализирован */ }
            }

            // Callback
            if (onComplete != null)
            {
                onComplete.Invoke(tex);
            }
            else
            {
                Destroy(tex);
            }

            _capturing = false;
        }

        #endregion

        #region Save

        private void EnsureDirectory()
        {
            string dir = GetScreenshotDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private string SaveTexture(Texture2D tex, string eventLabel = null)
        {
            EnsureDirectory();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string ext = config.format == ScreenshotFormat.PNG ? "png" : "jpg";
            string prefix = string.IsNullOrEmpty(eventLabel) ? "screenshot" : $"event_{eventLabel}";
            string filename = $"{prefix}_{timestamp}.{ext}";
            string path = Path.Combine(GetScreenshotDirectory(), filename);

            byte[] data;
            if (config.format == ScreenshotFormat.PNG)
                data = tex.EncodeToPNG();
            else
                data = tex.EncodeToJPG(config.jpgQuality);

            File.WriteAllBytes(path, data);
            return path;
        }

        #endregion
    }

    /// <summary>
    /// Копирование изображения в буфер обмена (Windows)
    /// </summary>
    public static class ClipboardHelper
    {
        public static void CopyTexture(Texture2D tex)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            CopyToClipboardWindows(tex);
#else
            Debug.Log("Буфер обмена не поддерживается на этой платформе");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint GMEM_MOVEABLE = 0x0002;

        private static void CopyToClipboardWindows(Texture2D tex)
        {
            try
            {
                byte[] pngData = tex.EncodeToPNG();
                if (pngData == null || pngData.Length == 0) return;

                uint cfPng = RegisterClipboardFormat("PNG");

                if (!OpenClipboard(IntPtr.Zero)) return;
                EmptyClipboard();

                if (cfPng != 0)
                {
                    SetClipboardBlob(cfPng, pngData);
                }

                SetClipboardDIB(tex);

                CloseClipboard();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Capture] Clipboard error: {e.Message}");
            }
        }

        private static void SetClipboardBlob(uint format, byte[] data)
        {
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
            if (hGlobal == IntPtr.Zero) return;
            IntPtr ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero) return;
            Marshal.Copy(data, 0, ptr, data.Length);
            GlobalUnlock(hGlobal);
            SetClipboardData(format, hGlobal);
        }

        private static void SetClipboardDIB(Texture2D tex)
        {
            const uint CF_DIB = 8;

            var corrected = new Texture2D(2, 2);
            corrected.LoadImage(tex.EncodeToPNG());

            int w = corrected.width;
            int h = corrected.height;
            var pixels = corrected.GetPixels32();
            UnityEngine.Object.DestroyImmediate(corrected);

            int stride = w * 4;
            int dataSize = stride * h;
            int totalSize = 40 + dataSize;

            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)totalSize);
            if (hGlobal == IntPtr.Zero) return;
            IntPtr ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero) return;

            byte[] header = new byte[40];
            WriteInt32(header, 0, 40);
            WriteInt32(header, 4, w);
            WriteInt32(header, 8, h);
            WriteInt16(header, 12, 1);
            WriteInt16(header, 14, 32);
            WriteInt32(header, 20, dataSize);
            Marshal.Copy(header, 0, ptr, 40);

            byte[] pixelData = new byte[dataSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                int dst = i * 4;
                pixelData[dst + 0] = c.b;
                pixelData[dst + 1] = c.g;
                pixelData[dst + 2] = c.r;
                pixelData[dst + 3] = c.a;
            }
            Marshal.Copy(pixelData, 0, IntPtr.Add(ptr, 40), dataSize);
            GlobalUnlock(hGlobal);
            SetClipboardData(CF_DIB, hGlobal);
        }

        private static void WriteInt32(byte[] buf, int offset, int value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] buf, int offset, short value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

#endif
    }
}
