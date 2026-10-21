using UnityEngine;
#if PROTO_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ProtoSystem.SpriteGen
{
    public static class StyleApplier
    {
        public const int IsolationLayer = 31;

        public static Shader ResolveStyleShader(RenderStyle style)
        {
            switch (style)
            {
                case RenderStyle.Toon:       return Shader.Find("ProtoSystem/SpriteGen/ToonLit");
                case RenderStyle.Silhouette: return Shader.Find("ProtoSystem/SpriteGen/Silhouette");
                case RenderStyle.TopDownLit: return Shader.Find("ProtoSystem/SpriteGen/TopDownLit");
                default: return null;
            }
        }

        public static Material[] ApplyStyleToInstance(GameObject instance, RenderStylePreset preset, Material[][] originals)
        {
            if (instance == null || preset == null) return null;
            if (preset.style == RenderStyle.Realistic || preset.style == RenderStyle.PixelArt) return null;

            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (preset.style == RenderStyle.CustomMaterial)
            {
                if (preset.customMaterial == null) return null;
                var clone = new Material(preset.customMaterial);
                ApplySingleMaterial(renderers, clone);
                return new[] { clone };
            }

            var shader = ResolveStyleShader(preset.style);
            if (shader == null)
            {
                LogShaderMissing(preset.style.ToString());
                return null;
            }

            var created = new System.Collections.Generic.List<Material>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var slots = renderers[i].sharedMaterials;
                var rendererOrigs = (originals != null && i < originals.Length) ? originals[i] : slots;

                Material donor = FindAlbedoDonor(rendererOrigs);

                var styleMats = new Material[slots.Length];
                for (int j = 0; j < slots.Length; j++)
                {
                    var origSlot = (j < rendererOrigs.Length) ? rendererOrigs[j] : slots[j];
                    Material src = origSlot;
                    if (origSlot == null || (!HasAlbedoTexture(origSlot) && donor != null))
                        src = donor;

                    var mat = new Material(shader);
                    InheritBaseAppearance(src, mat);
                    UpdateStyleMaterialProperties(mat, preset);
                    styleMats[j] = mat;
                    created.Add(mat);
                }
                renderers[i].sharedMaterials = styleMats;
            }
            return created.ToArray();
        }

        private static Material FindAlbedoDonor(Material[] mats)
        {
            if (mats == null) return null;
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (HasAlbedoTexture(m)) return m;
            }
            return null;
        }

        public static bool HasAlbedoTexture(Material src)
        {
            if (src == null) return false;
            if (src.mainTexture != null) return true;
            foreach (var name in BaseMapPropertyCandidates)
            {
                if (src.HasProperty(name) && src.GetTexture(name) != null) return true;
            }
            var names = src.GetTexturePropertyNames();
            foreach (var n in names)
            {
                if (LooksLikeNonAlbedo(n)) continue;
                if (src.GetTexture(n) != null) return true;
            }
            return false;
        }

        private static void ApplySingleMaterial(Renderer[] renderers, Material mat)
        {
            foreach (var r in renderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static readonly string[] BaseMapPropertyCandidates =
        {
            "_BaseMap", "_MainTex", "_BaseColorMap", "_AlbedoMap", "_Albedo"
        };

        private static void InheritBaseAppearance(Material src, Material dst)
        {
            if (src == null || dst == null) return;

            Texture baseTex = src.mainTexture;
            Vector2 tile = src.mainTextureScale;
            Vector2 offset = src.mainTextureOffset;

            if (baseTex == null)
            {
                foreach (var name in BaseMapPropertyCandidates)
                {
                    if (!src.HasProperty(name)) continue;
                    var t = src.GetTexture(name);
                    if (t == null) continue;
                    baseTex = t;
                    tile = src.GetTextureScale(name);
                    offset = src.GetTextureOffset(name);
                    break;
                }
            }

            if (baseTex == null)
            {
                var names = src.GetTexturePropertyNames();
                foreach (var n in names)
                {
                    if (LooksLikeNonAlbedo(n)) continue;
                    var t = src.GetTexture(n);
                    if (t == null) continue;
                    baseTex = t;
                    tile = src.GetTextureScale(n);
                    offset = src.GetTextureOffset(n);
                    break;
                }
            }

            if (baseTex != null)
            {
                dst.mainTexture = baseTex;
                dst.mainTextureScale = tile;
                dst.mainTextureOffset = offset;

                if (dst.HasProperty("_BaseMap"))
                {
                    dst.SetTexture("_BaseMap", baseTex);
                    dst.SetTextureScale("_BaseMap", tile);
                    dst.SetTextureOffset("_BaseMap", offset);
                }
                if (dst.HasProperty("_MainTex"))
                {
                    dst.SetTexture("_MainTex", baseTex);
                    dst.SetTextureScale("_MainTex", tile);
                    dst.SetTextureOffset("_MainTex", offset);
                }
            }

            Color tint = Color.white;
            if (src.HasProperty("_BaseColor")) tint = src.GetColor("_BaseColor");
            else if (src.HasProperty("_Color")) tint = src.GetColor("_Color");
            else if (src.HasProperty("_BaseColorTint")) tint = src.GetColor("_BaseColorTint");

            if (dst.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", tint);
            if (dst.HasProperty("_Color")) dst.SetColor("_Color", tint);
        }

        private static bool LooksLikeNonAlbedo(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("normal") || n.Contains("bump") || n.Contains("metal") ||
                   n.Contains("rough") || n.Contains("smooth") || n.Contains("specular") ||
                   n.Contains("occlusion") || n.Contains("ao") || n.Contains("emission") ||
                   n.Contains("emissive") || n.Contains("detail") || n.Contains("mask") ||
                   n.Contains("height") || n.Contains("parallax") || n.Contains("sss") ||
                   n.Contains("subsurface") || n.Contains("transmission") || n.Contains("clearcoat") ||
                   n.Contains("anisotropy") || n.Contains("thickness") || n.Contains("translucency");
        }

        public static void UpdateStyleMaterialPropertiesAll(Material[] mats, RenderStylePreset preset)
        {
            if (mats == null) return;
            for (int i = 0; i < mats.Length; i++)
                UpdateStyleMaterialProperties(mats[i], preset);
        }

        public static void UpdateStyleMaterialProperties(Material mat, RenderStylePreset preset)
        {
            if (mat == null || preset == null) return;

            switch (preset.style)
            {
                case RenderStyle.Silhouette:
                    if (mat.HasProperty("_SilhouetteColor"))
                        mat.SetColor("_SilhouetteColor", preset.silhouetteColor);
                    break;
                case RenderStyle.Toon:
                    if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", preset.outlineColor);
                    if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", preset.outlineWidth);
                    break;
                case RenderStyle.TopDownLit:
                    if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", preset.topDownRimColor);
                    if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", preset.topDownRimPower);
                    if (mat.HasProperty("_RimIntensity")) mat.SetFloat("_RimIntensity", preset.topDownRimIntensity);
                    if (mat.HasProperty("_WrapAmount")) mat.SetFloat("_WrapAmount", preset.topDownWrapAmount);
                    if (mat.HasProperty("_TopAccent")) mat.SetFloat("_TopAccent", preset.topDownTopAccent);
                    if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", preset.topDownOutlineColor);
                    if (mat.HasProperty("_OutlineWidth"))
                        mat.SetFloat("_OutlineWidth", preset.topDownOutlineEnabled ? preset.topDownOutlineWidth : 0f);
                    break;
            }
        }

        public static Material[][] CaptureOriginalMaterials(GameObject instance)
        {
            if (instance == null) return null;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var snapshot = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var src = renderers[i].sharedMaterials;
                var copy = new Material[src.Length];
                for (int j = 0; j < src.Length; j++) copy[j] = src[j];
                snapshot[i] = copy;
            }
            return snapshot;
        }

        public static void RestoreOriginalMaterials(GameObject instance, Material[][] snapshot)
        {
            if (instance == null || snapshot == null) return;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            int n = Mathf.Min(renderers.Length, snapshot.Length);
            for (int i = 0; i < n; i++)
                renderers[i].sharedMaterials = snapshot[i];
        }

        public static GameObject CreateExtraDirectional(string name, Color color, float intensity, Quaternion rotation, int cullingLayer)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.layer = cullingLayer;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.cullingMask = 1 << cullingLayer;
            l.shadows = LightShadows.None;
            go.transform.rotation = rotation;
#if PROTO_HAS_URP
            go.AddComponent<UniversalAdditionalLightData>();
#endif
            return go;
        }

        public static Texture2D ApplySobelOutline(Texture2D src, RenderStylePreset p)
        {
            if (src == null || p == null) return src;
            int w = src.width;
            int h = src.height;
            if (w < 3 || h < 3) return src;

            var pixels = src.GetPixels();
            var dst = new Color[pixels.Length];
            float threshold = p.sobelThreshold;
            Color outlineCol = p.sobelColor;
            bool useAlpha = p.sobelSource == SobelSource.Alpha;

            for (int i = 0; i < pixels.Length; i++) dst[i] = pixels[i];

            for (int y = 1; y < h - 1; y++)
            {
                int rowAbove = (y + 1) * w;
                int rowMid = y * w;
                int rowBelow = (y - 1) * w;

                for (int x = 1; x < w - 1; x++)
                {
                    float s00 = Sample(pixels[rowBelow + (x - 1)], useAlpha);
                    float s10 = Sample(pixels[rowBelow + x], useAlpha);
                    float s20 = Sample(pixels[rowBelow + (x + 1)], useAlpha);
                    float s01 = Sample(pixels[rowMid + (x - 1)], useAlpha);
                    float s21 = Sample(pixels[rowMid + (x + 1)], useAlpha);
                    float s02 = Sample(pixels[rowAbove + (x - 1)], useAlpha);
                    float s12 = Sample(pixels[rowAbove + x], useAlpha);
                    float s22 = Sample(pixels[rowAbove + (x + 1)], useAlpha);

                    float gx = -s00 - 2f * s01 - s02 + s20 + 2f * s21 + s22;
                    float gy = -s00 - 2f * s10 - s20 + s02 + 2f * s12 + s22;
                    float mag = Mathf.Sqrt(gx * gx + gy * gy);

                    if (mag > threshold)
                    {
                        int idx = rowMid + x;
                        float a = Mathf.Max(outlineCol.a, pixels[idx].a);
                        dst[idx] = new Color(outlineCol.r, outlineCol.g, outlineCol.b, a);
                    }
                }
            }

            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.filterMode = src.filterMode;
            result.SetPixels(dst);
            result.Apply();

            DestroyTexture(src);
            return result;
        }

        public static Texture2D ApplyPixelArt(Texture2D source, int downscale)
        {
            if (source == null || downscale <= 1) return source;

            int smallW = Mathf.Max(1, source.width / downscale);
            int smallH = Mathf.Max(1, source.height / downscale);

            var pixels = source.GetPixels();
            var smallPixels = new Color[smallW * smallH];

            for (int y = 0; y < smallH; y++)
            {
                for (int x = 0; x < smallW; x++)
                {
                    int srcX = Mathf.Clamp(x * downscale + downscale / 2, 0, source.width - 1);
                    int srcY = Mathf.Clamp(y * downscale + downscale / 2, 0, source.height - 1);
                    smallPixels[y * smallW + x] = pixels[srcY * source.width + srcX];
                }
            }

            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            var resultPixels = new Color[source.width * source.height];

            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    int px = Mathf.Clamp(x / downscale, 0, smallW - 1);
                    int py = Mathf.Clamp(y / downscale, 0, smallH - 1);
                    resultPixels[y * source.width + x] = smallPixels[py * smallW + px];
                }
            }
            result.SetPixels(resultPixels);
            result.Apply();

            DestroyTexture(source);
            return result;
        }

        private static float Sample(Color c, bool useAlpha)
        {
            return useAlpha ? c.a : (0.299f * c.r + 0.587f * c.g + 0.114f * c.b);
        }

        private static void DestroyTexture(Texture2D tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) Object.Destroy(tex);
            else Object.DestroyImmediate(tex);
        }

        private static void LogShaderMissing(string path)
        {
            Debug.LogWarning($"Shader '{path}' not found. Style not applied.");
        }
    }
}
