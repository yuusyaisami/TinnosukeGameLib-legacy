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

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not SharedLTSChannelCommandData typed)
                return UniTask.CompletedTask;

            if (!TryResolveHub(ctx.Scope, out var hub) || hub == null)
                return UniTask.CompletedTask;

            switch (typed.Operation)
            {
                case SharedLTSChannelOperation.Register:
                    ExecuteRegister(hub, typed, ctx);
                    break;

                case SharedLTSChannelOperation.Unregister:
                    hub.Unregister(typed.Tag);
                    break;

                case SharedLTSChannelOperation.ClearAll:
                    hub.Clear();
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void ExecuteRegister(ISharedLTSChannelHub hub, SharedLTSChannelCommandData typed, CommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(typed.Tag))
                return;

            var targetScope = ActorSourceFastResolver.Resolve(ctx.Scope, typed.ActorSource, ctx.CommandRootScope);
            if (targetScope == null)
                return;

            hub.Register(typed.Tag, targetScope);
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
