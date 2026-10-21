using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
#if PROTO_HAS_URP
using UnityEngine.Rendering.Universal;
#endif
using Object = UnityEngine.Object;

namespace ProtoSystem.SpriteGen.Editor
{
    public static class ModelRenderer
    {
        private const int IsolationLayer = 31;

        public static Texture2D RenderModel(SpriteGenConfig config, float yawAngle)
        {
            if (config.modelPrefab == null)
                throw new ArgumentException("Model prefab is not assigned in config.");

            var instance = InstantiateIsolated(config.modelPrefab);
            ApplyModelRotationOffset(instance, config);
            try
            {
                ApplyStyle(instance, config.stylePreset);
                var texture = CaptureFrame(instance, config, yawAngle);
                return texture;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static Texture2D RenderModelInstance(GameObject instance, SpriteGenConfig config, float yawAngle)
        {
            return CaptureFrame(instance, config, yawAngle);
        }

        private static GameObject InstantiateIsolated(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursive(instance, IsolationLayer);
            return instance;
        }

        /// <summary>Компенсация осей модели из конфига (см. SpriteGenConfig.modelRotationOffset)</summary>
        internal static void ApplyModelRotationOffset(GameObject instance, SpriteGenConfig config)
        {
            if (config != null && config.modelRotationOffset != Vector3.zero)
                instance.transform.rotation = Quaternion.Euler(config.modelRotationOffset);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static Texture2D CaptureFrame(GameObject instance, SpriteGenConfig config, float yawAngle)
        {
            int width = config.spriteWidth;
            int height = config.spriteHeight;

            var rtDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = 4,
                sRGB = true
            };
            var rt = RenderTexture.GetTemporary(rtDesc);

            var camGO = new GameObject("SpriteGen_Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();

#if PROTO_HAS_URP
            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderType = CameraRenderType.Base;
#endif

            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.backgroundColor;
            cam.cullingMask = 1 << IsolationLayer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            if (config.orthographic)
            {
                cam.orthographic = true;
                cam.orthographicSize = config.orthographicSize;
            }
            else
            {
                cam.orthographic = false;
                cam.fieldOfView = 60f;
            }

            PositionCamera(cam, instance, config, yawAngle);

            var lightGO = new GameObject("SpriteGen_Light") { hideFlags = HideFlags.HideAndDontSave };
            lightGO.layer = IsolationLayer;
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.cullingMask = 1 << IsolationLayer;
            lightGO.transform.rotation = Quaternion.Euler(50f, yawAngle + 30f, 0f);

#if PROTO_HAS_URP
            lightGO.AddComponent<UniversalAdditionalLightData>();
#endif

            var preset = config.stylePreset;
            GameObject backLightGO = null;
            GameObject topAccentGO = null;
            bool styleAcceptsExtraLights = preset == null || preset.style != RenderStyle.Silhouette;

            if (preset != null && styleAcceptsExtraLights && preset.enableBackLight)
                backLightGO = StyleApplier.CreateExtraDirectional(
                    "SpriteGen_BackLight",
                    preset.backLightColor,
                    preset.backLightIntensity,
                    Quaternion.Euler(preset.backLightPitch, yawAngle + 30f + preset.backLightYawOffset, 0f),
                    IsolationLayer);

            if (preset != null && styleAcceptsExtraLights && preset.enableTopAccentLight)
                topAccentGO = StyleApplier.CreateExtraDirectional(
                    "SpriteGen_TopAccent",
                    preset.topAccentColor,
                    preset.topAccentIntensity,
                    Quaternion.Euler(85f, yawAngle, 0f),
                    IsolationLayer);

            try
            {
                cam.Render();

                var prevRT = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                RenderTexture.active = prevRT;

                if (preset != null && preset.style == RenderStyle.PixelArt)
                    tex = StyleApplier.ApplyPixelArt(tex, preset.pixelArtDownscale);

                if (preset != null && preset.enableSobelOutlinePost)
                    tex = StyleApplier.ApplySobelOutline(tex, preset);

                return tex;
            }
            finally
            {
                if (topAccentGO != null) Object.DestroyImmediate(topAccentGO);
                if (backLightGO != null) Object.DestroyImmediate(backLightGO);
                Object.DestroyImmediate(lightGO);
                Object.DestroyImmediate(camGO);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void PositionCamera(Camera cam, GameObject target, SpriteGenConfig config, float yawAngle)
        {
            var bounds = CalculateBounds(target);
            var center = bounds.center;

            float pitch = config.cameraPitch;

            // Пресеты переопределяют pitch
            if (config.anglePreset == CameraAnglePreset.TopDown)
                pitch = 90f;
            else if (config.anglePreset == CameraAnglePreset.Side)
                pitch = 0f;

            float pitchRad = pitch * Mathf.Deg2Rad;
            float yawRad = yawAngle * Mathf.Deg2Rad;

            float distance = config.cameraDistance;

            var offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * distance;

            cam.transform.position = center + offset;

            if (config.anglePreset == CameraAnglePreset.TopDown)
            {
                // Взгляд строго вниз: LookAt с дефолтным up=Vector3.up вырожден
                // (forward параллелен up) и даёт случайный крен — «верх экрана»
                // задаём явно, поворот вокруг вертикали настраивается в конфиге
                var up = Quaternion.Euler(0f, config.topDownUpYaw, 0f) * Vector3.forward;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, up);
            }
            else
                cam.transform.LookAt(center);

            if (config.orthographic)
            {
                float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                if (config.orthographicSize <= 0f)
                    cam.orthographicSize = maxExtent * 1.2f;
            }
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        public static void ApplyStyle(GameObject instance, RenderStylePreset preset)
        {
            if (preset == null) return;
            var snapshot = StyleApplier.CaptureOriginalMaterials(instance);
            StyleApplier.ApplyStyleToInstance(instance, preset, snapshot);
        }

        public static void SaveTextureAsPNG(Texture2D texture, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
    }
}
