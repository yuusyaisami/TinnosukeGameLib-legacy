#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class PlaceTraitRuntimeCommandData : ICommandData
    {
        public int CommandId => CommandIds.PlaceTraitRuntime;

        public string DebugData
            => $"Holder={HolderKey} Selector={Selector.DebugData}";

        [BoxGroup("Holder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        public ActorSource HolderActorSource;

        [BoxGroup("Holder")]
        [LabelText("Holder Key")]
        public string HolderKey = string.Empty;

        [BoxGroup("Trait")]
        [InlineProperty]
        public TraitElementSelector Selector;

        [BoxGroup("Override")]
        [BoxGroup("Override Position")]
        [LabelText("Override Position")]
        public bool OverridePosition;

        [BoxGroup("Override")]
        [BoxGroup("Override Position")]
        [ShowIf(nameof(OverridePosition))]
        [LabelText("Position")]
        public DynamicValue<Vector3> Position = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Override")]
        [BoxGroup("Override Rotation")]
        [LabelText("Override Rotation")]
        public bool OverrideRotation;

        [BoxGroup("Override")]
        [BoxGroup("Override Rotation")]
        [ShowIf(nameof(OverrideRotation))]
        [LabelText("Rotation Euler")]
        public DynamicValue<Vector3> RotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Override")]
        [BoxGroup("Override Scale")]
        [LabelText("Override Scale")]
        public bool OverrideScale;

        [BoxGroup("Override")]
        [BoxGroup("Override Scale")]
        [ShowIf(nameof(OverrideScale))]
        [LabelText("Scale")]
        public DynamicValue<Vector3> Scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [BoxGroup("Override")]
        [BoxGroup("Override Parent")]
        [LabelText("Override Parent")]
        public bool OverrideParent;

        [BoxGroup("Override")]
        [BoxGroup("Override Parent")]
        [ShowIf(nameof(OverrideParent))]
        [LabelText("Use Parent")]
        public bool UseParent;

        [BoxGroup("Override")]
        [BoxGroup("Override Parent")]
        [ShowIf("@OverrideParent && UseParent")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ParentActorSource)")]
        public ActorSource ParentActorSource;

        [BoxGroup("After Place")]
        [LabelText("Run On Placed")]
        [Tooltip("PlaceTraitRuntime 実行成功後、spawn された Runtime を actor にして実行します。Trait のデータと spawn 後 Runtime の Blackboard を VarStore にマージした状態で流れます。")]
        public bool RunOnPlacedCommands;

        [BoxGroup("After Place")]
        [ShowIf(nameof(RunOnPlacedCommands))]
        [LabelText("On Placed Commands")]
        [Tooltip("PlaceTraitRuntime 実行成功後、spawn された Runtime を actor にして実行します。Trait のデータと spawn 後 Runtime の Blackboard を VarStore にマージした状態で流れます。")]
        public CommandListData OnPlacedCommands = new();
    }
}
