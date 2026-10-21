using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.SpriteGen.Editor
{
    public static class SpriteImportConfigurator
    {
        public static void ConfigureAsSingleSprite(
            string assetPath, bool pixelArt = false, Vector2? pivot = null)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            if (pivot.HasValue)
            {
                importer.spritePivot = pivot.Value;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot.Value;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }

        public static void ConfigureAsSpritesheet(
            string assetPath,
            List<SpritesheetPacker.SpriteRect> spriteRects,
            int spriteWidth,
            int spriteHeight,
            bool pixelArt = false,
            Vector2? pivot = null)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"TextureImporter не найден: {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            var pivotValue = pivot ?? new Vector2(0.5f, 0f);
            int alignment = pivot.HasValue ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.BottomCenter;

            var metaData = new SpriteMetaData[spriteRects.Count];
            for (int i = 0; i < spriteRects.Count; i++)
            {
                var sr = spriteRects[i];
                metaData[i] = new SpriteMetaData
                {
                    name = sr.name,
                    rect = sr.rect,
                    alignment = alignment,
                    pivot = pivotValue
                };
            }

            importer.spritesheet = metaData;
            importer.SaveAndReimport();
        }

        public static void ConfigureBatch(
            string[] assetPaths, bool pixelArt = false, Vector2? pivot = null)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in assetPaths)
                    ConfigureAsSingleSprite(path, pixelArt, pivot);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
        }
    }
}
