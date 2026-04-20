#nullable enable

using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.StatusEffect
{
    public enum StatusEffectInverseIntervalAction
    {
        None = 0,
        Disable = 10,
        BlockUseOnly = 20,
    }

    public enum StatusEffectActivePolicy
    {
        EnabledOnly = 10,
        RegisteredEvenIfDisabled = 20,
    }

    public enum StatusEffectRuntimeFilterMode
    {
        All = 10,
        DefinitionId = 20,
        RuntimeTag = 30,
        InstanceId = 40,
    }

    public enum StatusEffectScalarValueMode
    {
        DynamicValue = 10,
        RuntimeIntensity = 20,
    }

    public enum StatusEffectRuntimeIntensityReference
    {
        A = 10,
        B = 20,
        C = 30,
        D = 40,
        E = 50,
        F = 60,
        G = 70,
    }

    public enum StatusEffectRuntimeControlMode
    {
        Custom = 0,
        AutoGlobal = 10,
    }

    [Serializable]
    public sealed class StatusEffectAutoGlobalAdvancedOption
    {
        [LabelText("Lifetime End Action")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public EffectLifetimeEndAction LifetimeEndAction = EffectLifetimeEndAction.None;

        [LabelText("Count Exhausted Action")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public EffectCountExhaustedAction CountExhaustedAction = EffectCountExhaustedAction.None;
    }

    public enum ScalarModifierApplyMode
    {
        Add = 10,
        Mul = 20,
    }

    public enum StatusEffectBlockedUsePropagationMode
    {
        Continue = 10,
        Suspend = 20,
    }

    public enum StatusEffectHookKind
    {
        Apply = 10,
        Remove = 20,
        Enable = 30,
        Disable = 40,
        Use = 50,
        StackIntensity = 60,
        StackDuration = 70,
    }

    [Serializable]
    public struct StatusEffectRuntimeFilter : IEquatable<StatusEffectRuntimeFilter>
    {
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectRuntimeFilterMode Mode;

        [ShowIf(nameof(UsesTextValue))]
        [LabelText("Value")]
        [Tooltip("Inspector setting.")]
        public string Value;

        public StatusEffectRuntimeFilter(StatusEffectRuntimeFilterMode mode, string value)
        {
            Mode = mode;
            Value = value ?? string.Empty;
        }

        public static StatusEffectRuntimeFilter All => new(StatusEffectRuntimeFilterMode.All, string.Empty);

        public bool UsesTextValue()
            => Mode != StatusEffectRuntimeFilterMode.All;

        public bool Matches(StatusEffectRuntime runtime)
        {
            if (runtime == null)
                return false;

            return Matches(runtime.DefinitionId, runtime.RuntimeTag, runtime.InstanceId);
        }

        public bool Matches(string? definitionId, string? runtimeTag, string? instanceId)
        {
            switch (Mode)
            {
                case StatusEffectRuntimeFilterMode.All:
                    return true;

                case StatusEffectRuntimeFilterMode.DefinitionId:
                    return !string.IsNullOrWhiteSpace(Value)
                        && string.Equals(definitionId, Value, StringComparison.Ordinal);

                case StatusEffectRuntimeFilterMode.RuntimeTag:
                    return !string.IsNullOrWhiteSpace(Value)
                        && string.Equals(runtimeTag, Value, StringComparison.Ordinal);

                case StatusEffectRuntimeFilterMode.InstanceId:
                    return !string.IsNullOrWhiteSpace(Value)
                        && string.Equals(instanceId, Value, StringComparison.Ordinal);

                default:
                    return false;
            }
        }

        public string GetDebugLabel()
            => Mode == StatusEffectRuntimeFilterMode.All ? "All" : $"{Mode}:{Value}";

        public bool Equals(StatusEffectRuntimeFilter other)
            => Mode == other.Mode && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj)
            => obj is StatusEffectRuntimeFilter other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Mode * 397) ^ (Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0);
            }
        }
    }

    [Serializable]
    public sealed class StatusEffectHookSet
    {
        [CommandListFunctionName("StatusEffect.OnApply")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnApply = new();

        [CommandListFunctionName("StatusEffect.OnRemove")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnRemove = new();

        [CommandListFunctionName("StatusEffect.OnEnable")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnEnable = new();

        [CommandListFunctionName("StatusEffect.OnDisable")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnDisable = new();

        [CommandListFunctionName("StatusEffect.OnUse")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnUse = new();

        [CommandListFunctionName("StatusEffect.OnStackIntensity")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnStackIntensity = new();

        [CommandListFunctionName("StatusEffect.OnStackDuration")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnStackDuration = new();

        public StatusEffectHookSet Clone()
        {
            var clone = new StatusEffectHookSet();
            clone.OnApply.SetCommands(OnApply);
            clone.OnRemove.SetCommands(OnRemove);
            clone.OnEnable.SetCommands(OnEnable);
            clone.OnDisable.SetCommands(OnDisable);
            clone.OnUse.SetCommands(OnUse);
            clone.OnStackIntensity.SetCommands(OnStackIntensity);
            clone.OnStackDuration.SetCommands(OnStackDuration);
            return clone;
        }

        public CommandListData Resolve(StatusEffectHookKind kind)
        {
            return kind switch
            {
                StatusEffectHookKind.Apply => OnApply,
                StatusEffectHookKind.Remove => OnRemove,
                StatusEffectHookKind.Enable => OnEnable,
                StatusEffectHookKind.Disable => OnDisable,
                StatusEffectHookKind.Use => OnUse,
                StatusEffectHookKind.StackIntensity => OnStackIntensity,
                StatusEffectHookKind.StackDuration => OnStackDuration,
                _ => OnApply,
            };
        }

        public void ApplyMutations(StatusEffectHookMutationSet? mutations, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutations == null)
                return;

            OnApply.ApplyRuntimeMutation(mutations.OnApply, mutationService);
            OnRemove.ApplyRuntimeMutation(mutations.OnRemove, mutationService);
            OnEnable.ApplyRuntimeMutation(mutations.OnEnable, mutationService);
            OnDisable.ApplyRuntimeMutation(mutations.OnDisable, mutationService);
            OnUse.ApplyRuntimeMutation(mutations.OnUse, mutationService);
            OnStackIntensity.ApplyRuntimeMutation(mutations.OnStackIntensity, mutationService);
            OnStackDuration.ApplyRuntimeMutation(mutations.OnStackDuration, mutationService);
        }
    }

    [Serializable]
    public sealed class StatusEffectHookMutationSet
    {
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnApply = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnRemove = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnEnable = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnDisable = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnUse = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnStackIntensity = new();
        [Tooltip("Inspector setting.")]
        public CommandListMutationStep OnStackDuration = new();
    }

    [Serializable]
    public sealed class StatusEffectPeriodicCommandSet
    {
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [LabelText("Interval")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntervalSeconds = DynamicValueExtensions.FromLiteral(1f);

        [LabelText("Commands")]
        [CommandListFunctionName("StatusEffect.Commands")]
        [Tooltip("Inspector setting.")]
        public CommandListData Commands = new();
    }

    [Serializable]
    public sealed class StatusEffectApplyRequest
    {
        [LabelText("Definition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<BaseStatusEffectDefinitionData> Definition;

        [LabelText("Stack Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<StatusEffectStackPreset> StackPreset;

        [LabelText("Intensity A")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityA;

        [LabelText("Intensity B")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityB;

        [LabelText("Intensity C")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityC;

        [LabelText("Intensity D")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityD;

        [LabelText("Intensity E")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityE;

        [LabelText("Intensity F")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityF;

        [LabelText("Intensity G")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntensityG;

        [LabelText("Override Duration")]
        [Tooltip("Inspector setting.")]
        public bool OverrideDuration;

        [ShowIf(nameof(OverrideDuration))]
        [LabelText("Duration Override")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> DurationOverride;

        [LabelText("Runtime Tag")]
        [Tooltip("Inspector setting.")]
        public string RuntimeTag = string.Empty;

        [LabelText("Hook Mutations")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public StatusEffectHookMutationSet HookMutations = new();

        public StatusEffectResolvedIntensities ResolveIntensities(IDynamicContext evaluationContext)
        {
            StatusEffectResolvedIntensities intensities = default;
            intensities.A = IntensityA.HasSource ? IntensityA.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.B = IntensityB.HasSource ? IntensityB.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.C = IntensityC.HasSource ? IntensityC.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.D = IntensityD.HasSource ? IntensityD.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.E = IntensityE.HasSource ? IntensityE.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.F = IntensityF.HasSource ? IntensityF.GetOrDefault(evaluationContext, 0f) : 0f;
            intensities.G = IntensityG.HasSource ? IntensityG.GetOrDefault(evaluationContext, 0f) : 0f;
            return intensities;
        }
    }

    public interface IStatusEffectDefinitionData
    {
        string DefinitionId { get; }
        EffectVisualData VisualData { get; }
        string DefaultRuntimeTag { get; }
        DynamicValue<bool> Condition { get; }
        StatusEffectRuntimeControlMode RuntimeControlMode { get; }
        StatusEffectAutoGlobalAdvancedOption? AutoGlobalAdvancedOption { get; }
        bool UseDuration { get; }
        bool UseUseCooldown { get; }
        bool UseCount { get; }
        IStatusEffectDurationDefinition? DurationDefinition { get; }
        IStatusEffectUseCooldownDefinition? UseCooldownDefinition { get; }
        IStatusEffectCountDefinition? CountDefinition { get; }
        IReadOnlyList<IStatusEffectOperationDefinition> Operations { get; }
        StatusEffectPeriodicCommandSet PeriodicCommands { get; }
        StatusEffectHookSet DefaultHooks { get; }
    }

    [Serializable]
    public abstract class BaseStatusEffectDefinitionData :
        BaseProfileData,
        IStatusEffectDefinitionData,
        IDynamicManagedRefDebugText
    {
        public override Type ProfileType => GetType();

        public abstract string DefinitionId { get; }
        public abstract EffectVisualData VisualData { get; }
        public abstract string DefaultRuntimeTag { get; }
        public abstract DynamicValue<bool> Condition { get; }
        public abstract StatusEffectRuntimeControlMode RuntimeControlMode { get; }
        public abstract StatusEffectAutoGlobalAdvancedOption? AutoGlobalAdvancedOption { get; }
        public abstract bool UseDuration { get; }
        public abstract bool UseUseCooldown { get; }
        public abstract bool UseCount { get; }
        public abstract IStatusEffectDurationDefinition? DurationDefinition { get; }
        public abstract IStatusEffectUseCooldownDefinition? UseCooldownDefinition { get; }
        public abstract IStatusEffectCountDefinition? CountDefinition { get; }
        public abstract IReadOnlyList<IStatusEffectOperationDefinition> Operations { get; }
        public abstract StatusEffectPeriodicCommandSet PeriodicCommands { get; }
        public abstract StatusEffectHookSet DefaultHooks { get; }

        public string GetManagedRefDebugText()
            => DefinitionId;

        public override string ToString()
            => string.IsNullOrWhiteSpace(DefinitionId) ? GetType().Name : DefinitionId;
    }

    [Serializable]
    public sealed class ConfigurableStatusEffectDefinitionData : BaseStatusEffectDefinitionData
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
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        EffectVisualData visualData = new();

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

        [BoxGroup("Runtime")]
        [LabelText("Use Lifetime")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useDuration;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowDurationDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectDurationDefinition? durationDefinition;

        [BoxGroup("Runtime")]
        [LabelText("Use Cooldown")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useUseCooldown;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowUseCooldownDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectUseCooldownDefinition? useCooldownDefinition;

        [BoxGroup("Runtime")]
        [LabelText("Use Count")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool useCount;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowCountDefinition))]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        IStatusEffectCountDefinition? countDefinition;

        [BoxGroup("Operations")]
        [LabelText("Operations")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, ShowFoldout = true)]
        [SerializeReference]
        [Tooltip("Inspector setting.")]
        List<IStatusEffectOperationDefinition> operations = new();

        [BoxGroup("Commands")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        StatusEffectPeriodicCommandSet periodicCommands = new();

        [BoxGroup("Hooks")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        StatusEffectHookSet defaultHooks = new();

        public override string DefinitionId => definitionId;
        public override EffectVisualData VisualData => visualData;
        public override string DefaultRuntimeTag => defaultRuntimeTag;
        public override DynamicValue<bool> Condition => condition;
        public override StatusEffectRuntimeControlMode RuntimeControlMode => runtimeControlMode;
        public override StatusEffectAutoGlobalAdvancedOption? AutoGlobalAdvancedOption => autoGlobalAdvancedOption;
        public override bool UseDuration => useDuration;
        public override bool UseUseCooldown => useUseCooldown;
        public override bool UseCount => useCount;
        public override IStatusEffectDurationDefinition? DurationDefinition => durationDefinition;
        public override IStatusEffectUseCooldownDefinition? UseCooldownDefinition => useCooldownDefinition;
        public override IStatusEffectCountDefinition? CountDefinition => countDefinition;
        public override IReadOnlyList<IStatusEffectOperationDefinition> Operations => operations;
        public override StatusEffectPeriodicCommandSet PeriodicCommands => periodicCommands;
        public override StatusEffectHookSet DefaultHooks => defaultHooks;

        bool UsesCustomRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.Custom;
        bool UsesAutoGlobalRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.AutoGlobal;
        bool ShowDurationDefinition => UsesCustomRuntimeSettings && useDuration;
        bool ShowUseCooldownDefinition => UsesCustomRuntimeSettings && useUseCooldown;
        bool ShowCountDefinition => UsesCustomRuntimeSettings && useCount;
    }

    public interface IStatusEffectOperationDefinition
    {
        bool Enabled { get; }
        bool TryBuild(StatusEffectBuildContext context, out IStatusEffectOperationRuntime runtime);
    }

    public interface IStatusEffectOperationRuntime
    {
        string OperationId { get; }
        bool IsOperationEnabled { get; }
        StatusEffectBlockedUsePropagationMode BlockedUsePropagationMode { get; }
        void Apply();
        void Remove();
        void Enable();
        void Disable();
        void Reset();
        void RefreshValue();
        bool SetOperationEnabled(bool enabled);
    }

    [Serializable]
    public sealed class ScalarModifierOperationDefinition : IStatusEffectOperationDefinition
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        public bool Enabled = true;

        [LabelText("Target Key")]
        [Tooltip("Inspector setting.")]
        public ScalarKey TargetKey;

        [LabelText("Apply Mode")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public ScalarModifierApplyMode ApplyMode = ScalarModifierApplyMode.Add;

        [ShowIf(nameof(UsesMulPhase))]
        [LabelText("Mul Phase")]
        [Tooltip("Inspector setting.")]
        public ScalarMulPhase MulPhase = ScalarMulPhase.PreAdd;

        [LabelText("Value Mode")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectScalarValueMode ValueMode = StatusEffectScalarValueMode.RuntimeIntensity;

        [ShowIf(nameof(UsesRuntimeIntensity))]
        [LabelText("Runtime Intensity Slot")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectRuntimeIntensityReference RuntimeIntensitySlot = StatusEffectRuntimeIntensityReference.A;

        [ShowIf(nameof(UsesDynamicValue))]
        [LabelText("Value")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> Value;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource TargetActorSource;

        [LabelText("Layer")]
        [Tooltip("Inspector setting.")]
        public string Layer = string.Empty;

        [LabelText("Operation Id")]
        [Tooltip("Inspector setting.")]
        public string OperationId = string.Empty;

        [LabelText("Blocked Use Propagation")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectBlockedUsePropagationMode BlockedUsePropagation = StatusEffectBlockedUsePropagationMode.Continue;

        bool UsesMulPhase() => ApplyMode == ScalarModifierApplyMode.Mul;
        bool UsesDynamicValue() => ValueMode == StatusEffectScalarValueMode.DynamicValue;
        bool UsesRuntimeIntensity() => ValueMode == StatusEffectScalarValueMode.RuntimeIntensity;

        bool IStatusEffectOperationDefinition.Enabled => Enabled;

        public bool TryBuild(StatusEffectBuildContext context, out IStatusEffectOperationRuntime runtime)
        {
            runtime = default!;
            if (!DynamicValueResolver.HasScalarKey(TargetKey))
                return false;

            var targetScope = ActorSourceFastResolver.Resolve(
                context.OwnerScope,
                TargetActorSource,
                context.CommandRootScope);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var scalarService) || scalarService == null)
                return false;

            var evaluationContext = new StatusEffectOperationDynamicContext(
                context.RuntimeVars,
                context.OwnerScope,
                context.CommandRootScope);
            var valueExpression = Value;
            if (ValueMode == StatusEffectScalarValueMode.DynamicValue)
                valueExpression.TrySetExternalExpressionVariables(StatusEffectExpressionVariables.Variables, includeLocalVariables: true);

            runtime = new ScalarModifierOperationRuntime(
                targetScope,
                scalarService,
                TargetKey,
                ApplyMode,
                MulPhase,
                Layer,
                OperationId,
                ValueMode,
                RuntimeIntensitySlot,
                BlockedUsePropagation,
                valueExpression,
                evaluationContext,
                context.Definition.DefinitionId,
                Enabled);
            return true;
        }
    }

    static class StatusEffectExpressionVariables
    {
        public static readonly IReadOnlyList<ExpressionVariable> Variables = Build();
        public static readonly IReadOnlyList<ExpressionVariable> ConditionVariables = BuildConditionVariables();

        static IReadOnlyList<ExpressionVariable> Build()
        {
            return new List<ExpressionVariable>
            {
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA, "intensityA"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityB, "intensityB"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityC, "intensityC"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityD, "intensityD"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityE, "intensityE"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityF, "intensityF"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityG, "intensityG"),
                CreateInt(VarIds.GameLib.Base.StatusEffect.Runtime.Element.stackCount, "stackCount"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingDuration, "remainingDuration"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.totalDuration, "totalDuration"),
                CreateBool(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isEnabled, "isEnabled"),
                CreateBool(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isApplied, "isApplied"),
                CreateBool(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isActive, "isActive"),
                CreateBool(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isUseBlocked, "isUseBlocked"),
                CreateInt(VarIds.GameLib.Base.StatusEffect.Runtime.Element.usedCount, "usedCount"),
                CreateInt(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingUseCount, "remainingUseCount"),
                CreateInt(VarIds.GameLib.Base.StatusEffect.Runtime.Element.maxUseCount, "maxUseCount"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, "remainingUseCooldown"),
                CreateFloat(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, "remainingInverseInterval"),
            };
        }

        static IReadOnlyList<ExpressionVariable> BuildConditionVariables()
        {
            var variables = new List<ExpressionVariable>(Variables);

            // Condition 縺ｯ IsActive 縺ｮ閾ｪ蟾ｱ蜿ら・繧帝∩縺代ｋ縺溘ａ縲（sActive 繧貞､悶＠縺溷､画焚鄒､縺ｧ隧穂ｾ｡縺吶ｋ縲・
            variables.RemoveAll(v => string.Equals(v.ExpressionKey, "isActive", StringComparison.Ordinal));
            return variables;
        }

        static ExpressionVariable CreateFloat(int varId, string key)
            => ExpressionVariable.Create(DynamicValue.FromVarId(varId), key, ValueKind.Float);

        static ExpressionVariable CreateInt(int varId, string key)
            => ExpressionVariable.Create(DynamicValue.FromVarId(varId), key, ValueKind.Int);

        static ExpressionVariable CreateBool(int varId, string key)
            => ExpressionVariable.Create(DynamicValue.FromVarId(varId), key, ValueKind.Bool);
    }

    public interface IStatusEffectDurationDefinition
    {
        bool SyncWithGlobalLifetime { get; }
        EffectLifetimeEndAction EndAction { get; }
        bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectDurationController controller);
    }

    public interface IStatusEffectUseCooldownDefinition
    {
        bool SyncWithGlobalUseCooldown { get; }
        bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectUseCooldownController controller);
    }

    public interface IStatusEffectCountDefinition
    {
        bool SyncWithGlobalCount { get; }
        EffectCountExhaustedAction ExhaustedAction { get; }
        StatusEffectActivePolicy ActivePolicy { get; }
        bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectCountController controller);
    }

    public interface IStatusEffectDurationController
    {
        float TotalDuration { get; }
        float RemainingDuration { get; }
        bool IsExpired { get; }
        EffectLifetimeEndAction EndAction { get; }
        void Tick(float deltaTime);
        void Reset();
        bool ApplyStack(float value, StatusEffectStackOperation operation);
    }

    public interface IStatusEffectUseCooldownController
    {
        float TotalDuration { get; }
        float RemainingDuration { get; }
        bool IsActive { get; }
        bool CanUse { get; }
        void Tick(float deltaTime);
        void Start();
        void Reset();
    }

    public interface IStatusEffectCountController
    {
        int MaxCount { get; }
        int UsedCount { get; }
        int RemainingCount { get; }
        bool HasLimit { get; }
        bool CanUse { get; }
        EffectCountExhaustedAction ExhaustedAction { get; }
        StatusEffectActivePolicy ActivePolicy { get; }
        bool ConsumeUse();
        void Reset();
        bool ApplyMaxCountStack(int value, StatusEffectStackOperation operation);
    }

    [Serializable]
    public sealed class FixedDurationStatusEffectDefinition : IStatusEffectDurationDefinition
    {
        [LabelText("Sync With Global Lifetime")]
        [Tooltip("Inspector setting.")]
        public bool SyncWithGlobalLifetime;

        [LabelText("Duration")]
        [ShowIf("@!SyncWithGlobalLifetime")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> Duration;

        [LabelText("Expire Action")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public EffectLifetimeEndAction EndAction = EffectLifetimeEndAction.Remove;

        bool IStatusEffectDurationDefinition.SyncWithGlobalLifetime => SyncWithGlobalLifetime;
        EffectLifetimeEndAction IStatusEffectDurationDefinition.EndAction => EndAction;

        public bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectDurationController controller)
        {
            controller = default!;
            if (SyncWithGlobalLifetime)
                return false;

            var duration = context.Request.OverrideDuration
                ? context.Request.DurationOverride.GetOrDefault(context, Duration.GetOrDefault(context, -1f))
                : Duration.GetOrDefault(context, -1f);

            controller = new FixedStatusEffectDurationController(duration, EndAction);
            return true;
        }
    }

    [Serializable]
    public sealed class FixedUseCooldownStatusEffectDefinition : IStatusEffectUseCooldownDefinition
    {
        [LabelText("Sync With Global Cooldown")]
        [Tooltip("Inspector setting.")]
        public bool SyncWithGlobalUseCooldown;

        [LabelText("Duration")]
        [ShowIf("@!SyncWithGlobalUseCooldown")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> Duration;

        bool IStatusEffectUseCooldownDefinition.SyncWithGlobalUseCooldown => SyncWithGlobalUseCooldown;

        public bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectUseCooldownController controller)
        {
            controller = default!;
            if (SyncWithGlobalUseCooldown)
                return false;

            var duration = Mathf.Max(0f, Duration.GetOrDefault(context, 0f));
            controller = new FixedStatusEffectUseCooldownController(duration);
            return true;
        }
    }

    [Serializable]
    public sealed class DynamicCountStatusEffectDefinition : IStatusEffectCountDefinition
    {
        [LabelText("Sync With Global Count")]
        [Tooltip("Inspector setting.")]
        public bool SyncWithGlobalCount;

        [LabelText("Max Count")]
        [ShowIf("@!SyncWithGlobalCount")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> MaxCount;

        [LabelText("Exhausted Action")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public EffectCountExhaustedAction ExhaustedAction = EffectCountExhaustedAction.Disable;

        [LabelText("Active Policy")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectActivePolicy ActivePolicy = StatusEffectActivePolicy.RegisteredEvenIfDisabled;

        bool IStatusEffectCountDefinition.SyncWithGlobalCount => SyncWithGlobalCount;
        EffectCountExhaustedAction IStatusEffectCountDefinition.ExhaustedAction => ExhaustedAction;
        StatusEffectActivePolicy IStatusEffectCountDefinition.ActivePolicy => ActivePolicy;

        public bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectCountController controller)
        {
            controller = default!;
            if (SyncWithGlobalCount)
                return false;

            var maxCount = Mathf.Max(0, MaxCount.GetOrDefault(context, 0));
            controller = new DynamicCountStatusEffectController(
                maxCount,
                ExhaustedAction,
                ActivePolicy);
            return true;
        }
    }

    sealed class ScalarModifierOperationRuntime : IStatusEffectOperationRuntime
    {
        readonly IScopeNode _targetScope;
        readonly IBaseScalarService _scalarService;
        readonly ScalarKey _targetKey;
        readonly ScalarModifierApplyMode _applyMode;
        readonly ScalarMulPhase _mulPhase;
        readonly string _layer;
        readonly string _operationId;
        readonly StatusEffectScalarValueMode _valueMode;
        readonly StatusEffectRuntimeIntensityReference _runtimeIntensitySlot;
        readonly StatusEffectBlockedUsePropagationMode _blockedUsePropagationMode;
        readonly DynamicValue<float> _value;
        readonly IDynamicContext _evaluationContext;
        readonly string _tag;
        readonly object _source;

        ScalarHandle? _handle;
        bool _isApplied;
        bool _isExternallyEnabled;
        bool _isSuspended;

        public ScalarModifierOperationRuntime(
            IScopeNode targetScope,
            IBaseScalarService scalarService,
            ScalarKey targetKey,
            ScalarModifierApplyMode applyMode,
            ScalarMulPhase mulPhase,
            string layer,
            string operationId,
            StatusEffectScalarValueMode valueMode,
            StatusEffectRuntimeIntensityReference runtimeIntensitySlot,
            StatusEffectBlockedUsePropagationMode blockedUsePropagationMode,
            DynamicValue<float> value,
            IDynamicContext evaluationContext,
            string definitionId,
            bool isInitiallyEnabled)
        {
            _targetScope = targetScope;
            _scalarService = scalarService;
            _targetKey = targetKey;
            _applyMode = applyMode;
            _mulPhase = mulPhase;
            _layer = layer ?? string.Empty;
            _operationId = operationId ?? string.Empty;
            _valueMode = valueMode;
            _runtimeIntensitySlot = runtimeIntensitySlot;
            _blockedUsePropagationMode = blockedUsePropagationMode;
            _value = value;
            _evaluationContext = evaluationContext;
            _tag = BuildTag(definitionId, _operationId, Guid.NewGuid().ToString("N"));
            _source = targetScope;
            _isExternallyEnabled = isInitiallyEnabled;
        }

        public string OperationId => _operationId;
        public bool IsOperationEnabled => _isExternallyEnabled;
        public StatusEffectBlockedUsePropagationMode BlockedUsePropagationMode => _blockedUsePropagationMode;

        public void Apply()
        {
            _isSuspended = false;
            if (!_isExternallyEnabled)
            {
                DisposeHandle();
                return;
            }

            var currentValue = EvaluateCurrentValue();
            if (_isApplied)
            {
                _handle?.SetValue(currentValue);
                return;
            }

            _handle = _applyMode == ScalarModifierApplyMode.Mul
                ? _scalarService.LocalMul(_targetKey, _layer, currentValue, _mulPhase, -1f, _source, _tag)
                : _scalarService.LocalAdd(_targetKey, _layer, currentValue, -1f, _source, _tag);
            _isApplied = true;
        }

        public void Remove()
        {
            _isSuspended = true;
            DisposeHandle();
        }

        public void Enable()
        {
            _isSuspended = false;
            Apply();
        }

        public void Disable()
        {
            _isSuspended = true;
            DisposeHandle();
        }

        public void Reset()
        {
            DisposeHandle();
            Apply();
        }

        public void RefreshValue()
        {
            if (!_isExternallyEnabled || _isSuspended)
                return;

            Apply();
        }

        public bool SetOperationEnabled(bool enabled)
        {
            if (_isExternallyEnabled == enabled)
                return false;

            _isExternallyEnabled = enabled;
            if (!_isExternallyEnabled)
            {
                DisposeHandle();
                return true;
            }

            if (!_isSuspended)
                Apply();

            return true;
        }

        void DisposeHandle()
        {
            if (!_isApplied)
                return;

            _handle?.Dispose();
            _handle = null;
            _isApplied = false;
        }

        float EvaluateCurrentValue()
            => _valueMode == StatusEffectScalarValueMode.RuntimeIntensity
                ? ResolveRuntimeIntensity()
                : _value.GetOrDefault(_evaluationContext, _applyMode == ScalarModifierApplyMode.Mul ? 1f : 0f);

        static string BuildTag(string definitionId, string operationId, string runtimeOperationId)
        {
            var baseTag = string.IsNullOrWhiteSpace(definitionId) ? "StatusEffect" : definitionId;
            if (!string.IsNullOrWhiteSpace(operationId))
                baseTag = $"{baseTag}:{operationId}";

            return string.IsNullOrWhiteSpace(runtimeOperationId)
                ? baseTag
                : $"{baseTag}:{runtimeOperationId}";
        }

        float ResolveRuntimeIntensity()
        {
            var slot = _runtimeIntensitySlot switch
            {
                StatusEffectRuntimeIntensityReference.A => StatusEffectIntensitySlot.A,
                StatusEffectRuntimeIntensityReference.B => StatusEffectIntensitySlot.B,
                StatusEffectRuntimeIntensityReference.C => StatusEffectIntensitySlot.C,
                StatusEffectRuntimeIntensityReference.D => StatusEffectIntensitySlot.D,
                StatusEffectRuntimeIntensityReference.E => StatusEffectIntensitySlot.E,
                StatusEffectRuntimeIntensityReference.F => StatusEffectIntensitySlot.F,
                StatusEffectRuntimeIntensityReference.G => StatusEffectIntensitySlot.G,
                _ => StatusEffectIntensitySlot.A,
            };

            var varId = StatusEffectIntensitySlotUtility.GetRuntimeElementVarId(slot);
            if (_evaluationContext?.Vars != null &&
                _evaluationContext.Vars.TryGetVariant(varId, out var variant) &&
                variant.TryGet(out float intensity))
            {
                return intensity;
            }

            return 0f;
        }
    }

    sealed class FixedStatusEffectDurationController : IStatusEffectDurationController
    {
        readonly float _configuredDuration;
        bool _skipNextTick;

        public float TotalDuration { get; private set; }
        public float RemainingDuration { get; private set; }
        public bool IsExpired => TotalDuration >= 0f && RemainingDuration <= 0f;
        public EffectLifetimeEndAction EndAction { get; }

        public FixedStatusEffectDurationController(float duration, EffectLifetimeEndAction endAction)
        {
            _configuredDuration = duration;
            TotalDuration = duration;
            RemainingDuration = duration;
            EndAction = endAction;
            _skipNextTick = duration >= 0f && duration > 0f;
        }

        public void Tick(float deltaTime)
        {
            if (TotalDuration < 0f)
                return;

            if (_skipNextTick)
            {
                _skipNextTick = false;
                return;
            }

            RemainingDuration -= Mathf.Max(0f, deltaTime);
            if (RemainingDuration < 0f)
                RemainingDuration = 0f;
        }

        public void Reset()
        {
            TotalDuration = _configuredDuration;
            RemainingDuration = _configuredDuration;
            _skipNextTick = _configuredDuration >= 0f && _configuredDuration > 0f;
        }

        public bool ApplyStack(float value, StatusEffectStackOperation operation)
        {
            switch (operation)
            {
                case StatusEffectStackOperation.Set:
                    TotalDuration = value;
                    RemainingDuration = value;
                    return true;

                case StatusEffectStackOperation.Add:
                    if (TotalDuration < 0f || value < 0f)
                    {
                        TotalDuration = -1f;
                        RemainingDuration = -1f;
                        return true;
                    }

                    TotalDuration += value;
                    RemainingDuration += value;
                    return true;

                case StatusEffectStackOperation.Mul:
                    if (TotalDuration < 0f || RemainingDuration < 0f)
                        return false;

                    TotalDuration *= value;
                    RemainingDuration *= value;
                    if (TotalDuration < 0f)
                        TotalDuration = 0f;
                    if (RemainingDuration < 0f)
                        RemainingDuration = 0f;
                    return true;

                default:
                    return false;
            }
        }
    }

    sealed class DynamicCountStatusEffectController : IStatusEffectCountController
    {
        int _configuredMaxCount;

        public int MaxCount => _configuredMaxCount;
        public int UsedCount { get; private set; }
        public int RemainingCount => HasLimit ? Mathf.Max(0, _configuredMaxCount - UsedCount) : -1;
        public bool HasLimit => _configuredMaxCount > 0;
        public bool CanUse => !HasLimit || UsedCount < _configuredMaxCount;
        public EffectCountExhaustedAction ExhaustedAction { get; }
        public StatusEffectActivePolicy ActivePolicy { get; }

        public DynamicCountStatusEffectController(
            int maxCount,
            EffectCountExhaustedAction exhaustedAction,
            StatusEffectActivePolicy activePolicy)
        {
            _configuredMaxCount = maxCount;
            ExhaustedAction = exhaustedAction;
            ActivePolicy = activePolicy;
        }

        public bool ConsumeUse()
        {
            if (!CanUse)
                return false;

            if (HasLimit)
                UsedCount++;

            return true;
        }

        public void Reset()
        {
            UsedCount = 0;
        }

        public bool ApplyMaxCountStack(int value, StatusEffectStackOperation operation)
        {
            var before = _configuredMaxCount;

            switch (operation)
            {
                case StatusEffectStackOperation.Set:
                    _configuredMaxCount = Mathf.Max(0, value);
                    break;

                case StatusEffectStackOperation.Add:
                    _configuredMaxCount = Mathf.Max(0, _configuredMaxCount + value);
                    break;

                case StatusEffectStackOperation.Mul:
                    _configuredMaxCount = Mathf.Max(0, Mathf.RoundToInt(_configuredMaxCount * value));
                    break;

                default:
                    return false;
            }

            if (_configuredMaxCount > 0 && UsedCount > _configuredMaxCount)
                UsedCount = _configuredMaxCount;

            return before != _configuredMaxCount;
        }
    }

    sealed class FixedStatusEffectUseCooldownController : IStatusEffectUseCooldownController
    {
        readonly float _configuredDuration;
        bool _skipNextTick;

        public float TotalDuration => _configuredDuration;
        public float RemainingDuration { get; private set; }
        public bool IsActive => RemainingDuration > 0f;
        public bool CanUse => RemainingDuration <= 0f;

        public FixedStatusEffectUseCooldownController(float duration)
        {
            _configuredDuration = Mathf.Max(0f, duration);
        }

        public void Tick(float deltaTime)
        {
            if (RemainingDuration <= 0f)
                return;

            if (_skipNextTick)
            {
                _skipNextTick = false;
                return;
            }

            RemainingDuration -= Mathf.Max(0f, deltaTime);
            if (RemainingDuration < 0f)
                RemainingDuration = 0f;
        }

        public void Start()
        {
            RemainingDuration = _configuredDuration;
            _skipNextTick = _configuredDuration > 0f;
        }

        public void Reset()
        {
            RemainingDuration = 0f;
            _skipNextTick = false;
        }
    }
}
