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

    public enum ScalarModifierApplyMode
    {
        Add = 10,
        Mul = 20,
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
        [Tooltip("どの条件で effect を絞り込むかを指定します。")]
        public StatusEffectRuntimeFilterMode Mode;

        [ShowIf(nameof(UsesTextValue))]
        [LabelText("Value")]
        [Tooltip("Mode に応じて definitionId、runtimeTag、instanceId を入力します。")]
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
        [Tooltip("effect が最初に適用されたときに実行する command です。")]
        public CommandListData OnApply = new();

        [CommandListFunctionName("StatusEffect.OnRemove")]
        [Tooltip("effect が完全に削除されたときに実行する command です。")]
        public CommandListData OnRemove = new();

        [CommandListFunctionName("StatusEffect.OnEnable")]
        [Tooltip("一時的に無効だった effect が再度有効化されたときに実行する command です。")]
        public CommandListData OnEnable = new();

        [CommandListFunctionName("StatusEffect.OnDisable")]
        [Tooltip("effect が一時的に無効化されたときに実行する command です。")]
        public CommandListData OnDisable = new();

        [CommandListFunctionName("StatusEffect.OnUse")]
        [Tooltip("Use が実行されたときに呼ばれる command です。")]
        public CommandListData OnUse = new();

        [CommandListFunctionName("StatusEffect.OnStackIntensity")]
        [Tooltip("スタックで intensity が変化したときに実行する command です。")]
        public CommandListData OnStackIntensity = new();

        [CommandListFunctionName("StatusEffect.OnStackDuration")]
        [Tooltip("スタックで duration が変化したときに実行する command です。")]
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
        [Tooltip("Apply 時に OnApply をどう変更するかの指定です。")]
        public CommandListMutationStep OnApply = new();
        [Tooltip("Apply 時に OnRemove をどう変更するかの指定です。")]
        public CommandListMutationStep OnRemove = new();
        [Tooltip("Apply 時に OnEnable をどう変更するかの指定です。")]
        public CommandListMutationStep OnEnable = new();
        [Tooltip("Apply 時に OnDisable をどう変更するかの指定です。")]
        public CommandListMutationStep OnDisable = new();
        [Tooltip("Apply 時に OnUse をどう変更するかの指定です。")]
        public CommandListMutationStep OnUse = new();
        [Tooltip("Apply 時に OnStackIntensity をどう変更するかの指定です。")]
        public CommandListMutationStep OnStackIntensity = new();
        [Tooltip("Apply 時に OnStackDuration をどう変更するかの指定です。")]
        public CommandListMutationStep OnStackDuration = new();
    }

    [Serializable]
    public sealed class StatusEffectApplyRequest
    {
        [LabelText("Definition")]
        [Tooltip("付与する effect 定義です。DynamicValue で asset や inline を切り替えられます。")]
        public DynamicValue<BaseStatusEffectDefinitionData> Definition;

        [LabelText("Stack Preset")]
        [Tooltip("同じ slot に既存 effect がある場合の重ね処理 preset です。未指定時は DurationRefresh 相当の既定 preset を使います。")]
        public DynamicValue<StatusEffectStackPreset> StackPreset;

        [LabelText("Intensity A")]
        [Tooltip("RuntimeIntensity A を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityA;

        [LabelText("Intensity B")]
        [Tooltip("RuntimeIntensity B を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityB;

        [LabelText("Intensity C")]
        [Tooltip("RuntimeIntensity C を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityC;

        [LabelText("Intensity D")]
        [Tooltip("RuntimeIntensity D を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityD;

        [LabelText("Intensity E")]
        [Tooltip("RuntimeIntensity E を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityE;

        [LabelText("Intensity F")]
        [Tooltip("RuntimeIntensity F を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityF;

        [LabelText("Intensity G")]
        [Tooltip("RuntimeIntensity G を利用する operation に渡す強度値です。未指定時は 0 です。")]
        public DynamicValue<float> IntensityG;

        [LabelText("Override Duration")]
        [Tooltip("definition 側の duration を無視して、この request 側の duration を使います。")]
        public bool OverrideDuration;

        [ShowIf(nameof(OverrideDuration))]
        [LabelText("Duration Override")]
        [Tooltip("Override Duration が有効なときに使う持続時間です。")]
        public DynamicValue<float> DurationOverride;

        [LabelText("Runtime Tag")]
        [Tooltip("同じ definition を複数共存させたいときの slot 識別タグです。")]
        public string RuntimeTag = string.Empty;

        [LabelText("Hook Mutations")]
        [InlineProperty]
        [Tooltip("Apply 時に hook command を append / replace / clear するための変更セットです。")]
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
        StatusEffectRuntimeControlMode RuntimeControlMode { get; }
        bool UseDuration { get; }
        bool UseUseCooldown { get; }
        bool UseCount { get; }
        IStatusEffectDurationDefinition? DurationDefinition { get; }
        IStatusEffectUseCooldownDefinition? UseCooldownDefinition { get; }
        IStatusEffectCountDefinition? CountDefinition { get; }
        IReadOnlyList<IStatusEffectOperationDefinition> Operations { get; }
        StatusEffectHookSet DefaultHooks { get; }
    }

    [Serializable]
    public abstract class BaseStatusEffectDefinitionData :
        BaseProfileData,
        IStatusEffectDefinitionData
    {
        public override Type ProfileType => GetType();

        public abstract string DefinitionId { get; }
        public abstract EffectVisualData VisualData { get; }
        public abstract string DefaultRuntimeTag { get; }
        public abstract StatusEffectRuntimeControlMode RuntimeControlMode { get; }
        public abstract bool UseDuration { get; }
        public abstract bool UseUseCooldown { get; }
        public abstract bool UseCount { get; }
        public abstract IStatusEffectDurationDefinition? DurationDefinition { get; }
        public abstract IStatusEffectUseCooldownDefinition? UseCooldownDefinition { get; }
        public abstract IStatusEffectCountDefinition? CountDefinition { get; }
        public abstract IReadOnlyList<IStatusEffectOperationDefinition> Operations { get; }
        public abstract StatusEffectHookSet DefaultHooks { get; }

        public override string ToString()
            => string.IsNullOrWhiteSpace(DefinitionId) ? GetType().Name : DefinitionId;
    }

    [Serializable]
    public sealed class ConfigurableStatusEffectDefinitionData : BaseStatusEffectDefinitionData
    {
        [BoxGroup("Identity")]
        [LabelText("Definition Id")]
        [SerializeField]
        [Tooltip("この effect 定義を一意に識別する固定 ID です。")]
        string definitionId = string.Empty;

        [BoxGroup("Identity")]
        [LabelText("Default Runtime Tag")]
        [SerializeField]
        [Tooltip("Apply 時に runtimeTag が空だった場合に使う既定値です。")]
        string defaultRuntimeTag = string.Empty;

        [BoxGroup("Presentation")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("UI やログで見せる表示データです。")]
        EffectVisualData visualData = new();

        [BoxGroup("Runtime")]
        [LabelText("Runtime Control")]
        [EnumToggleButtons]
        [SerializeField]
        [Tooltip("Custom は definition 個別設定を使用します。AutoGlobal は Use/Cooldown/Count/Lifetime の利用判定を StatusEffectService の Global 設定に完全委譲します。")]
        StatusEffectRuntimeControlMode runtimeControlMode = StatusEffectRuntimeControlMode.Custom;

        [BoxGroup("Runtime")]
        [LabelText("Use Lifetime")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("effect の登録時から進む lifetime timer を使う場合に有効にします。")]
        bool useDuration;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowDurationDefinition))]
        [SerializeReference]
        [Tooltip("lifetime timer の生成方法です。Use Lifetime が有効なときだけ参照されます。")]
        IStatusEffectDurationDefinition? durationDefinition;

        [BoxGroup("Runtime")]
        [LabelText("Use Cooldown")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Use 実行後に始まる cooldown timer を使う場合に有効にします。")]
        bool useUseCooldown;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowUseCooldownDefinition))]
        [SerializeReference]
        [Tooltip("Use 後の cooldown の生成方法です。Use Cooldown が有効なときだけ参照されます。")]
        IStatusEffectUseCooldownDefinition? useCooldownDefinition;

        [BoxGroup("Runtime")]
        [LabelText("Use Count")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Use 回数システムを使う effect の場合に有効にします。")]
        bool useCount;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowCountDefinition))]
        [SerializeReference]
        [Tooltip("回数上限や回数切れ時の挙動を指定します。")]
        IStatusEffectCountDefinition? countDefinition;

        [BoxGroup("Operations")]
        [LabelText("Operations")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, ShowFoldout = true)]
        [SerializeReference]
        [Tooltip("effect の本体処理です。複数の operation を並べて構成できます。")]
        List<IStatusEffectOperationDefinition> operations = new();

        [BoxGroup("Hooks")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("Apply / Remove / Use などの各タイミングで挟む command 群です。")]
        StatusEffectHookSet defaultHooks = new();

        public override string DefinitionId => definitionId;
        public override EffectVisualData VisualData => visualData;
        public override string DefaultRuntimeTag => defaultRuntimeTag;
        public override StatusEffectRuntimeControlMode RuntimeControlMode => runtimeControlMode;
        public override bool UseDuration => useDuration;
        public override bool UseUseCooldown => useUseCooldown;
        public override bool UseCount => useCount;
        public override IStatusEffectDurationDefinition? DurationDefinition => durationDefinition;
        public override IStatusEffectUseCooldownDefinition? UseCooldownDefinition => useCooldownDefinition;
        public override IStatusEffectCountDefinition? CountDefinition => countDefinition;
        public override IReadOnlyList<IStatusEffectOperationDefinition> Operations => operations;
        public override StatusEffectHookSet DefaultHooks => defaultHooks;

        bool UsesCustomRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.Custom;
        bool ShowDurationDefinition => UsesCustomRuntimeSettings && useDuration;
        bool ShowUseCooldownDefinition => UsesCustomRuntimeSettings && useUseCooldown;
        bool ShowCountDefinition => UsesCustomRuntimeSettings && useCount;
    }

    public interface IStatusEffectOperationDefinition
    {
        bool TryBuild(StatusEffectBuildContext context, out IStatusEffectOperationRuntime runtime);
    }

    public interface IStatusEffectOperationRuntime
    {
        void Apply();
        void Remove();
        void Enable();
        void Disable();
        void Reset();
        void RefreshValue();
    }

    [Serializable]
    public sealed class ScalarModifierOperationDefinition : IStatusEffectOperationDefinition
    {
        [LabelText("Target Key")]
        [Tooltip("変更対象の scalar key です。")]
        public ScalarKey TargetKey;

        [LabelText("Apply Mode")]
        [EnumToggleButtons]
        [Tooltip("加算で入れるか、乗算で入れるかを指定します。")]
        public ScalarModifierApplyMode ApplyMode = ScalarModifierApplyMode.Add;

        [ShowIf(nameof(UsesMulPhase))]
        [LabelText("Mul Phase")]
        [Tooltip("乗算 modifier をどの計算段階に入れるかを指定します。")]
        public ScalarMulPhase MulPhase = ScalarMulPhase.PreAdd;

        [LabelText("Value Mode")]
        [EnumToggleButtons]
        [Tooltip("Intensity をそのまま使うか、StatusEffect Runtime の VarStore を含む DynamicValue 評価で決めるかを指定します。")]
        public StatusEffectScalarValueMode ValueMode = StatusEffectScalarValueMode.RuntimeIntensity;

        [ShowIf(nameof(UsesRuntimeIntensity))]
        [LabelText("Runtime Intensity Slot")]
        [EnumToggleButtons]
        [Tooltip("RuntimeIntensity 参照時にどのスロット（A〜G）を読むかを指定します。")]
        public StatusEffectRuntimeIntensityReference RuntimeIntensitySlot = StatusEffectRuntimeIntensityReference.A;

        [ShowIf(nameof(UsesDynamicValue))]
        [LabelText("Value")]
        [Tooltip("Value Mode が DynamicValue のときに使う値です。StatusEffect の intensity など runtime vars を参照できます。")]
        public DynamicValue<float> Value;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [Tooltip("どの Actor の ScalarService を変更するかを指定します。")]
        public ActorSource TargetActorSource;

        [LabelText("Layer")]
        [Tooltip("modifier を積む layer 名です。空文字でも使用できます。")]
        public string Layer = string.Empty;

        bool UsesMulPhase() => ApplyMode == ScalarModifierApplyMode.Mul;
        bool UsesDynamicValue() => ValueMode == StatusEffectScalarValueMode.DynamicValue;
        bool UsesRuntimeIntensity() => ValueMode == StatusEffectScalarValueMode.RuntimeIntensity;

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
                ValueMode,
                RuntimeIntensitySlot,
                valueExpression,
                evaluationContext,
                context.Definition.DefinitionId);
            return true;
        }
    }

    static class StatusEffectExpressionVariables
    {
        public static readonly IReadOnlyList<ExpressionVariable> Variables = Build();

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
        [Tooltip("有効な場合、この runtime は service-global lifetime を参照します。Duration の値は local では使いません。")]
        public bool SyncWithGlobalLifetime;

        [LabelText("Duration")]
        [ShowIf("@!SyncWithGlobalLifetime")]
        [Tooltip("effect の持続時間です。-1 を返すと無期限になります。")]
        public DynamicValue<float> Duration;

        [LabelText("Expire Action")]
        [EnumToggleButtons]
        [Tooltip("持続時間が切れたときに Disable するか Remove するかを指定します。")]
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
        [Tooltip("有効な場合、この runtime は service-global use cooldown を参照します。Duration の値は local では使いません。")]
        public bool SyncWithGlobalUseCooldown;

        [LabelText("Duration")]
        [ShowIf("@!SyncWithGlobalUseCooldown")]
        [Tooltip("Use 実行後に再使用可能になるまでの cooldown 秒数です。0 以下なら cooldown は発生しません。")]
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
        [Tooltip("有効な場合、この runtime は service-global count を参照します。Max Count は local では使いません。")]
        public bool SyncWithGlobalCount;

        [LabelText("Max Count")]
        [ShowIf("@!SyncWithGlobalCount")]
        [Tooltip("Use 可能な最大回数です。0 以下なら無制限です。")]
        public DynamicValue<int> MaxCount;

        [LabelText("Exhausted Action")]
        [EnumToggleButtons]
        [Tooltip("Use 回数が尽きたときに effect をどう扱うかを指定します。")]
        public EffectCountExhaustedAction ExhaustedAction = EffectCountExhaustedAction.Disable;

        [LabelText("Active Policy")]
        [EnumToggleButtons]
        [Tooltip("一時的に無効でも Active 扱いにするかを指定します。")]
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
        readonly StatusEffectScalarValueMode _valueMode;
        readonly StatusEffectRuntimeIntensityReference _runtimeIntensitySlot;
        readonly DynamicValue<float> _value;
        readonly IDynamicContext _evaluationContext;
        readonly string _tag;
        readonly object _source;

        ScalarHandle? _handle;
        bool _isApplied;

        public ScalarModifierOperationRuntime(
            IScopeNode targetScope,
            IBaseScalarService scalarService,
            ScalarKey targetKey,
            ScalarModifierApplyMode applyMode,
            ScalarMulPhase mulPhase,
            string layer,
            StatusEffectScalarValueMode valueMode,
            StatusEffectRuntimeIntensityReference runtimeIntensitySlot,
            DynamicValue<float> value,
            IDynamicContext evaluationContext,
            string definitionId)
        {
            _targetScope = targetScope;
            _scalarService = scalarService;
            _targetKey = targetKey;
            _applyMode = applyMode;
            _mulPhase = mulPhase;
            _layer = layer ?? string.Empty;
            _valueMode = valueMode;
            _runtimeIntensitySlot = runtimeIntensitySlot;
            _value = value;
            _evaluationContext = evaluationContext;
            _tag = BuildTag(definitionId);
            _source = targetScope;
        }

        public void Apply()
        {
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
            DisposeHandle();
        }

        public void Enable()
        {
            Apply();
        }

        public void Disable()
        {
            DisposeHandle();
        }

        public void Reset()
        {
            DisposeHandle();
            Apply();
        }

        public void RefreshValue()
        {
            Apply();
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

        static string BuildTag(string definitionId)
            => string.IsNullOrWhiteSpace(definitionId) ? "StatusEffect" : definitionId;

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
