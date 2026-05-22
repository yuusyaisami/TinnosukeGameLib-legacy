#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using VContainer;

namespace Game.Spawn
{
    /// <summary>
    /// Kinds of spawners used by patterns and services.
    /// Runtime kinds will later map to KernelScopeHost pools.
    /// </summary>
    public enum SpawnerKind { UI, UIElement, Entity, Field, RuntimeEntity, RuntimeUIElement }

    public interface ISpawnerService
    {
        SpawnerKind Kind { get; }
        string Tag { get; }
    }

    public interface IAsyncSpawnerService : ISpawnerService
    {
        UniTask<IRuntimeResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default);
        UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default)
            where T : BaseRuntimeTemplateSO;
    }

    public readonly struct SpawnerKey : IEquatable<SpawnerKey>
    {
        public SpawnerKind Kind { get; }
        public string Tag { get; }

        public SpawnerKey(SpawnerKind kind, string tag = "")
        {
            Kind = kind;
            Tag = NormalizeTag(tag);
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "";
            if (string.Equals(tag, "default", StringComparison.OrdinalIgnoreCase))
                return "";
            return tag;
        }

        public bool Equals(SpawnerKey o) => Kind == o.Kind && Tag == o.Tag;
        public override bool Equals(object? obj) => obj is SpawnerKey o && Equals(o);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Tag?.GetHashCode() ?? 0);
        public override string ToString() => string.IsNullOrEmpty(Tag) ? Kind.ToString() : $"{Kind}:{Tag}";
    }

    /// <summary>
    /// Scene-level spawner registry. Concrete spawner services register themselves here.
    /// </summary>
    public interface ISceneSpawnerRegistry : IDisposable
    {
        void Register(ISpawnerService s);
        T Get<T>(SpawnerKind kind, string tag = "") where T : ISpawnerService;
        T? TryGet<T>(SpawnerKind kind, string tag = "") where T : class, ISpawnerService;
        T? TryGetExact<T>(SpawnerKind kind, string tag = "") where T : class, ISpawnerService;
        bool Contains(SpawnerKind kind, string tag = "");
        bool ContainsExact(SpawnerKind kind, string tag = "");
        int Count { get; }
    }

    public sealed class SceneSpawnerRegistry : ISceneSpawnerRegistry
    {
        readonly Dictionary<SpawnerKey, ISpawnerService> _spawners = new(8);

        public void Register(ISpawnerService s)
        {
            var key = new SpawnerKey(s.Kind, s.Tag);
            if (_spawners.ContainsKey(key))
                throw new InvalidOperationException($"Spawner already registered: {key}");

            _spawners[key] = s;
        }

        public T Get<T>(SpawnerKind kind, string tag = "") where T : ISpawnerService
        {
            var key = new SpawnerKey(kind, tag);
            if (!_spawners.TryGetValue(key, out var s))
            {
                // If a specific tag was requested but not found, try falling back to the empty tag for convenience.
                if (!string.IsNullOrEmpty(key.Tag) && _spawners.TryGetValue(new SpawnerKey(kind, ""), out var fallback))
                {
                    try { UnityEngine.Debug.LogWarning($"[SceneSpawnerRegistry] Spawner not found: {key}. Falling back to {new SpawnerKey(kind, "")}."); } catch { }
                    s = fallback;
                }
                else
                {
                    try
                    {
                        UnityEngine.Debug.LogError($"[SceneSpawnerRegistry] Spawner not found: {key}. Registered keys: {string.Join(", ", _spawners.Keys)}");
                    }
                    catch { }
                    throw new KeyNotFoundException($"Spawner not found: {key}");
                }
            }

            return s is T t ? t : throw new InvalidCastException($"Spawner {key} is not {typeof(T).Name}");
        }

        public T? TryGet<T>(SpawnerKind kind, string tag = "") where T : class, ISpawnerService
        {
            var key = new SpawnerKey(kind, tag);
            if (_spawners.TryGetValue(key, out var s) && s is T t)
                return t;

            // Match Get() behavior: when a tag is explicitly specified but not found, fall back to the empty tag.
            if (!string.IsNullOrEmpty(key.Tag) && _spawners.TryGetValue(new SpawnerKey(kind, ""), out var fallback) && fallback is T ft)
                return ft;

            return null;
        }

        public T? TryGetExact<T>(SpawnerKind kind, string tag = "") where T : class, ISpawnerService
        {
            var key = new SpawnerKey(kind, tag);
            if (_spawners.TryGetValue(key, out var s) && s is T t)
                return t;
            return null;
        }

        public bool Contains(SpawnerKind kind, string tag = "")
        {
            var key = new SpawnerKey(kind, tag);
            if (_spawners.ContainsKey(key))
                return true;
            return !string.IsNullOrEmpty(key.Tag) && _spawners.ContainsKey(new SpawnerKey(kind, ""));
        }

        public bool ContainsExact(SpawnerKind kind, string tag = "")
        {
            var key = new SpawnerKey(kind, tag);
            return _spawners.ContainsKey(key);
        }
        public int Count => _spawners.Count;
        public void Dispose() => _spawners.Clear();
    }
}



