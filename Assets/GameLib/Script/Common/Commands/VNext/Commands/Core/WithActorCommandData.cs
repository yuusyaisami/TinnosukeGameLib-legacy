#nullable enable
using System;
using Game.Commands;
using Game.Common;
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
        TargetChannel = 11,
    }

    [Serializable]
    public sealed class SharedActorSourceRef
    {
        [LabelText("Shared Tag")]
        public string SharedTag = string.Empty;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Owner\", SharedHubActorSource)")]
        public ActorSource SharedHubActorSource = new() { Kind = ActorSourceKind.Current };
    }

    [Serializable]
    public sealed class TargetChannelActorSourceRef
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", ChannelOwnerActorSource)")]
        public ActorSource ChannelOwnerActorSource;

        [LabelText("Channel Tag")]
        public string ChannelTag = string.Empty;

        [LabelText("Target Select")]
        public TargetChannelTargetSelectMode TargetSelectMode = TargetChannelTargetSelectMode.First;

        [ShowIf("@TargetSelectMode == Game.Common.TargetChannelTargetSelectMode.FilterByActorSource")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Filter Actor\", FilterActorSource)")]
        public ActorSource FilterActorSource;

        [ShowIf("@TargetSelectMode == Game.Common.TargetChannelTargetSelectMode.FilterByActorSource")]
        [LabelText("Fallback To First On Miss")]
        public bool FallbackToFirstIfFilterMiss = true;
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
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        public SharedActorSourceRef? Shared;

        [ShowIf("@Kind == ActorSourceKind.ContextSlot")]
        [LabelText("Context Slot")]
        public CommandLtsSlot ContextSlot;

        [ShowIf("@Kind == ActorSourceKind.TargetChannel")]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        public TargetChannelActorSourceRef? TargetChannel;
    }

    public enum WithActorExecutionScope
    {
        ActorOnly = 0,
        ActorAndDescendants = 1,
        DescendantsOnly = 2,
    }

    public enum WithActorContextSlot
    {
        ContextA = 100,
        ContextB = 110,
        ContextC = 120,
        ContextD = 130,
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
                var actorContextLabel = StoreActorToContext ? $" CallerCtx={ActorContextSlot}" : string.Empty;
                return $"{actorLabel} Scope={ExecutionScope} Await={AwaitMode} Body={bodyCount}{actorContextLabel}";
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

        [HideInInspector]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorOnly;

        [BoxGroup("Context")]
        [ToggleLeft]
        [LabelText("Store Caller Actor To Context")]
        public bool StoreActorToContext;

        [BoxGroup("Context")]
        [ShowIf(nameof(StoreActorToContext))]
        [LabelText("Caller Context Slot")]
        [EnumToggleButtons]
        public WithActorContextSlot ActorContextSlot = WithActorContextSlot.ContextA;

        [LabelText("Body")]
        public CommandListData Body = new();



        bool ShouldShowDescendantFilterToggle() => ExecutionScope != WithActorExecutionScope.ActorOnly;
        bool ShouldShowDescendantFilter() => ShouldShowDescendantFilterToggle() && UseDescendantFilter;
    }
}
