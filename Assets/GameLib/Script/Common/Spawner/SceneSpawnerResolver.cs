#nullable enable

namespace Game.Spawn
{
    public readonly struct SpawnerResolveResult<T> where T : class, ISpawnerService
    {
        public readonly T? Spawner;
        public readonly SpawnerKind Kind;
        public readonly string Tag;
        public readonly bool UsedKindFallback;

        public bool HasValue => Spawner != null;

        public SpawnerResolveResult(T? spawner, SpawnerKind kind, string tag, bool usedKindFallback)
        {
            Spawner = spawner;
            Kind = kind;
            Tag = tag;
            UsedKindFallback = usedKindFallback;
        }

        public static SpawnerResolveResult<T> Empty => default;
    }

    public static class SceneSpawnerResolver
    {
        public static SpawnerResolveResult<IAsyncSpawnerService> TryResolveAsyncSpawner(
            ISceneSpawnerRegistry registry,
            SpawnerKind kind,
            string tag,
            bool allowTagFallback,
            bool allowRuntimeUiFallback)
        {
            return TryResolve<IAsyncSpawnerService>(registry, kind, tag, allowTagFallback, allowRuntimeUiFallback);
        }

        public static SpawnerResolveResult<ISpawnerService> TryResolveSpawner(
            ISceneSpawnerRegistry registry,
            SpawnerKind kind,
            string tag,
            bool allowTagFallback,
            bool allowRuntimeUiFallback)
        {
            return TryResolve<ISpawnerService>(registry, kind, tag, allowTagFallback, allowRuntimeUiFallback);
        }

        static SpawnerResolveResult<T> TryResolve<T>(
            ISceneSpawnerRegistry registry,
            SpawnerKind kind,
            string tag,
            bool allowTagFallback,
            bool allowRuntimeUiFallback)
            where T : class, ISpawnerService
        {
            if (registry == null)
                return SpawnerResolveResult<T>.Empty;

            T? spawner = allowTagFallback
                ? registry.TryGet<T>(kind, tag)
                : registry.TryGetExact<T>(kind, tag);

            if (spawner != null)
                return new SpawnerResolveResult<T>(spawner, kind, tag, usedKindFallback: false);

            if (allowRuntimeUiFallback && kind == SpawnerKind.RuntimeUIElement)
            {
                spawner = allowTagFallback
                    ? registry.TryGet<T>(SpawnerKind.RuntimeEntity, tag)
                    : registry.TryGetExact<T>(SpawnerKind.RuntimeEntity, tag);

                if (spawner != null)
                    return new SpawnerResolveResult<T>(spawner, SpawnerKind.RuntimeEntity, tag, usedKindFallback: true);
            }

            return SpawnerResolveResult<T>.Empty;
        }
    }
}
