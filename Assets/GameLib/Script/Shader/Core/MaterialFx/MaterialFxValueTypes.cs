using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// プロパティの送信先を示す。
    /// BaseShader = MPB/Material/Global 側（実運用は基本 MPB）
    /// </summary>
    public enum MaterialFxSenderKind
    {
        BaseShader = 0,
    }

    /// <summary>
    /// シェーダープロパティの値型。
    /// </summary>
    public enum ValueKind
    {
        Float = 0,
        Float2 = 1,
        Float3 = 2,
        Float4 = 3,
        Int = 4,
        Bool = 5,
        Color = 6,
        Matrix4x4 = 7,
        Texture = 8,
        TextureArray = 9,
        AnimationCurve = 10,
    }

    /// FlashプロパティーのMode列挙型(0=Lerp,1=Add)
    public enum MaterialFxFlashMode
    {
        Lerp = 0,
        Add = 1
    }

    // ColorBlendMode列挙型
    public enum MaterialFxColorBlendMode
    {
        Normal = 0,
        Multiply = 1,
        Additive = 2,
        Screen = 3,
        Overlay = 4,
        SoftLight = 5,
        HardLight = 6,
        ColorBurn = 7,
        ColorDodge = 8,
        Darken = 9,
        Lighten = 10,
        Difference = 11,
        Exclusion = 12,
        Hue = 13,
        Saturation = 14,
        Color = 15,
        Luminosity = 16
    }

    // AlphaMode列挙型(0=Preserve,1=fromRamp,2=FromNoise)
    public enum MaterialFxAlphaMode
    {
        Preserve = 0,
        FromRamp = 1,
        FromNoise = 2
    }
    public enum AdvancedFade2DDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3,
        RadialIn = 4,
        RadialOut = 5,
    }



    /// <summary>
    /// プロパティのメタデータ。Registry から取得される。
    /// </summary>
    [Serializable]
    public struct MaterialFxPropertyMeta
    {
        public string StableKey;            // 例: "BaseShader/Flash/Mode"
        public MaterialFxSenderKind Sender;
        public ValueKind ValueType;
        public string ShaderPropertyName;   // 例: "_FlashMode" / "_NoiseArray" etc
        public int ShaderPropertyId; // cached Shader.PropertyToID value (0 = not set)
        public string Description;

        /// <summary>
        /// Int/Float 型の場合に参照する EnumDefinition。
        /// null の場合は通常の数値入力。
        /// </summary>
        public MaterialFxEnumDefinitionSO EnumDefinition;

        /// <summary>EnumDefinition が設定されているか</summary>
        public bool HasEnumDefinition => EnumDefinition != null;

        /// <summary>Float 型の Range 制約が有効か</summary>
        public bool RangeEnabled;

        /// <summary>Range の最小値・最大値（x=min, y=max）</summary>
        public Vector2 RangeMinMax;
    }

    /// <summary>
    /// プロパティレジストリのランタイムインターフェース。
    /// </summary>
    public interface IMaterialFxPropertyRegistry
    {
        /// <summary>StableKey から完全なメタデータを取得</summary>
        bool TryGet(string stableKey, out MaterialFxPropertyMeta meta);

        /// <summary>StableKey から ValueType のみを高速取得</summary>
        bool TryGetValueType(string stableKey, out ValueKind type);

        /// <summary>StableKey から Sender のみを高速取得</summary>
        bool TryGetSender(string stableKey, out MaterialFxSenderKind sender);
    }
}
