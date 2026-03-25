#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SharedLTSChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SharedLTSChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SharedLTSChannelCommandData typed)
                return;

            var (hubScope, _) = await ActorScopeResolver.ResolveAsync(typed.HubSource, ctx, ct);
            if (!TryResolveHub(hubScope, out var hub) || hub == null)
                return;

            switch (typed.Operation)
            {
                case SharedLTSChannelOperation.Register:
                    ExecuteRegister(hub, typed, ctx);
                    break;

                case SharedLTSChannelOperation.Get:
                    ExecuteGet(hub, typed, ctx);
                    break;

                case SharedLTSChannelOperation.Unregister:
                    hub.Unregister(typed.Tag);
                    break;

                case SharedLTSChannelOperation.ClearAll:
                    hub.Clear();
                    break;
            }
        }

        static void ExecuteRegister(ISharedLTSChannelHub hub, SharedLTSChannelCommandData typed, CommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(typed.Tag))
                return;

            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.ActorSource);
            if (targetScope == null)
                return;

            hub.Register(typed.Tag, targetScope);
        }

        static void ExecuteGet(ISharedLTSChannelHub hub, SharedLTSChannelCommandData typed, CommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(typed.Tag))
            {
                ctx.SetScope(typed.ContextSlot, null);
                return;
            }

            if (hub.TryGet(typed.Tag, out var scope))
            {
                ctx.SetScope(typed.ContextSlot, scope);
                return;
            }

            ctx.SetScope(typed.ContextSlot, null);
        }

        static bool TryResolveHub(IScopeNode? scope, out ISharedLTSChannelHub? hub)
        {
            hub = null;
            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<ISharedLTSChannelHub>(out var resolved) && resolved != null)
                {
                    hub = resolved;
                    return true;
                }

                current = current.Parent;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SharedLTSChannelExecutor] ISharedLTSChannelHub is not registered.");
#endif
            return false;
        }
    }
}
