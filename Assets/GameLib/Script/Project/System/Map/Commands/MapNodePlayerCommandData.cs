#nullable enable
using System;
using Game.Common;
using Game.MapNode;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BuildMapNodeCommandData : ICommandData
    {
        public int CommandId => CommandIds.BuildMapNode;
        public string DebugData
        {
            get
            {
                var assetName = ProfileSource.Asset != null ? ProfileSource.Asset.name : "null";
                if (ProfileSource.PreferVar && ProfileSource.VarId != 0)
                    return $"VarId={ProfileSource.VarId} Asset={assetName}";
                return $"Asset={assetName}";
            }
        }

        public VarUnityObjectSource<MapNodeProfileSO> ProfileSource = new();
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(BuildScope)")]
        public ActorSource BuildScope;
    }

    [Serializable]
    public sealed class MoveMapNodeCommandData : ICommandData
    {
        public int CommandId => CommandIds.MoveMapNode;
        public string DebugData => $"NodeId={TargetNodeId.SourceTypeName} From={TargetNodeSource.Kind} Exec={ExecuteScope.Kind}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetNodeSource)")]
        public ActorSource TargetNodeSource;
        public DynamicValue<int> TargetNodeId = new();

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ExecuteScope)")]
        public ActorSource ExecuteScope;

        [InlineProperty]
        public MapNodeMoveOptions MoveOptions;
    }

    [Serializable]
    public sealed class RunMapNodeCommandsCommandData : ICommandData
    {
        public int CommandId => CommandIds.RunMapNodeCommands;
        public string DebugData
        {
            get
            {
                var count = Commands?.Count ?? 0;
                return $"Target={Target.Kind} Commands={count} All={ExecuteForAllTargets}";
            }
        }

        [InlineProperty]
        [LabelText("Target")]
        public MapNodeTarget Target;
        public CommandListData Commands = new();
        public bool ExecuteForAllTargets = true;
    }

    [Serializable]
    public sealed class RefreshMapNodeStateCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshMapNodeState;
        public string DebugData => $"WriteState={WriteState}";

        [InlineProperty]
        public MapNodeMoveOptions MoveOptions;
        public bool WriteState = true;
    }

    [Serializable]
    public sealed class WriteMapNodePlayerStateCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteMapNodePlayerState;
        public string DebugData => $"Target={SaveOptions.Target}";

        [InlineProperty]
        public MapNodePlayerSaveOptions SaveOptions;
    }
}
