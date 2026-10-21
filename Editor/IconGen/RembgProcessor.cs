using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Мост к локальному rembg: убрать фон у картинки, обрезать по объекту, отцентрировать и ужать
    /// до нужного размера. Композицию рисует генератор — здесь только чистка.
    ///
    /// rembg крутится на CPU (~4 c на картинку, VRAM не занимает — не конфликтует с ComfyUI).
    /// Пути к python/скрипту — в AiToolsSettings (не хардкод, переносимо между машинами).
    /// Вызывается синхронно: для редакторного инструмента заморозка на пару секунд приемлема,
    /// на пачке — с прогресс-баром между картинками.
    /// </summary>
    public static class RembgProcessor
    {
        private static string Python => AiToolsSettings.RembgPython;
        private static string Script => AiToolsSettings.RembgScript;

        public static bool IsAvailable => File.Exists(Python) && File.Exists(Script);

        /// <summary>
        /// Обработать файл: srcAbsolute → dstAbsolute (оба абсолютные пути).
        /// noBg=true — фон уже прозрачный, только кроп/ресайз.
        /// </summary>
        public static bool Process(string srcAbsolute, string dstAbsolute,
                                   int width, int height, float pad = 0.08f, bool noBg = false)
        {
            if (!IsAvailable)
            {
                Debug.LogError($"[rembg] Не найден rembg ({Python} / {Script}). " +
                               "Проверьте пути в Project Settings ▸ AI Icon Tools.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dstAbsolute));

            var pad_s = pad.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string args =
                $"\"{Script}\" --input \"{srcAbsolute}\" --output \"{dstAbsolute}\" " +
                $"--width {width} --height {height} --pad {pad_s} " +
                $"--model \"{AiToolsSettings.RembgModel}\"" + (noBg ? " --no-bg" : "");

            var info = new ProcessStartInfo
            {
                FileName = Python,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            try
            {
                using var process = System.Diagnostics.Process.Start(info);
                if (process == null) { Debug.LogError("[rembg] Процесс не запустился."); return false; }

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(120000);

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[rembg] код {process.ExitCode}: {stderr.Trim()}");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[rembg] {e.Message}");
                return false;
            }

            return File.Exists(dstAbsolute);
        }

        /// <summary>
        /// Полный путь ассета → обработать на месте (через временный файл) и переимпортировать
        /// как спрайт. Возвращает спрайт или null.
        /// </summary>
        public static Sprite ProcessAssetInPlace(string assetPath, int width, int height,
                                                 float pad = 0.08f, bool noBg = false)
        {
            string absolute = ToAbsolute(assetPath);
            if (!File.Exists(absolute))
            {
                Debug.LogError($"[rembg] Нет файла: {assetPath}");
                return null;
            }

            string temp = Path.Combine(Path.GetTempPath(), "lc_rembg_in.png");
            File.Copy(absolute, temp, overwrite: true);

            bool ok = Process(temp, absolute, width, height, pad, noBg);
            if (!ok) return null;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(assetPath,
                new ClipboardImageImporter.ImportOptions { maxSize = Mathf.Max(width, height) });
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>Сырые байты картинки → обработать → сохранить спрайтом по assetPath.</summary>
        public static Sprite ProcessBytes(byte[] png, string assetPath, int width, int height,
                                          float pad = 0.08f, bool noBg = false)
        {
            string temp = Path.Combine(Path.GetTempPath(), "lc_rembg_in.png");
            File.WriteAllBytes(temp, png);

            string dst = ToAbsolute(assetPath);
            if (!Process(temp, dst, width, height, pad, noBg)) return null;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ClipboardImageImporter.ConfigureAsSprite(assetPath,
                new ClipboardImageImporter.ImportOptions { maxSize = Mathf.Max(width, height) });
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static string ToAbsolute(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
