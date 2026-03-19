#nullable enable
using System;
using Game.Commands;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum ActorSourceKind
    {
        Current = 0,
        ByIdentity = 3,
        FromUnityObject = 4,
        GameLogicRoot = 5,
        Player = 6,
        CommandRootActor = 7,
        Global = 8,
        Shared = 9,
        ContextSlot = 10,
    }

    [Serializable]
    public struct ActorSource
    {
        [EnumToggleButtons]

        public ActorSourceKind Kind;

        [ShowIf("@Kind == ActorSourceKind.ByIdentity")]
        [LabelText("Identity")]
        public CommandTargetIdentityFilter Identity;

        [ShowIf("@Kind == ActorSourceKind.FromUnityObject")]
        [LabelText("Unity Object")]
        public UnityEngine.Object? UnityObject;

        [ShowIf("@Kind == ActorSourceKind.Shared")]
        [LabelText("Shared Tag")]
        public string SharedTag;

        [ShowIf("@Kind == ActorSourceKind.ContextSlot")]
        [LabelText("Context Slot")]
        public CommandLtsSlot ContextSlot;
    }

    public enum WithActorExecutionScope
    {
        ActorOnly = 0,
        ActorAndDescendants = 1,
        DescendantsOnly = 2,
    }

    public enum VarsPolicy
    {
        Inherit = 0,
        UseActorScopeVars = 1,
    }

    [Serializable]
    public sealed class WithActorCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithActor;
        public string DebugData
        {
            get
            {
                var actorLabel = ActorSourceOdinLabelHelper.GetLabel("Actor", ActorSource);
                var bodyCount = Body?.Count ?? 0;
                return $"{actorLabel} Scope={ExecutionScope} Body={bodyCount}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;
        [BoxGroup("Self Filter", ShowLabel = false)]
        [ShowIf("@ActorSource.Kind == ActorSourceKind.Current")]
        [ToggleLeft]
        [LabelText("Check Self Identity")]
        public bool CheckSelfIdentityFilter;

        [BoxGroup("Self Filter")]
        [ShowIf("@ActorSource.Kind == ActorSourceKind.Current && CheckSelfIdentityFilter")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetIdentityLabel(\"Self Filter\", SelfIdentityFilter)")]
        [InlineProperty]
        public CommandTargetIdentityFilter SelfIdentityFilter;

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
