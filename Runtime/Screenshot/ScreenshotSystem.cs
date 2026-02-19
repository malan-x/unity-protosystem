// Packages/com.protosystem.core/Runtime/Screenshot/ScreenshotSystem.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
#if PROTO_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProtoSystem
{
    /// <summary>
    /// Система скриншотов с поддержкой захвата с/без UI,
    /// суперсэмплинга, сохранения на диск и копирования в буфер обмена.
    /// </summary>
    [ProtoSystemComponent("Screenshot", "Захват скриншотов с/без UI", "Tools", "📸", 200)]
    public class ScreenshotSystem : InitializableSystemBase
    {
        #region InitializableSystemBase

        public override string SystemId => "screenshot";
        public override string DisplayName => "Screenshot System";
        public override string Description => "Захват скриншотов с/без UI, суперсэмплинг, сохранение на диск и буфер обмена.";

        #endregion

        protected override void InitEvents() { }

        #region Serialized Fields

        [Header("Configuration")]
        [SerializeField, InlineConfig] private ScreenshotConfig config;

        #endregion

        #region State

        private bool _capturing;
        private static ScreenshotSystem _instance;

        #endregion

        #region Properties

        public static ScreenshotSystem Instance => _instance;
        public ScreenshotConfig Config => config;
        public bool IsCapturing => _capturing;

        #endregion

        #region Initialization

        public override async Task<bool> InitializeAsync()
        {
            _instance = this;

            if (config == null)
            {
                LogWarning("ScreenshotConfig не назначен, используются значения по умолчанию");
                config = ScriptableObject.CreateInstance<ScreenshotConfig>();
            }

            EnsureDirectory();

            ReportProgress(1f);
            return true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        #endregion

        #region Update — Hotkey

        private void Update()
        {
            if (config == null || _capturing) return;
#if PROTO_HAS_INPUT_SYSTEM
            if (Keyboard.current == null) return;
            if (IsKeyPressed(config.hotkeyWithUI))
            {
                bool mod = IsModifierHeld(config.cleanModifier);
                TakeAndSave(!mod);
            }
#else
            if (Input.GetKeyDown(config.hotkeyWithUI))
            {
                bool mod = IsModifierHeldLegacy(config.cleanModifier);
                TakeAndSave(!mod);
            }
#endif
        }

        #endregion

        #region Public API

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
        /// Путь к папке скриншотов
        /// </summary>
        public string GetScreenshotDirectory()
        {
            return Path.Combine(Application.persistentDataPath, config.subfolder);
        }

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

        #region Capture Coroutine

        private IEnumerator CaptureCoroutine(bool includeUI, Action<Texture2D> onComplete, bool saveToFile)
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
                string path = SaveTexture(tex);
                LogRuntime($"Скриншот сохранён: {path}");
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

        private string SaveTexture(Texture2D tex)
        {
            EnsureDirectory();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string ext = config.format == ScreenshotFormat.PNG ? "png" : "jpg";
            string filename = $"screenshot_{timestamp}.{ext}";
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
                // EncodeToPNG гарантированно правильной ориентации (файл сохраняется корректно)
                byte[] pngData = tex.EncodeToPNG();
                if (pngData == null || pngData.Length == 0) return;

                uint cfPng = RegisterClipboardFormat("PNG");

                if (!OpenClipboard(IntPtr.Zero)) return;
                EmptyClipboard();

                // PNG format — поддерживается большинством современных приложений
                if (cfPng != 0)
                {
                    SetClipboardBlob(cfPng, pngData);
                }

                // CF_DIB как fallback для legacy-приложений
                SetClipboardDIB(tex);

                CloseClipboard();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Screenshot] Clipboard error: {e.Message}");
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

            // PNG roundtrip гарантирует правильную ориентацию независимо от графического API
            var corrected = new Texture2D(2, 2);
            corrected.LoadImage(tex.EncodeToPNG());

            int w = corrected.width;
            int h = corrected.height;
            var pixels = corrected.GetPixels32();
            UnityEngine.Object.DestroyImmediate(corrected);

            // BGRA 32-bit, bottom-up
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
            WriteInt32(header, 8, h); // положительный = bottom-up
            WriteInt16(header, 12, 1);
            WriteInt16(header, 14, 32);
            WriteInt32(header, 20, dataSize);
            Marshal.Copy(header, 0, ptr, 40);

            // LoadImage гарантирует GetPixels32 row 0 = bottom.
            // Bottom-up BMP byte 0 = bottom row. Прямое копирование.
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
