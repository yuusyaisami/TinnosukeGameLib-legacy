#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BuildRoomMapCommandData : ICommandData
    {
        public int CommandId => CommandIds.BuildRoomMap;
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
        public VarUnityObjectSource<Game.RoomMap.RoomMapProfileSO> ProfileSource = new();

        [BoxGroup("Visualizer")]
        [LabelText("Delay Override")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        public DynamicValue<float> VisualDelayOverride = new();
    }

    [Serializable]
    public sealed class ClearRoomMapCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearRoomMap;
        public string DebugData => "Clear";
    }

    [Serializable]
    public sealed class RemoveRoomMapRectCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveRoomMapRect;
        public string DebugData => $"Rect={Rect}";
        public RectInt Rect;
    }

    [Serializable]
    public sealed class ApplyRoomMapVisualCommandData : ICommandData
    {
        public int CommandId => CommandIds.ApplyRoomMapVisual;
        public string DebugData
        {
            get
            {
                var assetName = VisualSource.Asset != null ? VisualSource.Asset.name : "null";
                if (VisualSource.PreferVar && VisualSource.VarId != 0)
                    return $"VarId={VisualSource.VarId} Asset={assetName}";
                return $"Asset={assetName}";
            }
        }
        public VarUnityObjectSource<Game.RoomMap.RoomMapTileVisualSO> VisualSource = new();
    }

    [Serializable]
    public sealed class GetRoomMapCenterCommandData : ICommandData
    {
        public int CommandId => CommandIds.GetRoomMapCenter;
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
        public VarUnityObjectSource<Game.RoomMap.RoomMapProfileSO> ProfileSource = new();
    }
}
