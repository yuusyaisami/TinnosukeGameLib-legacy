#nullable enable
using Game.Channel;
using VContainer;

namespace Game.Commands.VNext
{
    internal static class TextChannelResolveUtility
    {
        public static bool TryResolvePlayerWithHub(CommandContext ctx, string tag, out ITextChannelPlayer? player, out ITextChannelHubService? hub)
        {
            player = null;
            hub = null;

            var origin = ctx.Actor ?? ctx.Scope;

            // Prefer hubs within actor/scope subtree first (strict ownership).
            if (TryResolvePlayerInSubtree(origin, tag, out player, out hub, strictOwnership: true))
                return true;

            // Fallback to ancestor chain from current scope (strict ownership).
            if (TryResolvePlayerInAncestors(ctx.Scope, tag, out player, out hub, strictOwnership: true))
                return true;

            // Last strict fallback: ancestor chain from actor (if different from scope).
            if (!ReferenceEquals(origin, ctx.Scope) && TryResolvePlayerInAncestors(origin, tag, out player, out hub, strictOwnership: true))
                return true;

            // Compatibility fallback: retry without owner strictness to avoid false negatives
            // in nested resolver graphs where the hub is reachable but owner node differs.
            if (TryResolvePlayerInSubtree(origin, tag, out player, out hub, strictOwnership: false))
                return true;

            if (TryResolvePlayerInAncestors(ctx.Scope, tag, out player, out hub, strictOwnership: false))
                return true;

            if (!ReferenceEquals(origin, ctx.Scope) && TryResolvePlayerInAncestors(origin, tag, out player, out hub, strictOwnership: false))
                return true;

            return false;
        }

        static bool TryResolvePlayerInSubtree(IScopeNode? origin, string tag, out ITextChannelPlayer? player, out ITextChannelHubService? hub, bool strictOwnership)
        {
            player = null;
            hub = null;

            if (origin == null)
                return false;

            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(origin, includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ITextChannelHubService>(out var foundHub) || foundHub == null)
                    continue;

                if (strictOwnership && !IsHubOwnedByNode(foundHub, node))
                    continue;

                if (foundHub.TryGetPlayer(tag, out var foundPlayer) && foundPlayer != null)
                {
                    player = foundPlayer;
                    hub = foundHub;
                    return true;
                }
            }

            return false;
        }

        static bool TryResolvePlayerInAncestors(IScopeNode? scope, string tag, out ITextChannelPlayer? player, out ITextChannelHubService? hub, bool strictOwnership)
        {
            player = null;
            hub = null;

            if (scope == null)
                return false;

            foreach (var node in scope.EnumerateAncestors(includeSelf: true))
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ITextChannelHubService>(out var foundHub) || foundHub == null)
                    continue;

                if (strictOwnership && !IsHubOwnedByNode(foundHub, node))
                    continue;

                if (foundHub.TryGetPlayer(tag, out var foundPlayer) && foundPlayer != null)
                {
                    player = foundPlayer;
                    hub = foundHub;
                    return true;
                }
            }

            return false;
        }

        static bool IsHubOwnedByNode(ITextChannelHubService hub, IScopeNode? node)
        {
            if (hub is TextChannelHubService textHub)
                return ReferenceEquals(textHub.OwnerScope, node);

            return true;
        }
    }
}