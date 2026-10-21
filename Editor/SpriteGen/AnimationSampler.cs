using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ProtoSystem.SpriteGen.Editor
{
    public static class AnimationSampler
    {
        public struct AnimationFrame
        {
            public Texture2D texture;
            public string animationName;
            public int frameIndex;
            public float angle;
            public float time;
        }

        public static List<AnimationFrame> SampleAnimation(
            SpriteGenConfig config,
            AnimationEntry entry,
            float yawAngle)
        {
            if (config.modelPrefab == null || entry == null || entry.clip == null)
                return new List<AnimationFrame>();

            var frames = new List<AnimationFrame>();
            var instance = Object.Instantiate(config.modelPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursive(instance, 31);

            // Компенсация осей модели (Z-up GLB): корень клипами не анимируется,
            // поэтому поворот переживает сэмплирование
            if (config.modelRotationOffset != Vector3.zero)
                instance.transform.rotation = Quaternion.Euler(config.modelRotationOffset);

            // Настроить Animator для Humanoid
            var animator = instance.GetComponent<Animator>();
            if (animator != null && config.rigType == AnimationRigType.Humanoid)
            {
                var srcAnimator = config.modelPrefab.GetComponent<Animator>();
                if (srcAnimator != null && srcAnimator.avatar != null)
                    animator.avatar = srcAnimator.avatar;
            }

            // Принудительный пересчёт мешей при каждом рендере
            var skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
                smr.forceMatrixRecalculationPerRender = true;

            ModelRenderer.ApplyStyle(instance, config.stylePreset);

            float[] times = entry.GetFrameTimes();

            try
            {
                AnimationMode.StartAnimationMode();

                for (int i = 0; i < times.Length; i++)
                {
                    float time = times[i];

                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(instance, entry.clip, time);
                    AnimationMode.EndSampling();

                    // Обновить трансформы костей
                    SceneView.RepaintAll();

                    var tex = ModelRenderer.RenderModelInstance(instance, config, yawAngle);

                    frames.Add(new AnimationFrame
                    {
                        texture = tex,
                        animationName = entry.name,
                        frameIndex = i,
                        angle = yawAngle,
                        time = time
                    });
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(instance);
            }

            return frames;
        }

        public static List<AnimationFrame> SampleAllAngles(
            SpriteGenConfig config,
            AnimationEntry entry,
            System.Action<float> onProgress = null)
        {
            var allFrames = new List<AnimationFrame>();
            var angles = config.GetAngles();

            for (int a = 0; a < angles.Length; a++)
            {
                onProgress?.Invoke((float)a / angles.Length);
                var frames = SampleAnimation(config, entry, angles[a]);
                allFrames.AddRange(frames);
            }

            return allFrames;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
