#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Background
{
    [Serializable]
    public readonly struct BackgroundTileCoord : IEquatable<BackgroundTileCoord>
    {
        public readonly int X;
        public readonly int Y;

        public BackgroundTileCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BackgroundTileCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is BackgroundTileCoord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(BackgroundTileCoord a, BackgroundTileCoord b) => a.Equals(b);
        public static bool operator !=(BackgroundTileCoord a, BackgroundTileCoord b) => !a.Equals(b);
    }

    [Serializable]
    public sealed class BackgroundConditionalCommand
    {
        [SerializeField]
        DynamicValue<bool> condition;

        [SerializeField]
        CommandListData commands = new();

        public DynamicValue<bool> Condition => condition;
        public CommandListData Commands => commands;
    }

    [Serializable]
    public sealed class BackgroundLayerDefinition
    {
        [SerializeField] string name = "Layer";
        [SerializeField, InlineProperty, HideLabel] DynamicValue<BaseRuntimeTemplatePreset> runtimeTemplatePreset;
        [SerializeField] Vector2 tileSize = new Vector2(32f, 32f);
        [SerializeField] Vector2 tilePadding = Vector2.zero;
        [SerializeField] Vector2 parallax = Vector2.one;
        [SerializeField] Vector2 scrollSpeed = Vector2.zero;
        [SerializeField] Vector2 initialOffset = Vector2.zero;
        [SerializeField] int sortingOrder = 0;
        [SerializeField] float zOffset = 0f;
        [SerializeField] SpawnerKind spawnerKind = SpawnerKind.RuntimeEntity;
        [SerializeField] string spawnerTag = string.Empty;
        [SerializeField] Transform? parentOverride;
        [SerializeField] BackgroundSpawnPivot spawnPivot = BackgroundSpawnPivot.TileCenter;
        [SerializeField] Vector2Int extraMarginTiles = Vector2Int.zero;
        [CommandListFunctionName("Background.Spawn")]
        [SerializeField] CommandListData spawnCommands = new();
        [SerializeField] List<BackgroundConditionalCommand> spawnConditionalCommands = new();

        public string Name => string.IsNullOrEmpty(name) ? "Layer" : name;
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset => runtimeTemplatePreset;
        public Vector2 TileSize => tileSize;
        public Vector2 TilePadding => tilePadding;
        public Vector2 Parallax => parallax;
        public Vector2 ScrollSpeed => scrollSpeed;
        public Vector2 InitialOffset => initialOffset;
        public int SortingOrder => sortingOrder;
        public float ZOffset => zOffset;
        public SpawnerKind SpawnerKind => spawnerKind;
        public string SpawnerTag => spawnerTag ?? string.Empty;
        public Transform? ParentOverride => parentOverride;
        public BackgroundSpawnPivot SpawnPivot => spawnPivot;
        public Vector2Int ExtraMarginTiles => extraMarginTiles;
        public CommandListData SpawnCommands => spawnCommands;
        public IReadOnlyList<BackgroundConditionalCommand> SpawnConditionalCommands => spawnConditionalCommands;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? resolvedRuntimeTemplate)
        {
            resolvedRuntimeTemplate = null;
            if (!runtimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            resolvedRuntimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return resolvedRuntimeTemplate != null;
        }

        public void EnsureDefaults(BackgroundSpace space)
        {
            if (tileSize.x <= 0f)
                tileSize.x = 1f;
            if (tileSize.y <= 0f)
                tileSize.y = 1f;
            if (extraMarginTiles.x < 0)
                extraMarginTiles.x = 0;
            if (extraMarginTiles.y < 0)
                extraMarginTiles.y = 0;
            spawnCommands ??= new CommandListData();
            spawnConditionalCommands ??= new List<BackgroundConditionalCommand>();
        }
    }

    public sealed class BackgroundElementHandle
    {
        public BackgroundTileCoord Coord { get; }
        public Transform? Transform { get; }
        public RectTransform? RectTransform { get; }
        public IScopeNode? ScopeNode { get; }
        public RuntimeLifetimeScope? RuntimeScope { get; }
        public BaseLifetimeScope? BaseScope { get; }
        public GameObject? Root { get; }
        public IRuntimeResolver? Resolver { get; }
        public IBackgroundElementAdapter? Adapter { get; }

        public BackgroundElementHandle(
            BackgroundTileCoord coord,
            Transform? transform,
            RectTransform? rectTransform,
            IScopeNode? scopeNode,
            RuntimeLifetimeScope? runtimeScope,
            BaseLifetimeScope? baseScope,
            GameObject? root,
            IRuntimeResolver? resolver,
            IBackgroundElementAdapter? adapter)
        {
            Coord = coord;
            Transform = transform;
            RectTransform = rectTransform;
            ScopeNode = scopeNode;
            RuntimeScope = runtimeScope;
            BaseScope = baseScope;
            Root = root;
            Resolver = resolver;
            Adapter = adapter;
        }
    }

    public readonly struct BackgroundLayerState
    {
        public int Index { get; }
        public string Name { get; }
        public Vector2 Offset { get; }
        public Vector2 ScrollSpeed { get; }

        public BackgroundLayerState(int index, string name, Vector2 offset, Vector2 scrollSpeed)
        {
            Index = index;
            Name = name;
            Offset = offset;
            ScrollSpeed = scrollSpeed;
        }
    }

    public readonly struct BackgroundElementContext
    {
        public BackgroundSpace Space { get; }
        public BackgroundMode Mode { get; }
        public int LayerIndex { get; }
        public string LayerName { get; }
        public BackgroundTileCoord TileCoord { get; }
        public Rect TileRect { get; }
        public Vector2 TileSize { get; }
        public Vector2 TilePadding { get; }
        public Vector2 Parallax { get; }
        public Vector2 LayerOffset { get; }
        public Rect ViewRect { get; }
        public Vector2 ViewCenter { get; }
        public Vector3 WorldPosition { get; }
        public int SortingOrder { get; }
        public float ZOffset { get; }
        public float Time { get; }
        public float DeltaTime { get; }

        public BackgroundElementContext(
            BackgroundSpace space,
            BackgroundMode mode,
            int layerIndex,
            string layerName,
            BackgroundTileCoord tileCoord,
            Rect tileRect,
            Vector2 tileSize,
            Vector2 tilePadding,
            Vector2 parallax,
            Vector2 layerOffset,
            Rect viewRect,
            Vector2 viewCenter,
            Vector3 worldPosition,
            int sortingOrder,
            float zOffset,
            float time,
            float deltaTime)
        {
            Space = space;
            Mode = mode;
            LayerIndex = layerIndex;
            LayerName = layerName;
            TileCoord = tileCoord;
            TileRect = tileRect;
            TileSize = tileSize;
            TilePadding = tilePadding;
            Parallax = parallax;
            LayerOffset = layerOffset;
            ViewRect = viewRect;
            ViewCenter = viewCenter;
            WorldPosition = worldPosition;
            SortingOrder = sortingOrder;
            ZOffset = zOffset;
            Time = time;
            DeltaTime = deltaTime;
        }
    }
}
