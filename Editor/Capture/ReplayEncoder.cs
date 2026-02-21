// Packages/com.protosystem.core/Editor/Capture/ReplayEncoder.cs
using System.IO;
using UnityEngine;
using UnityEditor.Media;

namespace ProtoSystem.Editor
{
    /// <summary>
    /// Кодирование replay buffer в MP4 через UnityEditor.Media.MediaEncoder.
    /// </summary>
    public static class ReplayEncoder
    {
        /// <summary>
        /// Закодировать содержимое ReplayBuffer в MP4 файл.
        /// </summary>
        public static string Encode(ReplayBuffer buffer, string outputPath)
        {
            if (buffer == null || buffer.Count == 0)
            {
                Debug.LogWarning("[Capture] Replay buffer пуст, нечего кодировать");
                return null;
            }

            var (width, height) = buffer.GetFrameDimensions();
            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning("[Capture] Невалидные размеры кадра в replay buffer");
                return null;
            }

            // H.264 требует чётные размеры
            width = width & ~1;
            height = height & ~1;

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Debug.Log($"[Capture] Кодирование replay: {buffer.Count} кадров, {width}x{height}, {buffer.Fps} fps");

            var videoAttrs = new VideoTrackAttributes
            {
                frameRate = new MediaRational(buffer.Fps, 1),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false
            };

            int framesAdded = 0;

            using (var encoder = new MediaEncoder(outputPath, videoAttrs))
            {
                buffer.ReadFramesInOrder((index, jpgData) =>
                {
                    // Декодируем JPEG в текстуру
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(jpgData))
                    {
                        Debug.LogWarning($"[Capture] Не удалось декодировать кадр {index}");
                        Object.DestroyImmediate(tex);
                        return;
                    }

                    // Blit через RT гарантирует RGBA32 + правильный размер
                    var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    Object.DestroyImmediate(tex);

                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var frameTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    frameTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    frameTex.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);

                    encoder.AddFrame(frameTex);
                    framesAdded++;
                    Object.DestroyImmediate(frameTex);
                });
            }

            if (framesAdded == 0)
            {
                Debug.LogWarning("[Capture] Ни один кадр не был закодирован");
                // Удалить пустой файл
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return null;
            }

            Debug.Log($"[Capture] Replay закодирован: {outputPath} ({framesAdded} кадров)");
            return outputPath;
        }
    }
}
