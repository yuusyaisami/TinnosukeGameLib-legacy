#nullable enable
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    [CreateAssetMenu(
        fileName = "NewRoomMapProfile",
        menuName = "Game/RoomMap/Profile",
        order = 140)]
    public sealed class RoomMapProfileSO : ScriptableObject
    {
        [BoxGroup("Refs")]
        [Required, AssetOrInternal]
        [SerializeField] RoomMapLayoutSO? layout;

        [BoxGroup("Refs")]
        [AssetOrInternal]
        [SerializeField] RoomMapDynamicLayoutSO? dynamicLayout;

        [BoxGroup("Refs")]
        [Required, AssetOrInternal]
        [SerializeField] RoomMapTileDefinitionSO? definition;

        [BoxGroup("Refs")]
        [Required, AssetOrInternal]
        [SerializeField] RoomMapTileVisualSO? visual;

        [BoxGroup("Refs")]
        [AssetOrInternal]
        [SerializeField] RoomMapTileVisualSO? dynamicVisual;

        [BoxGroup("Hooks")]
        [SerializeField] CommandListData onBegin = new();

        [BoxGroup("Hooks")]
        [SerializeField] CommandListData onCompleted = new();

        [BoxGroup("Transform")]
        [SerializeField] Vector2 cellSize = Vector2.one;

        [BoxGroup("Transform")]
        [LabelText("Local Cell Offset")]
        [SerializeField] Vector2 localCellOffset;

        [BoxGroup("Transform")]
        [EnumToggleButtons]
        [SerializeField] RoomMapOriginMode originMode = RoomMapOriginMode.TopLeft;

        [BoxGroup("Transform")]
        [SerializeField] bool worldSpace = false;

        [BoxGroup("Transform")]
        [SerializeField] Vector3 basePosition;

        [BoxGroup("Transform")]
        [SerializeField] float baseRotationDegZ;

        [BoxGroup("Visualize")]
        [SerializeField] RoomMapVisualOrder visualOrder = RoomMapVisualOrder.RowMajor_TopLeft;

        [BoxGroup("Visualize")]
        [SerializeField] float delayPerCellSeconds;

        [BoxGroup("Visualize")]
        [EnumToggleButtons]
        [SerializeField] RoomMapFailurePolicy failurePolicy = RoomMapFailurePolicy.FailFast;

        public RoomMapLayoutSO Layout => layout!;
        public RoomMapDynamicLayoutSO? DynamicLayout => dynamicLayout;
        public RoomMapTileDefinitionSO Definition => definition!;
        public RoomMapTileVisualSO Visual => visual!;
        public RoomMapTileVisualSO DynamicVisualOrFallback => dynamicVisual != null ? dynamicVisual : visual!;

        public CommandListData OnBegin => onBegin;
        public CommandListData OnCompleted => onCompleted;

        public Vector2 CellSize => cellSize;
        public Vector2 LocalCellOffset => localCellOffset;
        public RoomMapOriginMode OriginMode => originMode;
        public bool WorldSpace => worldSpace;
        public Vector3 BasePosition => basePosition;
        public float BaseRotationDegZ => baseRotationDegZ;

        public RoomMapVisualOrder VisualOrder => visualOrder;
        public float DelayPerCellSeconds => delayPerCellSeconds;
        public RoomMapFailurePolicy FailurePolicy => failurePolicy;
    }
}
