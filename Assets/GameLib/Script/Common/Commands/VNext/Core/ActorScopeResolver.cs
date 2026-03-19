#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using System.Linq;

namespace Game.Commands.VNext
{
    static class ActorScopeResolver
    {
        const int ResolveRetryFrames = 5;
        static IBaseLifetimeScopeRegistry? s_fallbackRegistry;

        public static async UniTask<(IScopeNode? scope, string error)> ResolveAsync(
            ActorSource source,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (source.Kind == ActorSourceKind.ByIdentity)
                return await ResolveByIdentityWithRetryAsync(source.Identity, ctx, ct);

            return ResolveWithoutIdentity(source, ctx);
        }

        static (IScopeNode? scope, string error) ResolveWithoutIdentity(ActorSource source, CommandContext ctx)
        {
            var resolved = ActorSourceFastResolver.Resolve(ctx.Scope, source, ctx.CommandRootScope);
            if (resolved != null)
                return (resolved, string.Empty);

            return source.Kind switch
            {
                ActorSourceKind.Current => (null, "Current scope is not available."),
                ActorSourceKind.GameLogicRoot => (null, "GameLogicRoot scope was not found."),
                ActorSourceKind.Player => (null, "Player scope was not found."),
                ActorSourceKind.CommandRootActor => (null, "Command root actor scope was not found."),
                ActorSourceKind.Global => (null, "Global scope was not found."),
                ActorSourceKind.Shared => (null, $"Shared actor tag was not found. tag='{source.SharedTag}'"),
                ActorSourceKind.FromUnityObject => (null, "UnityObject does not resolve to a scope."),
                _ => (null, "Unknown actor source."),
            };
        }

        static async UniTask<(IScopeNode? scope, string error)> ResolveByIdentityWithRetryAsync(
            CommandTargetIdentityFilter identity,
            CommandContext ctx,
            CancellationToken ct)
        {
            var origin = ctx.Scope;
            if (!TryResolveScopeRegistry(origin, out var registry) || registry == null)
                return (null, "Scope registry is not available.");

            for (int attempt = 0; attempt <= ResolveRetryFrames; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var scope = registry.Resolve(identity, origin);
                if (scope != null)
                    return (scope, string.Empty);

                if (attempt == ResolveRetryFrames)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // Provide richer error information to aid debugging: include the identity filter and origin scope path
            string DescribeFilter(CommandTargetIdentityFilter f)
            {
                try
                {
                    return $"kind={f.kind}, id='{f.id}', category='{f.category}', requireActive={f.requireActive}, searchScope={f.searchScope}";
                }
                catch
                {
                    return "(failed to describe filter)";
                }
            }

            string DescribeOrigin(IScopeNode? o)
            {
                try
                {
                    if (o == null) return "null";
                    var path = o.GetPathFromRoot();
                    if (path == null || path.Count == 0) return $"{o.Kind}:{o.Identity?.Id ?? "(no id)"}";
                    // represent as Root/.../Origin
                    var ids = string.Join("/", path.Select(n => string.IsNullOrEmpty(n.Identity?.Id) ? n.Kind.ToString() : n.Identity!.Id));
                    return ids;
                }
                catch
                {
                    return "(failed to describe origin)";
                }
            }

            var filterDesc = DescribeFilter(identity);
            var originDesc = DescribeOrigin(origin);
            var snapshotDesc = DescribeRegistrySnapshot(registry);
            return (null, $"Target scope was not found. Filter=[{filterDesc}] Origin=[{originDesc}] Registry=[{snapshotDesc}]");
        }

        static bool TryResolveFromUnityObject(UnityEngine.Object? obj, out IScopeNode scope)
        {
            scope = null!;
            if (obj == null)
                return false;

            var raw = obj as object;
            if (raw is IScopeNode node)
            {
                scope = node;
                return true;
            }

            if (obj is Component comp)
            {
                var found = FindScopeNode(comp.gameObject);
                if (found != null)
                {
                    scope = found;
                    return true;
                }
                return false;
            }

            if (obj is GameObject go)
            {
                var found = FindScopeNode(go);
                if (found != null)
                {
                    scope = found;
                    return true;
                }
                return false;
            }

            return false;
        }

        static IScopeNode? FindScopeNode(GameObject go)
        {
            if (go == null)
                return null;

            // NOTE:
            // FromUnityObject の実運用では子オブジェクト参照が多く、
            // 親探索順によっては RuntimeScope ではなく BaseScope を拾ってしまう。
            // それを避けるため、近い Transform から Runtime -> Base の優先順で判定する。
            for (var t = go.transform; t != null; t = t.parent)
            {
                var runtimeScope = t.GetComponent<RuntimeLifetimeScope>();
                if (runtimeScope != null)
                    return runtimeScope;

                var baseScope = t.GetComponent<BaseLifetimeScope>();
                if (baseScope != null)
                    return baseScope;

                var sameLevel = t.GetComponents<Component>();
                for (var i = 0; i < sameLevel.Length; i++)
                {
                    if (sameLevel[i] is IScopeNode node)
                        return node;
                }
            }

            return null;
        }

        static bool TryResolveScopeRegistry(IScopeNode? origin, out IBaseLifetimeScopeRegistry? registry)
        {
            if (s_fallbackRegistry != null)
            {
                registry = s_fallbackRegistry;
                return true;
            }

            var current = origin;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    s_fallbackRegistry = resolved;
                    registry = resolved;
                    return true;
                }
                current = current.Parent;
            }

            var projects = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projects != null && projects.Length > 0)
            {
                var projectResolver = projects[0] != null ? projects[0].Resolver : null;
                if (projectResolver != null &&
                    projectResolver.TryResolve<IBaseLifetimeScopeRegistry>(out var projectRegistry) &&
                    projectRegistry != null)
                {
                    s_fallbackRegistry = projectRegistry;
                    registry = projectRegistry;
                    return true;
                }
            }

            registry = null;
            return false;
        }

        static string DescribeRegistrySnapshot(IBaseLifetimeScopeRegistry registry)
        {
            var entries = registry.ResolveAll(new CommandTargetIdentityFilter());
            if (entries == null || entries.Count == 0)
                return "empty";

            var sample = entries
                .Select(scope =>
                {
                    if (scope == null)
                        return "<null>";

                    var id = scope.Identity?.Id;
                    if (!string.IsNullOrEmpty(id))
                        return id;

                    var kind = scope.Kind;
                    var cat = scope.Identity?.Category;
                    return !string.IsNullOrEmpty(cat)
                        ? $"{kind}:(no id) cat='{cat}'"
                        : $"{kind}:(no id)";
                })
                .Where(s => !string.IsNullOrEmpty(s))
                .Take(5)
                .ToArray();
            return $"count={entries.Count}, sample=[{string.Join(", ", sample)}]";
        }
    }
}
