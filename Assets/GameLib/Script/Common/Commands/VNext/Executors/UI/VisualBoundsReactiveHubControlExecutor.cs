#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class VisualBoundsReactiveHubControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.VisualBoundsReactiveHubControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not VisualBoundsReactiveHubControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "VisualBoundsReactiveHubControlCommandData is required.");

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                Warn($"Target resolve failed: {error}");
                return;
            }

            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);
            if (!TryResolve(targetScope, out IVisualBoundsReactiveHubService? hub) || hub == null)
            {
                Warn("IVisualBoundsReactiveHubService is missing on target scope.");
                return;
            }

            switch (typed.Operation)
            {
                case VisualBoundsReactiveHubControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, targetScope, ctx);
                    return;

                case VisualBoundsReactiveHubControlOperation.Unregister:
                    if (!hub.Unregister(typed.NormalizedChannelTag))
                        Warn($"Unregister skipped: channel '{typed.NormalizedChannelTag}' was not found.");
                    return;

                case VisualBoundsReactiveHubControlOperation.ClearAll:
                    hub.Clear();
                    return;

                case VisualBoundsReactiveHubControlOperation.ResetRuntimeOverrides:
                    if (!hub.ResetRuntimeOverrides(typed.NormalizedChannelTag))
                        Warn($"ResetRuntimeOverrides skipped: channel '{typed.NormalizedChannelTag}' was not found.");
                    return;

                case VisualBoundsReactiveHubControlOperation.ResetAllRuntimeOverrides:
                    hub.ResetAllRuntimeOverrides();
                    return;

                default:
                    Warn($"Unsupported operation: {typed.Operation}");
                    return;
            }
        }

        static void ExecuteRegisterOrReplace(
            IVisualBoundsReactiveHubService hub,
            VisualBoundsReactiveHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.Preset.TryGet(dynamicContext, out VisualBoundsReactiveChannelPreset? preset) || preset == null)
            {
                Warn("RegisterOrReplace skipped: preset resolve failed.");
                return;
            }

            if (!hub.RegisterOrReplace(typed.NormalizedChannelTag, preset))
                Warn($"RegisterOrReplace skipped: channel '{typed.NormalizedChannelTag}' could not be updated.");
        }

        static bool TryResolve<T>(IScopeNode scope, out T? service) where T : class
        {
            service = null;
            var resolver = scope.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out service) && service != null;
        }

        static void Warn(string message)
        {
            Debug.LogWarning($"[VisualBoundsReactiveHubControlExecutor] {message}");
        }
    }
}