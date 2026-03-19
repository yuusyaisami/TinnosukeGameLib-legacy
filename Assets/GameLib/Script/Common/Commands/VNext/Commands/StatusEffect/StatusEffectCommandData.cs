#nullable enable

using System;
using Game.Common;
using Game.StatusEffect;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum StatusEffectCommandOp
    {
        Apply = 10,
        Remove = 20,
        Enable = 30,
        Disable = 40,
        Use = 50,
        Reset = 60,
        ClearAll = 70,
    }

    public enum StatusEffectServiceScope
    {
        Scope = 10,
        Actor = 20,
    }

    [Serializable]
    public sealed class StatusEffectCommandData : ICommandData
    {
        public int CommandId => CommandIds.StatusEffectControl;
        public string DebugData => $"Op={Op} Target={ServiceScope} Apply={GetApplyLabel()} Filter={BuildFilter().GetDebugLabel()}";

        [BoxGroup("Operation")]
        [EnumToggleButtons]
        [Tooltip("StatusEffectService に対して実行する操作を選びます。")]
        public StatusEffectCommandOp Op = StatusEffectCommandOp.Apply;

        [BoxGroup("Target")]
        [EnumToggleButtons]
        [Tooltip("どのスコープ上の StatusEffectService を対象にするかを指定します。")]
        public StatusEffectServiceScope ServiceScope = StatusEffectServiceScope.Actor;

        [BoxGroup("Target")]
        [ShowIf(nameof(UseActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [Tooltip("ServiceScope が Actor のときに、対象の Actor を解決する方法です。")]
        public ActorSource TargetActorSource;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Definition")]
        [Tooltip("付与する StatusEffect の定義データです。asset や inline、DynamicSource から指定できます。")]
        public DynamicValue<BaseStatusEffectDefinitionData> Definition;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Stack Mode")]
        [EnumToggleButtons]
        [Tooltip("同じ slot に既存 effect がある場合の重ね方を指定します。")]
        public EffectStackMode StackMode = EffectStackMode.Refresh;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Intensity")]
        [Tooltip("RuntimeIntensity を使う operation に渡す強度値です。")]
        public DynamicValue<float> Intensity;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Override Duration")]
        [Tooltip("definition 側の duration を無視して、この command から持続時間を上書きします。")]
        public bool OverrideDuration;

        [BoxGroup("Apply")]
        [ShowIf(nameof(ShowDurationOverride))]
        [LabelText("Duration Override")]
        [Tooltip("Override Duration が有効なときに使う持続時間です。-1 は無期限です。")]
        public DynamicValue<float> DurationOverride;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Runtime Tag")]
        [Tooltip("同じ definition を別 slot として共存させたいときの識別タグです。")]
        public string RuntimeTag = string.Empty;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Apply 時に hook command を append / replace するための変更セットです。")]
        public StatusEffectHookMutationSet HookMutations = new();

        [BoxGroup("Filter")]
        [ShowIf(nameof(ShowFilterSettings))]
        [EnumToggleButtons]
        [Tooltip("Apply 以外の操作で、どの effect を対象にするかの基準です。")]
        public StatusEffectRuntimeFilterMode FilterMode = StatusEffectRuntimeFilterMode.All;

        [BoxGroup("Filter")]
        [ShowIf(nameof(ShowFilterValue))]
        [LabelText("Filter Value")]
        [Tooltip("FilterMode に対応する definitionId / runtimeTag / instanceId を入力します。")]
        public string FilterValue = string.Empty;

        bool IsApply => Op == StatusEffectCommandOp.Apply;
        bool IsClearAll => Op == StatusEffectCommandOp.ClearAll;
        bool UseActorSource => ServiceScope == StatusEffectServiceScope.Actor;
        bool ShowFilterSettings => !IsApply && !IsClearAll;
        bool ShowFilterValue => ShowFilterSettings && FilterMode != StatusEffectRuntimeFilterMode.All;
        bool ShowDurationOverride => IsApply && OverrideDuration;

        public StatusEffectRuntimeFilter BuildFilter()
        {
            if (IsApply || IsClearAll)
                return StatusEffectRuntimeFilter.All;

            return new StatusEffectRuntimeFilter(FilterMode, FilterValue);
        }

        public StatusEffectApplyRequest BuildApplyRequest()
        {
            return new StatusEffectApplyRequest
            {
                Definition = Definition,
                StackMode = StackMode,
                Intensity = Intensity,
                OverrideDuration = OverrideDuration,
                DurationOverride = DurationOverride,
                RuntimeTag = RuntimeTag ?? string.Empty,
                HookMutations = HookMutations ?? new StatusEffectHookMutationSet(),
            };
        }

        string GetApplyLabel()
            => IsApply ? Definition.SourceTypeName : "-";
    }
}
