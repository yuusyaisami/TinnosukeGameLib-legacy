#nullable enable
using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    public enum RoomMapAutoTileCondKind
    {
        Any = 0,
        OutOfBounds = 1,
        ExactTileId = 2,
        HasTag = 3,
        SameAsCenter = 4,
        DifferentFromCenter = 5,
    }

    [Serializable]
    public struct RoomMapAutoTileCellCond
    {
        [SerializeField] RoomMapAutoTileCondKind kind;
        [SerializeField] int tileId;
        [SerializeField] RoomMapTileTagFlags tag;

        public RoomMapAutoTileCondKind Kind => kind;
        public int TileId => tileId;
        public RoomMapTileTagFlags Tag => tag;

        public static RoomMapAutoTileCellCond Any() => new() { kind = RoomMapAutoTileCondKind.Any };
        public static RoomMapAutoTileCellCond OutOfBounds() => new() { kind = RoomMapAutoTileCondKind.OutOfBounds };
        public static RoomMapAutoTileCellCond ExactTileId(int id) => new() { kind = RoomMapAutoTileCondKind.ExactTileId, tileId = id };
        public static RoomMapAutoTileCellCond HasTag(RoomMapTileTagFlags t) => new() { kind = RoomMapAutoTileCondKind.HasTag, tag = t };
        public static RoomMapAutoTileCellCond SameAsCenter() => new() { kind = RoomMapAutoTileCondKind.SameAsCenter };
        public static RoomMapAutoTileCellCond DifferentFromCenter() => new() { kind = RoomMapAutoTileCondKind.DifferentFromCenter };
    }

    [Serializable]
    public sealed class RoomMapAutoTilePattern
    {
        [Tooltip("3x3 conditions, index: left-top -> right-bottom")]
        [SerializeField]
        RoomMapAutoTileCellCond[] conds = new RoomMapAutoTileCellCond[9]
        {
            default, default, default,
            default, default, default,
            default, default, default,
        };

        public RoomMapAutoTileCellCond[] Conds => conds;

        public RoomMapAutoTileCellCond GetAt(int x01, int y01)
        {
            var idx = y01 * 3 + x01;
            if (conds == null || idx < 0 || idx >= 9)
                return default;
            return conds[idx];
        }

        public void EnsureSize()
        {
            if (conds == null || conds.Length != 9)
                conds = new RoomMapAutoTileCellCond[9];

            // Default to Any for readability
            for (int i = 0; i < 9; i++)
            {
                if (conds[i].Kind == 0 && conds[i].TileId == 0 && conds[i].Tag == 0)
                    conds[i] = RoomMapAutoTileCellCond.Any();
            }
        }
    }

    [Serializable]
    public sealed class RoomMapAutoTileRule
    {
        [LabelWidth(150)]
        public bool Enabled = true;

        [SerializeField, LabelWidth(150)]
        string displayName = string.Empty;
        [LabelWidth(150)]
        public int Priority;

        [InlineProperty, LabelWidth(150)]
        public RoomMapAutoTilePattern Pattern = new();

        [InlineProperty, LabelWidth(150)]
        public RoomTileVisualData ResultOverride = new();
        [LabelWidth(150)]
        public bool AllowRotate90;
        [LabelWidth(150)]
        public bool AllowMirrorX;
        [LabelWidth(150)]
        public bool AllowMirrorY;

        public string DisplayName => displayName;

        public void EnsureDefaults()
        {
            Pattern ??= new RoomMapAutoTilePattern();
            Pattern.EnsureSize();
            ResultOverride ??= new RoomTileVisualData();
        }
    }

    [CreateAssetMenu(
        fileName = "NewRoomMapAutoTileRuleSet",
        menuName = "Game/RoomMap/AutoTile RuleSet",
        order = 131)]
    public sealed class RoomMapAutoTileRuleSetSO : ScriptableObject
    {
        [BoxGroup("Rules")]
        [SerializeField]
        RoomMapAutoTileRule[] rules = Array.Empty<RoomMapAutoTileRule>();
        public RoomMapAutoTileRule[] Rules => rules;

        void OnValidate()
        {
            if (rules == null)
                rules = Array.Empty<RoomMapAutoTileRule>();

            for (int i = 0; i < rules.Length; i++)
                rules[i]?.EnsureDefaults();
        }
    }
}
