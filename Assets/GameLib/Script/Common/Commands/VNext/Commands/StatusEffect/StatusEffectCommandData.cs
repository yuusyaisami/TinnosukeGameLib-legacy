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
        EnableOperation = 35,
        Disable = 40,
        DisableOperation = 45,
        Use = 50,
        RestoreState = 60,
        ClearAll = 70,
        UseGlobal = 80,
        ConfigureServiceSettings = 90,
    }

    public enum StatusEffectServiceScope
    {
        Scope = 10,
        Actor = 20,
    }

    [Serializable]
    public sealed class StatusEffectCommandData : ICommandData
    {
        [Serializable]
        public sealed class StatusEffectServiceSettingsCommandSection
        {
            [LabelText("Apply")]
            public bool Apply;

            [LabelText("Preset")]
            [ShowIf(nameof(Apply))]
            public DynamicValue<StatusEffectGlobalLifetimeSettings> LifetimePreset;

            [LabelText("Preset")]
            [ShowIf(nameof(Apply))]
            public DynamicValue<StatusEffectGlobalUseCooldownSettings> UseCooldownPreset;

            [LabelText("Preset")]
            [ShowIf(nameof(Apply))]
            public DynamicValue<StatusEffectGlobalCountSettings> CountPreset;
        }

        public int CommandId => CommandIds.StatusEffectControl;
        public string DebugData => $"Op={Op} Target={ServiceScope} Apply={GetApplyLabel()} Filter={BuildFilter().GetDebugLabel()} OperationId={GetOperationIdLabel()}";

        [BoxGroup("Operation")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectCommandOp Op = StatusEffectCommandOp.Apply;

        [BoxGroup("Operation")]
        [ShowIf(nameof(UsesOperationId))]
        [LabelText("Operation Id")]
        [Tooltip("Inspector setting.")]
        public string OperationId = string.Empty;

        [BoxGroup("Target")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectServiceScope ServiceScope = StatusEffectServiceScope.Actor;

        [BoxGroup("Target")]
        [ShowIf(nameof(UseActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource TargetActorSource;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Definition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<BaseStatusEffectDefinitionData> Definition;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Stack Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<StatusEffectStackPreset> StackPreset;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Override Duration")]
        [Tooltip("Inspector setting.")]
        public bool OverrideDuration;

        [BoxGroup("Apply")]
        [ShowIf(nameof(ShowDurationOverride))]
        [LabelText("Duration Override")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> DurationOverride;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Runtime Tag")]
        [Tooltip("Inspector setting.")]
        public string RuntimeTag = string.Empty;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        public StatusEffectHookMutationSet HookMutations = new();

        [BoxGroup("Filter")]
        [ShowIf(nameof(ShowFilterSettings))]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectRuntimeFilterMode FilterMode = StatusEffectRuntimeFilterMode.All;

        [BoxGroup("Filter")]
        [ShowIf(nameof(ShowFilterValue))]
        [LabelText("Filter Value")]
        [Tooltip("Inspector setting.")]
        public string FilterValue = string.Empty;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(IsConfigureServiceSettings))]
        [LabelText("Apply Global Lifetime")]
        public bool ApplyGlobalLifetimeSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(ShowGlobalLifetimeSettings))]
        [LabelText("Global Lifetime Preset")]
        public DynamicValue<StatusEffectGlobalLifetimeSettings> GlobalLifetimeSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(IsConfigureServiceSettings))]
        [LabelText("Apply Global UseCooldown")]
        public bool ApplyGlobalUseCooldownSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(ShowGlobalUseCooldownSettings))]
        [LabelText("Global UseCooldown Preset")]
        public DynamicValue<StatusEffectGlobalUseCooldownSettings> GlobalUseCooldownSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(IsConfigureServiceSettings))]
        [LabelText("Apply Global Count")]
        public bool ApplyGlobalCountSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(ShowGlobalCountSettings))]
        [LabelText("Global Count Preset")]
        public DynamicValue<StatusEffectGlobalCountSettings> GlobalCountSettings;

        [BoxGroup("Service Settings")]
        [ShowIf(nameof(IsConfigureServiceSettings))]
        [LabelText("Reset Global State")]
        [Tooltip("Inspector setting.")]
        public bool ResetGlobalState = true;

        [BoxGroup("Restore")]
        [ShowIf(nameof(IsRestoreState))]
        [LabelText("Restore Global State")]
        [Tooltip("Inspector setting.")]
        public bool ResetGlobalStateOnReset;

        bool IsApply => Op == StatusEffectCommandOp.Apply;
        bool IsRestoreState => Op == StatusEffectCommandOp.RestoreState;
        bool IsClearAll => Op == StatusEffectCommandOp.ClearAll;
        bool IsUseGlobal => Op == StatusEffectCommandOp.UseGlobal;
        bool IsConfigureServiceSettings => Op == StatusEffectCommandOp.ConfigureServiceSettings;
        bool IsEnableOperation => Op == StatusEffectCommandOp.EnableOperation;
        bool IsDisableOperation => Op == StatusEffectCommandOp.DisableOperation;
        bool UsesOperationId => IsEnableOperation || IsDisableOperation;
        bool UseActorSource => ServiceScope == StatusEffectServiceScope.Actor;
        bool ShowFilterSettings => !IsApply && !IsClearAll && !IsUseGlobal && !IsConfigureServiceSettings;
        bool ShowFilterValue => ShowFilterSettings && FilterMode != StatusEffectRuntimeFilterMode.All;
        bool ShowDurationOverride => IsApply && OverrideDuration;
        bool ShowGlobalLifetimeSettings => IsConfigureServiceSettings && ApplyGlobalLifetimeSettings;
        bool ShowGlobalUseCooldownSettings => IsConfigureServiceSettings && ApplyGlobalUseCooldownSettings;
        bool ShowGlobalCountSettings => IsConfigureServiceSettings && ApplyGlobalCountSettings;

        public StatusEffectRuntimeFilter BuildFilter()
        {
            if (IsApply || IsClearAll || IsUseGlobal)
                return StatusEffectRuntimeFilter.All;

            return new StatusEffectRuntimeFilter(FilterMode, FilterValue);
        }

        public StatusEffectApplyRequest BuildApplyRequest()
        {
            return new StatusEffectApplyRequest
            {
                Definition = Definition,
                StackPreset = StackPreset,
                OverrideDuration = OverrideDuration,
                DurationOverride = DurationOverride,
                RuntimeTag = RuntimeTag ?? string.Empty,
                HookMutations = HookMutations ?? new StatusEffectHookMutationSet(),
            };
        }

        public StatusEffectServiceSettingsOverrideRequest BuildServiceSettingsRequest(IDynamicContext context)
        {
            return new StatusEffectServiceSettingsOverrideRequest
            {
                ApplyGlobalLifetimeSettings = ApplyGlobalLifetimeSettings,
                GlobalLifetimeSettings = ResolveLifetimeSettings(context),
                ApplyGlobalUseCooldownSettings = ApplyGlobalUseCooldownSettings,
                GlobalUseCooldownSettings = ResolveUseCooldownSettings(context),
                ApplyGlobalCountSettings = ApplyGlobalCountSettings,
                GlobalCountSettings = ResolveCountSettings(context),
                ResetGlobalState = ResetGlobalState,
            };
        }

        string GetApplyLabel()
            => IsApply ? Definition.SourceTypeName : "-";

        string GetOperationIdLabel()
            => UsesOperationId ? (OperationId ?? string.Empty) : "-";

        StatusEffectGlobalLifetimeSettings? ResolveLifetimeSettings(IDynamicContext context)
        {
            if (!ApplyGlobalLifetimeSettings)
                return null;

            return GlobalLifetimeSettings.GetOrDefault(context, StatusEffectGlobalLifetimeSettings.CreateDisabled())
                   ?? StatusEffectGlobalLifetimeSettings.CreateDisabled();
        }

        StatusEffectGlobalUseCooldownSettings? ResolveUseCooldownSettings(IDynamicContext context)
        {
            if (!ApplyGlobalUseCooldownSettings)
                return null;

            return GlobalUseCooldownSettings.GetOrDefault(context, StatusEffectGlobalUseCooldownSettings.CreateDisabled())
                   ?? StatusEffectGlobalUseCooldownSettings.CreateDisabled();
        }

        StatusEffectGlobalCountSettings? ResolveCountSettings(IDynamicContext context)
        {
            if (!ApplyGlobalCountSettings)
                return null;

            return GlobalCountSettings.GetOrDefault(context, StatusEffectGlobalCountSettings.CreateDisabled())
                   ?? StatusEffectGlobalCountSettings.CreateDisabled();
        }
    }
}
