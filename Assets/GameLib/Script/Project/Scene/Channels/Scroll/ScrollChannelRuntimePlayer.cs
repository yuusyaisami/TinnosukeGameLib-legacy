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
        public SpawnedLifetimeHandle Lifetime;

        public IScopeNode? ScopeNode => Lifetime.ScopeNode;
        public GameObject? Root => Lifetime.Root;
        public IRuntimeResolver? Resolver => Lifetime.Resolver;
        public bool UsesRuntimeLifetimeScope => Lifetime.UsesRuntimeLifetimeScope;
        public bool UsesBaseLifetimeScope => Lifetime.UsesBaseLifetimeScope;

        public ScrollTileHandle(
            ScrollTileCoord coord,
            Transform? transform,
            RectTransform? rectTransform,
            SpawnedLifetimeHandle lifetime)
        {
            Coord = coord;
            Transform = transform;
            RectTransform = rectTransform;
            Lifetime = lifetime;
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
