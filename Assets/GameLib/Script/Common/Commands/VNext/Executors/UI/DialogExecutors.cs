#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class UIDialogChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UIDialogChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UIDialogChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UIDialogChannelCommandData is required.");

            if (!ctx.Resolver.TryResolve<IDialogChannelHubService>(out var hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IDialogChannelHubService is missing.");

            var channelKey = typed.ChannelKey ?? string.Empty;
            if (!hub.TryGetChannel(channelKey, out var channel) || channel == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Dialog channel '{channelKey}' not found.");

            var initialVars = BuildInitialVars(typed, ctx);

            if (typed.Mode == UIDialogInvokeMode.ShowOnly)
            {
                DialogEventBinding[]? additionalBindings = null;
                if (typed.UseEventCommandMapping)
                    additionalBindings = BuildBindings(typed.Mappings);

                var request = new UIDialogRequest(
                    owner: ctx.Scope,
                    initialVariables: initialVars,
                    subscribeBindingsOverride: null,
                    additionalSubscribeBindings: additionalBindings);

                channel.Show(request);
                return;
            }

            var spec = new DialogAwaitSpec
            {
                EventKeys = typed.AwaitEventKeys ?? Array.Empty<string>(),
                CloseAfterEvent = typed.CloseAfterEvent,
            };

            DialogAwaitResult result;
            try
            {
                var request = new UIDialogRequest(owner: ctx.Scope, initialVariables: initialVars);
                result = await channel.ShowAndWaitAsync(request, spec, ct);
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled; keep it a clean failure path.
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw new CommandExecutionException(CommandRunFailureKind.Exception, "Dialog.ShowAndWaitAsync failed.");
            }

            ApplyResultToVars(typed, ctx, result);

            if (typed.UseEventCommandMapping && !result.WasCancelled)
            {
                var mapping = FindMapping(typed.Mappings, result.EventKey);
                if (mapping != null && mapping.Commands != null && mapping.Commands.Count > 0)
                {
                    var varsForCommands = new VarStore();
                    (ctx.Vars ?? NullVarStore.Instance).MergeInto(varsForCommands, overwrite: true);

                    if (result.Payload != null)
                        result.Payload.MergeInto(varsForCommands, overwrite: true);

                    InjectDialogRefs(channel, varsForCommands);

                    var actor = channel.DialogScope ?? ctx.Scope;
                    var cmdCtx = new CommandContext(ctx.Scope, varsForCommands, ctx.Runner, actor: actor, options: ctx.Options, commandRootScope: ctx.CommandRootScope, rootActor: ctx.RootActor, callerActor: ctx.Actor);

                    try
                    {
                        await ctx.Runner.ExecuteListAsync(mapping.Commands, cmdCtx, ct, cmdCtx.Options);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        throw new CommandExecutionException(CommandRunFailureKind.Exception, "Dialog mapped commands failed.");
                    }

                    if (mapping.CloseAfterInvoke)
                        channel.Hide(DialogCloseReason.ActionInvoked);
                }
            }
        }

        static IVarStore? BuildInitialVars(UIDialogChannelCommandData typed, CommandContext ctx)
        {
            var hasPayload = typed.InitialVariables != null && typed.InitialVariables.Entries != null && typed.InitialVariables.Entries.Count > 0;
            if (!typed.UseContextVarsAsInitialVariables && !hasPayload)
                return null;

            var vars = new VarStore();

            if (typed.UseContextVarsAsInitialVariables)
                (ctx.Vars ?? NullVarStore.Instance).MergeInto(vars, overwrite: true);

            if (typed.InitialVariables != null)
                typed.InitialVariables.ApplyTo(vars, overwrite: typed.OverwriteInitialVariables);

            return vars;
        }

        static void ApplyResultToVars(UIDialogChannelCommandData typed, CommandContext ctx, DialogAwaitResult result)
        {
            if (!typed.WriteResultToVars)
                return;

            var dest = ctx.Vars;
            if (dest == null)
                return;

            if (!string.IsNullOrWhiteSpace(typed.ResultEventKeyStableKey) &&
                VarIdResolver.TryResolve(typed.ResultEventKeyStableKey, out var eventKeyVarId) && eventKeyVarId != 0)
            {
                dest.TrySetVariant(eventKeyVarId, DynamicVariant.FromString(result.EventKey ?? string.Empty));
            }

            if (!string.IsNullOrWhiteSpace(typed.ResultSelectedIndexStableKey) &&
                VarIdResolver.TryResolve(typed.ResultSelectedIndexStableKey, out var indexVarId) && indexVarId != 0)
            {
                dest.TrySetVariant(indexVarId, DynamicVariant.FromInt(result.WasCancelled ? -1 : result.SelectedIndex));
            }

            if (!string.IsNullOrWhiteSpace(typed.ResultWasCancelledStableKey) &&
                VarIdResolver.TryResolve(typed.ResultWasCancelledStableKey, out var cancelVarId) && cancelVarId != 0)
            {
                dest.TrySetVariant(cancelVarId, DynamicVariant.FromBool(result.WasCancelled));
            }

            if (typed.MergeEventPayloadToVars && result.Payload != null)
                result.Payload.MergeInto(dest, overwrite: typed.OverwriteExistingVars);
        }

        static UIDialogEventCommandMapping? FindMapping(UIDialogEventCommandMapping[]? mappings, string? eventKey)
        {
            if (mappings == null || mappings.Length == 0)
                return null;

            var key = eventKey ?? string.Empty;
            for (int i = 0; i < mappings.Length; i++)
            {
                var m = mappings[i];
                if (m == null)
                    continue;
                if (string.Equals(m.EventKey ?? string.Empty, key, StringComparison.Ordinal))
                    return m;
            }

            return null;
        }

        static DialogEventBinding[]? BuildBindings(UIDialogEventCommandMapping[]? mappings)
        {
            if (mappings == null || mappings.Length == 0)
                return null;

            var list = new DialogEventBinding[mappings.Length];
            for (int i = 0; i < mappings.Length; i++)
            {
                var m = mappings[i];
                list[i] = new DialogEventBinding
                {
                    EventKey = m?.EventKey ?? string.Empty,
                    Commands = m?.Commands ?? new CommandListData(),
                    CloseAfterInvoke = m?.CloseAfterInvoke ?? true,
                };
            }
            return list;
        }

        static void InjectDialogRefs(DialogChannelRuntime channel, IVarStore dest)
        {
            void SetRef(string stableKey, object value)
            {
                if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                    dest.TrySetManagedRef(varId, value);
            }

            void SetString(string stableKey, string value)
            {
                if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                    dest.TrySetVariant(varId, DynamicVariant.FromString(value));
            }

            if (channel.DialogScope != null)
                SetRef(UIDialogVarKeys.DialogScope, channel.DialogScope);
            if (channel.Owner != null)
                SetRef(UIDialogVarKeys.DialogOwner, channel.Owner);
            SetString(UIDialogVarKeys.DialogChannelKey, channel.ChannelKey);
        }
    }
}
