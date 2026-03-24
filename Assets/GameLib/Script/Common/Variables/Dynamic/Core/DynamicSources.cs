// Game.Common.DynamicSources.cs
//
// 各種 IDynamicSource 実装
//
// 設計決定:
// - Literal: 定数値（非ジェネリック、Type選択あり）
// - Literal<T>: 型固定の定数値（ジェネリック、Type選択なし）
// - LiteralToVariable: 定数値 + VarStore への書き込み副作用（旧互換名）
// - VarStore: VarStore からの読み取り（varId ベース）
// - SelfScalar/OtherScalar: ScalarService からの読み取り（float のみ）
// - SelfBlackboard/OtherBlackboard: Blackboard からの読み取り
// - UnityObjectRef: Unity Object 参照

#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Game;
using Game.Scalar;
using Game.Commands;
using Game.Commands.VNext;
using Game.Channel;
using Game.Health;
using Game.Movement;
using Game.StateMachine;
using Game.Trait;
using VContainer;
using Object = UnityEngine.Object;
using Game.DI;

namespace Game.Common
{
    public enum BlackboardReadScope
    {
        Local = 0,
        Global = 1,
    }

    public enum BlackboardReadFallback
    {
        Default = 0,
        Fail = 1,
        CreateLocal = 2,
        CreateGameLogicRoot = 3,
        CreateRoot = 4,
    }

    // ================================================================
    // Literal Source（非ジェネリック、Type選択あり）
    // ================================================================

    /// <summary>
    /// 定数値ソース（int/float/bool/string/Vector/Color）。
    /// DynamicValue（非ジェネリック）で使用。
    /// </summary>
    [Serializable]
    public sealed class LiteralSource : IDynamicSource
    {
        public enum LiteralType { Int, Float, Bool, String, Vector2, Vector3, Vector4, Color }

        [SerializeField, LabelWidth(80)] LiteralType type = LiteralType.Float;

        [SerializeField, ShowIf(nameof(type), LiteralType.Int)]
        int intValue;

        [SerializeField, ShowIf(nameof(type), LiteralType.Float)]
        float floatValue;

        [SerializeField, ShowIf(nameof(type), LiteralType.Bool)]
        bool boolValue;

        [SerializeField, ShowIf(nameof(type), LiteralType.String)]
        string stringValue = string.Empty;

        [SerializeField, ShowIf(nameof(type), LiteralType.Vector2)]
        Vector2 vector2Value;

        [SerializeField, ShowIf(nameof(type), LiteralType.Vector3)]
        Vector3 vector3Value;

        [SerializeField, ShowIf(nameof(type), LiteralType.Vector4)]
        Vector4 vector4Value;

        [SerializeField, ShowIf(nameof(type), LiteralType.Color)]
        Color colorValue = Color.white;

        public string SourceTypeName => "Literal";
        public string GetDebugData => type switch
        {
            LiteralType.Int => intValue.ToString(),
            LiteralType.Float => floatValue.ToString(),
            LiteralType.Bool => boolValue.ToString(),
            LiteralType.String => stringValue ?? "null",
            LiteralType.Vector2 => vector2Value.ToString(),
            LiteralType.Vector3 => vector3Value.ToString(),
            LiteralType.Vector4 => vector4Value.ToString(),
            LiteralType.Color => colorValue.ToString(),
            _ => "Unknown"
        };

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            return type switch
            {
                LiteralType.Int => DynamicVariant.FromInt(intValue),
                LiteralType.Float => DynamicVariant.FromFloat(floatValue),
                LiteralType.Bool => DynamicVariant.FromBool(boolValue),
                LiteralType.String => DynamicVariant.FromString(stringValue),
                LiteralType.Vector2 => DynamicVariant.FromVector2(vector2Value),
                LiteralType.Vector3 => DynamicVariant.FromVector3(vector3Value),
                LiteralType.Vector4 => DynamicVariant.FromVector4(vector4Value),
                LiteralType.Color => DynamicVariant.FromColor(colorValue),
                _ => DynamicVariant.Null
            };
        }

        // ファクトリ
        public static LiteralSource FromInt(int value) => new() { type = LiteralType.Int, intValue = value };
        public static LiteralSource FromFloat(float value) => new() { type = LiteralType.Float, floatValue = value };
        public static LiteralSource FromBool(bool value) => new() { type = LiteralType.Bool, boolValue = value };
        public static LiteralSource FromString(string value) => new() { type = LiteralType.String, stringValue = value };
        public static LiteralSource FromVector2(Vector2 value) => new() { type = LiteralType.Vector2, vector2Value = value };
        public static LiteralSource FromVector3(Vector3 value) => new() { type = LiteralType.Vector3, vector3Value = value };
    }

    // ================================================================
    // 型固定 Literal Sources（DynamicValue<T> 用）
    // ================================================================

    /// <summary>int 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralIntSource : IDynamicSource
    {
        [SerializeField, HideLabel] int value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromInt(value);

        public LiteralIntSource()
        {
        }

        public LiteralIntSource(int value)
        {
            this.value = value;
        }
    }

    /// <summary>float 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralFloatSource : IDynamicSource
    {
        [SerializeField, HideLabel] float value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromFloat(value);

        public LiteralFloatSource()
        {
        }

        public LiteralFloatSource(float value)
        {
            this.value = value;
        }
    }

    /// <summary>bool 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralBoolSource : IDynamicSource
    {
        [SerializeField, HideLabel] bool value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromBool(value);

        public LiteralBoolSource()
        {
        }

        public LiteralBoolSource(bool value)
        {
            this.value = value;
        }
    }

    /// <summary>string 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStringSource : IDynamicSource
    {
        [SerializeField, HideLabel] string value = string.Empty;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value ?? "null";
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromString(value ?? string.Empty);

        public LiteralStringSource()
        {
        }

        public LiteralStringSource(string value)
        {
            this.value = value ?? string.Empty;
        }
    }

    /// <summary>Vector2 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralVector2Source : IDynamicSource
    {
        [SerializeField, HideLabel] Vector2 value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromVector2(value);

        public LiteralVector2Source()
        {
        }

        public LiteralVector2Source(Vector2 value)
        {
            this.value = value;
        }
    }

    /// <summary>Vector3 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralVector3Source : IDynamicSource
    {
        [SerializeField, HideLabel] Vector3 value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromVector3(value);

        public LiteralVector3Source()
        {
        }

        public LiteralVector3Source(Vector3 value)
        {
            this.value = value;
        }
    }

    /// <summary>Vector4 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralVector4Source : IDynamicSource
    {
        [SerializeField, HideLabel] Vector4 value;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromVector4(value);

        public LiteralVector4Source()
        {
        }

        public LiteralVector4Source(Vector4 value)
        {
            this.value = value;
        }
    }

    /// <summary>Color 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralColorSource : IDynamicSource
    {
        [SerializeField, HideLabel] Color value = Color.white;
        public string SourceTypeName => "Literal";
        public string GetDebugData => value.ToString();
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromColor(value);

        public LiteralColorSource()
        {
        }

        public LiteralColorSource(Color value)
        {
            this.value = value;
        }
    }

    /// <summary>AnimationSpritePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralAnimationSpritePresetSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        AnimationSpritePreset value = new();

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.playMode.ToString() : "null";
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromManagedRef(value);
    }

    /// <summary>AnimationSpritePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetAnimationSpritePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        AnimationSpritePresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.preset != null
                ? DynamicVariant.FromManagedRef(value.preset)
                : DynamicVariant.Null;
    }

    /// <summary>StateMachinePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStateMachinePresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StateMachinePreset? value;

        public LiteralStateMachinePresetSource()
        {
        }

        public LiteralStateMachinePresetSource(StateMachinePreset value)
        {
            this.value = value;
        }

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"layers={value.LayerPriorityOverrides.Count}, states={value.StatePriorityOverrides.Count}" : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StateMachinePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetStateMachinePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StateMachineProfileSO? value;

        public AssetStateMachinePresetSource()
        {
        }

        AssetStateMachinePresetSource(StateMachineProfileSO value)
        {
            this.value = value;
        }

        public static AssetStateMachinePresetSource FromAsset(StateMachineProfileSO value) => new(value);

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>StateAnimationPreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStateAnimationPresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StateAnimationPreset? value;

        public LiteralStateAnimationPresetSource()
        {
        }

        public LiteralStateAnimationPresetSource(StateAnimationPreset value)
        {
            this.value = value;
        }

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"rules={value.Rules.Count}" : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StateAnimationPreset アセット参照</summary>
    [Serializable]
    public sealed class AssetStateAnimationPresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StateAnimationProfileSO? value;

        public AssetStateAnimationPresetSource()
        {
        }

        AssetStateAnimationPresetSource(StateAnimationProfileSO value)
        {
            this.value = value;
        }

        public static AssetStateAnimationPresetSource FromAsset(StateAnimationProfileSO value) => new(value);

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>HealthPreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralHealthPresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        HealthPreset? value;

        public LiteralHealthPresetSource()
        {
        }

        public LiteralHealthPresetSource(HealthPreset value)
        {
            this.value = value;
        }

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"maxHp={value.MaxHPFallback}" : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>HealthPreset アセット参照</summary>
    [Serializable]
    public sealed class AssetHealthPresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        HealthProfileSO? value;

        public AssetHealthPresetSource()
        {
        }

        AssetHealthPresetSource(HealthProfileSO value)
        {
            this.value = value;
        }

        public static AssetHealthPresetSource FromAsset(HealthProfileSO value) => new(value);

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var preset = value?.Preset;
            return preset != null
                ? DynamicVariant.FromManagedRef(preset)
                : DynamicVariant.Null;
        }
    }

    /// <summary>MotionPreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralMotionPresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        MotionPreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.GetStableKey() : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>MotionPreset アセット参照</summary>
    [Serializable]
    public sealed class AssetMotionPresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        MotionPresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>TransformAnimationPreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralTransformAnimationPresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        TransformAnimationPreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"{value.Steps?.Count ?? 0} steps" : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>BaseRuntimeTemplatePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        BaseRuntimeTemplatePreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.TemplateId : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>BaseRuntimeTemplatePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        BaseRuntimeTemplatePresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>ParticleRuntimeTemplatePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralParticleRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        ParticleRuntimeTemplatePreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.TemplateId : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>ParticleRuntimeTemplatePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetParticleRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        ParticleRuntimeTemplatePresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>FirePatternRuntimeTemplatePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralFirePatternRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        FirePatternRuntimeTemplatePreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.TemplateId : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>FirePatternRuntimeTemplatePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetFirePatternRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        FirePatternRuntimeTemplatePresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>SpawnPatternRuntimeTemplatePreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralSpawnPatternRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        SpawnPatternRuntimeTemplatePreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.TemplateId : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Prefab != null
                ? DynamicVariant.FromManagedRef(value)
                : DynamicVariant.Null;
    }

    /// <summary>SpawnPatternRuntimeTemplatePreset アセット参照</summary>
    [Serializable]
    public sealed class AssetSpawnPatternRuntimeTemplatePresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        SpawnPatternRuntimeTemplatePresetAssetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";
        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null && value.Preset.Prefab != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>MaterialFxPayload 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralMaterialFxPayloadSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        MaterialFxPayload value = new();

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.ContextTag : "null";
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromManagedRef(value);
    }

    // ================================================================
    // Unity Object Reference Source
    // ================================================================

    /// <summary>
    /// Unity Object 参照ソース（非ジェネリック）。
    /// </summary>
    [Serializable]
    public sealed class UnityObjectRefSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        Object? objectValue;

        public string SourceTypeName => "Object";
        public string GetDebugData => objectValue != null ? objectValue.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            return DynamicVariant.FromUnityObject(objectValue);
        }

        public static UnityObjectRefSource FromObject(Object obj) => new() { objectValue = obj };
    }

    /// <summary>
    /// 型指定の Unity Object 参照ソース。
    /// DynamicValue&lt;AnimationData&gt; などで使用。
    /// </summary>
    [Serializable]
    public sealed class UnityObjectRefSource<T> : IDynamicSource where T : Object
    {
        [SerializeField, HideLabel]
        T? objectValue;

        public string SourceTypeName => "Object";
        public string GetDebugData => objectValue != null ? objectValue.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            return DynamicVariant.FromUnityObject(objectValue);
        }

        public static UnityObjectRefSource<T> FromObject(T obj) => new() { objectValue = obj };
    }

    /// <summary>
    /// TraitDefinitionSO 専用の asset 参照ソース。
    /// DynamicValue&lt;TraitDefinitionSO&gt; での authoring 意図を明確にする。
    /// </summary>
    [Serializable]
    public sealed class AssetTraitDefinitionSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        TraitDefinitionSO? asset;

        public string SourceTypeName => "Asset";
        public string GetDebugData => asset != null ? asset.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            _ = context;
            return DynamicVariant.FromUnityObject(asset);
        }

        public static AssetTraitDefinitionSource FromAsset(TraitDefinitionSO? value) => new() { asset = value };
    }

    // ================================================================
    // LiteralToVariable Source
    // ================================================================

    /// <summary>
    /// 定数値を返しつつ、VarStore にも書き込むソース。
    /// （後方互換のためクラス名は維持）
    /// </summary>
    [Serializable]
    public sealed class LiteralToVariableSource : IDynamicSource
    {
        [SerializeField] LiteralSource.LiteralType type = LiteralSource.LiteralType.Float;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Int)]
        int intValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Float)]
        float floatValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Bool)]
        bool boolValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.String)]
        string stringValue = string.Empty;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector2)]
        Vector2 vector2Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector3)]
        Vector3 vector3Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector4)]
        Vector4 vector4Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Color)]
        Color colorValue = Color.white;

        [SerializeField, LabelText("Variable Key")]
        string variableKey = string.Empty;

        public string SourceTypeName => "LiteralToVariable";
        public string GetDebugData
        {
            get
            {
                string names = variableKey + " : ";
                return names + type switch
                {
                    LiteralSource.LiteralType.Int => intValue.ToString(),
                    LiteralSource.LiteralType.Float => floatValue.ToString(),
                    LiteralSource.LiteralType.Bool => boolValue.ToString(),
                    LiteralSource.LiteralType.String => stringValue ?? "null",
                    LiteralSource.LiteralType.Vector2 => vector2Value.ToString(),
                    LiteralSource.LiteralType.Vector3 => vector3Value.ToString(),
                    LiteralSource.LiteralType.Vector4 => vector4Value.ToString(),
                    LiteralSource.LiteralType.Color => colorValue.ToString(),
                    _ => "Unknown"
                };
            }
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var result = type switch
            {
                LiteralSource.LiteralType.Int => DynamicVariant.FromInt(intValue),
                LiteralSource.LiteralType.Float => DynamicVariant.FromFloat(floatValue),
                LiteralSource.LiteralType.Bool => DynamicVariant.FromBool(boolValue),
                LiteralSource.LiteralType.String => DynamicVariant.FromString(stringValue),
                LiteralSource.LiteralType.Vector2 => DynamicVariant.FromVector2(vector2Value),
                LiteralSource.LiteralType.Vector3 => DynamicVariant.FromVector3(vector3Value),
                LiteralSource.LiteralType.Vector4 => DynamicVariant.FromVector4(vector4Value),
                LiteralSource.LiteralType.Color => DynamicVariant.FromColor(colorValue),
                _ => DynamicVariant.Null
            };

            // 副作用: VarStore に書き込み（旧互換）
            if (context?.Vars != null && !string.IsNullOrEmpty(variableKey))
            {
                if (VarIdResolver.TryResolve(variableKey, out var varId) && varId != 0)
                    context.Vars.TrySetVariant(varId, result);
            }

            return result;
        }


    }

    // ================================================================
    // VarStore Sources (vNext)
    // ================================================================

    /// <summary>
    /// VarStore(IVarStore) から値を読み取るソース（varId ベース）。
    /// Variant / ManagedRef 両方をサポート。
    /// </summary>
    [Serializable]
    public sealed class VarStoreSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        VarKeyRef key;

        public string SourceTypeName => "Var";
        public string GetDebugData => string.IsNullOrEmpty(key.StableKey)
            ? $"varId={key.VarId}"
            : $"{key.StableKey} (varId={key.VarId})";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Vars == null)
                return DynamicVariant.Null;

            var vars = context.Vars;
            var varId = ResolveVarId(vars);
            if (varId <= 0)
                return DynamicVariant.Null;

            if (vars.GetVarKind(varId) == ValueKind.ManagedRef)
            {
                // ManagedRef として取得（非UnityEngine.Object のクラスもサポート）
                if (vars.TryGetManagedRef(varId, out var managed) && managed != null)
                    return DynamicVariant.FromManagedRef(managed);
                return DynamicVariant.Null;
            }

            // Variant として取得を試みる
            if (vars.TryGetVariant(varId, out var v))
                return v;

            return DynamicVariant.Null;
        }

        int ResolveVarId(IVarStore vars)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId;
        }

        public static VarStoreSource FromVarId(int id) => new() { key = new VarKeyRef(id) };
    }

    /// <summary>
    /// 定数値を返しつつ、VarStore に副作用で書き込むソース。
    /// 多用するとデバッグ性が落ちるため、運用は原則禁止寄り。
    /// </summary>
    [Serializable]
    public sealed class LiteralToVarStoreSource : IDynamicSource
    {
        [SerializeField] LiteralSource.LiteralType type = LiteralSource.LiteralType.Float;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Int)]
        int intValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Float)]
        float floatValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Bool)]
        bool boolValue;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.String)]
        string stringValue = string.Empty;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector2)]
        Vector2 vector2Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector3)]
        Vector3 vector3Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Vector4)]
        Vector4 vector4Value;

        [SerializeField, ShowIf(nameof(type), LiteralSource.LiteralType.Color)]
        Color colorValue = Color.white;

        [SerializeField, InlineProperty, HideLabel]
        VarKeyRef target;

        public string SourceTypeName => "ConstSetVar";
        public string GetDebugData => target.VarId <= 0
            ? "unset target"
            : (string.IsNullOrEmpty(target.StableKey)
                ? $"set varId={target.VarId}"
                : $"set {target.StableKey} (varId={target.VarId})");

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var result = type switch
            {
                LiteralSource.LiteralType.Int => DynamicVariant.FromInt(intValue),
                LiteralSource.LiteralType.Float => DynamicVariant.FromFloat(floatValue),
                LiteralSource.LiteralType.Bool => DynamicVariant.FromBool(boolValue),
                LiteralSource.LiteralType.String => DynamicVariant.FromString(stringValue),
                LiteralSource.LiteralType.Vector2 => DynamicVariant.FromVector2(vector2Value),
                LiteralSource.LiteralType.Vector3 => DynamicVariant.FromVector3(vector3Value),
                LiteralSource.LiteralType.Vector4 => DynamicVariant.FromVector4(vector4Value),
                LiteralSource.LiteralType.Color => DynamicVariant.FromColor(colorValue),
                _ => DynamicVariant.Null
            };

            if (target.VarId <= 0)
                return result;

            if (context?.Vars == null)
                return result;

            if (result.Kind == ValueKind.Null)
                context.Vars.TryUnset(target.VarId);
            else
                context.Vars.TrySetVariant(target.VarId, result);

            return result;
        }
    }

    // ================================================================
    // Scalar Sources
    // ================================================================

    /// <summary>
    /// 自スコープの ScalarService から値を読み取るソース。
    /// </summary>
    [Serializable]
    public sealed class SelfScalarSource : IDynamicSource
    {
        [SerializeField]
        ScalarKey scalarKey;

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        [SerializeField, LabelText("Create If Missing")]
        bool createIfMissing;

        [SerializeField, ShowIf(nameof(createIfMissing)), LabelText("Baseline Value")]
        float baselineValue;

        [SerializeField, LabelText("Search Include Global")]
        bool searchIncludeGlobal;

        public ScalarKey ScalarKey => scalarKey;
        public string SourceTypeName => "SelfScalar";
        public string GetDebugData => $"{scalarKey} @ {targetActorSource.Kind}";

        public static SelfScalarSource FromScalarKey(
            ScalarKey scalarKey,
            bool createIfMissing = false,
            float baselineValue = 0f,
            bool searchIncludeGlobal = false)
        {
            return new SelfScalarSource
            {
                scalarKey = scalarKey,
                targetActorSource = new ActorSource { Kind = ActorSourceKind.Current },
                createIfMissing = createIfMissing,
                baselineValue = baselineValue,
                searchIncludeGlobal = searchIncludeGlobal,
            };
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (targetScope?.Resolver == null)
                return DynamicVariant.Null;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var svc))
                return DynamicVariant.Null;

            if (svc.LocalTryGet(scalarKey, out float value))
                return DynamicVariant.FromFloat(value);
            if (searchIncludeGlobal && svc.GlobalTryGet(scalarKey, out float gvalue))
                return DynamicVariant.FromFloat(gvalue);

            if (createIfMissing)
            {
                svc.SetRuntimeBaseline(scalarKey, baselineValue);
                return DynamicVariant.FromFloat(baselineValue);
            }

            return DynamicVariant.Null;
        }
    }

    /// <summary>
    /// 他スコープの ScalarService から値を読み取るソース。
    /// </summary>
    [Serializable]
    public sealed class OtherScalarSource : IDynamicSource
    {
        [SerializeField]
        ScalarKey scalarKey;

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.ContextSlot, ContextSlot = CommandLtsSlot.ContextA };

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        [SerializeField, LabelText("Create If Missing")]
        bool createIfMissing;

        [SerializeField, ShowIf(nameof(createIfMissing)), LabelText("Baseline Value")]
        float baselineValue;

        public ScalarKey ScalarKey => scalarKey;
        public string SourceTypeName => "OtherScalar";
        public string GetDebugData => $"{scalarKey} @ {targetActorSource.Kind}";

        public static OtherScalarSource FromScalarKey(
            ScalarKey scalarKey,
            ActorSource targetActorSource,
            bool createIfMissing = false,
            float baselineValue = 0f)
        {
            return new OtherScalarSource
            {
                scalarKey = scalarKey,
                targetActorSource = targetActorSource,
                createIfMissing = createIfMissing,
                baselineValue = baselineValue,
            };
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (targetScope?.Resolver == null)
                return DynamicVariant.Null;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var svc))
                return DynamicVariant.Null;

            if (svc.LocalTryGet(scalarKey, out float value))
                return DynamicVariant.FromFloat(value);

            if (createIfMissing)
            {
                svc.SetRuntimeBaseline(scalarKey, baselineValue);
                return DynamicVariant.FromFloat(baselineValue);
            }

            return DynamicVariant.Null;
        }
    }

    // ================================================================
    // Blackboard Sources
    // ================================================================

    /// <summary>
    /// 自スコープの Blackboard から値を読み取るソース。
    /// </summary>
    [Serializable]
    public sealed class SelfBlackboardSource : IDynamicSource
    {
        [SerializeField, LabelText("Blackboard Key"), VarIdDropdown]
        int blackboardId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Fallback")]
        BlackboardReadFallback fallback = BlackboardReadFallback.Default;

        [SerializeField, LabelText("Fallback Initial Value")]
        [ShowIf("@fallback == BlackboardReadFallback.CreateLocal || fallback == BlackboardReadFallback.CreateGameLogicRoot || fallback == BlackboardReadFallback.CreateRoot")]
        DynamicValue fallbackInitialValue;

        public string SourceTypeName => "SelfBlackboard";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || blackboardId == 0)
                return DynamicVariant.Null;

            var resolvedFallback = BlackboardSourceUtility.ResolveFallback(fallback, readScope);
            var initialValue = fallbackInitialValue.HasSource
                ? fallbackInitialValue.Evaluate(context)
                : DynamicVariant.Null;
            if (readScope == BlackboardReadScope.Global)
            {
                // Global means: search this scope -> parents, consulting each scope's *local* var store.
                // This avoids relying on which IBlackboardService instance DI returned.
                if (TryGetHierarchical(context.Scope, blackboardId, out var variant))
                    return variant;
                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"SelfBlackboard(global) resolve failed: key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} was not found in hierarchy from scope id={context.Scope.Identity?.Id ?? "(none)"}.");
                Debug.LogWarning($"[SelfBlackboardSource] Global read did not find varId={blackboardId}({VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}) in scope hierarchy starting from scope kind={context.Scope.Kind}, id={context.Scope.Identity?.Id ?? "(none)"}");
                return BlackboardSourceUtility.ApplyFallback(context.Scope, null, blackboardId, resolvedFallback, initialValue);
            }
            if (!context.Scope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null)
            {
                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"SelfBlackboard(local) resolve failed: IBlackboardService is missing on scope id={context.Scope.Identity?.Id ?? "(none)"}.");
                return DynamicVariant.Null;
            }

            var localVars = bb.LocalVars;
            if (localVars != null)
            {
                var kind = localVars.GetVarKind(blackboardId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (localVars.TryGetManagedRef(blackboardId, out var managed) && managed != null)
                        return DynamicVariant.FromManagedRef(managed);
                }
                else if (kind != ValueKind.Null && bb.TryLocalGetVariant(blackboardId, out var localVariant))
                {
                    return localVariant;
                }
            }

            if (resolvedFallback == BlackboardReadFallback.Fail)
                return BlackboardSourceUtility.FailOrNull(
                    context,
                    $"SelfBlackboard(local) resolve failed: key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} was not found on scope id={context.Scope.Identity?.Id ?? "(none)"}.");

            Debug.LogWarning($"[SelfBlackboardSource] Local read did not find varId={blackboardId}({VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}) in scope kind={context.Scope.Kind}, id={context.Scope.Identity?.Id ?? "(none)"}");
            return BlackboardSourceUtility.ApplyFallback(context.Scope, bb, blackboardId, resolvedFallback, initialValue);
        }

        static bool TryGetHierarchical(IScopeNode? origin, int varId, out DynamicVariant value)
        {
            // Search nearest -> farthest by scope parent chain.
            // We intentionally consult each scope's *local* var store to avoid
            // depending on which IBlackboardService instance DI happens to return.
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IBlackboardService>(out var bb) || bb == null)
                    continue;

                var local = bb.LocalVars;
                if (local == null || !local.Contains(varId))
                    continue;

                var kind = local.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (local.TryGetManagedRef(varId, out var managed) && managed != null)
                    {
                        value = DynamicVariant.FromManagedRef(managed);
                        return true;
                    }
                }
                else if (bb.TryLocalGetVariant(varId, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }
        public static SelfBlackboardSource FromVarId(
            int key,
            BlackboardReadScope scope = BlackboardReadScope.Local,
            BlackboardReadFallback fallbackMode = BlackboardReadFallback.Default)
            => new() { blackboardId = key, readScope = scope, fallback = fallbackMode };
    }

    /// <summary>
    /// 他スコープの Blackboard から値を読み取るソース。
    /// </summary>
    [Serializable]
    public sealed class OtherBlackboardSource : IDynamicSource
    {
        [SerializeField, LabelText("Blackboard Key"), VarIdDropdown]
        int blackboardId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Fallback")]
        BlackboardReadFallback fallback = BlackboardReadFallback.Default;

        [SerializeField, LabelText("Fallback Initial Value")]
        [ShowIf("@fallback == BlackboardReadFallback.CreateLocal || fallback == BlackboardReadFallback.CreateGameLogicRoot || fallback == BlackboardReadFallback.CreateRoot")]
        DynamicValue fallbackInitialValue;

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherBlackboard";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || blackboardId == 0)
                return DynamicVariant.Null;

            var resolvedFallback = BlackboardSourceUtility.ResolveFallback(fallback, readScope);
            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope?.Resolver == null)
            {
                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"OtherBlackboard resolve failed: target actor scope could not be resolved. key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} actorKind={targetActor.Kind}.");
                return DynamicVariant.Null;
            }

            var evalContext = new SimpleDynamicContext(context.Vars ?? NullVarStore.Instance, targetScope);
            var initialValue = fallbackInitialValue.HasSource
                ? fallbackInitialValue.Evaluate(evalContext)
                : DynamicVariant.Null;
            if (readScope == BlackboardReadScope.Global)
            {
                if (TryGetHierarchical(targetScope, blackboardId, out var variant))
                    return variant;

                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"OtherBlackboard(global) resolve failed: key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} was not found in hierarchy from target scope id={targetScope.Identity?.Id ?? "(none)"}.");
                return BlackboardSourceUtility.ApplyFallback(targetScope, null, blackboardId, resolvedFallback, initialValue);
            }

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null)
            {
                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"OtherBlackboard(local) resolve failed: IBlackboardService is missing on target scope id={targetScope.Identity?.Id ?? "(none)"}.");
                return DynamicVariant.Null;
            }

            var localVars = bb.LocalVars;
            if (localVars != null)
            {
                var kind = localVars.GetVarKind(blackboardId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (localVars.TryGetManagedRef(blackboardId, out var managed) && managed != null)
                        return DynamicVariant.FromManagedRef(managed);
                }
                else if (kind != ValueKind.Null && bb.TryLocalGetVariant(blackboardId, out var localVariant))
                {
                    return localVariant;
                }
            }

            if (resolvedFallback == BlackboardReadFallback.Fail)
                return BlackboardSourceUtility.FailOrNull(
                    context,
                    $"OtherBlackboard(local) resolve failed: key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} was not found on target scope id={targetScope.Identity?.Id ?? "(none)"}.");

            return BlackboardSourceUtility.ApplyFallback(targetScope, bb, blackboardId, resolvedFallback, initialValue);
        }

        static bool TryGetHierarchical(IScopeNode? origin, int varId, out DynamicVariant value)
        {
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IBlackboardService>(out var bb) || bb == null)
                    continue;

                var local = bb.LocalVars;
                if (local == null || !local.Contains(varId))
                    continue;

                var kind = local.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (local.TryGetManagedRef(varId, out var managed) && managed != null)
                    {
                        value = DynamicVariant.FromManagedRef(managed);
                        return true;
                    }
                }
                else if (bb.TryLocalGetVariant(varId, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    static class BlackboardSourceUtility
    {
        public static DynamicVariant FailOrNull(IDynamicContext? context, string message)
        {
            if (context is CommandContext)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, message);

            Debug.LogError(message);
            return DynamicVariant.Null;
        }

        public static BlackboardReadFallback ResolveFallback(BlackboardReadFallback fallback, BlackboardReadScope readScope)
        {
            if (fallback != BlackboardReadFallback.Default)
                return fallback;
            return readScope == BlackboardReadScope.Global
                ? BlackboardReadFallback.CreateGameLogicRoot
                : BlackboardReadFallback.CreateLocal;
        }

        public static DynamicVariant ApplyFallback(
            IScopeNode origin,
            IBlackboardService? localBlackboard,
            int varId,
            BlackboardReadFallback fallback,
            in DynamicVariant initialValue)
        {
            if (origin == null || varId == 0 || fallback == BlackboardReadFallback.Fail)
                return DynamicVariant.Null;

            var value = initialValue;
            switch (fallback)
            {
                case BlackboardReadFallback.CreateLocal:
                    if (localBlackboard != null && localBlackboard.TryLocalSetVariant(varId, in value))
                        return value;
                    return DynamicVariant.Null;

                case BlackboardReadFallback.CreateGameLogicRoot:
                    return TrySetOnGameLogicRoot(origin, varId, in value) ? value : DynamicVariant.Null;

                case BlackboardReadFallback.CreateRoot:
                    return TrySetOnRoot(origin, varId, in value) ? value : DynamicVariant.Null;

                default:
                    return DynamicVariant.Null;
            }
        }

        static bool TrySetOnGameLogicRoot(IScopeNode origin, int varId, in DynamicVariant value)
        {
            var logicRoot = ScopeNodeHierarchy.FindNearestGameLogicRoot(origin, includeSelf: true);
            if (logicRoot != null && TryResolveBlackboard(logicRoot, out var bb) && bb != null)
                return bb.TryLocalSetVariant(varId, in value);

            return TrySetOnRoot(origin, varId, in value);
        }

        static bool TrySetOnRoot(IScopeNode? origin, int varId, in DynamicVariant value)
        {
            IBlackboardService? root = null;
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                root = bb;
            }

            return root?.TryLocalSetVariant(varId, in value) ?? false;
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve(out blackboard) || blackboard == null)
                return false;

            return true;
        }
    }

    // ================================================================
    // ManagedRef Sources (non-UnityEngine.Object)
    // ================================================================

    /// <summary>
    /// インラインで任意の参照型（非UnityEngine.Object）を格納するソース。
    /// SerializeReference を使用し、[Serializable] なクラスを直接格納可能。
    /// DynamicValue&lt;T&gt; と組み合わせて使用。
    /// </summary>
    [Serializable]
    public sealed class ManagedRefSource : IDynamicSource
    {
        [SerializeReference, HideLabel]
        object? value;

        public string SourceTypeName => "ManagedRef";
        public string GetDebugData => value?.GetType().Name ?? "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (value == null)
                return DynamicVariant.Null;
            return DynamicVariant.FromManagedRef(value);
        }

        /// <summary>
        /// ファクトリメソッド。
        /// </summary>
        public static ManagedRefSource FromValue(object? obj) => new() { value = obj };
    }
}
