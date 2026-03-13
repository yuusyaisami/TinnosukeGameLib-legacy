#nullable enable
using System;
using Game.Commands;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum DescendantFilterMode
    {
        Include = 0,
        Exclude = 1,
    }

    /// <summary>
    /// A purpose-built command for executing commands on an actor's descendants with a filter.
    /// - Executes <see cref="Common"/> on every target.
    /// - Executes <see cref="OnMatched"/> when the target matches the filter (or does NOT match when mode is Exclude).
    /// - Executes <see cref="OnUnmatched"/> on the opposite set.
    /// </summary>
    [Serializable]
    public sealed class WithActorDescendantRouterCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithActorDescendantRouter;
        public string DebugData
        {
            get
            {
                var actorLabel = ActorSourceOdinLabelHelper.GetLabel("Actor", ActorSource);
                var commonCount = Common?.Count ?? 0;
                var matchedCount = OnMatched?.Count ?? 0;
                var unmatchedCount = OnUnmatched?.Count ?? 0;
                return $"{actorLabel} Mode={FilterMode} Common={commonCount} Match={matchedCount} Unmatch={unmatchedCount}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorAndDescendants;

        [LabelText("Filter Mode")]
        [EnumToggleButtons]
        public DescendantFilterMode FilterMode = DescendantFilterMode.Include;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Descendant Filter\", DescendantFilter)")]
        public ActorSource DescendantFilter;

        [LabelText("Common")]
        public CommandListData Common = new();

        [LabelText("On Matched")]
        public CommandListData OnMatched = new();

        [LabelText("On Unmatched")]
        public CommandListData OnUnmatched = new();

        public bool HasAnyCommands()
        {
            return (Common != null && Common.Count > 0)
                || (OnMatched != null && OnMatched.Count > 0)
                || (OnUnmatched != null && OnUnmatched.Count > 0);
        }
    }
}
