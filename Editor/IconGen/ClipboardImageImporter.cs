using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoSystem.IconGen.Editor
{
    /// <summary>
    /// Вставка картинки из системного буфера прямо в проект.
    ///
    /// У Unity нет API для КАРТИНКИ в буфере (EditorGUIUtility.systemCopyBuffer — только текст),
    /// поэтому картинку достаёт PowerShell через System.Windows.Forms.Clipboard. Это Windows-only;
    /// на других платформах остаётся запасной путь — если в буфере лежит ПУТЬ к файлу картинки,
    /// файл просто копируется.
    /// </summary>
    public static class ClipboardImageImporter
    {
        /// <summary>Размеры импорта: 0 — не ограничивать.</summary>
        public struct ImportOptions
        {
            public int maxSize;          // maxTextureSize импортёра (0 → 2048)
            public bool singleChannel;   // монохромные иконки — не нужно, оставлено на будущее
            public Vector4 spriteBorder; // 9-slice рамка (L,B,R,T) — для UI-подложек; Zero → обычный спрайт
        }

        /// <summary>
        /// Забрать картинку из буфера, положить по assetPath (Assets/...), импортировать как Sprite.
        /// Возвращает спрайт или null, если в буфере картинки нет.
        /// </summary>
        public static Sprite PasteAsSprite(string assetPath, ImportOptions options = default)
        {
            string absolute = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));

            Directory.CreateDirectory(Path.GetDirectoryName(absolute));

            if (!TryWriteClipboardImage(absolute) && !TryCopyClipboardFile(absolute))
            {
                Debug.LogWarning("[Буфер] В буфере обмена нет картинки. " +
                                 "Скопируйте изображение (Ctrl+C) или путь к PNG-файлу.");
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsSprite(assetPath, options);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                Debug.LogError($"[Буфер] Файл записан ({assetPath}), но спрайт не загрузился — проверьте импортёр.");

            return sprite;
        }

        /// <summary>
        /// Вытащить картинку из буфера в PNG-файл. Вся возня с форматами буфера —
        /// в ClipboardReader: раньше здесь стоял голый Clipboard.GetImage(), который
        /// теряет альфа-канал, и вставленные картинки приезжали с чёрным фоном.
        /// </summary>
        private static bool TryWriteClipboardImage(string absolutePath)
        {
            return ClipboardReader.IsImage(ClipboardReader.WriteImage(absolutePath));
        }

        /// <summary>Запасной путь: в буфере не картинка, а путь к файлу картинки.</summary>
        private static bool TryCopyClipboardFile(string absolutePath)
        {
            string text = EditorGUIUtility.systemCopyBuffer?.Trim().Trim('"');
            if (string.IsNullOrEmpty(text) || !File.Exists(text)) return false;

            string ext = Path.GetExtension(text).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") return false;

            File.Copy(text, absolutePath, overwrite: true);
            return true;
        }

        /// <summary>
        /// Забрать картинку из буфера как текстуру, ничего не сохраняя в проект
        /// (нужно, чтобы нарезать атлас на отдельные ячейки).
        /// Возвращает null, если в буфере картинки нет.
        /// </summary>
        public static Texture2D GrabClipboardTexture()
        {
            string temp = Path.Combine(Path.GetTempPath(), "lc_clipboard_grab.png");
            if (File.Exists(temp)) File.Delete(temp);

            if (!TryWriteClipboardImage(temp) && !TryCopyClipboardFile(temp))
            {
                Debug.LogWarning("[Буфер] В буфере обмена нет картинки.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(temp)))
            {
                Debug.LogError("[Буфер] Картинка из буфера не читается.");
                return null;
            }

            return texture;
        }

        /// <summary>Сохранить кусок текстуры в проект как спрайт.</summary>
        public static Sprite SaveRegionAsSprite(Texture2D source, RectInt region, string assetPath)
        {
            var pixels = source.GetPixels(region.x, region.y, region.width, region.height);
            var cell = new Texture2D(region.width, region.height, TextureFormat.RGBA32, false);
            cell.SetPixels(pixels);
            cell.Apply();

            string absolute = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, cell.EncodeToPNG());
            Object.DestroyImmediate(cell);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsSprite(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>Импортёр под UI-спрайт: без сжатия, с альфой, без мип-мапов.</summary>
        public static void ConfigureAsSprite(string assetPath, ImportOptions options = default)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = options.maxSize > 0 ? options.maxSize : 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 9-slice: рамка + FullRect-меш (иначе тайлинг тянет по прозрачному bbox, не по кадру).
            // Порт логики LastConvoyUIGenerator.Sprites.cs::ConfigureSpriteImport.
            if (options.spriteBorder != Vector4.zero)
            {
                importer.spriteBorder = options.spriteBorder;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }
    }
}
