// Packages/com.protosystem.core/Editor/Capture/ReplayEncoder.cs
using System.IO;
using UnityEngine;
using UnityEditor.Media;
using Unity.Collections;

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
        /// <param name="buffer">Replay buffer с JPEG-кадрами</param>
        /// <param name="outputPath">Путь к выходному .mp4 файлу</param>
        /// <returns>Путь к созданному файлу</returns>
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

            // Выравниваем размеры до чётных (требование H.264)
            width = width & ~1;
            height = height & ~1;

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var videoAttrs = new VideoTrackAttributes
            {
                frameRate = new MediaRational(buffer.Fps),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false
            };

            using (var encoder = new MediaEncoder(outputPath, videoAttrs))
            {
                var tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                buffer.ReadFramesInOrder((index, jpgData) =>
                {
                    tempTex.LoadImage(jpgData);

                    // Если размеры не совпадают с target — масштабируем
                    if (tempTex.width != width || tempTex.height != height)
                    {
                        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(tempTex, rt);
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        tempTex.Reinitialize(width, height);
                        tempTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tempTex.Apply();
                        RenderTexture.active = prev;
                        RenderTexture.ReleaseTemporary(rt);
                    }

                    encoder.AddFrame(tempTex);
                });

                Object.DestroyImmediate(tempTex);
            }

            Debug.Log($"[Capture] Replay закодирован: {outputPath} ({buffer.Count} кадров)");
            return outputPath;
        }
    }
}
