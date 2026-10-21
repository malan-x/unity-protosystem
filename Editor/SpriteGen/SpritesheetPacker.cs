using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.SpriteGen.Editor
{
    public static class SpritesheetPacker
    {
        public struct SpritesheetResult
        {
            public Texture2D atlas;
            public List<SpriteRect> spriteRects;
        }

        public struct SpriteRect
        {
            public string name;
            public Rect rect;
            public int row;
            public int col;
        }

        public static SpritesheetResult PackGrid(
            Texture2D[] textures,
            int columns,
            int spriteWidth,
            int spriteHeight,
            string[] names = null)
        {
            int count = textures.Length;
            int rows = Mathf.CeilToInt((float)count / columns);

            int atlasWidth = columns * spriteWidth;
            int atlasHeight = rows * spriteHeight;

            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            var clearPixels = new Color[atlasWidth * atlasHeight];
            atlas.SetPixels(clearPixels);

            var spriteRects = new List<SpriteRect>();

            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = rows - 1 - (i / columns);

                int x = col * spriteWidth;
                int y = row * spriteHeight;

                var pixels = textures[i].GetPixels();
                atlas.SetPixels(x, y, spriteWidth, spriteHeight, pixels);

                string spriteName = names != null && i < names.Length
                    ? names[i]
                    : $"sprite_{i}";

                spriteRects.Add(new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(x, y, spriteWidth, spriteHeight),
                    row = i / columns,
                    col = col
                });
            }

            atlas.Apply();

            return new SpritesheetResult
            {
                atlas = atlas,
                spriteRects = spriteRects
            };
        }

        public static SpritesheetResult PackAnimationGrid(
            List<AnimationSampler.AnimationFrame> frames,
            float[] angles,
            int framesPerAnimation,
            int spriteWidth,
            int spriteHeight,
            string[] customNames = null)
        {
            int columns = framesPerAnimation;
            int rows = angles.Length;

            var textures = new Texture2D[rows * columns];
            var names = new string[rows * columns];

            for (int i = 0; i < frames.Count && i < textures.Length; i++)
            {
                textures[i] = frames[i].texture;
                names[i] = (customNames != null && i < customNames.Length)
                    ? customNames[i]
                    : $"{frames[i].animationName}_angle{frames[i].angle:F0}_f{frames[i].frameIndex:D2}";
            }

            for (int i = frames.Count; i < textures.Length; i++)
            {
                textures[i] = new Texture2D(spriteWidth, spriteHeight, TextureFormat.RGBA32, false);
                names[i] = $"empty_{i}";
            }

            return PackGrid(textures, columns, spriteWidth, spriteHeight, names);
        }

        public static SpritesheetResult PackGridPowerOfTwo(
            Texture2D[] textures,
            int spriteWidth,
            int spriteHeight,
            string[] names = null,
            int maxAtlasSize = 8192)
        {
            int count = textures.Length;
            if (count == 0)
                return new SpritesheetResult
                {
                    atlas = new Texture2D(1, 1, TextureFormat.RGBA32, false),
                    spriteRects = new List<SpriteRect>()
                };

            var (atlasWidth, atlasHeight) = FindOptimalPOTSize(
                count, spriteWidth, spriteHeight, maxAtlasSize);

            int columns = atlasWidth / spriteWidth;
            int rows = atlasHeight / spriteHeight;

            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            var clearPixels = new Color[atlasWidth * atlasHeight];
            atlas.SetPixels(clearPixels);

            var spriteRects = new List<SpriteRect>();

            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = rows - 1 - (i / columns);

                int x = col * spriteWidth;
                int y = row * spriteHeight;

                var pixels = textures[i].GetPixels();
                atlas.SetPixels(x, y, spriteWidth, spriteHeight, pixels);

                string spriteName = names != null && i < names.Length
                    ? names[i]
                    : $"sprite_{i}";

                spriteRects.Add(new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(x, y, spriteWidth, spriteHeight),
                    row = i / columns,
                    col = col
                });
            }

            atlas.Apply();

            return new SpritesheetResult
            {
                atlas = atlas,
                spriteRects = spriteRects
            };
        }

        /// <summary>
        /// Finds minimum power-of-two atlas dimensions that fit the given number of sprites.
        /// Prefers squarer textures when areas are equal.
        /// </summary>
        private static (int width, int height) FindOptimalPOTSize(
            int spriteCount, int spriteWidth, int spriteHeight, int maxSize)
        {
            int bestW = maxSize, bestH = maxSize;
            long bestArea = (long)maxSize * maxSize;

            for (int w = Mathf.NextPowerOfTwo(spriteWidth); w <= maxSize; w *= 2)
            {
                int cols = w / spriteWidth;
                if (cols <= 0) continue;

                int rowsNeeded = Mathf.CeilToInt((float)spriteCount / cols);
                int h = Mathf.NextPowerOfTwo(rowsNeeded * spriteHeight);
                if (h > maxSize) continue;

                int actualRows = h / spriteHeight;
                if (cols * actualRows < spriteCount) continue;

                long area = (long)w * h;
                if (area < bestArea ||
                    (area == bestArea && Mathf.Abs(w - h) < Mathf.Abs(bestW - bestH)))
                {
                    bestW = w;
                    bestH = h;
                    bestArea = area;
                }
            }

            return (bestW, bestH);
        }

        public static string SaveSpritesheet(SpritesheetResult result, string path)
        {
            ModelRenderer.SaveTextureAsPNG(result.atlas, path);
            AssetDatabase.Refresh();
            return path;
        }
    }
}
