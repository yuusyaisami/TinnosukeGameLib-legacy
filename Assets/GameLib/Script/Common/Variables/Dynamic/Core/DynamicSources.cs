// Game.Common.DynamicSources.cs
//
// 蜷・ｨｮ IDynamicSource 螳溯｣・
//
// 險ｭ險域ｱｺ螳・
// - Literal: 螳壽焚蛟､・磯撼繧ｸ繧ｧ繝阪Μ繝・け縲ゝype驕ｸ謚槭≠繧奇ｼ・
// - Literal<T>: 蝙句崋螳壹・螳壽焚蛟､・医ず繧ｧ繝阪Μ繝・け縲ゝype驕ｸ謚槭↑縺暦ｼ・
// - LiteralToVariable: 螳壽焚蛟､ + VarStore 縺ｸ縺ｮ譖ｸ縺崎ｾｼ縺ｿ蜑ｯ菴懃畑・域立莠呈鋤蜷搾ｼ・
// - VarStore: VarStore 縺九ｉ縺ｮ隱ｭ縺ｿ蜿悶ｊ・・arId 繝吶・繧ｹ・・
// - SelfScalar/OtherScalar: ScalarService 縺九ｉ縺ｮ隱ｭ縺ｿ蜿悶ｊ・・loat 縺ｮ縺ｿ・・
// - SelfBlackboard/OtherBlackboard: Blackboard 縺九ｉ縺ｮ隱ｭ縺ｿ蜿悶ｊ
// - UnityObjectRef: Unity Object 蜿ら・

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
    // Literal Source・磯撼繧ｸ繧ｧ繝阪Μ繝・け縲ゝype驕ｸ謚槭≠繧奇ｼ・
    // ================================================================

    /// <summary>
    /// 螳壽焚蛟､繧ｽ繝ｼ繧ｹ・・nt/float/bool/string/Vector/Color・峨・
    /// DynamicValue・磯撼繧ｸ繧ｧ繝阪Μ繝・け・峨〒菴ｿ逕ｨ縲・
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

        // 繝輔ぃ繧ｯ繝医Μ
        public static LiteralSource FromInt(int value) => new() { type = LiteralType.Int, intValue = value };
        public static LiteralSource FromFloat(float value) => new() { type = LiteralType.Float, floatValue = value };
        public static LiteralSource FromBool(bool value) => new() { type = LiteralType.Bool, boolValue = value };
        public static LiteralSource FromString(string value) => new() { type = LiteralType.String, stringValue = value };
        public static LiteralSource FromVector2(Vector2 value) => new() { type = LiteralType.Vector2, vector2Value = value };
        public static LiteralSource FromVector3(Vector3 value) => new() { type = LiteralType.Vector3, vector3Value = value };
    }

    // ================================================================
    // 蝙句崋螳・Literal Sources・・ynamicValue<T> 逕ｨ・・
    // ================================================================

    /// <summary>int 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>float 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>bool 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>string 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>Vector2 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>Vector3 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>Vector4 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>Color 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>AnimationSpritePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
    [Serializable]
    public sealed class LiteralAnimationSpritePresetSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        AnimationSpritePreset value = new();

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? value.playMode.ToString() : "null";
        public DynamicVariant Evaluate(IDynamicContext context) => DynamicVariant.FromManagedRef(value);
    }

    /// <summary>AnimationSpritePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StateMachinePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StateMachinePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StateAnimationPreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StateAnimationPreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>HealthPreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>HealthPreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>MotionPreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>MotionPreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>TransformAnimationPreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>CommandListData 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>Table 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
    [Serializable]
    public sealed class LiteralTableSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        Table value = new();

        public LiteralTableSource()
        {
        }

        public LiteralTableSource(Table? value)
        {
            this.value = value ?? new Table();
        }

        public string SourceTypeName => "LiteralTable";
        public string GetDebugData => value != null ? $"rows={value.RowCount}" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            LogLiteralTableEvaluate(value, context);
            return value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void LogLiteralTableEvaluate(Table? table, IDynamicContext? context)
        {
            var scopeLabel = context?.Scope?.Identity != null
                ? $"{context.Scope.Identity.Id}:{context.Scope.Identity.Kind}"
                : context?.Scope?.GetType().Name ?? "<null>";

            if (table == null)
            {
                Debug.Log($"[LiteralTableSource] Evaluate Source=LiteralTable Data=null Scope={scopeLabel}");
                return;
            }

            var rowCount = table.RowCount;
            var maxRows = Math.Min(rowCount, 8);
            var rowColumns = new List<string>(maxRows);
            for (var rowIndex = 0; rowIndex < maxRows; rowIndex++)
            {
                if (table.TryGetColumnCount(rowIndex, out var columnCount))
                    rowColumns.Add($"{rowIndex}:{columnCount}");
                else
                    rowColumns.Add($"{rowIndex}:?");
            }

            var truncated = rowCount > maxRows ? ", ..." : string.Empty;
            //Debug.Log($"[LiteralTableSource] Evaluate Source=LiteralTable Data=rows={rowCount} Scope={scopeLabel} RowColumns=[{string.Join(", ", rowColumns)}{truncated}]");
        }
    }

    /// <summary>VarStorePayload 蝗ｺ螳壹Μ繝・Λ繝ｫ・井ｺ呈鋤逕ｨ・・/summary>
    [Serializable]
    public sealed class LiteralVarStorePayloadSource : IDynamicSource
    {
        [SerializeField, InlineProperty, HideLabel]
        VarStorePayload value = new();

        public LiteralVarStorePayloadSource()
        {
        }

        public LiteralVarStorePayloadSource(VarStorePayload? value)
        {
            this.value = value ?? new VarStorePayload();
        }

        public string SourceTypeName => "Literal";
        public string GetDebugData => value != null ? $"entries={value.Entries.Count}, tables={value.Tables.Count}" : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }

    /// <summary>BaseStatusEffectDefinitionData 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>BaseStatusEffectDefinitionData 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StatusEffectStackPreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StatusEffectStackPreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StatusEffectGlobalLifetimeSettings 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StatusEffectGlobalLifetimeSettings 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StatusEffectGlobalUseCooldownSettings 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StatusEffectGlobalUseCooldownSettings 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>StatusEffectGlobalCountSettings 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>StatusEffectGlobalCountSettings 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>BaseRuntimeTemplatePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>BaseRuntimeTemplatePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>ParticleRuntimeTemplatePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>ParticleRuntimeTemplatePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>FirePatternRuntimeTemplatePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>FirePatternRuntimeTemplatePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>SpawnPatternRuntimeTemplatePreset 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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

    /// <summary>SpawnPatternRuntimeTemplatePreset 繧｢繧ｻ繝・ヨ蜿ら・</summary>
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

    /// <summary>MaterialFxPayload 蝗ｺ螳壹Μ繝・Λ繝ｫ</summary>
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
    /// Unity Object 蜿ら・繧ｽ繝ｼ繧ｹ・磯撼繧ｸ繧ｧ繝阪Μ繝・け・峨・
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
    /// 蝙区欠螳壹・ Unity Object 蜿ら・繧ｽ繝ｼ繧ｹ縲・
    /// DynamicValue&lt;AnimationData&gt; 縺ｪ縺ｩ縺ｧ菴ｿ逕ｨ縲・
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
    /// TraitDefinitionSO 蟆ら畑縺ｮ asset 蜿ら・繧ｽ繝ｼ繧ｹ縲・
    /// DynamicValue&lt;TraitDefinitionSO&gt; 縺ｧ縺ｮ authoring 諢丞峙繧呈・遒ｺ縺ｫ縺吶ｋ縲・
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
    /// 螳壽焚蛟､繧定ｿ斐＠縺､縺､縲〃arStore 縺ｫ繧よ嶌縺崎ｾｼ繧繧ｽ繝ｼ繧ｹ縲・
    /// ・亥ｾ梧婿莠呈鋤縺ｮ縺溘ａ繧ｯ繝ｩ繧ｹ蜷阪・邯ｭ謖・ｼ・
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

            // 蜑ｯ菴懃畑: VarStore 縺ｫ譖ｸ縺崎ｾｼ縺ｿ・域立莠呈鋤・・
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
    /// VarStore(IVarStore) 縺九ｉ蛟､繧定ｪｭ縺ｿ蜿悶ｋ繧ｽ繝ｼ繧ｹ・・arId 繝吶・繧ｹ・峨・
    /// Variant / ManagedRef 荳｡譁ｹ繧偵し繝昴・繝医・
    /// </summary>
    [Serializable]
    public sealed class VarStoreSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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
                // ManagedRef 縺ｨ縺励※蜿門ｾ暦ｼ磯撼UnityEngine.Object 縺ｮ繧ｯ繝ｩ繧ｹ繧ゅし繝昴・繝茨ｼ・
                if (vars.TryGetManagedRef(varId, out var managed) && managed != null)
                {
                    if (DeferredDynamicVarResolver.TryResolve(managed, context, $"VarStore:{varId}", out var deferred))
                        return deferred;
                    return DynamicVariant.FromManagedRef(managed);
                }
                return DynamicVariant.Null;
            }

            // Variant 縺ｨ縺励※蜿門ｾ励ｒ隧ｦ縺ｿ繧・
            if (vars.TryGetVariant(varId, out var v))
                return v;

            return DynamicVariant.Null;
        }

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var varId = ResolveVarId();
            if (varId <= 0 || context?.Vars == null)
                return 0;

            return context.Vars.GetVarVersion(varId);
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
    /// 螳壽焚蛟､繧定ｿ斐＠縺､縺､縲〃arStore 縺ｫ蜑ｯ菴懃畑縺ｧ譖ｸ縺崎ｾｼ繧繧ｽ繝ｼ繧ｹ縲・
    /// 螟夂畑縺吶ｋ縺ｨ繝・ヰ繝・げ諤ｧ縺瑚誠縺｡繧九◆繧√・°逕ｨ縺ｯ蜴溷援遖∵ｭ｢蟇・ｊ縲・
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
    /// 閾ｪ繧ｹ繧ｳ繝ｼ繝励・ ScalarService 縺九ｉ蛟､繧定ｪｭ縺ｿ蜿悶ｋ繧ｽ繝ｼ繧ｹ縲・
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
    /// 莉悶せ繧ｳ繝ｼ繝励・ ScalarService 縺九ｉ蛟､繧定ｪｭ縺ｿ蜿悶ｋ繧ｽ繝ｼ繧ｹ縲・
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
    /// 閾ｪ繧ｹ繧ｳ繝ｼ繝励・ Blackboard 縺九ｉ蛟､繧定ｪｭ縺ｿ蜿悶ｋ繧ｽ繝ｼ繧ｹ縲・
    /// </summary>
    [Serializable]
    public sealed class SelfBlackboardSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = fallbackInitialValue.GetSourceDependencyRevision(context);
            if (context?.Scope == null || blackboardId == 0)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (!vars.Contains(blackboardId))
                        continue;

                    var scopeIdentity = DynamicEvaluationOrigin.ComputeStableScopeIdentity(node);
                    var varVersion = vars.GetVarVersion(blackboardId);
                    return TableVarStoreSourceUtility.CombineRevision(revision, scopeIdentity, varVersion);
                }

                return revision;
            }

            if (!context.Scope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null || bb.LocalVars == null)
                return revision;

            if (!bb.LocalVars.Contains(blackboardId))
                return revision;

            var localScopeIdentity = DynamicEvaluationOrigin.ComputeStableScopeIdentity(context.Scope);
            return TableVarStoreSourceUtility.CombineRevision(revision, localScopeIdentity, bb.LocalVars.GetVarVersion(blackboardId));
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
    /// 莉悶せ繧ｳ繝ｼ繝励・ Blackboard 縺九ｉ蛟､繧定ｪｭ縺ｿ蜿悶ｋ繧ｽ繝ｼ繧ｹ縲・
    /// </summary>
    [Serializable]
    public sealed class OtherBlackboardSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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
        public string GetDebugData
        {
            get
            {
                var keyLabel = VarIdResolver.TryGetIdToStable(blackboardId) ?? "(none)";
                var targetLabel = ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActor);
                return $"Key={keyLabel} ReadScope={readScope} Target={targetLabel} Fallback={fallback}";
            }
        }

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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = fallbackInitialValue.GetSourceDependencyRevision(context);
            if (context == null || blackboardId == 0)
                return revision;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (!vars.Contains(blackboardId))
                        continue;

                    var scopeIdentity = DynamicEvaluationOrigin.ComputeStableScopeIdentity(node);
                    return TableVarStoreSourceUtility.CombineRevision(revision, scopeIdentity, vars.GetVarVersion(blackboardId));
                }

                return revision;
            }

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null || bb.LocalVars == null)
                return revision;

            if (!bb.LocalVars.Contains(blackboardId))
                return revision;

            var targetScopeIdentity = DynamicEvaluationOrigin.ComputeStableScopeIdentity(targetScope);
            return TableVarStoreSourceUtility.CombineRevision(revision, targetScopeIdentity, bb.LocalVars.GetVarVersion(blackboardId));
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
    public sealed class SelfGridBlackboardSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        [SerializeField, LabelText("Var Key縺ｧ邨槭ｊ霎ｼ繧")]
        bool useVarKeyFilter = true;

        [SerializeField, ShowIf(nameof(useVarKeyFilter)), LabelText("Var Key"), VarIdDropdown]
        [FormerlySerializedAs("gridVarId")]
        int varIdFilter;

        // Removed malformed inspector attribute.
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

        public bool AllowTrackedEvaluation => false;

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
    public sealed class OtherGridBlackboardSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
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

        public bool AllowTrackedEvaluation => false;

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
    public sealed class SelfGridBlackboardColumnCountSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
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

        public bool AllowTrackedEvaluation => false;

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
    public sealed class OtherGridBlackboardColumnCountSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
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

        public bool AllowTrackedEvaluation => false;

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
    public sealed class SelfGridBlackboardRowCountSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
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

        public bool AllowTrackedEvaluation => false;

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
    public sealed class SelfTableRowCountSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            if (context?.Scope == null || tableVarId == 0)
                return 0;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.ContainsTable(tableVarId))
                        return vars.GetTableVersion(tableVarId);
                }

                return 0;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return 0;

            return localVars.ContainsTable(tableVarId) ? localVars.GetTableVersion(tableVarId) : 0;
        }
    }

    [Serializable]
    public sealed class OtherTableRowCountSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            if (context == null || tableVarId == 0)
                return 0;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return 0;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.ContainsTable(tableVarId))
                        return vars.GetTableVersion(tableVarId);
                }

                return 0;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return 0;

            return localVars.ContainsTable(tableVarId) ? localVars.GetTableVersion(tableVarId) : 0;
        }
    }

    [Serializable]
    public sealed class SelfTableColumnCountSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = row.GetSourceDependencyRevision(context);
            if (context?.Scope == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            if (rowIndex < 0)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableRowVersion(tableVarId, rowIndex, out var rowVersion))
                        return unchecked((revision * 397) ^ rowVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return revision;

            return localVars.TryGetTableRowVersion(tableVarId, rowIndex, out var localRowVersion)
                ? unchecked((revision * 397) ^ localRowVersion)
                : revision;
        }
    }

    [Serializable]
    public sealed class OtherTableColumnCountSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = row.GetSourceDependencyRevision(context);
            if (context == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            if (rowIndex < 0)
                return revision;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableRowVersion(tableVarId, rowIndex, out var rowVersion))
                        return unchecked((revision * 397) ^ rowVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return revision;

            return localVars.TryGetTableRowVersion(tableVarId, rowIndex, out var localRowVersion)
                ? unchecked((revision * 397) ^ localRowVersion)
                : revision;
        }
    }

    [Serializable]
    public sealed class SelfTableCellExistsSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = unchecked((row.GetSourceDependencyRevision(context) * 397) ^ column.GetSourceDependencyRevision(context));
            if (context?.Scope == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var cellVersion))
                        return unchecked((revision * 397) ^ cellVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return revision;

            return localVars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var localCellVersion)
                ? unchecked((revision * 397) ^ localCellVersion)
                : revision;
        }
    }

    [Serializable]
    public sealed class OtherTableCellExistsSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = unchecked((row.GetSourceDependencyRevision(context) * 397) ^ column.GetSourceDependencyRevision(context));
            if (context == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return revision;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var cellVersion))
                        return unchecked((revision * 397) ^ cellVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return revision;

            return localVars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var localCellVersion)
                ? unchecked((revision * 397) ^ localCellVersion)
                : revision;
        }
    }

    [Serializable]
    public sealed class SelfTableCellSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = unchecked((row.GetSourceDependencyRevision(context) * 397) ^ column.GetSourceDependencyRevision(context));
            if (context?.Scope == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = context.Scope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var cellVersion))
                        return unchecked((revision * 397) ^ cellVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(context.Scope, out var localVars))
                return revision;

            return localVars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var localCellVersion)
                ? unchecked((revision * 397) ^ localCellVersion)
                : revision;
        }
    }

    [Serializable]
    public sealed class OtherTableCellSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
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

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = unchecked((row.GetSourceDependencyRevision(context) * 397) ^ column.GetSourceDependencyRevision(context));
            if (context == null || tableVarId == 0)
                return revision;

            var rowIndex = TableVarStoreSourceUtility.EvaluateIndex(context, row);
            var columnIndex = TableVarStoreSourceUtility.EvaluateIndex(context, column);
            if (rowIndex < 0 || columnIndex < 0)
                return revision;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActor, ref _cache);
            if (targetScope == null)
                return revision;

            if (readScope == BlackboardReadScope.Global)
            {
                for (IScopeNode? node = targetScope; node != null; node = node.Parent)
                {
                    if (!TableVarStoreSourceUtility.TryResolveScopeVars(node, out var vars))
                        continue;

                    if (vars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var cellVersion))
                        return unchecked((revision * 397) ^ cellVersion);
                }

                return revision;
            }

            if (!TableVarStoreSourceUtility.TryResolveScopeVars(targetScope, out var localVars))
                return revision;

            return localVars.TryGetTableCellVersion(tableVarId, rowIndex, columnIndex, out var localCellVersion)
                ? unchecked((revision * 397) ^ localCellVersion)
                : revision;
        }
    }

    static class TableVarStoreSourceUtility
    {
        public static int CombineRevision(int first, int second, int third)
        {
            unchecked
            {
                var hash = first;
                hash = (hash * 397) ^ second;
                hash = (hash * 397) ^ third;
                return hash;
            }
        }

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
    /// ActorSource 縺ｮ蟄伜惠蛻､螳壹た繝ｼ繧ｹ縲・
    /// - ResolveOnly: 謖・ｮ・ActorSource 縺瑚ｧ｣豎ｺ縺ｧ縺阪ｋ縺九ｒ蛻､螳壹☆繧・
    /// - SearchRelation: 謖・ｮ・ActorSource 繧定ｵｷ轤ｹ縺ｫ縲∬ｦｪ/蟄先婿蜷代∈蛻･ ActorSource 繧呈爾邏｢縺吶ｋ
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
    /// 謖・ｮ壹＠縺・Shared hub 蜀・↓蟇ｾ雎｡ ActorSource 縺檎匳骭ｲ縺輔ｌ縺ｦ縺・ｋ縺九ｒ蛻､螳壹☆繧九た繝ｼ繧ｹ縲・
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

            if (!TryResolveTargetScopes(context, hubOwnerScope, targetActorSource, ref _targetActorCache, out var targetScope, out var targetScopes))
                return false;

            if (string.IsNullOrWhiteSpace(this.tag))
                return targetScopes != null
                    ? TryFindTagFromSharedHub(hubOwnerScope, targetScopes, out tag)
                    : TryFindTagFromSharedHub(hubOwnerScope, targetScope!, out tag);

            return targetScopes != null
                ? TryMatchTagFromSharedHub(hubOwnerScope, targetScopes, this.tag, out tag)
                : TryMatchTagFromSharedHub(hubOwnerScope, targetScope!, this.tag, out tag);
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

        internal static bool TryFindTagFromSharedHub(IScopeNode hubOwnerScope, IReadOnlyList<IScopeNode> targetScopes, out string tag)
        {
            tag = string.Empty;
            if (targetScopes == null || targetScopes.Count == 0)
                return false;

            for (var current = hubOwnerScope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ISharedLTSChannelHub>(out var hub) || hub == null)
                    continue;

                for (int i = 0; i < targetScopes.Count; i++)
                {
                    var targetScope = targetScopes[i];
                    if (targetScope == null)
                        continue;

                    if (hub.TryFindTag(targetScope, out tag))
                        return true;
                }

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

        static bool TryMatchTagFromSharedHub(IScopeNode hubOwnerScope, IReadOnlyList<IScopeNode> targetScopes, string expectedTag, out string resolvedTag)
        {
            resolvedTag = string.Empty;
            if (string.IsNullOrWhiteSpace(expectedTag) || targetScopes == null || targetScopes.Count == 0)
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

                for (int i = 0; i < targetScopes.Count; i++)
                {
                    var candidate = targetScopes[i];
                    if (candidate != null && ReferenceEquals(registeredScope, candidate))
                    {
                        resolvedTag = expectedTag;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        internal static bool TryResolveTargetScopes(
            IDynamicContext context,
            IScopeNode hubOwnerScope,
            ActorSource source,
            ref ActorSourceResolveCache cache,
            out IScopeNode? targetScope,
            out IReadOnlyList<IScopeNode>? targetScopes)
        {
            targetScope = null;
            targetScopes = null;

            if (source.Kind == ActorSourceKind.ByIdentity &&
                TryResolveScopeRegistry(hubOwnerScope, out var registry) &&
                registry != null)
            {
                var resolvedScopes = registry.ResolveAll(source.Identity, hubOwnerScope);
                if (resolvedScopes != null && resolvedScopes.Count > 0)
                {
                    targetScope = resolvedScopes[0];
                    targetScopes = resolvedScopes;
                    return true;
                }
            }

            targetScope = ActorSourceFastResolver.ResolveCached(context, source, ref cache, hubOwnerScope);
            return targetScope != null;
        }

        static bool TryResolveScopeRegistry(IScopeNode? origin, out IBaseLifetimeScopeRegistry? registry)
        {
            var current = origin;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    registry = resolved;
                    return true;
                }

                current = current.Parent;
            }

            registry = null;
            return false;
        }
    }

    /// <summary>
    /// 謖・ｮ壹＠縺・Shared hub 蜀・〒蟇ｾ雎｡ ActorSource 縺ｫ蟇ｾ蠢懊☆繧・shared tag 蜷阪ｒ霑斐☆繧ｽ繝ｼ繧ｹ縲・
    /// 隕九▽縺九ｉ縺ｪ縺・ｴ蜷医・遨ｺ譁・ｭ怜・繧定ｿ斐☆縲・
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

            if (!SharedActorSourceExistsSource.TryResolveTargetScopes(context, hubOwnerScope, targetActorSource, ref _targetActorCache, out var targetScope, out var targetScopes))
                return DynamicVariant.FromString(string.Empty);

            if (targetScopes != null)
                return SharedActorSourceExistsSource.TryFindTagFromSharedHub(hubOwnerScope, targetScopes, out var tag)
                    ? DynamicVariant.FromString(tag)
                    : DynamicVariant.FromString(string.Empty);

            return SharedActorSourceExistsSource.TryFindTagFromSharedHub(hubOwnerScope, targetScope!, out var singleTag)
                ? DynamicVariant.FromString(singleTag)
                : DynamicVariant.FromString(string.Empty);
        }
    }

    /// <summary>
    /// 謖・ｮ壹＠縺・UI LTS 縺ｮ ModalStack 縺ｧ迴ｾ蝨ｨ繧｢繧ｯ繝・ぅ繝悶↑ root縲√∪縺溘・縺昴・蠖ｱ髻ｿ遽・峇縺ｫ
    /// 豈碑ｼ・ｯｾ雎｡ ActorSource 縺悟性縺ｾ繧後※縺・ｋ縺九ｒ蛻､螳壹☆繧九・
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
            if (VerifiedValueRuntimeBridge.IsActive)
                return BlackboardReadFallback.Fail;

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
            if (VerifiedValueRuntimeBridge.IsActive)
            {
                if (fallback != BlackboardReadFallback.Fail)
                {
                    VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                        "DynamicSources.BlackboardFallback",
                        "Wave D verified value authority blocked DynamicSources blackboard fallback creation. Dynamic blackboard sources must not create fallback values outside verified value authority.");
                }

                return DynamicVariant.Null;
            }

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
    /// 繧､繝ｳ繝ｩ繧､繝ｳ縺ｧ莉ｻ諢上・蜿ら・蝙具ｼ磯撼UnityEngine.Object・峨ｒ譬ｼ邏阪☆繧九た繝ｼ繧ｹ縲・
    /// SerializeReference 繧剃ｽｿ逕ｨ縺励ーSerializable] 縺ｪ繧ｯ繝ｩ繧ｹ繧堤峩謗･譬ｼ邏榊庄閭ｽ縲・
    /// DynamicValue&lt;T&gt; 縺ｨ邨・∩蜷医ｏ縺帙※菴ｿ逕ｨ縲・
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
        /// 繝輔ぃ繧ｯ繝医Μ繝｡繧ｽ繝・ラ縲・
        /// </summary>
        public static ManagedRefSource FromValue(object? obj) => new() { value = obj };
    }
}
