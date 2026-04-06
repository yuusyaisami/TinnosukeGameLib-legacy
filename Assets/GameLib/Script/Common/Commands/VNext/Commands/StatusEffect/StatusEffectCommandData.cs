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
        [LabelText("Stack Preset")]
        [Tooltip("同じ slot に既存 effect がある場合の重ね方を preset で指定します。Intensity A-G の初期値もこの preset 側で定義します。未定義の Intensity は自動で 0 として扱われます。未指定時は DurationRefresh 相当の既定 preset を使います。")]
        public DynamicValue<StatusEffectStackPreset> StackPreset;

        [BoxGroup("Apply")]
        [ShowIf(nameof(IsApply))]
        [LabelText("Override Duration")]
        [Tooltip("definition 側の duration ではなく request 側の duration を使う場合に有効にします。")]
        public bool OverrideDuration;

        [BoxGroup("Apply")]
        [ShowIf(nameof(ShowDurationOverride))]
        [LabelText("Duration Override")]
        [Tooltip("Override Duration が有効なときに使う持続時間です。")]
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
        [Tooltip("true のとき、設定差し替え後に service の global runtime state を再初期化します。")]
        public bool ResetGlobalState = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(IsReset))]
        [LabelText("Reset Global State")]
        [Tooltip("true のとき、Reset 実行時に service の global runtime state も再初期化します。")]
        public bool ResetGlobalStateOnReset;

        bool IsApply => Op == StatusEffectCommandOp.Apply;
        bool IsReset => Op == StatusEffectCommandOp.Reset;
        bool IsClearAll => Op == StatusEffectCommandOp.ClearAll;
        bool IsUseGlobal => Op == StatusEffectCommandOp.UseGlobal;
        bool IsConfigureServiceSettings => Op == StatusEffectCommandOp.ConfigureServiceSettings;
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
