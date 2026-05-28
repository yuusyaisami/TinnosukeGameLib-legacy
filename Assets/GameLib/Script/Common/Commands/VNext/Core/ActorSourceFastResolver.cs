#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Commands;
using Game.Common;
using Game.Targeting;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public struct ActorSourceResolveCache
    {
        public ActorSource Source;
        public IScopeNode? Origin;
        public IScopeNode? CachedScope;
        public bool HasCache;
    }

    public static class ActorSourceFastResolver
    {
        static IBaseLifetimeScopeRegistry? s_fallbackRegistry;

        public static IScopeNode? ResolveCached(IDynamicContext context, in ActorSource source, ref ActorSourceResolveCache cache, IScopeNode? originOverride = null)
        {
            if (context == null)
                return null;

            var runtimeContext = GetRuntimeContext(context);
            var origin = originOverride ?? context.Scope;
            if (runtimeContext != null)
                return ResolveCached(runtimeContext, source, ref cache, origin);

            return ResolveCached(origin, source, ref cache, context.CommandRootScope);
        }

        public static IScopeNode? ResolveCached(CommandContext context, in ActorSource source, ref ActorSourceResolveCache cache, IScopeNode? originOverride = null)
        {
            if (context == null)
                return null;

            var origin = originOverride ?? context.Actor ?? context.Scope;
            if (origin == null)
                return null;

            if (cache.HasCache &&
                ReferenceEquals(cache.Origin, origin) &&
                SourceEquals(cache.Source, source))
            {
                if (cache.CachedScope != null && IsCacheValid(origin, source, cache.CachedScope))
                    return cache.CachedScope;
            }

            var resolved = Resolve(context, source, origin);
            if (ShouldCache(source))
            {
                cache.Source = source;
                cache.Origin = origin;
                cache.CachedScope = resolved;
                cache.HasCache = true;
            }
            else
            {
                cache.HasCache = false;
                cache.CachedScope = null;
                cache.Origin = null;
            }

            return resolved;
        }

        public static IScopeNode? ResolveCached(IScopeNode? origin, in ActorSource source, ref ActorSourceResolveCache cache, IScopeNode? commandRootScope = null)
        {
            if (origin == null)
                return null;

            if (cache.HasCache &&
                ReferenceEquals(cache.Origin, origin) &&
                SourceEquals(cache.Source, source))
            {
                if (cache.CachedScope != null && IsCacheValid(origin, source, cache.CachedScope))
                    return cache.CachedScope;
            }

            var resolved = Resolve(origin, source, commandRootScope);
            if (ShouldCache(source))
            {
                cache.Source = source;
                cache.Origin = origin;
                cache.CachedScope = resolved;
                cache.HasCache = true;
            }
            else
            {
                cache.HasCache = false;
                cache.CachedScope = null;
                cache.Origin = null;
            }

            return resolved;
        }

        public static IScopeNode? Resolve(IDynamicContext context, in ActorSource source, IScopeNode? originOverride = null)
        {
            if (context == null)
                return null;

            var runtimeContext = GetRuntimeContext(context);
            if (runtimeContext != null)
                return Resolve(runtimeContext, source, originOverride);

            return Resolve(originOverride ?? context.Scope, source, context.CommandRootScope);
        }

        public static IScopeNode? Resolve(CommandContext context, in ActorSource source, IScopeNode? originOverride = null)
        {
            if (context == null)
                return null;

            if (source.Kind == ActorSourceKind.ContextSlot)
                return context.GetScope(source.ContextSlot);

            var origin = originOverride ?? context.Actor ?? context.Scope;
            return Resolve(origin, source, context.CommandRootScope);
        }

        public static IScopeNode? Resolve(IScopeNode? origin, in ActorSource source, IScopeNode? commandRootScope = null)
        {
            if (origin == null)
                return null;

            switch (source.Kind)
            {
                case ActorSourceKind.Current:
                    return origin;
                case ActorSourceKind.GameLogicRoot:
                    return ResolveGameLogicRoot(origin);
                case ActorSourceKind.Player:
                    return ResolvePlayerScope(origin);
                case ActorSourceKind.CommandRootActor:
                    return commandRootScope;
                case ActorSourceKind.Global:
                    return ResolveNearestGlobalScope(origin);
                case ActorSourceKind.Shared:
                    return ResolveShared(origin, source.Shared, commandRootScope);
                case ActorSourceKind.ContextSlot:
                    return null;
                case ActorSourceKind.ByIdentity:
                    return ResolveByIdentity(origin, source.Identity);
                case ActorSourceKind.FromUnityObject:
                    return TryResolveFromUnityObject(source.UnityObject, out var scope) ? scope : null;
                case ActorSourceKind.TargetChannel:
                    return ResolveFromTargetChannel(origin, source, commandRootScope);
                default:
                    return null;
            }
        }

        static CommandContext? GetRuntimeContext(IDynamicContext context)
        {
            if (context is CommandContext commandContext)
                return commandContext;

            if (context is CommandResolveContext resolveContext)
                return resolveContext.RuntimeContext;

            return null;
        }

        static bool ShouldCache(in ActorSource source)
        {
            if (source.Kind == ActorSourceKind.ByIdentity)
                return source.Identity.searchScope == CommandTargetSearchScope.All;
            return source.Kind == ActorSourceKind.FromUnityObject;
        }

        static IScopeNode? ResolveShared(IScopeNode origin, SharedActorSourceRef? shared, IScopeNode? commandRootScope)
        {
            if (shared == null)
                return null;

            var tag = shared.SharedTag;
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            var hubOwner = Resolve(origin, shared.SharedHubActorSource, commandRootScope) ?? origin;
            var current = hubOwner;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null &&
                    resolver.TryResolve<ISharedLTSChannelHub>(out var hub) &&
                    hub != null &&
                    hub.TryGet(tag, out var scope) &&
                    scope != null)
                {
                    return scope;
                }

                current = current.Parent;
            }

            return null;
        }

        static bool IsCacheValid(IScopeNode origin, in ActorSource source, IScopeNode cachedScope)
        {
            if (cachedScope == null)
                return false;

            if (source.Kind == ActorSourceKind.ByIdentity)
            {
                var identity = cachedScope.Identity;
                if (identity == null)
                    return false;

                var filter = source.Identity;
                if (filter.requireActive && !identity.IsActive)
                    return false;
                if (filter.kind != LifetimeScopeKind.None && identity.Kind != filter.kind)
                    return false;
                if (!string.IsNullOrEmpty(filter.id) && identity.Id != filter.id)
                    return false;
                if (!string.IsNullOrEmpty(filter.category) && identity.Category != filter.category)
                    return false;
                if (!IsInSearchScope(origin, cachedScope, filter.searchScope))
                    return false;
            }

            return true;
        }

        static IScopeNode? ResolveByIdentity(IScopeNode origin, CommandTargetIdentityFilter filter)
        {
            if (TryResolveScopeRegistry(origin, out var registry) && registry != null)
            {
                var resolved = registry.Resolve(filter, origin);
                if (resolved != null)
                    return resolved;
            }

            // Registry can be unavailable during early scope build.
            // For simple kind-only lookups, fall back to hierarchy search.
            return ResolveByIdentityHierarchyFallback(origin, filter);
        }

        static IScopeNode? ResolveByIdentityHierarchyFallback(IScopeNode origin, CommandTargetIdentityFilter filter)
        {
            switch (filter.searchScope)
            {
                case CommandTargetSearchScope.AncestorsOnly:
                    for (var node = origin.Parent; node != null; node = node.Parent)
                    {
                        if (MatchesIdentity(node, filter))
                            return node;
                    }
                    return null;

                case CommandTargetSearchScope.DescendantsOnly:
                    foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(origin, includeSelf: false))
                    {
                        if (MatchesIdentity(node, filter))
                            return node;
                    }
                    return ResolveByIdentityTransformFallback(origin, filter);

                case CommandTargetSearchScope.All:
                default:
                    {
                        var root = FindRoot(origin);
                        if (root == null)
                            return null;

                        foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(root, includeSelf: true))
                        {
                            if (ReferenceEquals(node, origin))
                                continue;

                            if (MatchesIdentity(node, filter))
                                return node;
                        }

                        return ResolveByIdentityTransformFallback(origin, filter);
                    }
            }
        }

        static IScopeNode? ResolveByIdentityTransformFallback(IScopeNode origin, in CommandTargetIdentityFilter filter)
        {
            var originTransform = origin.Identity?.SelfTransform;
            if (originTransform == null)
                return null;

            switch (filter.searchScope)
            {
                case CommandTargetSearchScope.AncestorsOnly:
                    for (var current = originTransform.parent; current != null; current = current.parent)
                    {
                        if (TryResolveMatchingScope(current, filter, out var scope))
                            return scope;
                    }
                    return null;

                case CommandTargetSearchScope.DescendantsOnly:
                    return FindMatchingScopeInSubtree(originTransform, filter, includeSelf: false);

                case CommandTargetSearchScope.All:
                default:
                    {
                        var root = originTransform;
                        while (root.parent != null)
                            root = root.parent;

                        return FindMatchingScopeInSubtree(root, filter, includeSelf: true);
                    }
            }
        }

        static IScopeNode? FindMatchingScopeInSubtree(Transform root, in CommandTargetIdentityFilter filter, bool includeSelf)
        {
            if (root == null)
                return null;

            var queue = new Queue<Transform>();
            if (includeSelf)
            {
                queue.Enqueue(root);
            }
            else
            {
                for (var i = 0; i < root.childCount; i++)
                    queue.Enqueue(root.GetChild(i));
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (TryResolveMatchingScope(current, filter, out var scope))
                    return scope;

                for (var i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return null;
        }

        static bool TryResolveMatchingScope(Transform transform, in CommandTargetIdentityFilter filter, out IScopeNode? scope)
        {
            scope = null;
            if (transform == null)
                return false;

            if (transform.TryGetComponent<BaseLifetimeScope>(out var baseScope) && baseScope != null)
            {
                if (!MatchesIdentity(baseScope, filter))
                    return false;

                scope = baseScope;
                return true;
            }

            if (transform.TryGetComponent<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                if (!MatchesIdentity(runtimeScope, filter))
                    return false;

                scope = runtimeScope;
                return true;
            }

            return false;
        }

        static IScopeNode? ResolveGameLogicRoot(IScopeNode origin)
        {
            // まずは従来どおり、親チェーン上の GameLogicRoot を優先
            var nearest = ScopeNodeHierarchy.FindNearestGameLogicRoot(origin, includeSelf: true);
            if (nearest != null)
                return nearest;

            // 別ツリー構成（例: UI scope から Global/GameLogic 参照）向けに
            // レジストリ全体から UseAsGameLogicRoot を探す
            if (TryResolveScopeRegistry(origin, out var registry) && registry != null)
            {
                var all = registry.ResolveAll(new CommandTargetIdentityFilter { searchScope = CommandTargetSearchScope.All }, origin);
                IScopeNode? firstMarked = null;
                for (int i = 0; i < all.Count; i++)
                {
                    var candidate = all[i];
                    if (candidate is not BaseLifetimeScope baseScope || !baseScope.UseAsGameLogicRoot)
                        continue;

                    var identity = candidate.Identity;
                    if (identity != null && !identity.IsActive)
                        continue;

                    if (identity != null && string.Equals(identity.Id, "GameLogicRoot", StringComparison.Ordinal))
                        return candidate;

                    firstMarked ??= candidate;
                }

                if (firstMarked != null)
                    return firstMarked;

                // 最後の保険: Id 直指定のスコープがあれば採用
                for (int i = 0; i < all.Count; i++)
                {
                    var candidate = all[i];
                    var identity = candidate?.Identity;
                    if (identity != null && identity.IsActive &&
                        string.Equals(identity.Id, "GameLogicRoot", StringComparison.Ordinal))
                        return candidate;
                }
            }

            return null;
        }

        static IScopeNode? ResolveNearestGlobalScope(IScopeNode origin)
        {
            var nearest = ScopeNodeHierarchy.FindNearestAncestorByKind(
                origin,
                LifetimeScopeKind.Global,
                includeSelf: true);
            if (nearest != null)
                return nearest;

            if (TryResolveScopeRegistry(origin, out var registry) && registry != null)
            {
                var all = registry.ResolveAll(
                    new CommandTargetIdentityFilter
                    {
                        kind = LifetimeScopeKind.Global,
                        searchScope = CommandTargetSearchScope.All,
                    },
                    origin);

                IScopeNode? fallback = null;
                for (int i = 0; i < all.Count; i++)
                {
                    var candidate = all[i];
                    if (candidate == null || candidate.Kind != LifetimeScopeKind.Global)
                        continue;

                    fallback ??= candidate;

                    var identity = candidate.Identity;
                    if (identity == null || identity.IsActive)
                        return candidate;
                }

                if (fallback != null)
                    return fallback;
            }

            var globals = UnityEngine.Object.FindObjectsByType<GlobalLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (globals == null || globals.Length == 0)
                return null;

            IScopeNode? inactiveFallback = null;
            for (int i = 0; i < globals.Length; i++)
            {
                var candidate = globals[i];
                if (candidate == null)
                    continue;

                inactiveFallback ??= candidate;

                var identity = candidate.Identity;
                if (identity == null || identity.IsActive)
                    return candidate;
            }

            return inactiveFallback;
        }

        static bool MatchesIdentity(IScopeNode scope, in CommandTargetIdentityFilter filter)
        {
            if (scope == null)
                return false;

            var identity = scope.Identity;
            if (identity == null)
                return false;

            if (filter.requireActive && !identity.IsActive)
                return false;

            if (filter.kind != LifetimeScopeKind.None && identity.Kind != filter.kind)
                return false;

            if (!string.IsNullOrEmpty(filter.id) &&
                !string.Equals(identity.Id, filter.id, StringComparison.Ordinal))
                return false;

            if (!string.IsNullOrEmpty(filter.category) &&
                !string.Equals(identity.Category, filter.category, StringComparison.Ordinal))
                return false;

            return true;
        }

        static IScopeNode? FindRoot(IScopeNode origin)
        {
            IScopeNode? root = null;
            for (var node = origin; node != null; node = node.Parent)
                root = node;
            return root;
        }

        static bool IsInSearchScope(IScopeNode origin, IScopeNode candidate, CommandTargetSearchScope scope)
        {
            switch (scope)
            {
                case CommandTargetSearchScope.AncestorsOnly:
                    for (var node = origin.Parent; node != null; node = node.Parent)
                    {
                        if (ReferenceEquals(node, candidate))
                            return true;
                    }
                    return false;
                case CommandTargetSearchScope.DescendantsOnly:
                    return origin.TryGetPathTo(candidate, out var _, includeSelf: false);
                default:
                    return true;
            }
        }

        static bool TryResolveFromUnityObject(UnityEngine.Object? obj, out IScopeNode scope)
        {
            scope = null!;
            if (obj == null)
                return false;

            if (obj is IScopeNode node)
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
            // FromUnityObject で参照されるのは Scope 本体ではなく子オブジェクトであることが多い。
            // このとき GetComponentInParent で種類ごとに探すと、より近い RuntimeScope より
            // 先に BaseScope が拾われるケースがあり、期待チャネルが見つからない不具合につながる。
            // そのため Transform を近傍から順にたどり、各階層で Runtime -> Base の順で評価する。
            // Resolve by nearest Transform first, preferring Runtime scope over Base scope.
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

            // Last-resort fallback for detached scope trees:
            // resolve registry from ProjectLifetimeScope.
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

        static bool SourceEquals(in ActorSource a, in ActorSource b)
        {
            if (a.Kind != b.Kind)
                return false;

            switch (a.Kind)
            {
                case ActorSourceKind.ByIdentity:
                    return IdentityEquals(a.Identity, b.Identity);
                case ActorSourceKind.FromUnityObject:
                    return ReferenceEquals(a.UnityObject, b.UnityObject);
                case ActorSourceKind.Shared:
                    return SharedEquals(a.Shared, b.Shared);
                case ActorSourceKind.ContextSlot:
                    return a.ContextSlot == b.ContextSlot;
                case ActorSourceKind.TargetChannel:
                    return TargetChannelEquals(a.TargetChannel, b.TargetChannel);
                default:
                    return true;
            }
        }

        static IScopeNode? ResolveFromTargetChannel(IScopeNode origin, in ActorSource source, IScopeNode? commandRootScope)
        {
            var targetChannel = source.TargetChannel;
            if (targetChannel == null)
                return null;

            var channelOwner = Resolve(origin, targetChannel.ChannelOwnerActorSource, commandRootScope);
            if (channelOwner == null)
                return null;

            var normalizedTag = string.IsNullOrWhiteSpace(targetChannel.ChannelTag) ? "default" : targetChannel.ChannelTag.Trim();
            if (!TargetChannelTargetPositionSourceHelper.TryResolveRuntimeFromScopeChain(channelOwner, normalizedTag, out var runtime) || runtime == null)
                return null;

            var hits = runtime.Hits;
            if (hits == null || hits.Count == 0)
                return null;

            if (targetChannel.TargetSelectMode != TargetChannelTargetSelectMode.FilterByActorSource)
            {
                return TargetChannelTargetPositionSourceHelper.TryGetFirstAliveHit(hits, out var firstHit)
                    ? firstHit.Scope
                    : null;
            }

            var filterScope = Resolve(origin, targetChannel.FilterActorSource, commandRootScope);
            if (filterScope != null)
            {
                for (int i = 0; i < hits.Count; i++)
                {
                    var candidate = hits[i];
                    if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(candidate))
                        continue;

                    if (ReferenceEquals(candidate.Scope, filterScope) || ReferenceEquals(candidate.Identity, filterScope.Identity))
                        return candidate.Scope;
                }
            }

            if (!targetChannel.FallbackToFirstIfFilterMiss)
                return null;

            return TargetChannelTargetPositionSourceHelper.TryGetFirstAliveHit(hits, out var fallbackHit)
                ? fallbackHit.Scope
                : null;
        }

        static bool TargetChannelEquals(TargetChannelActorSourceRef? a, TargetChannelActorSourceRef? b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return string.Equals(a.ChannelTag, b.ChannelTag, StringComparison.Ordinal)
                   && a.TargetSelectMode == b.TargetSelectMode
                   && a.FallbackToFirstIfFilterMiss == b.FallbackToFirstIfFilterMiss
                   && SourceEquals(a.ChannelOwnerActorSource, b.ChannelOwnerActorSource)
                   && SourceEquals(a.FilterActorSource, b.FilterActorSource);
        }

        static bool SharedEquals(SharedActorSourceRef? a, SharedActorSourceRef? b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return string.Equals(a.SharedTag, b.SharedTag, StringComparison.Ordinal)
                   && SourceEquals(a.SharedHubActorSource, b.SharedHubActorSource);
        }

        static bool IdentityEquals(in CommandTargetIdentityFilter a, in CommandTargetIdentityFilter b)
        {
            return a.kind == b.kind
                   && string.Equals(a.id, b.id, System.StringComparison.Ordinal)
                   && string.Equals(a.category, b.category, System.StringComparison.Ordinal)
                   && a.requireActive == b.requireActive
                   && a.searchScope == b.searchScope;
        }

        static IScopeNode? ResolvePlayerScope(IScopeNode origin)
        {
            if (!TryResolvePlayerLocator(origin, out var locator) || locator == null)
                return null;

            return locator.TryGetPlayerScope(out var scope) && scope != null
                ? scope
                : null;
        }

        static bool TryResolvePlayerLocator(IScopeNode? scope, out IPlayerLocationService? locator)
        {
            locator = null;
            if (scope == null)
                return false;

            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IPlayerLocationService>(out var found) && found != null)
                {
                    locator = found;
                    return true;
                }

                current = current.Parent;
            }

            // Detached tree fallback: resolve from ProjectLifetimeScope.
            var projects = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projects != null && projects.Length > 0)
            {
                var projectResolver = projects[0] != null ? projects[0].Resolver : null;
                if (projectResolver != null &&
                    projectResolver.TryResolve<IPlayerLocationService>(out var projectLocator) &&
                    projectLocator != null)
                {
                    locator = projectLocator;
                    return true;
                }
            }

            return false;
        }
    }
}
