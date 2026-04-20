#nullable enable
using System.Collections.Generic;
using Game;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal sealed class ScrollTileHandle
    {
        public ScrollTileCoord Coord;
        public Transform? Transform;
        public RectTransform? RectTransform;
        public IScopeNode? ScopeNode;
        public RuntimeLifetimeScope? RuntimeScope;
        public BaseLifetimeScope? BaseScope;
        public GameObject? Root;
        public IRuntimeResolver? Resolver;

        public ScrollTileHandle(
            ScrollTileCoord coord,
            Transform? transform,
            RectTransform? rectTransform,
            IScopeNode? scopeNode,
            RuntimeLifetimeScope? runtimeScope,
            BaseLifetimeScope? baseScope,
            GameObject? root,
            IRuntimeResolver? resolver)
        {
            Coord = coord;
            Transform = transform;
            RectTransform = rectTransform;
            ScopeNode = scopeNode;
            RuntimeScope = runtimeScope;
            BaseScope = baseScope;
            Root = root;
            Resolver = resolver;
        }
    }

    internal sealed class ScrollChannelRuntimePlayer
    {
        public readonly ScrollChannelDefinition Definition;
        public readonly Dictionary<ScrollTileCoord, ScrollTileHandle> Tiles = new();
        public readonly HashSet<ScrollTileCoord> PendingSpawn = new();
        public readonly HashSet<ScrollTileCoord> PendingRemove = new();
        public readonly HashSet<ScrollTileCoord> RequiredTiles = new();
        public readonly List<ScrollTileCoord> RemoveBuffer = new();

        public Vector2 Offset;
        public bool LoggedMissingTemplate;
        public bool LoggedMissingSpawner;

        public ScrollChannelRuntimePlayer(ScrollChannelDefinition definition)
        {
            Definition = definition;
            Offset = Vector2.zero;
            LoggedMissingTemplate = false;
            LoggedMissingSpawner = false;
        }
    }
}
