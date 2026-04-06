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
        [Tooltip("この effect を一意に識別する固定 ID です。通常は StatusEffect.GameLogic.* の形式で付けます。")]
        string definitionId = string.Empty;

        [BoxGroup("Identity")]
        [LabelText("Default Runtime Tag")]
        [SerializeField]
        [Tooltip("付与時にタグが省略された場合に使う既定の runtimeTag です。")]
        string defaultRuntimeTag = string.Empty;

        [BoxGroup("Presentation")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("UI やログに見せる名前・説明・分類などの表示データです。")]
        EffectVisualData visualData = new();

        [BoxGroup("Operation")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("この effect が実際に変更する scalar の内容です。")]
        ScalarModifierOperationDefinition scalarOperation = CreateDefaultOperation();

        [BoxGroup("Runtime")]
        [LabelText("Runtime Control")]
        [EnumToggleButtons]
        [SerializeField]
        [Tooltip("Custom は definition 個別設定を使用します。AutoGlobal は Use/Cooldown/Count/Lifetime の利用判定を StatusEffectService の Global 設定に完全委譲します。")]
        StatusEffectRuntimeControlMode runtimeControlMode = StatusEffectRuntimeControlMode.Custom;

        [BoxGroup("Duration")]
        [LabelText("Use Lifetime")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("effect 登録時から進む lifetime timer を使う場合に有効にします。")]
        bool useDuration;

        [BoxGroup("Duration")]
        [ShowIf(nameof(ShowDurationDefinition))]
        [SerializeReference]
        [Tooltip("lifetime timer の計算方法です。Use Lifetime が有効なときだけ使われます。")]
        IStatusEffectDurationDefinition? durationDefinition;

        [BoxGroup("Cooldown")]
        [LabelText("Use Cooldown")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Use 実行後に始まる cooldown timer を使う場合に有効にします。")]
        bool useUseCooldown;

        [BoxGroup("Cooldown")]
        [ShowIf(nameof(ShowUseCooldownDefinition))]
        [SerializeReference]
        [Tooltip("Use 実行後の cooldown の計算方法です。")]
        IStatusEffectUseCooldownDefinition? useCooldownDefinition;

        [BoxGroup("Count")]
        [LabelText("Use Count")]
        [ShowIf(nameof(UsesCustomRuntimeSettings))]
        [SerializeField]
        [Tooltip("Use 回数システムを使う effect の場合に有効にします。")]
        bool useCount;

        [BoxGroup("Count")]
        [ShowIf(nameof(ShowCountDefinition))]
        [SerializeReference]
        [Tooltip("Use 回数の上限や切れたときの挙動です。既定では Nail の MaxHitCount を参照します。")]
        IStatusEffectCountDefinition? countDefinition = CreateDefaultCountDefinition();

        [BoxGroup("Hooks")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Apply / Remove / Use など各タイミングで挟む command 群です。")]
        StatusEffectHookSet defaultHooks = new();

        [NonSerialized]
        IReadOnlyList<IStatusEffectOperationDefinition>? singleOperationCache;

        public override string DefinitionId => definitionId ?? string.Empty;
        public override EffectVisualData VisualData => visualData ?? new EffectVisualData();
        public override string DefaultRuntimeTag => defaultRuntimeTag ?? string.Empty;
        public override StatusEffectRuntimeControlMode RuntimeControlMode => runtimeControlMode;
        public override bool UseDuration => useDuration;
        public override bool UseUseCooldown => useUseCooldown;
        public override bool UseCount => useCount;
        public override IStatusEffectDurationDefinition? DurationDefinition => durationDefinition;
        public override IStatusEffectUseCooldownDefinition? UseCooldownDefinition => useCooldownDefinition;
        public override IStatusEffectCountDefinition? CountDefinition => countDefinition;
        public override IReadOnlyList<IStatusEffectOperationDefinition> Operations => GetOperations();
        public override StatusEffectHookSet DefaultHooks => defaultHooks ?? new StatusEffectHookSet();

        bool UsesCustomRuntimeSettings => runtimeControlMode == StatusEffectRuntimeControlMode.Custom;
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
