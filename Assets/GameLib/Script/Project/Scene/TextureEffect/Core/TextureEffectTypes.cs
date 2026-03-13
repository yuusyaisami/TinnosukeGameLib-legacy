#nullable enable
using System;
using UnityEngine;

namespace Game.TextureEffect
{
    // ── TextureEffectKind ───────────────────────────────────────

    public enum TextureEffectKind
    {
        None = 0,
        Blur = 10,
        Mosaic = 20,
        Distort = 30,
        Refraction = 40,
        Ripple = 50,
        ColorShift = 60,
        Posterize = 70,
        Shatter = 80,
    }

    // ── MaskShapeKind ───────────────────────────────────────────

    public enum MaskShapeKind
    {
        RendererShape = 0,
        BoundsRect = 10,
        Circle = 20,
        Custom = 100,
    }

    // ── TextureEffectLayerDef ───────────────────────────────────

    [Serializable]
    public struct TextureEffectLayerDef
    {
        public string LayerTag;
        public int Order;
        public string InputTag;
        public string OutputTag;
        public TextureEffectKind EffectKind;
        public bool Enabled;
        [Range(0.1f, 1.0f)]
        public float ResolutionScale;
        public Material? EffectMaterial;

        /// <summary>Effect 固有パラメータ。Effect 種別ごとに解釈が異なる。</summary>
        public TextureEffectParams Params;
    }

    // ── TextureEffectParams ─────────────────────────────────────

    [Serializable]
    public struct TextureEffectParams
    {
        // Blur
        [Range(0, 8)] public int BlurIterations;
        [Range(0f, 4f)] public float BlurSpread;
        [Range(1, 8)] public int BlurDownsample;

        // Mosaic
        [Range(1f, 128f)] public float MosaicBlockSize;

        // Distort
        [Range(0f, 1f)] public float DistortStrength;
        public Texture? DistortNoiseTex;

        // ColorShift
        [Range(-1f, 1f)] public float HueShift;
        [Range(0f, 2f)] public float SaturationMultiplier;
        public Color ColorMultiply;
        public Color ColorAdd;

        public static TextureEffectParams DefaultBlur => new()
        {
            BlurIterations = 2,
            BlurSpread = 0.6f,
            BlurDownsample = 2,
        };

        public static TextureEffectParams DefaultMosaic => new()
        {
            MosaicBlockSize = 16f,
        };

        public static TextureEffectParams DefaultDistort => new()
        {
            DistortStrength = 0.1f,
        };

        public static TextureEffectParams DefaultColorShift => new()
        {
            HueShift = 0f,
            SaturationMultiplier = 1f,
            ColorMultiply = Color.white,
            ColorAdd = Color.clear,
        };
    }

    // ── TextureEffectMaskEntry ──────────────────────────────────

    [Serializable]
    public struct TextureEffectMaskEntry
    {
        public string LayerTag;
        public int RegistrationId;
        public Renderer? MaskRenderer;
        public MaskShapeKind ShapeKind;
        public bool Enabled;

        // Circle mask params
        public Vector2 CircleCenter;
        public float CircleRadius;
    }
}
