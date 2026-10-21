using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProtoSystem.ModelGen.Editor
{
    /// <summary>
    /// Рендер 3D-модели в спрайт-лист: N направлений (поворот вокруг Y) под углом
    /// камеры игры (топ-даун с наклоном), ортографика, прозрачный фон.
    /// Результат — PNG-атлас в одну строку, вход для пайплайна «Арт мобов».
    /// </summary>
    public static class ModelSpriteSheetRenderer
    {
        /// <param name="modelAssetPath">Assets-путь импортированной модели (GLB).</param>
        /// <param name="outAssetPath">Assets-путь итогового PNG.</param>
        /// <param name="directions">Число направлений (кадры по кругу).</param>
        /// <param name="cellSize">Размер кадра в пикселях.</param>
        /// <param name="elevationDeg">Наклон камеры: 90 — строго сверху.</param>
        public static string Render(string modelAssetPath, string outAssetPath,
            int directions = 8, int cellSize = 256, float elevationDeg = 55f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (prefab == null)
            {
                Debug.LogError($"[ModelStudio] Модель не загрузилась: {modelAssetPath}");
                return null;
            }

            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                preview.camera.orthographic = true;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;

                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                if (preview.lights.Length > 1) preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

                var instance = preview.InstantiatePrefabInScene(prefab);
                var bounds = ComputeBounds(instance);
                float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
                preview.camera.orthographicSize = radius * 1.1f;

                var sheet = new Texture2D(cellSize * directions, cellSize, TextureFormat.RGBA32, false);
                var rect = new Rect(0, 0, cellSize, cellSize);

                for (int i = 0; i < directions; i++)
                {
                    float azimuth = 360f * i / directions;
                    var rot = Quaternion.Euler(elevationDeg, azimuth, 0f);
                    preview.camera.transform.position = bounds.center + rot * Vector3.back * radius * 3f;
                    preview.camera.transform.rotation = rot;

                    preview.BeginPreview(rect, GUIStyle.none);
                    preview.Render(true);
                    var tex = preview.EndPreview() as RenderTexture;

                    var prev = RenderTexture.active;
                    RenderTexture.active = tex;
                    var cell = new Texture2D(cellSize, cellSize, TextureFormat.RGBA32, false);
                    cell.ReadPixels(new Rect(0, 0, cellSize, cellSize), 0, 0);
                    cell.Apply();
                    RenderTexture.active = prev;

                    sheet.SetPixels(i * cellSize, 0, cellSize, cellSize, cell.GetPixels());
                    Object.DestroyImmediate(cell);
                }
                sheet.Apply();

                string dir = Path.GetDirectoryName(outAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir)) ModelVariantStore.EnsureFolder(dir);
                File.WriteAllBytes(outAssetPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);

                AssetDatabase.ImportAsset(outAssetPath);
                if (AssetImporter.GetAtPath(outAssetPath) is TextureImporter imp)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Multiple;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.SaveAndReimport();
                }
                return outAssetPath;
            }
            finally
            {
                preview.Cleanup();
            }
        }

        /// <summary>
        /// Анимированный спрайт-лист: каждый AnimationClip модели — ряд из frames кадров,
        /// камера фиксирована (топ-даун, как в игре). Возвращает путь PNG и имена клипов
        /// по рядам (снизу вверх — порядок пикселей Unity).
        /// </summary>
        public static string RenderAnimated(string modelAssetPath, string outAssetPath,
            out string[] clipNames, int frames = 8, int cellSize = 256,
            float elevationDeg = 55f, float yawDeg = 180f)
        {
            clipNames = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            var clips = new List<AnimationClip>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(modelAssetPath))
                if (a is AnimationClip c && (c.hideFlags & HideFlags.HideInHierarchy) == 0)
                    clips.Add(c);
            if (prefab == null || clips.Count == 0)
            {
                Debug.LogError($"[ModelStudio] Нет клипов в модели: {modelAssetPath}");
                return null;
            }

            var preview = new PreviewRenderUtility();
            try
            {
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                preview.camera.orthographic = true;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                if (preview.lights.Length > 1) preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

                var instance = preview.InstantiatePrefabInScene(prefab);

                // Габариты по всей длине первого клипа — чтобы кадры не «плавали»
                var bounds = ComputeBounds(instance);
                clips[0].SampleAnimation(instance, 0f);
                bounds.Encapsulate(ComputeBounds(instance));
                float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
                preview.camera.orthographicSize = radius * 1.15f;

                var rot = Quaternion.Euler(elevationDeg, yawDeg, 0f);
                preview.camera.transform.position = bounds.center + rot * Vector3.back * radius * 3f;
                preview.camera.transform.rotation = rot;

                var sheet = new Texture2D(cellSize * frames, cellSize * clips.Count, TextureFormat.RGBA32, false);
                var rect = new Rect(0, 0, cellSize, cellSize);

                for (int row = 0; row < clips.Count; row++)
                {
                    var clip = clips[row];
                    for (int f = 0; f < frames; f++)
                    {
                        float t = clip.length * f / Mathf.Max(1, frames - 1);
                        clip.SampleAnimation(instance, t);

                        preview.BeginPreview(rect, GUIStyle.none);
                        preview.Render(true);
                        var tex = preview.EndPreview() as RenderTexture;

                        var prevRt = RenderTexture.active;
                        RenderTexture.active = tex;
                        var cell = new Texture2D(cellSize, cellSize, TextureFormat.RGBA32, false);
                        cell.ReadPixels(new Rect(0, 0, cellSize, cellSize), 0, 0);
                        cell.Apply();
                        RenderTexture.active = prevRt;

                        sheet.SetPixels(f * cellSize, row * cellSize, cellSize, cellSize, cell.GetPixels());
                        Object.DestroyImmediate(cell);
                    }
                }
                sheet.Apply();

                string dir = Path.GetDirectoryName(outAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir)) ModelVariantStore.EnsureFolder(dir);
                File.WriteAllBytes(outAssetPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);

                AssetDatabase.ImportAsset(outAssetPath);
                if (AssetImporter.GetAtPath(outAssetPath) is TextureImporter imp)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Multiple;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.SaveAndReimport();
                }

                clipNames = new string[clips.Count];
                for (int i = 0; i < clips.Count; i++) clipNames[i] = clips[i].name;
                return outAssetPath;
            }
            finally { preview.Cleanup(); }
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
