// Packages/com.protosystem.core/Editor/Publishing/StoreAssets/ClipboardImageHelper.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Хелпер для работы с изображениями в буфере обмена (Windows)
    /// </summary>
    public static class ClipboardImageHelper
    {
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

        private const uint CF_BITMAP = 2;
        private const uint CF_DIB = 8;
        private const uint DIB_RGB_COLORS = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] bmiColors;
        }
#endif

        /// <summary>
        /// Проверить наличие изображения в буфере обмена
        /// </summary>
        public static bool HasImage()
        {
#if UNITY_EDITOR_WIN
            return IsClipboardFormatAvailable(CF_BITMAP) || IsClipboardFormatAvailable(CF_DIB);
#else
            return false;
#endif
        }

        /// <summary>
        /// Получить изображение из буфера обмена
        /// </summary>
        public static Texture2D GetImage()
        {
#if UNITY_EDITOR_WIN
            if (!OpenClipboard(IntPtr.Zero))
                return null;

            try
            {
                if (!IsClipboardFormatAvailable(CF_BITMAP))
                    return null;

                IntPtr hBitmap = GetClipboardData(CF_BITMAP);
                if (hBitmap == IntPtr.Zero)
                    return null;

                return BitmapToTexture2D(hBitmap);
            }
            finally
            {
                CloseClipboard();
            }
#else
            Debug.LogWarning("Clipboard image paste is only supported on Windows");
            return null;
#endif
        }

#if UNITY_EDITOR_WIN
        private static Texture2D BitmapToTexture2D(IntPtr hBitmap)
        {
            BITMAP bmp = new BITMAP();
            GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bmp);

            int width = bmp.bmWidth;
            int height = bmp.bmHeight;

            if (width <= 0 || height <= 0)
                return null;

            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB
            bmi.bmiColors = new uint[256];

            byte[] pixels = new byte[width * height * 4];

            IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
            try
            {
                GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
            }
            finally
            {
                DeleteDC(hdc);
            }

            // Convert BGRA to RGBA
            // Note: biHeight is negative so DIB is top-down, Unity textures are bottom-up
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] colors = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = (y * width + x) * 4;
                    // Unity textures are bottom-up, DIB with negative height is top-down
                    // So we need to flip Y
                    int destIndex = ((height - 1 - y) * width + x);
                    colors[destIndex] = new Color32(
                        pixels[srcIndex + 2], // R (was B)
                        pixels[srcIndex + 1], // G
                        pixels[srcIndex + 0], // B (was R)
                        255  // Force full alpha (clipboard bitmaps often have garbage in alpha)
                    );
                }
            }

            texture.SetPixels32(colors);
            texture.Apply();

            return texture;
        }
#endif

        /// <summary>
        /// Сохранить текстуру как PNG
        /// </summary>
        public static bool SaveAsPng(Texture2D texture, string path)
        {
            if (texture == null || string.IsNullOrEmpty(path))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClipboardImageHelper] Failed to save image: {ex.Message}");
                return false;
            }
        }
    }
}
