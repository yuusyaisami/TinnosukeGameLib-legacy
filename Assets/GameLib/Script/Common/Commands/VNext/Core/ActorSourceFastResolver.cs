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
            var resolver = origin.Resolver;
            if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) && registry != null)
            {
                var resolved = registry.Resolve(filter, origin);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }

        static IScopeNode? ResolveGameLogicRoot(IScopeNode origin)
        {
            return ScopeNodeHierarchy.FindNearestGameLogicRoot(origin, includeSelf: true);
        }

        static IScopeNode? ResolveNearestGlobalScope(IScopeNode origin)
        {
            return ScopeNodeHierarchy.FindNearestAncestorByKind(
                origin,
                LifetimeScopeKind.Global,
                includeSelf: true);
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

            return ScopeFeatureInstallerUtility.TryGetScopeNode(go, includeInactive: true, out var node)
                ? node
                : null;
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

            return false;
        }
    }
}
