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

            LogDebug(ctx, $"Received. {typed.DebugData} actor={DescribeScope(ctx.Actor)} scope={DescribeScope(ctx.Scope)} ctCanBeCanceled={ct.CanBeCanceled} ctIsCancellationRequested={ct.IsCancellationRequested}");

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                Warn($"Target resolve failed: {error}");
                return;
            }

            LogDebug(ctx, $"Target resolved. targetScope={DescribeScope(targetScope)} operation={typed.Operation}");

            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);
            if (!TryResolve(targetScope, out IVisualBoundsReactiveHubService? hub) || hub == null)
            {
                Warn("IVisualBoundsReactiveHubService is missing on target scope.");
                return;
            }

            LogDebug(ctx, $"Hub resolved. channelCount={hub.ChannelCount} enableDebugLog={hub.EnableDebugLog} targetScope={DescribeScope(targetScope)}");

            switch (typed.Operation)
            {
                case VisualBoundsReactiveHubControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, targetScope, ctx);
                    return;

                case VisualBoundsReactiveHubControlOperation.Unregister:
                    if (!hub.Unregister(typed.NormalizedChannelTag))
                        Warn($"Unregister skipped: channel '{typed.NormalizedChannelTag}' was not found.");
                    else
                        LogDebug(ctx, $"Unregister succeeded. tag={typed.NormalizedChannelTag} channelCount={hub.ChannelCount}");
                    return;

                case VisualBoundsReactiveHubControlOperation.ClearAll:
                    hub.Clear();
                    LogDebug(ctx, $"ClearAll succeeded. channelCount={hub.ChannelCount}");
                    return;

                case VisualBoundsReactiveHubControlOperation.ResetRuntimeOverrides:
                    if (!hub.ResetRuntimeOverrides(typed.NormalizedChannelTag))
                        Warn($"ResetRuntimeOverrides skipped: channel '{typed.NormalizedChannelTag}' was not found.");
                    else
                        LogDebug(ctx, $"ResetRuntimeOverrides succeeded. tag={typed.NormalizedChannelTag} channelCount={hub.ChannelCount}");
                    return;

                case VisualBoundsReactiveHubControlOperation.ResetAllRuntimeOverrides:
                    hub.ResetAllRuntimeOverrides();
                    LogDebug(ctx, $"ResetAllRuntimeOverrides succeeded. channelCount={hub.ChannelCount}");
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

            LogDebug(ctx, $"RegisterOrReplace begin. tag={typed.NormalizedChannelTag} preset={DescribePreset(preset)} targetScope={DescribeScope(targetScope)}");

            if (!hub.RegisterOrReplace(typed.NormalizedChannelTag, preset))
                Warn($"RegisterOrReplace skipped: channel '{typed.NormalizedChannelTag}' could not be updated.");
            else
                LogDebug(ctx, $"RegisterOrReplace succeeded. tag={typed.NormalizedChannelTag} channelCount={hub.ChannelCount}");
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

        static void LogDebug(CommandContext ctx, string message)
        {
            if (!ShouldLogDebug(ctx))
                return;

            Debug.Log($"[VisualBoundsReactiveHubControlExecutor] {message}");
        }

        static bool ShouldLogDebug(CommandContext ctx)
        {
            if (ctx == null)
                return false;

            var scope = ctx.Scope;
            if (scope?.Resolver != null && TryResolve(scope, out IVisualBoundsReactiveHubService? hub) && hub != null)
                return hub.EnableDebugLog;

            return false;
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var id = scope.Identity?.Id;
            var identityText = string.IsNullOrWhiteSpace(id) ? "<none>" : id;
            return $"{scope.Kind} id='{identityText}'";
        }

        static string DescribePreset(VisualBoundsReactiveChannelPreset? preset)
        {
            if (preset == null)
                return "<null>";

            return $"Enabled={preset.Enabled} TargetUseActorSource={preset.Target.UseActorSource} Output={preset.Output.GetType().Name}";
        }
    }
}