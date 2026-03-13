#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class HealthApplyDamageCommandData : ICommandData
    {
        public int CommandId => CommandIds.HealthApplyDamage;
        public string DebugData => $"Target={Target.Kind} Type={DamageType}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Damage")]
        [LabelText("Amount")]
        public DynamicValue<float> Amount = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Damage")]
        [LabelText("Damage Type")]
        public DamageType DamageType = DamageType.Physical;

        [BoxGroup("Damage")]
        [LabelText("Is Critical")]
        public bool IsCritical;

        [BoxGroup("Damage")]
        [LabelText("Tag")]
        public string Tag = string.Empty;

        [BoxGroup("Payload")]
        [LabelText("Use Command Vars As Source")]
        [Tooltip("true: CommandContext.Vars を DamageContext.Source に渡す")]
        public bool UseCommandVarsAsSource = true;

        [BoxGroup("Payload")]
        [LabelText("Use Command Vars As Extra Payload")]
        [Tooltip("true: CommandContext.Vars を DamageContext.ExtraPayload に渡す")]
        public bool UseCommandVarsAsExtraPayload;
    }

    [Serializable]
    public sealed class HealthApplyHealCommandData : ICommandData
    {
        public int CommandId => CommandIds.HealthApplyHeal;
        public string DebugData => $"Target={Target.Kind} Type={HealType}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Heal")]
        [LabelText("Amount")]
        public DynamicValue<float> Amount = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Heal")]
        [LabelText("Heal Type")]
        public HealType HealType = HealType.Normal;

        [BoxGroup("Heal")]
        [LabelText("Tag")]
        public string Tag = string.Empty;

        [BoxGroup("Payload")]
        [LabelText("Use Command Vars As Source")]
        public bool UseCommandVarsAsSource = true;

        [BoxGroup("Payload")]
        [LabelText("Use Command Vars As Extra Payload")]
        public bool UseCommandVarsAsExtraPayload;
    }

    public enum HealthControlAction
    {
        Kill = 0,
        Revive = 1,
        SetHP = 2,
        SetMaxHP = 3,
        SetInvincibleLayer = 4,
        RemoveInvincibleLayer = 5,
        RegisterModifier = 6,
        UnregisterModifier = 7,
        MutateEventCommands = 8,
    }

    [Flags]
    public enum HealthEventCommandTargets
    {
        None = 0,
        OnDamaged = 1 << 0,
        OnHealed = 1 << 1,
        OnDied = 1 << 2,
        OnRevived = 1 << 3,
        OnInvincibleStarted = 1 << 4,
        OnInvincibleEnded = 1 << 5,
        All = OnDamaged | OnHealed | OnDied | OnRevived | OnInvincibleStarted | OnInvincibleEnded,
    }

    public enum HealthEventCommandBindingSelectMode
    {
        All = 0,
        First = 1,
        Index = 2,
    }

    [Serializable]
    public sealed class HealthEventCommandMutationStep
    {
        [LabelText("Targets")]
        [EnumToggleButtons]
        public HealthEventCommandTargets Targets = HealthEventCommandTargets.OnDamaged;

        [LabelText("Binding Select")]
        [EnumToggleButtons]
        public HealthEventCommandBindingSelectMode BindingSelect = HealthEventCommandBindingSelectMode.All;

        [ShowIf(nameof(ShouldShowBindingIndex))]
        [LabelText("Binding Index")]
        [MinValue(0)]
        public int BindingIndex;

        [ShowIf(nameof(ShouldShowCreateIfMissing))]
        [ToggleLeft]
        [LabelText("Create Binding If Missing")]
        [Tooltip("対象バインディングが存在しない場合に新規作成します。")]
        public bool CreateBindingIfMissing = true;

        [LabelText("Mutation")]
        [InlineProperty]
        public CommandListMutationStep Mutation = new();

        bool ShouldShowBindingIndex() => BindingSelect == HealthEventCommandBindingSelectMode.Index;
        bool ShouldShowCreateIfMissing() => Mutation != null && Mutation.RequiresCommands();
    }

    [Serializable]
    public sealed class HealthEventCommandMutationProgram
    {
        [LabelText("Steps")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<HealthEventCommandMutationStep> Steps = new();
    }

    [Serializable]
    public sealed class HealthControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.HealthControl;
        public string DebugData => $"Target={Target.Kind} Action={Action}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Control")]
        [LabelText("Action")]
        public HealthControlAction Action = HealthControlAction.Kill;

        [BoxGroup("Revive")]
        [ShowIf("@Action == HealthControlAction.Revive")]
        [LabelText("HP Ratio")]
        public DynamicValue<float> ReviveHPRatio = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Set HP")]
        [ShowIf("@Action == HealthControlAction.SetHP")]
        [LabelText("HP")]
        public DynamicValue<float> HPValue = DynamicValueExtensions.FromLiteral(100f);

        [BoxGroup("Set MaxHP")]
        [ShowIf("@Action == HealthControlAction.SetMaxHP")]
        [LabelText("Max HP")]
        public DynamicValue<float> MaxHPValue = DynamicValueExtensions.FromLiteral(100f);

        [BoxGroup("Invincible")]
        [ShowIf("@Action == HealthControlAction.SetInvincibleLayer || Action == HealthControlAction.RemoveInvincibleLayer")]
        [LabelText("Layer Key")]
        public string InvincibleLayerKey = "command";

        [BoxGroup("Invincible")]
        [ShowIf("@Action == HealthControlAction.SetInvincibleLayer")]
        [LabelText("Value")]
        public DynamicValue<bool> InvincibleValue = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Modifier")]
        [ShowIf("@Action == HealthControlAction.RegisterModifier")]
        [LabelText("Modifier")]
        public BaseHealthModifierSO? Modifier;

        [BoxGroup("Modifier")]
        [ShowIf("@Action == HealthControlAction.UnregisterModifier")]
        [LabelText("Modifier Id")]
        public string ModifierId = string.Empty;

        [BoxGroup("Event Commands")]
        [ShowIf("@Action == HealthControlAction.MutateEventCommands")]
        [LabelText("Program")]
        [InlineProperty]
        [HideLabel]
        public HealthEventCommandMutationProgram EventCommandProgram = new();
    }
}
