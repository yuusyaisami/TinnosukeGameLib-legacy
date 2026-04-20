#nullable enable

using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [Serializable]
    public sealed class GameLogicScalarStatusEffectDefinitionData : BaseStatusEffectDefinitionData
    {
        [BoxGroup("Identity")]
        [LabelText("Definition Id")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        string definitionId = string.Empty;

        [BoxGroup("Identity")]
        [LabelText("Default Runtime Tag")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        string defaultRuntimeTag = string.Empty;

        [BoxGroup("Presentation")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        EffectVisualData visualData = new();

        [BoxGroup("Operation")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        ScalarModifierOperationDefinition scalarOperation = CreateDefaultOperation();

        [BoxGroup("Runtime")]
        [LabelText("Runtime Control")]
        [EnumToggleButtons]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        StatusEffectRuntimeControlMode runtimeControlMode = StatusEffectRuntimeControlMode.Custom;

        [BoxGroup("Runtime")]
        [LabelText("Condition")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        DynamicValue<bool> condition = DynamicValueExtensions.FromLiteral(true);

        [FoldoutGroup("AdvancedOption", Expanded = true)]
        [ShowIf(nameof(UsesAutoGlobalRuntimeSettings))]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        StatusEffectAutoGlobalAdvancedOption autoGlobalAdvancedOption = new();

        [BoxGroup("Duration")]
        [LabelText("Use Lifetime")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useDuration;

        [BoxGroup("Duration")]
        [ShowIf(nameof(ShowDurationDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectDurationDefinition? durationDefinition;

        [BoxGroup("Cooldown")]
        [LabelText("Use Cooldown")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useUseCooldown;

        [BoxGroup("Cooldown")]
        [ShowIf(nameof(ShowUseCooldownDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectUseCooldownDefinition? useCooldownDefinition;

        [BoxGroup("Count")]
        [LabelText("Use Count")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useCount;

        [BoxGroup("Count")]
        [ShowIf(nameof(ShowCountDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectCountDefinition? countDefinition = CreateDefaultCountDefinition();

        [BoxGroup("Hooks")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        StatusEffectHookSet defaultHooks = new();

        [BoxGroup("Commands")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        StatusEffectPeriodicCommandSet periodicCommands = new();

        [NonSerialized]
        IReadOnlyList<IStatusEffectOperationDefinition>? singleOperationCache;

        public override string DefinitionId => definitionId ?? string.Empty;
        public override EffectVisualData VisualData => visualData ?? new EffectVisualData();
        public override string DefaultRuntimeTag => defaultRuntimeTag ?? string.Empty;
        public override DynamicValue<bool> Condition => condition;
        public override StatusEffectRuntimeControlMode RuntimeControlMode => runtimeControlMode;
        public override StatusEffectAutoGlobalAdvancedOption? AutoGlobalAdvancedOption => autoGlobalAdvancedOption;
        public override bool UseDuration => useDuration;
        public override bool UseUseCooldown => useUseCooldown;
        public override bool UseCount => useCount;
        public override IStatusEffectDurationDefinition? DurationDefinition => durationDefinition;
        public override IStatusEffectUseCooldownDefinition? UseCooldownDefinition => useCooldownDefinition;
        public override IStatusEffectCountDefinition? CountDefinition => countDefinition;
        public override IReadOnlyList<IStatusEffectOperationDefinition> Operations => GetOperations();
        public override StatusEffectPeriodicCommandSet PeriodicCommands => periodicCommands ?? new StatusEffectPeriodicCommandSet();
        public override StatusEffectHookSet DefaultHooks => defaultHooks ?? new StatusEffectHookSet();

        bool UsesCustomRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.Custom;
        bool UsesAutoGlobalRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.AutoGlobal;
        bool ShowDurationDefinition => UsesCustomRuntimeSettings && useDuration;
        bool ShowUseCooldownDefinition => UsesCustomRuntimeSettings && useUseCooldown;
        bool ShowCountDefinition => UsesCustomRuntimeSettings && useCount;

#if UNITY_EDITOR
        [BoxGroup("Identity")]
        [Button("Build Id From Scalar Key")]
        void BuildDefinitionIdFromScalarKey()
        {
            if (scalarOperation == null || string.IsNullOrWhiteSpace(scalarOperation.TargetKey.Name))
                return;

            definitionId = $"StatusEffect.{scalarOperation.TargetKey.Name}";
        }

        [BoxGroup("Count")]
        [ShowIf(nameof(ShowCountDefinition))]
        [Button("Reset Count To Nail Default")]
        void ResetCountToNailDefault()
        {
            countDefinition = CreateDefaultCountDefinition();
        }
#endif

        IReadOnlyList<IStatusEffectOperationDefinition> GetOperations()
        {
            scalarOperation ??= CreateDefaultOperation();
            return singleOperationCache ??= new IStatusEffectOperationDefinition[]
            {
                scalarOperation
            };
        }

        static ScalarModifierOperationDefinition CreateDefaultOperation()
        {
            return new ScalarModifierOperationDefinition
            {
                ApplyMode = ScalarModifierApplyMode.Add,
                MulPhase = ScalarMulPhase.PreAdd,
                ValueMode = StatusEffectScalarValueMode.RuntimeIntensity,
                Value = DynamicValueExtensions.FromLiteral(0f),
                TargetActorSource = new ActorSource
                {
                    Kind = ActorSourceKind.Current
                },
                Layer = string.Empty,
            };
        }

        static IStatusEffectCountDefinition CreateDefaultCountDefinition()
        {
            return new DynamicCountStatusEffectDefinition
            {
                MaxCount = DynamicValue<int>.FromSource(
                    SelfScalarSource.FromScalarKey(new ScalarKey(ScalarKeys.GameLogic.NailProfile.Effect.MaxHitCount))),
                ExhaustedAction = EffectCountExhaustedAction.Disable,
                ActivePolicy = StatusEffectActivePolicy.RegisteredEvenIfDisabled,
            };
        }
    }
}
