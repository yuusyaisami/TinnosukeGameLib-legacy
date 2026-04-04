#nullable enable

using System;
using Game.Common;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    public enum StatusEffectStackOperation
    {
        Set = 10,
        Add = 20,
        Mul = 30,
    }

    public enum StatusEffectIntensitySlot
    {
        A = 10,
        B = 20,
        C = 30,
        D = 40,
        E = 50,
        F = 60,
        G = 70,
    }

    [Serializable]
    public struct StatusEffectResolvedIntensities
    {
        public float A;
        public float B;
        public float C;
        public float D;
        public float E;
        public float F;
        public float G;

        public float Get(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => A,
                StatusEffectIntensitySlot.B => B,
                StatusEffectIntensitySlot.C => C,
                StatusEffectIntensitySlot.D => D,
                StatusEffectIntensitySlot.E => E,
                StatusEffectIntensitySlot.F => F,
                StatusEffectIntensitySlot.G => G,
                _ => 0f,
            };
        }

        public void Set(StatusEffectIntensitySlot slot, float value)
        {
            switch (slot)
            {
                case StatusEffectIntensitySlot.A:
                    A = value;
                    break;
                case StatusEffectIntensitySlot.B:
                    B = value;
                    break;
                case StatusEffectIntensitySlot.C:
                    C = value;
                    break;
                case StatusEffectIntensitySlot.D:
                    D = value;
                    break;
                case StatusEffectIntensitySlot.E:
                    E = value;
                    break;
                case StatusEffectIntensitySlot.F:
                    F = value;
                    break;
                case StatusEffectIntensitySlot.G:
                    G = value;
                    break;
            }
        }
    }

    public static class StatusEffectIntensitySlotUtility
    {
        public static readonly StatusEffectIntensitySlot[] OrderedSlots =
        {
            StatusEffectIntensitySlot.A,
            StatusEffectIntensitySlot.B,
            StatusEffectIntensitySlot.C,
            StatusEffectIntensitySlot.D,
            StatusEffectIntensitySlot.E,
            StatusEffectIntensitySlot.F,
            StatusEffectIntensitySlot.G,
        };

        public static int GetRuntimeElementVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityB,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityC,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityD,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityE,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityF,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityG,
                _ => VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA,
            };
        }

        public static int GetStackOperationVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.operation,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Stack.IntensityB.operation,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Stack.IntensityC.operation,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Stack.IntensityD.operation,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Stack.IntensityE.operation,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Stack.IntensityF.operation,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Stack.IntensityG.operation,
                _ => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.operation,
            };
        }

        public static int GetStackLocalVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.local,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Stack.IntensityB.local,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Stack.IntensityC.local,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Stack.IntensityD.local,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Stack.IntensityE.local,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Stack.IntensityF.local,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Stack.IntensityG.local,
                _ => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.local,
            };
        }

        public static int GetStackUseGlobalVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.useGlobal,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Stack.IntensityB.useGlobal,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Stack.IntensityC.useGlobal,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Stack.IntensityD.useGlobal,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Stack.IntensityE.useGlobal,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Stack.IntensityF.useGlobal,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Stack.IntensityG.useGlobal,
                _ => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.useGlobal,
            };
        }

        public static int GetStackGlobalVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.global,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Stack.IntensityB.global,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Stack.IntensityC.global,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Stack.IntensityD.global,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Stack.IntensityE.global,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Stack.IntensityF.global,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Stack.IntensityG.global,
                _ => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.global,
            };
        }

        public static int GetStackIgnoreGlobalWhenMinusOneVarId(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.B => VarIds.GameLib.Base.StatusEffect.Stack.IntensityB.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.C => VarIds.GameLib.Base.StatusEffect.Stack.IntensityC.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.D => VarIds.GameLib.Base.StatusEffect.Stack.IntensityD.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.E => VarIds.GameLib.Base.StatusEffect.Stack.IntensityE.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.F => VarIds.GameLib.Base.StatusEffect.Stack.IntensityF.ignoreGlobalWhenMinusOne,
                StatusEffectIntensitySlot.G => VarIds.GameLib.Base.StatusEffect.Stack.IntensityG.ignoreGlobalWhenMinusOne,
                _ => VarIds.GameLib.Base.StatusEffect.Stack.IntensityA.ignoreGlobalWhenMinusOne,
            };
        }
    }

    [Serializable]
    public sealed class StatusEffectStackRule
    {
        [LabelText("Operation")]
        [EnumToggleButtons]
        public StatusEffectStackOperation Operation = StatusEffectStackOperation.Add;

        [LabelText("Apply Local")]
        public bool ApplyLocalValue = true;

        [ShowIf(nameof(ApplyLocalValue))]
        [LabelText("Local")]
        public DynamicValue<float> LocalValue;

        [LabelText("Apply Global")]
        public bool ApplyGlobalValue;

        [ShowIf(nameof(ApplyGlobalValue))]
        [LabelText("Global")]
        public DynamicValue<float> GlobalValue;

        [ShowIf(nameof(ApplyGlobalValue))]
        [LabelText("Ignore Global On -1")]
        public bool IgnoreGlobalWhenMinusOne;

        public static StatusEffectStackRule Disabled()
            => new()
            {
                Operation = StatusEffectStackOperation.Add,
                ApplyLocalValue = false,
                LocalValue = DynamicValueExtensions.FromLiteral(0f),
                ApplyGlobalValue = false,
                GlobalValue = DynamicValueExtensions.FromLiteral(0f),
            };
    }

    [Serializable]
    public sealed class StatusEffectStackPreset : IDynamicManagedRefValue
    {
        [LabelText("Ignore If Existing")]
        [Tooltip("同じ slot に既存 effect がある場合、この preset 適用をスキップします。")]
        public bool IgnoreIfExisting;

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity A")]
        public bool ApplyIntensityA;

        [BoxGroup("Rules")]
        [LabelText("Intensity A")]
        [ShowIf(nameof(ApplyIntensityA))]
        [InlineProperty]
        public StatusEffectStackRule IntensityA = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity B")]
        public bool ApplyIntensityB;

        [BoxGroup("Rules")]
        [LabelText("Intensity B")]
        [ShowIf(nameof(ApplyIntensityB))]
        [InlineProperty]
        public StatusEffectStackRule IntensityB = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity C")]
        public bool ApplyIntensityC;

        [BoxGroup("Rules")]
        [LabelText("Intensity C")]
        [ShowIf(nameof(ApplyIntensityC))]
        [InlineProperty]
        public StatusEffectStackRule IntensityC = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity D")]
        public bool ApplyIntensityD;

        [BoxGroup("Rules")]
        [LabelText("Intensity D")]
        [ShowIf(nameof(ApplyIntensityD))]
        [InlineProperty]
        public StatusEffectStackRule IntensityD = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity E")]
        public bool ApplyIntensityE;

        [BoxGroup("Rules")]
        [LabelText("Intensity E")]
        [ShowIf(nameof(ApplyIntensityE))]
        [InlineProperty]
        public StatusEffectStackRule IntensityE = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity F")]
        public bool ApplyIntensityF;

        [BoxGroup("Rules")]
        [LabelText("Intensity F")]
        [ShowIf(nameof(ApplyIntensityF))]
        [InlineProperty]
        public StatusEffectStackRule IntensityF = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity G")]
        public bool ApplyIntensityG;

        [BoxGroup("Rules")]
        [LabelText("Intensity G")]
        [ShowIf(nameof(ApplyIntensityG))]
        [InlineProperty]
        public StatusEffectStackRule IntensityG = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Duration")]
        public bool ApplyDuration = true;

        [BoxGroup("Rules")]
        [LabelText("Duration")]
        [ShowIf(nameof(ApplyDuration))]
        [InlineProperty]
        public StatusEffectStackRule Duration = CreateDefaultDurationRule();

        [BoxGroup("Rules")]
        [LabelText("Apply Current Count")]
        public bool ApplyCurrentCount;

        [BoxGroup("Rules")]
        [LabelText("Current Count")]
        [ShowIf(nameof(ApplyCurrentCount))]
        [InlineProperty]
        public StatusEffectStackRule CurrentCount = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Max Count")]
        public bool ApplyMaxCount;

        [BoxGroup("Rules")]
        [LabelText("Max Count")]
        [ShowIf(nameof(ApplyMaxCount))]
        [InlineProperty]
        public StatusEffectStackRule MaxCount = StatusEffectStackRule.Disabled();

        [BoxGroup("Description")]
        [LabelText("Apply Stack Description Override")]
        public bool ApplyStackDescriptionOverride;

        [BoxGroup("Description")]
        [LabelText("Override Stack Description")]
        [Tooltip("設定されている場合、Definition の StackDescription より優先して使用されます。")]
        [ShowIf(nameof(ApplyStackDescriptionOverride))]
        public DynamicValue<string> StackDescriptionOverride;

        public static StatusEffectStackPreset CreateDurationRefreshPreset()
            => new()
            {
                IgnoreIfExisting = false,
                ApplyIntensityA = false,
                IntensityA = StatusEffectStackRule.Disabled(),
                ApplyIntensityB = false,
                IntensityB = StatusEffectStackRule.Disabled(),
                ApplyIntensityC = false,
                IntensityC = StatusEffectStackRule.Disabled(),
                ApplyIntensityD = false,
                IntensityD = StatusEffectStackRule.Disabled(),
                ApplyIntensityE = false,
                IntensityE = StatusEffectStackRule.Disabled(),
                ApplyIntensityF = false,
                IntensityF = StatusEffectStackRule.Disabled(),
                ApplyIntensityG = false,
                IntensityG = StatusEffectStackRule.Disabled(),
                ApplyDuration = true,
                Duration = CreateDefaultDurationRule(),
                ApplyCurrentCount = false,
                CurrentCount = StatusEffectStackRule.Disabled(),
                ApplyMaxCount = false,
                MaxCount = StatusEffectStackRule.Disabled(),
                ApplyStackDescriptionOverride = false,
                StackDescriptionOverride = new(),
            };

        public bool ShouldApplyIntensity(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => ApplyIntensityA,
                StatusEffectIntensitySlot.B => ApplyIntensityB,
                StatusEffectIntensitySlot.C => ApplyIntensityC,
                StatusEffectIntensitySlot.D => ApplyIntensityD,
                StatusEffectIntensitySlot.E => ApplyIntensityE,
                StatusEffectIntensitySlot.F => ApplyIntensityF,
                StatusEffectIntensitySlot.G => ApplyIntensityG,
                _ => false,
            };
        }

        public StatusEffectStackRule GetIntensityRule(StatusEffectIntensitySlot slot)
        {
            return slot switch
            {
                StatusEffectIntensitySlot.A => IntensityA,
                StatusEffectIntensitySlot.B => IntensityB,
                StatusEffectIntensitySlot.C => IntensityC,
                StatusEffectIntensitySlot.D => IntensityD,
                StatusEffectIntensitySlot.E => IntensityE,
                StatusEffectIntensitySlot.F => IntensityF,
                StatusEffectIntensitySlot.G => IntensityG,
                _ => StatusEffectStackRule.Disabled(),
            };
        }

        static StatusEffectStackRule CreateDefaultDurationRule()
        {
            return new StatusEffectStackRule
            {
                Operation = StatusEffectStackOperation.Set,
                ApplyLocalValue = true,
                LocalValue = DynamicValueExtensions.FromLiteral(0f),
                ApplyGlobalValue = false,
                GlobalValue = DynamicValueExtensions.FromLiteral(0f),
                IgnoreGlobalWhenMinusOne = true,
            };
        }

        public override string ToString()
        {
            return $"Ignore={IgnoreIfExisting} " +
                   $"IA={ApplyIntensityA}:{IntensityA.Operation} IB={ApplyIntensityB}:{IntensityB.Operation} IC={ApplyIntensityC}:{IntensityC.Operation} " +
                   $"ID={ApplyIntensityD}:{IntensityD.Operation} IE={ApplyIntensityE}:{IntensityE.Operation} IF={ApplyIntensityF}:{IntensityF.Operation} IG={ApplyIntensityG}:{IntensityG.Operation} " +
                   $"Duration={ApplyDuration}:{Duration.Operation} Count={ApplyCurrentCount}:{CurrentCount.Operation} Max={ApplyMaxCount}:{MaxCount.Operation} " +
                   $"DescOverride={ApplyStackDescriptionOverride}:{StackDescriptionOverride.HasSource}";
        }
    }
}
