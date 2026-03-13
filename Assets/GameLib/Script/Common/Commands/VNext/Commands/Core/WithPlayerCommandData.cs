#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WithPlayerCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithPlayer;
        public string DebugData
        {
            get
            {
                var bodyCount = Body?.Count ?? 0;
                return $"Player Scope={ExecutionScope} Body={bodyCount}";
            }
        }

        [HideLabel]
        [ShowIf(nameof(ShouldShowDescendantFilterToggle))]
        [ToggleLeft]
        public bool UseDescendantFilter;

        [ShowIf(nameof(ShouldShowDescendantFilter))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Descendant Filter\", DescendantFilter)")]
        [InlineProperty]
        public ActorSource DescendantFilter;

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorOnly;

        [LabelText("Body")]
        public CommandListData Body = new();

        bool ShouldShowDescendantFilterToggle() => ExecutionScope != WithActorExecutionScope.ActorOnly;
        bool ShouldShowDescendantFilter() => ShouldShowDescendantFilterToggle() && UseDescendantFilter;
    }
}
