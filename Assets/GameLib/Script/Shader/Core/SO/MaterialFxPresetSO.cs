#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using VInspector;
using Sirenix.OdinInspector;

namespace Game.MaterialFx
{
    /// <summary>
    /// プリセット内の 1 エントリ。StableKey + 値 + ブレンドモード。
    /// </summary>
    [Serializable]
    public struct MaterialFxPresetEntry
    {
        [Tooltip("StableKey（文字列）")]
        [MaterialFxPropertyPicker]
        public string Key;

        [Tooltip("値（シリアライズ用）")]
        public MaterialFxSerializedValue Value;

        [Tooltip("ブレンドモード")]
        public MaterialFxBlendMode BlendMode;

        [Tooltip("生存時間（秒）。-1 = 無期限（デフォルト）")]
        public float LifetimeSeconds;

        [Tooltip("Weight フェードを適用する")]
        public bool ApplyWeightFade;

        [Tooltip("フェード先 Weight（0=影響なし, 1=完全適用）")]
        public float TargetWeight;

        [Tooltip("フェード時間（秒）")]
        public float FadeDuration;

        [Tooltip("フェードの Ease")]
        public Ease FadeEase;
    }

    /// <summary>
    /// シリアライズ用の値コンテナ。Inspector で編集可能。
    /// </summary>
    [Serializable]
    public struct MaterialFxSerializedValue
    {
        public ValueKind Type;
        public float Float;
        public int Int;
        public Vector2 Float2;
        public Vector3 Float3;
        public Vector4 Float4;
        public Color Color;
        public Texture? Texture;

        public MaterialFxTypedValue ToTypedValue(ValueKind expectedType)
        {
            return expectedType switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(Float),
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
