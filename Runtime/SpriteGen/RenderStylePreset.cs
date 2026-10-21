using UnityEngine;

namespace ProtoSystem.SpriteGen
{
    public enum RenderStyle
    {
        Realistic,
        Toon,
        PixelArt,
        Silhouette,
        CustomMaterial,
        TopDownLit
    }

    public enum SobelSource
    {
        Alpha,
        Luminance
    }

    [CreateAssetMenu(fileName = "RenderStylePreset", menuName = "ProtoSystem/SpriteGen/Render Style Preset")]
    public class RenderStylePreset : ScriptableObject
    {
        public RenderStyle style = RenderStyle.Realistic;

        [Header("Custom Material")]
        public Material customMaterial;

        [Header("Silhouette")]
        public Color silhouetteColor = Color.black;

        [Header("Pixel Art")]
        [Range(1, 8)]
        public int pixelArtDownscale = 4;

        [Header("Toon")]
        public Color outlineColor = Color.black;
        [Range(0f, 0.1f)]
        public float outlineWidth = 0.02f;

        [Header("Top-Down Lighting (any 3D style)")]
        [Tooltip("Adds a second directional light from the opposite hemisphere to separate silhouette from floor. Requires URP additional lights enabled in the active RP asset for non-Lit shaders.")]
        public bool enableBackLight = false;
        public Color backLightColor = new Color(0.6f, 0.7f, 1f, 1f);
        [Range(0f, 2f)] public float backLightIntensity = 0.5f;
        [Range(0f, 360f)] public float backLightYawOffset = 180f;
        [Range(0f, 80f)] public float backLightPitch = 35f;

        [Tooltip("Adds a near-vertical accent light to brighten upward-facing surfaces (heads, shoulders).")]
        public bool enableTopAccentLight = false;
        public Color topAccentColor = Color.white;
        [Range(0f, 1.5f)] public float topAccentIntensity = 0.3f;

        [Header("Sobel Outline Post (any style)")]
        [Tooltip("Applies a 1px Sobel edge-detect outline on the captured texture. Style-agnostic.")]
        public bool enableSobelOutlinePost = false;
        public Color sobelColor = Color.black;
        [Range(0f, 1f)] public float sobelThreshold = 0.2f;
        public SobelSource sobelSource = SobelSource.Alpha;

        [Header("TopDownLit Style")]
        public Color topDownRimColor = Color.white;
        [Range(0.5f, 8f)] public float topDownRimPower = 3f;
        [Range(0f, 3f)] public float topDownRimIntensity = 1f;
        [Range(0f, 1f)] public float topDownWrapAmount = 0.5f;
        [Range(0f, 1f)] public float topDownTopAccent = 0.15f;
        public bool topDownOutlineEnabled = false;
        public Color topDownOutlineColor = Color.black;
        [Range(0f, 0.1f)] public float topDownOutlineWidth = 0.01f;
    }
}
