#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.Chunk.Biome;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk
{
    [Serializable]
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Y;

        public ChunkCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }

    public enum ChunkSpawnPivot
    {
        ChunkCenter = 0,
        ChunkOriginCell = 1,
    }

    [Serializable]
    public struct ChunkSettings
    {
        [SerializeField, LabelText("Chunk Size (Cells)")]
        Vector2Int chunkSizeCells;
        [SerializeField, LabelText("Remove Distance"), MinValue(0f)]
        float removeDistance;
        [SerializeField, LabelText("View Margin (Chunks)"), MinValue(0)]
        int viewMarginChunks;
        [SerializeField, LabelText("Update Interval (Sec)"), MinValue(0f)]
        float updateIntervalSeconds;
        [SerializeField, LabelText("Max Chunks/Frame"), MinValue(1)]
        int maxChunksPerFrame;

        public Vector2Int ChunkSizeCells => chunkSizeCells;
        public float RemoveDistance => removeDistance;
        public int ViewMarginChunks => viewMarginChunks;
        public float UpdateIntervalSeconds => updateIntervalSeconds;
        public int MaxChunksPerFrame => maxChunksPerFrame;

        public void EnsureDefaults()
        {
            if (chunkSizeCells.x <= 0) chunkSizeCells.x = 1;
            if (chunkSizeCells.y <= 0) chunkSizeCells.y = 1;
            if (viewMarginChunks < 0) viewMarginChunks = 0;
            if (removeDistance < 0f) removeDistance = 0f;
            if (updateIntervalSeconds < 0f) updateIntervalSeconds = 0f;
            if (maxChunksPerFrame <= 0) maxChunksPerFrame = 1;
        }
    }

    [Serializable]
    public struct ChunkOriginSettings
    {
        [SerializeField, LabelText("World Origin Cell")]
        Vector2Int worldOriginCell;
        [SerializeField, LabelText("World Origin Position")]
        Vector2 worldOriginPosition;
        [SerializeField, LabelText("Cell Size")]
        Vector2 cellSize;
        [SerializeField, LabelText("Center Aligned")]
        bool centerAligned;
        [SerializeField, LabelText("Chunk Zero Cell")]
        Vector2Int chunkZeroCell;

        public Vector2Int WorldOriginCell => worldOriginCell;
        public Vector2 WorldOriginPosition => worldOriginPosition;
        public Vector2 CellSize => cellSize;
        public bool CenterAligned => centerAligned;
        public Vector2Int ChunkZeroCell => chunkZeroCell;

        public void EnsureDefaults()
        {
            if (cellSize.x <= 0f) cellSize.x = 1f;
            if (cellSize.y <= 0f) cellSize.y = 1f;
        }
    }

    public readonly struct ChunkContext
    {
        public readonly ChunkCoord Coord;
        public readonly RectInt CellRect;
        public readonly Bounds WorldBounds;
        public readonly Vector2 WorldCenter;
        public readonly ChunkSettings Settings;
        public readonly ChunkOriginSettings OriginSettings;

        public ChunkContext(
            ChunkCoord coord,
            RectInt cellRect,
            Bounds worldBounds,
            Vector2 worldCenter,
            ChunkSettings settings,
            ChunkOriginSettings originSettings)
        {
            Coord = coord;
            CellRect = cellRect;
            WorldBounds = worldBounds;
            WorldCenter = worldCenter;
            Settings = settings;
            OriginSettings = originSettings;
        }
    }

    public sealed class ChunkHandle
    {
        public readonly ChunkCoord Coord;
        public readonly Bounds WorldBounds;
        public readonly IScopeNode? ScopeNode;
        public readonly RuntimeLifetimeScope? RuntimeScope;
        public readonly BaseLifetimeScope? BaseScope;
        public readonly UnityEngine.GameObject? Root;
        public readonly IRuntimeResolver? Resolver;

        public ChunkHandle(
            ChunkCoord coord,
            Bounds worldBounds,
            IScopeNode? scopeNode,
            RuntimeLifetimeScope? runtimeScope,
            BaseLifetimeScope? baseScope,
            UnityEngine.GameObject? root,
            IRuntimeResolver? resolver)
        {
            Coord = coord;
            WorldBounds = worldBounds;
            ScopeNode = scopeNode;
            RuntimeScope = runtimeScope;
            BaseScope = baseScope;
            Root = root;
            Resolver = resolver;
        }
    }

    [Serializable]
    public sealed class ConditionalCommand
    {
        [SerializeField]
        DynamicValue<bool> condition;

        [SerializeField]
        CommandListData commands = new();

        public DynamicValue<bool> Condition => condition;
        public CommandListData Commands => commands;
    }

    [Serializable]
    public sealed class ChunkPlan
    {
        [SerializeField] int seed;
        [SerializeField] string biomeId = string.Empty;
        [SerializeField] ChunkVarBox varBox = new();
        [SerializeField] CommandListData commonCommands = new();
        [SerializeField] List<ConditionalCommand> conditionalCommands = new();

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public string BiomeId
        {
            get => biomeId;
            set => biomeId = value ?? string.Empty;
        }

        public ChunkVarBox VarBox => varBox;
        public CommandListData CommonCommands => commonCommands;
        public List<ConditionalCommand> ConditionalCommands => conditionalCommands;

        public void SetVarBox(ChunkVarBox? box)
        {
            varBox = box ?? new ChunkVarBox();
        }

        public void ApplyProfile(ChunkProfileSO? profile)
        {
            if (profile == null)
                return;

            if (profile.CommonCommands != null)
                CopyCommands(profile.CommonCommands, commonCommands);

            if (profile.ConditionalCommands != null)
                conditionalCommands.AddRange(profile.ConditionalCommands);
        }

        static void CopyCommands(CommandListData? src, CommandListData dest)
        {
            if (src == null || dest == null)
                return;

            var list = src.Commands;
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                var cmd = list[i];
                if (cmd != null)
                    dest.Add(cmd);
            }
        }
    }
}
