#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public readonly struct ScrollTileCoord : IEquatable<ScrollTileCoord>
    {
        public readonly int X;
        public readonly int Y;

        public ScrollTileCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ScrollTileCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is ScrollTileCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(ScrollTileCoord a, ScrollTileCoord b) => a.Equals(b);
        public static bool operator !=(ScrollTileCoord a, ScrollTileCoord b) => !a.Equals(b);
    }

    [Serializable]
    public sealed class ScrollChannelDefinition : ChannelDefBase
    {
        [LabelText("Enabled")]
        public bool Enabled = true;

        [LabelText("Origin")]
        public DynamicValue<Vector3> Origin = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [LabelText("Tile Size")]
        public Vector2 TileSize = new(8f, 8f);

        [LabelText("Scroll Speed")]
        public Vector2 ScrollSpeed = Vector2.zero;

        [LabelText("Extra Margin Tiles")]
        public Vector2Int ExtraMarginTiles = Vector2Int.zero;

        [LabelText("Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [LabelText("On Spawned")]
        [CommandListFunctionName("ScrollChannel.OnSpawned")]
        public CommandListData OnSpawnedCommands = new();

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [LabelText("Spawner Kind")]
        [EnumToggleButtons]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [LabelText("Spawner Tag")]
        public string SpawnerTag = string.Empty;

        [LabelText("Allow Pooling")]
        public bool AllowPooling = true;

        [LabelText("Transform Parent")]
        public Transform? TransformParent;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (TileSize.x <= 0f)
                TileSize.x = 1f;
            if (TileSize.y <= 0f)
                TileSize.y = 1f;
            if (ExtraMarginTiles.x < 0)
                ExtraMarginTiles.x = 0;
            if (ExtraMarginTiles.y < 0)
                ExtraMarginTiles.y = 0;
        }
    }

    public interface IScrollChannelHubService : IChannelHubService
    {
    }
}
