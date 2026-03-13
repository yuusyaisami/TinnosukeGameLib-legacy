#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.DI;
using Game.UI;

namespace Game.Commands.VNext
{
    public sealed class ShowToastExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ShowToast;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ShowToastCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ShowToastCommandData is required.");

            if (!TryResolveToastService(ctx.Scope, typed.SystemTag, out var toastService) || toastService == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Toast system not found. Tag={typed.SystemTag}");

            BaseRuntimeTemplatePreset? overridePreset = null;
            if (typed.OverrideRuntimeTemplatePreset.HasSource &&
                typed.OverrideRuntimeTemplatePreset.TryGet(ctx, out var resolvedPreset))
            {
                overridePreset = resolvedPreset;
            }

            var lifetimeOverride = typed.LifetimeSecondsOverride.Resolve(ctx);

            var request = new ToastRequest(
                overrideTemplatePreset: overridePreset,
                onSpawnCommands: typed.OnSpawnCommands,
                onShowCommands: typed.OnShowCommands,
                onCloseCommands: typed.OnCloseCommands,
                onStackAdjustedCommands: typed.OnStackAdjustedCommands,
                lifetimeSecondsOverride: lifetimeOverride,
                sourceVars: ctx.Vars);

            if (!toastService.TryEnqueue(request, out var handle) || handle == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Toast enqueue failed.");

            if (typed.AwaitMode == ToastCommandAwaitMode.WaitForShown)
            {
                await handle.WaitForShownAsync(ct);
                return;
            }

            if (typed.AwaitMode == ToastCommandAwaitMode.WaitForClosed)
            {
                await handle.WaitForClosedAsync(ct);
                return;
            }
        }

        static bool TryResolveToastService(IScopeNode? scope, string? tag, out IToastSystemService? service)
        {
            service = null;
            var normalizedTag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();

            if (scope == null)
                return false;

            if (scope.TryResolveInAncestors<IScopeMultiRegistry>(out var registry) && registry != null)
            {
                var services = registry.GetAll<IToastSystemService>();
                for (int i = 0; i < services.Count; i++)
                {
                    var candidate = services[i];
                    if (candidate == null)
                        continue;

                    if (string.Equals(candidate.SystemTag, normalizedTag, System.StringComparison.OrdinalIgnoreCase))
                    {
                        service = candidate;
                        return true;
                    }
                }

                if (services.Count > 0)
                {
                    service = services[0];
                    return true;
                }
            }

            if (scope.TryResolveInAncestors<IToastSystemService>(out var single) && single != null)
            {
                service = single;
                return true;
            }

            return false;
        }
    }
}
