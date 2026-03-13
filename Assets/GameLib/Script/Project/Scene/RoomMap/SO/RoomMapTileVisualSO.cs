#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    [CreateAssetMenu(
        fileName = "NewRoomMapTileVisual",
        menuName = "Game/RoomMap/Tile Visual",
        order = 130)]
    public sealed class RoomMapTileVisualSO : ScriptableObject
    {
        [BoxGroup("Common")]
        [SerializeField]
        CommandListData commonCommand = new();

        [BoxGroup("AutoTile")]
        [SerializeField, AssetOrInternal]
        RoomMapAutoTileRuleSetSO? autoTileRuleSet;
        [Serializable]
        public sealed class RoomMapTileVisualEntry
        {
            [HorizontalGroup("Row", Width = 0.45f)]
            [LabelText("Tile")]
            [MinValue(0)]
            [RoomMapTileIdDropdown]
            public int TileId;

            [HorizontalGroup("Row", Width = 0.55f)]
            [HideLabel]
            [InlineProperty]
            public RoomTileVisualData Visual = new();
        }

        [BoxGroup("Visuals")]
        [SerializeField]
        List<RoomMapTileVisualEntry> tileVisuals = new();

        public CommandListData CommonCommand => commonCommand;
        public RoomMapAutoTileRuleSetSO? AutoTileRuleSet => autoTileRuleSet;
        public IReadOnlyList<RoomMapTileVisualEntry> TileVisuals => tileVisuals;

        public bool TryGetTileVisual(int tileId, out RoomTileVisualData? visual)
        {
            visual = null;
            if (tileId <= 0 || tileVisuals == null)
                return false;

            for (int i = 0; i < tileVisuals.Count; i++)
            {
                var e = tileVisuals[i];
                if (e == null)
                    continue;
                if (e.TileId != tileId)
                    continue;
                visual = e.Visual;
                return visual != null;
            }

            return false;
        }

        void OnValidate()
        {
            if (tileVisuals == null)
                tileVisuals = new List<RoomMapTileVisualEntry>();

            for (int i = 0; i < tileVisuals.Count; i++)
            {
                tileVisuals[i] ??= new RoomMapTileVisualEntry();
                tileVisuals[i].Visual ??= new RoomTileVisualData();
            }

            commonCommand ??= new CommandListData();
        }
    }
}
