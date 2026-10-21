using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
#if PROTO_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ProtoSystem.SpriteGen.Editor
{
    public static class PreviewSceneGenerator
    {
        private const string PreviewSceneFolder = "Assets/SpriteGen/Generated";
        private const string PreviewSceneName = "SpriteGen_Preview";

        public static void OpenPreviewScene(SpriteGenConfig config)
        {
            if (config == null || config.modelPrefab == null)
            {
                Debug.LogError("Конфиг или модель не назначены.");
                return;
            }

            // Сохранить текущую сцену
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Построить AnimatorController
            var controller = AnimatorControllerBuilder.BuildForPreview(config);

            // Создать сцену
            string scenePath = Path.Combine(PreviewSceneFolder, PreviewSceneName + ".unity");
            EnsureDirectory(PreviewSceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Модель ---
            var model = Object.Instantiate(config.modelPrefab);
            model.name = config.modelPrefab.name;

            // Animator
            var animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;

            // Rig Avatar — для Humanoid нужен avatar из модели
            if (config.rigType == AnimationRigType.Humanoid)
            {
                var existingAnimator = config.modelPrefab.GetComponent<Animator>();
                if (existingAnimator != null && existingAnimator.avatar != null)
                    animator.avatar = existingAnimator.avatar;
            }

            // --- Камера ---
            var camGO = new GameObject("Preview Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.backgroundColor.a > 0.01f
                ? config.backgroundColor
                : new Color(0.2f, 0.2f, 0.25f, 1f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;

            if (config.orthographic)
            {
                cam.orthographic = true;
                // orthographicSize <= 0 — авто-подгонка по габаритам модели (как в ModelRenderer)
                float size = config.orthographicSize;
                if (size <= 0f)
                {
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        var b = renderers[0].bounds;
                        foreach (var r in renderers) b.Encapsulate(r.bounds);
                        size = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.2f;
                    }
                    size = Mathf.Max(size, 0.01f);
                }
                cam.orthographicSize = size;
            }

#if PROTO_HAS_URP
            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderType = CameraRenderType.Base;
#endif

            // --- Свет ---
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.5f;
            lightGO.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
#if PROTO_HAS_URP
            lightGO.AddComponent<UniversalAdditionalLightData>();
#endif

            // Ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);

            // --- Контроллер ---
            var controllerGO = new GameObject("PreviewController");
            var previewCtrl = controllerGO.AddComponent<PreviewSceneController>();
            previewCtrl.config = config;
            previewCtrl.Initialize(model, cam, light);
            previewCtrl.pitch = config.cameraPitch;

            // Переопределить pitch для пресетов
            if (config.anglePreset == CameraAnglePreset.TopDown)
                previewCtrl.pitch = 90f;
            else if (config.anglePreset == CameraAnglePreset.Side)
                previewCtrl.pitch = 0f;

            // Сохранить сцену
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Превью-сцена создана: {scenePath}\nНажмите Play для просмотра.");

            // Автоматический запуск Play mode
            if (EditorUtility.DisplayDialog("Запустить превью?",
                "Сцена создана. Запустить Play mode для просмотра?", "Play", "Нет"))
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static void EnsureDirectory(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            if (!Directory.Exists(absolutePath))
                Directory.CreateDirectory(absolutePath);
        }
    }
}
