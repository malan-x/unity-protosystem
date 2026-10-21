using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoSystem.SpriteGen
{
    public enum CameraAnglePreset
    {
        TopDown,
        Side,
        FourDirections,
        EightDirections,
        SixteenDirections,
        Custom
    }

    public enum OutputMode
    {
        IndividualSprites,
        Spritesheet,
        Both
    }

    public enum AnimationRigType
    {
        Generic,
        Humanoid
    }

    public enum FrameSelectionMode
    {
        Uniform,
        Custom
    }

    public enum GenerationMode
    {
        Individual,     // Индивидуальные настройки (per-animation OutputMode)
        CombinedSheet   // Общий спрайтшит из всех выделенных анимаций
    }

    public enum SpritePivot
    {
        Center,
        BottomCenter,
        TopCenter,
        Custom
    }

    [Serializable]
    public class AnimationEntry
    {
        public string name;
        public AnimationClip clip;
        public bool generate = true;
        public OutputMode outputMode = OutputMode.Both;
        public int frameCount = 8;
        public bool loop = true;
        public FrameSelectionMode frameMode = FrameSelectionMode.Uniform;
        public float[] customFrameTimes = new float[] { 0f };
        public SpritePivot pivot = SpritePivot.BottomCenter;
        public Vector2 customPivot = new Vector2(0.5f, 0f);

        public AnimationEntry() { }

        public AnimationEntry(string name, bool loop = true)
        {
            this.name = name;
            this.loop = loop;
            this.frameCount = 8;
            this.generate = true;
            this.outputMode = OutputMode.Both;
        }

        public float[] GetFrameTimes()
        {
            if (clip == null) return new float[] { 0f };

            if (frameMode == FrameSelectionMode.Custom)
            {
                return customFrameTimes != null && customFrameTimes.Length > 0
                    ? customFrameTimes
                    : new float[] { 0f };
            }

            // Uniform
            int count = Mathf.Max(1, frameCount);
            var times = new float[count];
            float clipLen = clip.length;

            if (count == 1)
            {
                times[0] = 0f;
            }
            else
            {
                float step = clipLen / (count - 1);
                for (int i = 0; i < count; i++)
                    times[i] = Mathf.Min(i * step, clipLen);
            }
            return times;
        }

        public Vector2 GetPivotValue()
        {
            switch (pivot)
            {
                case SpritePivot.Center: return new Vector2(0.5f, 0.5f);
                case SpritePivot.BottomCenter: return new Vector2(0.5f, 0f);
                case SpritePivot.TopCenter: return new Vector2(0.5f, 1f);
                case SpritePivot.Custom: return customPivot;
                default: return new Vector2(0.5f, 0f);
            }
        }
    }

    [CreateAssetMenu(fileName = "SpriteGenConfig", menuName = "ProtoSystem/SpriteGen/Config")]
    public class SpriteGenConfig : ScriptableObject
    {
        [Header("Model")]
        public GameObject modelPrefab;
        public AnimationRigType rigType = AnimationRigType.Generic;

        [Header("Sprite Size")]
        public int spriteWidth = 128;
        public int spriteHeight = 128;

        [Header("Camera Angles")]
        public CameraAnglePreset anglePreset = CameraAnglePreset.EightDirections;
        public float[] customAngles = new float[] { 0f, 90f, 180f, 270f };

        [Header("Camera")]
        public float cameraDistance = 5f;
        public float cameraPitch = 30f;
        public bool orthographic = true;
        public float orthographicSize = 1f;

        [Tooltip("TopDown: куда смотрит «верх экрана» (поворот вокруг вертикали, °). " +
                 "0 = мировой +Z. Крутите, если модель на листе лежит боком/вверх ногами")]
        public float topDownUpYaw = 0f;

        [Tooltip("Компенсация осей модели (эйлеры, °): применяется к корню инстанса перед " +
                 "рендером. Нужна Z-up экспортам (Meshy GLB: анимация кладёт персонажа " +
                 "на спину — X=90 ставит его на ноги)")]
        public Vector3 modelRotationOffset = Vector3.zero;

        [Header("Animations")]
        public List<AnimationEntry> animations = new List<AnimationEntry>();

        [Header("Output")]
        public GenerationMode generationMode = GenerationMode.Individual;
        public Color backgroundColor = new Color(0f, 0f, 0f, 0f);
        public string outputFolder = "Assets/SpriteGen/Output";

        [Header("Render Style")]
        public RenderStylePreset stylePreset;

        public float[] GetAngles()
        {
            switch (anglePreset)
            {
                case CameraAnglePreset.TopDown:
                    return new float[] { 0f };
                case CameraAnglePreset.Side:
                    return new float[] { 0f };
                case CameraAnglePreset.FourDirections:
                    return new float[] { 0f, 90f, 180f, 270f };
                case CameraAnglePreset.EightDirections:
                    return new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
                case CameraAnglePreset.SixteenDirections:
                    var angles = new float[16];
                    for (int i = 0; i < 16; i++)
                        angles[i] = i * 22.5f;
                    return angles;
                case CameraAnglePreset.Custom:
                    return customAngles != null && customAngles.Length > 0
                        ? customAngles
                        : new float[] { 0f };
                default:
                    return new float[] { 0f };
            }
        }
    }
}
