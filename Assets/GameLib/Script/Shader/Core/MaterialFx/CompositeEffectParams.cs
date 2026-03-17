using System;
using UnityEngine;

namespace Game.MaterialFx
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CompositeEffectParams.cs - コンポジットエフェクトのパラメータ構造体
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // 仕様書 BaseShader-CompositeSystem-v1.0 Part 2/5 準拠
    //
    // 各エフェクトのパラメータを C# 構造体として定義。
    // シェーダー側の CBUFFER と同期すること。
    //
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dissolve エフェクトのパラメータ。
    /// 仕様: Part 2/5 Dissolve2D Section
    /// </summary>
    [Serializable]
    public struct DissolveParams
    {
        [Header("Source")]
        public TextureSlotRef Source;

        [Header("Dissolve")]
        [Range(0f, 1f)]
        public float Progress;

        [Header("Edge")]
        [Range(0f, 0.5f)]
        public float EdgeWidth;
        public Color EdgeColor;

        [Header("Softness")]
        [Range(0f, 1f)]
        public float Softness;

        public bool Enabled => Progress > 0f;

        public static DissolveParams Default => new()
        {
            Source = TextureSlotRef.ForExternal(TextureSlotType.ExternalA),
            Progress = 0f,
            EdgeWidth = 0.05f,
            EdgeColor = Color.white,
            Softness = 0.1f,
        };

        /// <summary>
        /// シェーダーの _Dissolve_* パラメータに対応する Vector4 配列を生成。
        /// </summary>
        public readonly void GetShaderVectors(out Vector4 source, out Vector4 param, out Vector4 edge)
        {
            source = new Vector4(
                (int)Source.SlotType,
                (int)Source.Channel,
                (int)Source.UVSpace,
                0
            );
            param = new Vector4(Progress, EdgeWidth, Softness, 0);
            edge = new Vector4(EdgeColor.r, EdgeColor.g, EdgeColor.b, EdgeColor.a);
        }
    }

    /// <summary>
    /// FlowWarp エフェクトのパラメータ。
    /// 仕様: Part 2/5 FlowWarp2D Section
    /// </summary>
    [Serializable]
    public struct FlowWarpParams
    {
        [Header("Source")]
        public TextureSlotRef Source;

        [Header("Strength")]
        public Vector2 Strength;

        [Header("Speed")]
        public float Speed;

        [Header("Normalize")]
        public bool Normalize;

        public bool Enabled => Strength.sqrMagnitude > 0.0001f;

        public static FlowWarpParams Default => new()
        {
            Source = TextureSlotRef.ForExternal(TextureSlotType.ExternalA, ChannelMask.RG),
            Strength = Vector2.zero,
            Speed = 1f,
            Normalize = true,
        };

        /// <summary>
        /// シェーダーの _FlowWarp_* パラメータに対応する Vector4 配列を生成。
        /// </summary>
        public readonly void GetShaderVectors(out Vector4 source, out Vector4 param)
        {
            source = new Vector4(
                (int)Source.SlotType,
                (int)Source.Channel,
                (int)Source.UVSpace,
                0
            );
            param = new Vector4(Strength.x, Strength.y, Speed, Normalize ? 1f : 0f);
        }
    }

    /// <summary>
    /// Mask エフェクトのパラメータ。
    /// 仕様: Part 2/5 Mask2D Section
    /// </summary>
    [Serializable]
    public struct MaskParams
    {
        [Header("Source")]
        public TextureSlotRef Source;

        [Header("Threshold")]
        [Range(0f, 1f)]
        public float Threshold;

        [Header("Softness")]
        [Range(0f, 1f)]
        public float Softness;

        [Header("Invert")]
        public bool Invert;

        public bool Enabled => Threshold > 0f || Softness > 0f;

        public static MaskParams Default => new()
        {
            Source = TextureSlotRef.ForExternal(TextureSlotType.ExternalA),
            Threshold = 0f,
            Softness = 0.1f,
            Invert = false,
        };

        /// <summary>
        /// シェーダーの _Mask_* パラメータに対応する Vector4 配列を生成。
        /// </summary>
        public readonly void GetShaderVectors(out Vector4 source, out Vector4 param)
        {
            source = new Vector4(
                (int)Source.SlotType,
                (int)Source.Channel,
                (int)Source.UVSpace,
                0
            );
            param = new Vector4(Threshold, Softness, Invert ? 1f : 0f, 0);
        }
    }

    /// <summary>
    /// Emission エフェクトのパラメータ。
    /// 仕様: Part 2/5 Emission Section
    /// </summary>
    [Serializable]
    public struct EmissionParams
    {
        [Header("Color & Intensity")]
        [ColorUsage(false, true)]
        public Color Color;
        public float Intensity;

        [Header("Pulse")]
        public float PulseSpeed;
        public float PulseMin;

        public bool Enabled => Intensity > 0f;

        public static EmissionParams Default => new()
        {
            Color = Color.white,
            Intensity = 0f,
            PulseSpeed = 0f,
            PulseMin = 0f,
        };

        /// <summary>
        /// シェーダーの _Emission_* パラメータに対応する Vector4 を生成。
        /// Color.a に Intensity を埋め込む形式。
        /// </summary>
        public readonly Vector4 GetShaderVector()
        {
            return new Vector4(Color.r * Intensity, Color.g * Intensity, Color.b * Intensity, Intensity);
        }
    }

    /// <summary>
    /// 全コンポジットエフェクトをまとめた構造体。
    /// MaterialFx layer が利用する。
    /// </summary>
    [Serializable]
    public struct CompositeEffectBundle
    {
        public DissolveParams Dissolve;
        public FlowWarpParams FlowWarp;
        public MaskParams Mask;
        public EmissionParams Emission;

        public static CompositeEffectBundle Default => new()
        {
            Dissolve = DissolveParams.Default,
            FlowWarp = FlowWarpParams.Default,
            Mask = MaskParams.Default,
            Emission = EmissionParams.Default,
        };
    }
}
