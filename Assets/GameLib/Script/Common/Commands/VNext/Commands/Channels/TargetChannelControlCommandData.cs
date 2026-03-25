#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Targeting;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum TargetChannelControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
        SwapPreset = 30,
        MutateSettings = 40,
        ResetRuntimeOverrides = 50,
        SetDirectTargets = 60,
        ClearDirectTargets = 70,
    }

    [Serializable]
    public sealed class TargetChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.TargetChannelControl;

        public string DebugData => $"Hub={HubSource.Kind} Op={Operation} Tag={Tag}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public TargetChannelControlOperation Operation = TargetChannelControlOperation.RegisterOrReplace;

        [BoxGroup("Target")]
        [ShowIf(nameof(UsesTag))]
        [LabelText("Tag")]
        public string Tag = string.Empty;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<TargetChannelPreset> Preset = new();

        [BoxGroup("Mutation")]
        [ShowIf(nameof(UsesMutation))]
        [InlineProperty]
        [HideLabel]
        public TargetChannelRuntimeMutation Mutation = new();

        [BoxGroup("Targets")]
        [ShowIf(nameof(UsesDirectTargets))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<ActorSource> DirectTargets = new();

        bool UsesTag =>
            Operation != TargetChannelControlOperation.RegisterOrReplace;

        bool UsesPreset =>
            Operation == TargetChannelControlOperation.RegisterOrReplace ||
            Operation == TargetChannelControlOperation.SwapPreset;

        bool UsesMutation => Operation == TargetChannelControlOperation.MutateSettings;
        bool UsesDirectTargets => Operation == TargetChannelControlOperation.SetDirectTargets;
    }
}
