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
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Game;
using Game.Scalar;
using Game.Commands;
using Game.Commands.VNext;
using Game.Channel;
using Game.Health;
using Game.Movement;
using Game.StateMachine;
using Game.Trait;
using Game.StatusEffect;
using Game.UI;
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

    /// <summary>CommandListData 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralCommandListDataSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        CommandListData? value = new();

        public LiteralCommandListDataSource()
        {
        }

        public LiteralCommandListDataSource(CommandListData? value)
        {
            this.value = value;
        }

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"{value.Count} commands" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>BaseStatusEffectDefinitionData 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStatusEffectDefinitionSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        BaseStatusEffectDefinitionData? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.DefinitionId : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>BaseStatusEffectDefinitionData アセット参照</summary>
    [Serializable]
    public sealed class AssetStatusEffectDefinitionSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StatusEffectDefinitionSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>StatusEffectStackPreset 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStatusEffectStackPresetSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StatusEffectStackPreset? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? "stack-preset" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StatusEffectStackPreset アセット参照</summary>
    [Serializable]
    public sealed class AssetStatusEffectStackPresetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StatusEffectStackPresetSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalLifetimeSettings 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStatusEffectGlobalLifetimeSettingsSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StatusEffectGlobalLifetimeSettings? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? "global-lifetime" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalLifetimeSettings アセット参照</summary>
    [Serializable]
    public sealed class AssetStatusEffectGlobalLifetimeSettingsSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StatusEffectGlobalLifetimeSettingsSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalUseCooldownSettings 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStatusEffectGlobalUseCooldownSettingsSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StatusEffectGlobalUseCooldownSettings? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? "global-use-cooldown" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalUseCooldownSettings アセット参照</summary>
    [Serializable]
    public sealed class AssetStatusEffectGlobalUseCooldownSettingsSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StatusEffectGlobalUseCooldownSettingsSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalCountSettings 固定リテラル</summary>
    [Serializable]
    public sealed class LiteralStatusEffectGlobalCountSettingsSource : IDynamicSource
    {
        [SerializeReference, InlineProperty, HideLabel]
        StatusEffectGlobalCountSettings? value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? "global-count" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>StatusEffectGlobalCountSettings アセット参照</summary>
    [Serializable]
    public sealed class AssetStatusEffectGlobalCountSettingsSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        StatusEffectGlobalCountSettingsSO? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null && value.Preset != null
                ? DynamicVariant.FromManagedRef(value.Preset)
                : DynamicVariant.Null;
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

        public int VarId => ResolveVarId();

        public string SourceTypeName => "Var";
        public string GetDebugData => string.IsNullOrEmpty(key.StableKey)
            ? $"varId={key.VarId}"
            : $"{key.StableKey} (varId={key.VarId})";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Vars == null)
                return DynamicVariant.Null;

            var vars = context.Vars;
            var varId = ResolveVarId();
            if (varId <= 0)
                return DynamicVariant.Null;

            if (vars.GetVarKind(varId) == ValueKind.ManagedRef)
            {
                // ManagedRef として取得（非UnityEngine.Object のクラスもサポート）
                if (vars.TryGetManagedRef(varId, out var managed) && managed != null)
                {
                    if (DeferredDynamicVarResolver.TryResolve(managed, context, $"VarStore:{varId}", out var deferred))
                        return deferred;
                    return DynamicVariant.FromManagedRef(managed);
                }
                return DynamicVariant.Null;
            }

            // Variant として取得を試みる
            if (vars.TryGetVariant(varId, out var v))
                return v;

            return DynamicVariant.Null;
        }

        int ResolveVarId()
        {
            if (key.VarId > 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return 0;
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
        public ActorSource TargetActorSource => targetActorSource;
        public string SourceTypeName => "SelfScalar";
        public string GetDebugData => $"{scalarKey} @ {DescribeTargetActorSource()}";

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
            if (targetScope == null)
                return DynamicVariant.Null;

            if (!TryResolveScalarService(targetScope, out var svc) || svc == null)
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

        static bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? svc)
        {
            for (var node = scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IBaseScalarService>(out var resolved) && resolved != null)
                {
                    svc = resolved;
                    return true;
                }
            }

            svc = null;
            return false;
        }

        string DescribeTargetActorSource()
        {
            return ActorSourceOdinLabelHelper.GetLabel("Target", targetActorSource);
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
        public ActorSource TargetActorSource => targetActorSource;
        public string SourceTypeName => "OtherScalar";
        public string GetDebugData => $"{scalarKey} @ {DescribeTargetActorSource()}";

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
            if (targetScope == null)
                return DynamicVariant.Null;

            if (!TryResolveScalarService(targetScope, out var svc) || svc == null)
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

        static bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? svc)
        {
            for (var node = scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IBaseScalarService>(out var resolved) && resolved != null)
                {
                    svc = resolved;
                    return true;
                }
            }

            svc = null;
            return false;
        }

        string DescribeTargetActorSource()
        {
            return ActorSourceOdinLabelHelper.GetLabel("Target", targetActorSource);
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

        public int BlackboardVarId => blackboardId;
        public BlackboardReadScope ReadScope => readScope;

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
                if (TryGetHierarchical(context.Scope, blackboardId, context, out var variant))
                    return variant;
                if (resolvedFallback == BlackboardReadFallback.Fail)
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"SelfBlackboard(global) resolve failed: key='{VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)"}' varId={blackboardId} was not found in hierarchy from scope id={context.Scope.Identity?.Id ?? "(none)"}.");
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
                    {
                        if (DeferredDynamicVarResolver.TryResolve(managed, context, $"SelfBlackboard:local:{blackboardId}", out var deferred))
                            return deferred;
                        return DynamicVariant.FromManagedRef(managed);
                    }
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
            return BlackboardSourceUtility.ApplyFallback(context.Scope, bb, blackboardId, resolvedFallback, initialValue);
        }

        static bool TryGetHierarchical(IScopeNode? origin, int varId, IDynamicContext context, out DynamicVariant value)
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
                        if (DeferredDynamicVarResolver.TryResolve(managed, context, $"SelfBlackboard:global:{varId}@{node.Identity?.Id ?? "(none)"}", out var deferred))
                        {
                            value = deferred;
                            return true;
                        }

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

        public int BlackboardVarId => blackboardId;
        public BlackboardReadScope ReadScope => readScope;
        public ActorSource TargetActor => targetActor;

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
                if (TryGetHierarchical(targetScope, blackboardId, context, out var variant))
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
                    {
                        if (DeferredDynamicVarResolver.TryResolve(managed, context, $"OtherBlackboard:local:{blackboardId}@{targetScope.Identity?.Id ?? "(none)"}", out var deferred))
                            return deferred;
                        return DynamicVariant.FromManagedRef(managed);
                    }
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

        static bool TryGetHierarchical(IScopeNode? origin, int varId, IDynamicContext context, out DynamicVariant value)
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
                        if (DeferredDynamicVarResolver.TryResolve(managed, context, $"OtherBlackboard:global:{varId}@{node.Identity?.Id ?? "(none)"}", out var deferred))
                        {
                            value = deferred;
                            return true;
                        }

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

    [Serializable]
    public sealed class SelfGridBlackboardSource : IDynamicSource
    {
        [SerializeField, LabelText("Var Keyで絞り込む")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        [FormerlySerializedAs("gridVarId")]
        int varIdFilter;

        [SerializeField, LabelText("参照スコープ")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        public string SourceTypeName => "SelfGridBlackboard";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(first var in cell)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            if (!GridBlackboardSourceUtility.TryEvaluateIndices(context, row, column, out var rowIndex, out var columnIndex))
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                return GridBlackboardSourceUtility.TryGetHierarchical(context.Scope, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var found)
                    ? found
                    : DynamicVariant.Null;
            }

            if (!context.Scope.Resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                return DynamicVariant.Null;

            return grid.TryGetCellVariant(rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var value, out _)
                ? value
                : DynamicVariant.Null;
        }

    }

    [Serializable]
    public sealed class OtherGridBlackboardSource : IDynamicSource
    {
        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        [FormerlySerializedAs("gridVarId")]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherGridBlackboard";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(first var in cell)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            if (!GridBlackboardSourceUtility.TryEvaluateIndices(context, row, column, out var rowIndex, out var columnIndex))
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope?.Resolver == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                return GridBlackboardSourceUtility.TryGetHierarchical(targetScope, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var found)
                    ? found
                    : DynamicVariant.Null;
            }

            if (!targetScope.Resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                return DynamicVariant.Null;

            return grid.TryGetCellVariant(rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var value, out _)
                ? value
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class SelfGridBlackboardColumnCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        [FormerlySerializedAs("gridVarId")]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Target Row")]
        DynamicValue<int> targetRow = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        public string SourceTypeName => "SelfGridBlackboardColumnCount";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(all vars)";

        internal bool TryGetGridLinkHint(IDynamicContext context, out int rowIndex, out int filterVarId)
        {
            rowIndex = GridBlackboardSourceUtility.EvaluateIndex(context, targetRow);
            filterVarId = 0;
            if (rowIndex < 0)
                return false;

            if (useVarKeyFilter)
            {
                if (varIdFilter <= 0)
                    return false;

                filterVarId = varIdFilter;
            }

            return true;
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            var rowIndex = GridBlackboardSourceUtility.EvaluateIndex(context, targetRow);
            if (rowIndex < 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    var resolver = node.Resolver;
                    if (resolver == null)
                        continue;

                    if (!resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                        continue;

                    if (useVarKeyFilter
                        ? grid.TryGetColumnCount(varIdFilter, rowIndex, out var count)
                        : grid.TryGetColumnCount(rowIndex, out count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!context.Scope.Resolver.TryResolve<IGridBlackboardService>(out var selfGrid) || selfGrid == null)
                return DynamicVariant.Null;

            return (useVarKeyFilter
                ? selfGrid.TryGetColumnCount(varIdFilter, rowIndex, out var localCount)
                : selfGrid.TryGetColumnCount(rowIndex, out localCount))
            ? DynamicVariant.FromInt(localCount)
            : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class OtherGridBlackboardColumnCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Target Row")]
        DynamicValue<int> targetRow = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherGridBlackboardColumnCount";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(all vars)";

        internal bool TryGetGridLinkHint(IDynamicContext context, out int rowIndex, out int filterVarId)
        {
            rowIndex = GridBlackboardSourceUtility.EvaluateIndex(context, targetRow);
            filterVarId = 0;
            if (rowIndex < 0)
                return false;

            if (useVarKeyFilter)
            {
                if (varIdFilter <= 0)
                    return false;

                filterVarId = varIdFilter;
            }

            return true;
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            var rowIndex = GridBlackboardSourceUtility.EvaluateIndex(context, targetRow);
            if (rowIndex < 0)
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope?.Resolver == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    var resolver = node.Resolver;
                    if (resolver == null)
                        continue;

                    if (!resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                        continue;

                    if (useVarKeyFilter
                        ? grid.TryGetColumnCount(varIdFilter, rowIndex, out var count)
                        : grid.TryGetColumnCount(rowIndex, out count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!targetScope.Resolver.TryResolve<IGridBlackboardService>(out var localGrid) || localGrid == null)
                return DynamicVariant.Null;

            return (useVarKeyFilter
                    ? localGrid.TryGetColumnCount(varIdFilter, rowIndex, out var localCount)
                    : localGrid.TryGetColumnCount(rowIndex, out localCount))
                ? DynamicVariant.FromInt(localCount)
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class SelfGridBlackboardRowCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        [FormerlySerializedAs("gridVarId")]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        public string SourceTypeName => "SelfGridBlackboardRowCount";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(all vars)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    var resolver = node.Resolver;
                    if (resolver == null)
                        continue;

                    if (!resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                        continue;

                    if (useVarKeyFilter
                        ? grid.TryGetRowCount(varIdFilter, out var count)
                        : grid.TryGetRowCount(out count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!context.Scope.Resolver.TryResolve<IGridBlackboardService>(out var selfGrid) || selfGrid == null)
                return DynamicVariant.Null;

            return (useVarKeyFilter
                ? selfGrid.TryGetRowCount(varIdFilter, out var localCount)
                : selfGrid.TryGetRowCount(out localCount))
            ? DynamicVariant.FromInt(localCount)
            : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class SelfTableRowCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        public string SourceTypeName => "SelfTableRowCount";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || tableVarId == 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableRowCount(tableVarId, out var count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return DynamicVariant.Null;

            return localVars.TryGetTableRowCount(tableVarId, out var localCount)
                ? DynamicVariant.FromInt(localCount)
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class OtherTableRowCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherTableRowCount";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || tableVarId == 0)
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableRowCount(tableVarId, out var count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return DynamicVariant.Null;

            return localVars.TryGetTableRowCount(tableVarId, out var localCount)
                ? DynamicVariant.FromInt(localCount)
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class SelfTableColumnCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        public string SourceTypeName => "SelfTableColumnCount";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || tableVarId == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            if (rowIndex < 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableColumnCount(tableVarId, rowIndex, out var count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return DynamicVariant.Null;

            return localVars.TryGetTableColumnCount(tableVarId, rowIndex, out var localCount)
                ? DynamicVariant.FromInt(localCount)
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class OtherTableColumnCountSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherTableColumnCount";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || tableVarId == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            if (rowIndex < 0)
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableColumnCount(tableVarId, rowIndex, out var count))
                        return DynamicVariant.FromInt(count);
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return DynamicVariant.Null;

            return localVars.TryGetTableColumnCount(tableVarId, rowIndex, out var localCount)
                ? DynamicVariant.FromInt(localCount)
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class SelfTableCellExistsSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        public string SourceTypeName => "SelfTableCellExists";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || tableVarId == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryHasTableCell(tableVarId, rowIndex, columnIndex))
                        return DynamicVariant.FromBool(true);
                }

                return DynamicVariant.FromBool(false);
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return DynamicVariant.Null;

            return DynamicVariant.FromBool(localVars.TryHasTableCell(tableVarId, rowIndex, columnIndex));
        }
    }

    [Serializable]
    public sealed class OtherTableCellExistsSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherTableCellExists";
        public string GetDebugData => VarIdResolver.TryGetIdToStable(tableVarId) ?? "(none)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || tableVarId == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryHasTableCell(tableVarId, rowIndex, columnIndex))
                        return DynamicVariant.FromBool(true);
                }

                return DynamicVariant.FromBool(false);
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return DynamicVariant.Null;

            return DynamicVariant.FromBool(localVars.TryHasTableCell(tableVarId, rowIndex, columnIndex));
        }
    }

    [Serializable]
    public sealed class SelfTableCellSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        public string SourceTypeName => "SelfTableCell";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(first var in cell)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || tableVarId == 0)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (TableVarStoreSourceUtility.TryGetCellVariant(vars, tableVarId, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var value))
                        return value;
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return DynamicVariant.Null;

            return TableVarStoreSourceUtility.TryGetCellVariant(localVars, tableVarId, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var localValue)
                ? localValue
                : DynamicVariant.Null;
        }
    }

    [Serializable]
    public sealed class OtherTableCellSource : IDynamicSource
    {
        [SerializeField, LabelText("Table Var Key"), VarIdDropdown]
        int tableVarId;

        [SerializeField, LabelText("Use Var Key Filter")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        int varIdFilter;

        [SerializeField, LabelText("Read Scope")]
        BlackboardReadScope readScope = BlackboardReadScope.Local;

        [SerializeField, LabelText("Row")]
        DynamicValue<int> row = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("Column")]
        DynamicValue<int> column = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [SerializeField, LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor)")]
        ActorSource targetActor;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "OtherTableCell";
        public string GetDebugData
            => useVarKeyFilter
                ? (VarIdResolver.TryGetIdToStable(varIdFilter) ?? "(none)")
                : "(first var in cell)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || tableVarId == 0)
                return DynamicVariant.Null;

            if (useVarKeyFilter && varIdFilter == 0)
                return DynamicVariant.Null;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return DynamicVariant.Null;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return DynamicVariant.Null;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (TableVarStoreSourceUtility.TryGetCellVariant(vars, tableVarId, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var value))
                        return value;
                }

                return DynamicVariant.Null;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return DynamicVariant.Null;

            return TableVarStoreSourceUtility.TryGetCellVariant(localVars, tableVarId, rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out var localValue)
                ? localValue
                : DynamicVariant.Null;
        }
    }

    static class TableVarStoreSourceUtility
    {
        public static int EvaluateIndex(IDynamicContext context, DynamicValue<int> value)
        {
            if (value.HasSource)
                return value.Evaluate(context).TryGet<int>(out var evaluated) ? evaluated : -1;

            return 0;
        }

        public static bool TryResolveScopeVars(IScopeNode? scope, out IVarStore vars)
        {
            vars = NullVarStore.Instance;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IVarStore>(out var resolved) || resolved == null)
                return false;

            vars = resolved;
            return true;
        }

        public static bool TryGetCellVariant(
            IVarStore vars,
            int tableVarId,
            int rowIndex,
            int columnIndex,
            bool useVarKeyFilter,
            int varIdFilter,
            out DynamicVariant value)
        {
            if (!vars.TryGetTableCellStore(tableVarId, rowIndex, columnIndex, out var cellVars))
            {
                value = default;
                return false;
            }

            if (useVarKeyFilter)
            {
                if (cellVars.GetVarKind(varIdFilter) == ValueKind.ManagedRef)
                {
                    value = default;
                    return false;
                }

                return cellVars.TryGetVariant(varIdFilter, out value);
            }

            foreach (var varId in cellVars.EnumerateVarIds())
            {
                if (cellVars.GetVarKind(varId) == ValueKind.ManagedRef)
                    continue;

                if (cellVars.TryGetVariant(varId, out value))
                    return true;
            }

            value = default;
            return false;
        }
    }

    static class GridBlackboardSourceUtility
    {
        public static bool TryEvaluateIndices(IDynamicContext context, DynamicValue<int> rowValue, DynamicValue<int> columnValue, out int rowIndex, out int columnIndex)
        {
            rowIndex = EvaluateIndex(context, rowValue);
            columnIndex = EvaluateIndex(context, columnValue);
            return rowIndex >= 0 && columnIndex >= 0;
        }

        public static int EvaluateIndex(IDynamicContext context, DynamicValue<int> value)
        {
            if (value.HasSource)
                return value.Evaluate(context).TryGet<int>(out var evaluated) ? evaluated : -1;

            return 0;
        }

        public static bool TryGetHierarchical(
            IScopeNode? origin,
            int rowIndex,
            int columnIndex,
            bool useVarKeyFilter,
            int varIdFilter,
            out DynamicVariant value)
        {
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                    continue;

                if (grid.TryGetCellVariant(rowIndex, columnIndex, useVarKeyFilter, varIdFilter, out value, out _))
                    return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// ActorSource の存在判定ソース。
    /// - ResolveOnly: 指定 ActorSource が解決できるかを判定する
    /// - SearchRelation: 指定 ActorSource を起点に、親/子方向へ別 ActorSource を探索する
    /// </summary>
    [Serializable]
    public sealed class ActorSourceExistsSource : IDynamicSource
    {
        public enum EvaluateMode
        {
            ResolveOnly = 0,
            SearchRelation = 1,
        }

        public enum SearchDirection
        {
            Parent = 0,
            Child = 1,
        }

        [SerializeField, LabelText("Mode"), EnumToggleButtons]
        EvaluateMode mode = EvaluateMode.ResolveOnly;

        [SerializeField, ShowIf(nameof(IsResolveOnlyMode))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, ShowIf(nameof(IsSearchRelationMode))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(originActorSource)")]
        ActorSource originActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, ShowIf(nameof(IsSearchRelationMode))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(relationTargetActorSource)")]
        ActorSource relationTargetActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, ShowIf(nameof(IsSearchRelationMode)), EnumToggleButtons]
        SearchDirection searchDirection = SearchDirection.Parent;

        [SerializeField, ShowIf(nameof(IsSearchRelationMode)), MinValue(0)]
        [LabelText("Search Range")]
        [PropertyTooltip("0 = unlimited. 1 = immediate parent/child only.")]
        int searchRange;

        [NonSerialized]
        ActorSourceResolveCache _originActorCache;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "ActorExists";
        public string GetDebugData => mode switch
        {
            EvaluateMode.ResolveOnly => $"Target={DescribeActorSource(targetActorSource)}",
            EvaluateMode.SearchRelation => $"Origin={DescribeActorSource(originActorSource)} Target={DescribeActorSource(relationTargetActorSource)} Dir={searchDirection} Range={(searchRange <= 0 ? "All" : searchRange.ToString())}",
            _ => "Unknown",
        };

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            var result = mode switch
            {
                EvaluateMode.ResolveOnly => EvaluateResolveOnly(context),
                EvaluateMode.SearchRelation => EvaluateSearchRelation(context),
                _ => false,
            };

            return DynamicVariant.FromBool(result);
        }

        bool EvaluateResolveOnly(IDynamicContext context)
        {
            return ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache) != null;
        }

        bool EvaluateSearchRelation(IDynamicContext context)
        {
            var originScope = ActorSourceFastResolver.ResolveCached(context, originActorSource, ref _originActorCache);
            if (originScope == null)
                return false;

            if (relationTargetActorSource.Kind == ActorSourceKind.ByIdentity)
                return SearchByIdentity(originScope, searchDirection, searchRange, relationTargetActorSource.Identity);

            var targetScope = ActorSourceFastResolver.ResolveCached(context, relationTargetActorSource, ref _targetActorCache, originScope);
            if (targetScope == null)
                return false;

            return searchDirection == SearchDirection.Parent
                ? HasAncestor(originScope, targetScope, searchRange)
                : HasDescendant(originScope, targetScope, searchRange);
        }

        static bool HasAncestor(IScopeNode originScope, IScopeNode targetScope, int maxDepth)
        {
            var depth = 1;
            for (var current = originScope.Parent; current != null; current = current.Parent, depth++)
            {
                if (maxDepth > 0 && depth > maxDepth)
                    break;

                if (ReferenceEquals(current, targetScope))
                    return true;
            }

            return false;
        }

        static bool HasDescendant(IScopeNode originScope, IScopeNode targetScope, int maxDepth)
        {
            var stack = new Stack<(IScopeNode node, int depth)>();
            var children = ScopeNodeHierarchy.GetChildrenOrEmpty(originScope);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != null)
                    stack.Push((child, 1));
            }

            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();
                if (maxDepth > 0 && depth > maxDepth)
                    continue;

                if (ReferenceEquals(node, targetScope))
                    return true;

                if (maxDepth > 0 && depth >= maxDepth)
                    continue;

                var nodeChildren = ScopeNodeHierarchy.GetChildrenOrEmpty(node);
                for (var i = 0; i < nodeChildren.Count; i++)
                {
                    var child = nodeChildren[i];
                    if (child != null)
                        stack.Push((child, depth + 1));
                }
            }

            return false;
        }

        static bool SearchByIdentity(IScopeNode originScope, SearchDirection searchDirection, int searchRange, CommandTargetIdentityFilter filter)
        {
            if (searchDirection == SearchDirection.Parent)
            {
                var depth = 1;
                for (IScopeNode? node = originScope.Parent; node != null; node = node.Parent, depth++)
                {
                    if (searchRange > 0 && depth > searchRange)
                        break;

                    if (MatchesIdentity(node, filter))
                        return true;
                }

                return false;
            }

            var stack = new Stack<(IScopeNode node, int depth)>();
            var children = ScopeNodeHierarchy.GetChildrenOrEmpty(originScope);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != null)
                    stack.Push((child, 1));
            }

            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();
                if (searchRange > 0 && depth > searchRange)
                    continue;

                if (MatchesIdentity(node, filter))
                    return true;

                if (searchRange > 0 && depth >= searchRange)
                    continue;

                var nodeChildren = ScopeNodeHierarchy.GetChildrenOrEmpty(node);
                for (var i = 0; i < nodeChildren.Count; i++)
                {
                    var child = nodeChildren[i];
                    if (child != null)
                        stack.Push((child, depth + 1));
                }
            }

            return false;
        }

        static bool MatchesIdentity(IScopeNode scope, in CommandTargetIdentityFilter filter)
        {
            var identity = scope.Identity;
            if (identity == null)
                return false;

            if (filter.requireActive && !identity.IsActive)
                return false;

            if (filter.kind != LifetimeScopeKind.None && filter.kind != identity.Kind)
                return false;

            if (!string.IsNullOrEmpty(filter.id) &&
                !string.Equals(identity.Id, filter.id, StringComparison.Ordinal))
                return false;

            if (!string.IsNullOrEmpty(filter.category) &&
                !string.Equals(identity.Category, filter.category, StringComparison.Ordinal))
                return false;

            return true;
        }

        static string DescribeActorSource(ActorSource source)
        {
            return ActorSourceOdinLabelHelper.GetLabel("Actor", source);
        }

        bool IsResolveOnlyMode() => mode == EvaluateMode.ResolveOnly;
        bool IsSearchRelationMode() => mode == EvaluateMode.SearchRelation;

        public static ActorSourceExistsSource FromResolveOnly(ActorSource targetActorSource)
        {
            return new ActorSourceExistsSource
            {
                mode = EvaluateMode.ResolveOnly,
                targetActorSource = targetActorSource,
            };
        }

        public static ActorSourceExistsSource FromSearchRelation(
            ActorSource originActorSource,
            ActorSource relationTargetActorSource,
            SearchDirection searchDirection = SearchDirection.Parent,
            int searchRange = 0)
        {
            return new ActorSourceExistsSource
            {
                mode = EvaluateMode.SearchRelation,
                originActorSource = originActorSource,
                relationTargetActorSource = relationTargetActorSource,
                searchDirection = searchDirection,
                searchRange = searchRange,
            };
        }
    }

    /// <summary>
    /// 指定した Shared hub 内に対象 ActorSource が登録されているかを判定するソース。
    /// </summary>
    [Serializable]
    public sealed class SharedActorSourceExistsSource : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(sharedHubActorSource)")]
        ActorSource sharedHubActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [LabelText("Tag")]
        string tag = string.Empty;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [NonSerialized]
        ActorSourceResolveCache _sharedHubActorCache;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "SharedActorExists";
        public string GetDebugData => $"Hub={ActorSourceOdinLabelHelper.GetActorSourceLabel(sharedHubActorSource)} Tag={tag} Target={ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            var exists = TryFindSharedTag(context, out _);
            return DynamicVariant.FromBool(exists);
        }

        bool TryFindSharedTag(IDynamicContext context, out string tag)
        {
            tag = string.Empty;

            var hubOwnerScope = ActorSourceFastResolver.ResolveCached(context, sharedHubActorSource, ref _sharedHubActorCache);
            if (hubOwnerScope == null)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache, hubOwnerScope);
            if (targetScope == null)
                return false;

            if (string.IsNullOrWhiteSpace(this.tag))
                return TryFindTagFromSharedHub(hubOwnerScope, targetScope, out tag);

            return TryMatchTagFromSharedHub(hubOwnerScope, targetScope, this.tag, out tag);
        }

        internal static bool TryFindTagFromSharedHub(IScopeNode hubOwnerScope, IScopeNode targetScope, out string tag)
        {
            tag = string.Empty;
            for (var current = hubOwnerScope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ISharedLTSChannelHub>(out var hub) || hub == null)
                    continue;

                if (hub.TryFindTag(targetScope, out tag))
                    return true;

                return false;
            }

            return false;
        }

        static bool TryMatchTagFromSharedHub(IScopeNode hubOwnerScope, IScopeNode targetScope, string expectedTag, out string resolvedTag)
        {
            resolvedTag = string.Empty;
            if (string.IsNullOrWhiteSpace(expectedTag))
                return false;

            for (var current = hubOwnerScope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ISharedLTSChannelHub>(out var hub) || hub == null)
                    continue;

                if (!hub.TryGet(expectedTag, out var registeredScope) || registeredScope == null)
                    return false;

                if (!ReferenceEquals(registeredScope, targetScope))
                    return false;

                resolvedTag = expectedTag;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 指定した Shared hub 内で対象 ActorSource に対応する shared tag 名を返すソース。
    /// 見つからない場合は空文字列を返す。
    /// </summary>
    [Serializable]
    public sealed class SharedActorSourceTagSource : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(sharedHubActorSource)")]
        ActorSource sharedHubActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [NonSerialized]
        ActorSourceResolveCache _sharedHubActorCache;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "SharedActorTag";
        public string GetDebugData => $"Hub={ActorSourceOdinLabelHelper.GetActorSourceLabel(sharedHubActorSource)} Target={ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            var hubOwnerScope = ActorSourceFastResolver.ResolveCached(context, sharedHubActorSource, ref _sharedHubActorCache);
            if (hubOwnerScope == null)
                return DynamicVariant.FromString(string.Empty);

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache, hubOwnerScope);
            if (targetScope == null)
                return DynamicVariant.FromString(string.Empty);

            return SharedActorSourceExistsSource.TryFindTagFromSharedHub(hubOwnerScope, targetScope, out var tag)
                ? DynamicVariant.FromString(tag)
                : DynamicVariant.FromString(string.Empty);
        }
    }

    /// <summary>
    /// 指定した UI LTS の ModalStack で現在アクティブな root、またはその影響範囲に
    /// 比較対象 ActorSource が含まれているかを判定する。
    /// </summary>
    [Serializable]
    public sealed class UIModalStackActorMatchSource : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"UI Modal Stack\", modalStackActorSource)")]
        ActorSource modalStackActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Compare Actor\", compareActorSource)")]
        ActorSource compareActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [LabelText("Include Affected Scope")]
        bool includeAffectedScope;

        [NonSerialized]
        ActorSourceResolveCache _modalStackActorCache;

        [NonSerialized]
        ActorSourceResolveCache _compareActorCache;

        public string SourceTypeName => "UIModalStackActorMatch";
        public string GetDebugData => $"Modal={modalStackActorSource.Kind} Compare={compareActorSource.Kind} IncludeAffected={includeAffectedScope}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.Null;

            var modalScope = ActorSourceFastResolver.ResolveCached(context, modalStackActorSource, ref _modalStackActorCache);
            if (!TryResolveModalStackChannelTelemetry(modalScope, out var modalStackTelemetry) || modalStackTelemetry == null)
                return DynamicVariant.FromBool(false);

            var compareScope = ActorSourceFastResolver.ResolveCached(context, compareActorSource, ref _compareActorCache);
            if (compareScope == null)
                return DynamicVariant.FromBool(false);

            var layerStates = modalStackTelemetry.LayerStates;
            if (layerStates == null || layerStates.Count == 0)
                return DynamicVariant.FromBool(false);

            for (int i = 0; i < layerStates.Count; i++)
            {
                var layerState = layerStates[i];
                if (!layerState.InputActive)
                    continue;

                var root = layerState.ActiveRoot;
                var ownerScope = root?.OwnerScope;
                if (root == null || ownerScope == null)
                    continue;

                if (ReferenceEquals(ownerScope, compareScope))
                    return DynamicVariant.FromBool(true);

                if (includeAffectedScope && root.IsDescendant(compareScope))
                    return DynamicVariant.FromBool(true);
            }

            return DynamicVariant.FromBool(false);
        }

        static bool TryResolveModalStackChannelTelemetry(IScopeNode? scope, out IModalStackChannelTelemetry? modalStackTelemetry)
        {
            modalStackTelemetry = null;
            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver != null &&
                    resolver.TryResolve<IModalStackChannelTelemetry>(out var resolved) &&
                    resolved != null)
                {
                    modalStackTelemetry = resolved;
                    return true;
                }
            }

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
