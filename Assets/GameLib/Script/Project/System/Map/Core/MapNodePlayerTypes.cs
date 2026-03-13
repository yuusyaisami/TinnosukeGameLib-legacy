#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Sirenix.OdinInspector;

namespace Game.MapNode
{
    public enum MapNodeTargetKind
    {
        Current,
        NextAvailable,
        NotNext,
        ByIdList,
        ByState,
        ByType,
        ByLayerIndex,
    }

    [Serializable]
    public struct MapNodeTarget
    {
        [EnumToggleButtons]
        public MapNodeTargetKind Kind;

        [ShowIf("@Kind == MapNodeTargetKind.ByIdList")]
        [LabelText("Single Id")]
        public int SingleNodeId;

        [ShowIf("@Kind == MapNodeTargetKind.ByIdList")]
        [LabelText("Id List")]
        public int[]? NodeIds;

        [ShowIf("@Kind == MapNodeTargetKind.ByState")]
        public MapNodeState State;

        [ShowIf("@Kind == MapNodeTargetKind.ByType")]
        public MapNodeType Type;

        [ShowIf("@Kind == MapNodeTargetKind.ByLayerIndex")]
        public int LayerIndex;
    }

    public enum MapNodeMoveResult
    {
        Ok,
        InvalidNodeId,
        NotBuilt,
        NotAllowedByState,
        NoCurrentNode,
    }

    public enum MapNodeLayerMoveDirection
    {
        Both = 0,
        ForwardOnly = 1,
        BackwardOnly = 2,
    }

    [Serializable]
    public struct MapNodeLayerMoveRule
    {
        public int LayerIndex;
        public MapNodeLayerMoveDirection Direction;
    }

    [Serializable]
    public struct MapNodeMoveOptions
    {
        [BoxGroup("Allow")]
        public bool AllowMoveToLocked;
        [BoxGroup("Allow")]
        public bool AllowMoveToVisited;
        [BoxGroup("Allow")]
        public bool AllowMoveToCompleted;
        [BoxGroup("Allow")]
        public bool AllowMoveToDisabled;

        [BoxGroup("Direction")]
        public MapNodeLayerMoveDirection DefaultLayerDirection;
        [BoxGroup("Direction")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<MapNodeLayerMoveRule>? LayerMoveRules;

        [BoxGroup("State Update")]
        public bool AutoUnlockNext;
        [BoxGroup("State Update")]
        public bool AutoLockOthers;

        [BoxGroup("State Update")]
        public MapNodeState StateForCurrent;
        [BoxGroup("State Update")]
        public MapNodeState StateForPrevious;
    }

    public enum MapNodePlayerSaveTarget
    {
        None = 0,
        Blackboard = 1,
        Scalar = 2,
        Both = 3,
    }

    [Serializable]
    public struct MapNodePlayerSaveOptions
    {
        [EnumToggleButtons]
        public MapNodePlayerSaveTarget Target;

        [BoxGroup("Write")]
        public bool WriteCurrentNode;
        [BoxGroup("Write")]
        public bool WriteNodeLists;

        [BoxGroup("Override")]
        public bool UseOverrideScope;
        [BoxGroup("Override")]
        [ShowIf(nameof(UseOverrideScope))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(OverrideScope)")]
        public ActorSource OverrideScope;
    }

    public sealed class MapNodePlayerState
    {
        public int CurrentNodeId = -1;
        public int PreviousNodeId = -1;
        public IReadOnlyList<int> NextNodeIds = Array.Empty<int>();
        public IReadOnlyList<int> NotNextNodeIds = Array.Empty<int>();
        public IReadOnlyList<int> VisitedNodeIds = Array.Empty<int>();
        public MapNodeRuntime? Runtime;
        public MapNodeProfileSO? ActiveProfile;
    }
}
