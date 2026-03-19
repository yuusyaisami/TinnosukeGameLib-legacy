#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Common;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.MaterialFx
{
    /// <summary>
    /// プリセット内の 1 エントリ。StableKey + 値 + ブレンドモード。
    /// </summary>
    [Serializable]
    public struct MaterialFxPresetEntry
    {
        [LabelText("Key")]
        [Tooltip("StableKey（文字列）")]
        [MaterialFxPropertyPicker]
        public string Key;

        [InlineProperty]
        [HideLabel]
        [Tooltip("値（シリアライズ用）")]
        public MaterialFxSerializedValue Value;

        [LabelText("Blend Mode")]
        [Tooltip("ブレンドモード")]
        public MaterialFxBlendMode BlendMode;

        [LabelText("Lifetime Seconds (-1 = Infinite)")]
        [Tooltip("生存時間（秒）。-1 = 無期限（デフォルト）")]
        public float LifetimeSeconds;

        [LabelText("Apply Weight Fade")]
        [Tooltip("Weight フェードを適用する")]
        public bool ApplyWeightFade;

        [ShowIf(nameof(ApplyWeightFade))]
        [LabelText("Target Weight")]
        [Tooltip("フェード先 Weight（0=影響なし, 1=完全適用）")]
        public DynamicValue<float> TargetWeight;

        [ShowIf(nameof(ApplyWeightFade))]
        [LabelText("Fade Duration")]
        [Tooltip("フェード時間（秒）")]
        public DynamicValue<float> FadeDuration;

        [ShowIf(nameof(ApplyWeightFade))]
        [LabelText("Fade Ease")]
        [Tooltip("フェードの Ease")]
        public Ease FadeEase;

        public float ResolveTargetWeight(IDynamicContext? context)
        {
            return ResolveDynamicFloat(TargetWeight, context, 1f);
        }

        public float ResolveFadeDuration(IDynamicContext? context)
        {
            return Mathf.Max(0f, ResolveDynamicFloat(FadeDuration, context, 0f));
        }

        public MaterialFxPresetEntry Resolve(IDynamicContext? context)
        {
            var resolved = this;
            resolved.Value = Value.Resolve(context);
            resolved.TargetWeight = DynamicValueExtensions.FromLiteral(ResolveTargetWeight(context));
            resolved.FadeDuration = DynamicValueExtensions.FromLiteral(ResolveFadeDuration(context));
            return resolved;
        }

        static float ResolveDynamicFloat(in DynamicValue<float> value, IDynamicContext? context, float fallback)
        {
            if (context != null)
                return value.GetOrDefault(context, fallback);

            return value.GetOrDefaultWithoutContext(fallback);
        }
    }

    /// <summary>
    /// シリアライズ用の値コンテナ。Inspector で編集可能。
    /// </summary>
    [Serializable]
    public struct MaterialFxSerializedValue
    {
        [HideInInspector]
        public ValueKind Type;

        [ShowIf(nameof(IsFloat))]
        [LabelText("Value (Float)")]
        public DynamicValue<float> Float;

        [ShowIf(nameof(IsInt))]
        [LabelText("Value (Int)")]
        public int Int;

        [ShowInInspector]
        [ShowIf(nameof(IsBool))]
        [LabelText("Value (Bool)")]
        bool BoolValue
        {
            readonly get => Int != 0;
            set => Int = value ? 1 : 0;
        }

        [ShowIf(nameof(IsFloat2))]
        [LabelText("Value (Float2)")]
        public Vector2 Float2;

        [ShowIf(nameof(IsFloat3))]
        [LabelText("Value (Float3)")]
        public Vector3 Float3;

        [ShowIf(nameof(IsFloat4))]
        [LabelText("Value (Float4)")]
        public Vector4 Float4;

        [ShowIf(nameof(IsColor))]
        [LabelText("Value (Color)")]
        public Color Color;

        [ShowIf(nameof(IsTextureLike))]
        [LabelText("$" + nameof(TextureLabel))]
        public Texture? Texture;

        readonly bool IsFloat => Type == ValueKind.Float;
        readonly bool IsInt => Type == ValueKind.Int;
        readonly bool IsBool => Type == ValueKind.Bool;
        readonly bool IsFloat2 => Type == ValueKind.Float2;
        readonly bool IsFloat3 => Type == ValueKind.Float3;
        readonly bool IsFloat4 => Type == ValueKind.Float4;
        readonly bool IsColor => Type == ValueKind.Color;
        readonly bool IsTextureLike => Type == ValueKind.Texture || Type == ValueKind.TextureArray;
        readonly string TextureLabel => Type == ValueKind.TextureArray ? "Value (Texture Array)" : "Value (Texture)";

        public float ResolveFloat(IDynamicContext? context)
        {
            if (context != null)
                return Float.GetOrDefault(context, 0f);

            return Float.GetOrDefaultWithoutContext(0f);
        }

        public MaterialFxSerializedValue Resolve(IDynamicContext? context)
        {
            if (Type != ValueKind.Float)
                return this;

            var resolved = this;
            resolved.Float = DynamicValueExtensions.FromLiteral(ResolveFloat(context));
            return resolved;
        }

        public MaterialFxTypedValue ToTypedValue(ValueKind expectedType, IDynamicContext? context = null)
        {
            return expectedType switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(ResolveFloat(context)),
                ValueKind.Int => MaterialFxTypedValue.FromInt(Int),
                ValueKind.Bool => MaterialFxTypedValue.FromBool(Int != 0),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2(Float2),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3(Float3),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(Float4),
                ValueKind.Color => MaterialFxTypedValue.FromColor(Color),
                ValueKind.Texture => MaterialFxTypedValue.FromTexture(Texture),
                ValueKind.TextureArray => MaterialFxTypedValue.FromTextureArray(Texture),
                _ => MaterialFxTypedValue.GetDefaultFallback(expectedType)
            };
        }
    }

    /// <summary>
    /// MaterialFx プリセット。複数の StableKey + Value ペアを保持。
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialFxPreset", menuName = "Game/MaterialFx/MaterialFxPreset")]
    public sealed class MaterialFxPresetSO : ScriptableObject
    {

        [Tooltip("Preset entries. Each entry maps a StableKey to a value.")]
        [Sirenix.OdinInspector.ListDrawerSettings(ShowPaging = false, ShowFoldout = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> Entries = new();

    }
}
