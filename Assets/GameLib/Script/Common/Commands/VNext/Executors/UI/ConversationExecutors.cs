#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using Game.Conversation;
using Game.Dialogue;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ConversationFlowExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ConversationFlow;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ConversationFlowCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ConversationFlowCommandData is required.");

            var targetScope = await ConversationExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            ConversationExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!ConversationExecutorUtility.TryResolve(targetScope, out IConversationChannelHubService? hub) || hub == null)
            {
                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, "IConversationChannelHubService is missing on target scope.");
                return;
            }

            var tag = typed.NormalizedConversationTag;
            switch (typed.Operation)
            {
                case ConversationFlowOperation.Start:
                case ConversationFlowOperation.Run:
                    {
                        if (!TryResolveFlowPreset(typed, ctx, targetScope, tag, out var preset, out var presetError) || preset == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, presetError);
                            return;
                        }

                        if (!hub.TryStartSession(tag, preset, out var startedSession, out var startError) || startedSession == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, startError);
                            return;
                        }

                        var startHookResult = await ExecuteBranchCommandsAsync(startedSession.FlowPreset.Hooks.OnStartedCommands, ctx, ct, "flow.onStarted");
                        if (!startHookResult.Success)
                        {
                            hub.TryEndSession(tag, startHookResult.Canceled ? ConversationSessionEndKind.Canceled : ConversationSessionEndKind.Failed, startHookResult.Message, out _);
                            EnsureStrict(typed.Strict, false, startHookResult.Canceled ? CommandRunFailureKind.Canceled : CommandRunFailureKind.Exception, startHookResult.Message);
                            return;
                        }

                        if (typed.Operation == ConversationFlowOperation.Start)
                            return;

                        await RunActiveSessionAsync(typed, hub, startedSession, targetScope, ctx, ct);
                        return;
                    }

                case ConversationFlowOperation.Continue:
                    {
                        if (!hub.TryGetSession(tag, out var session) || session == null || !session.IsActive)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-201] Active conversation session was not found. tag='{tag}'");
                            return;
                        }

                        await RunActiveSessionAsync(typed, hub, session, targetScope, ctx, ct);
                        return;
                    }

                case ConversationFlowOperation.End:
                    {
                        if (!hub.TryGetSession(tag, out var session) || session == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-201] Active conversation session was not found. tag='{tag}'");
                            return;
                        }

                        var resolvedDialogue = await ConversationExecutorUtility.ResolveDialogueServiceAsync(session, targetScope, ctx, ct);
                        var dialogue = resolvedDialogue.dialogue;
                        var dialogueError = resolvedDialogue.error;
                        var dialogueClosed = false;

                        if (dialogue != null)
                            dialogueClosed = await TryCloseDialogueAsync(session, dialogue, ct);

                        if (!hub.TryEndSession(tag, ConversationSessionEndKind.Forced, typed.EndMessage, out _))
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-202] Conversation session end failed. tag='{tag}'");
                            return;
                        }

                        if (dialogue == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, dialogueError);
                            return;
                        }

                        if (!dialogueClosed && !ct.IsCancellationRequested)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-203] Dialogue channel end failed. tag='{session.DialogueChannelTag}'");
                            return;
                        }

                        return;
                    }

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported Conversation flow operation: {typed.Operation}");
            }
        }

        static bool TryResolveFlowPreset(
            ConversationFlowCommandData typed,
            CommandContext ctx,
            IScopeNode targetScope,
            string tag,
            out ConversationFlowPreset? preset,
            out string error)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (typed.FlowPreset.TryGet(dynamicContext, out preset) && preset != null)
            {
                preset = preset.CreateRuntimeCopy();
                error = string.Empty;
                return true;
            }

            preset = null;
            error = $"[CONV-200] Conversation flow preset could not be resolved. tag='{tag}'";
            return false;
        }

        static async UniTask RunActiveSessionAsync(
            ConversationFlowCommandData typed,
            IConversationChannelHubService hub,
            IConversationRuntimeSession session,
            IScopeNode targetScope,
            CommandContext ctx,
            CancellationToken ct)
        {
            var (dialogue, dialogueError) = await ConversationExecutorUtility.ResolveDialogueServiceAsync(session, targetScope, ctx, ct);
            if (dialogue == null)
            {
                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, dialogueError);
                return;
            }

            ConversationRunOutcome outcome;
            try
            {
                outcome = await RunSessionLoopAsync(session, dialogue, ctx, typed.MaxNodeStepsOverride, ct);
            }
            catch (OperationCanceledException)
            {
                outcome = ConversationRunOutcome.Canceled("[CONV-250] Conversation run was canceled by token.");
            }

            var dialogueClosed = await TryCloseDialogueAsync(session, dialogue, ct);

            if (!hub.TryEndSession(session.Tag, outcome.EndKind, outcome.Message, out _))
            {
                if (ct.IsCancellationRequested || !session.IsActive)
                    return;

                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-251] Conversation session end failed. tag='{session.Tag}'");
                return;
            }

            if (!dialogueClosed && !ct.IsCancellationRequested)
            {
                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-252] Dialogue channel end failed. tag='{session.DialogueChannelTag}'");
                return;
            }

            var hookResult = await ExecuteFlowEndHookAsync(session.FlowPreset.Hooks, outcome.EndKind, ctx, ct);
            if (!hookResult.Success)
            {
                EnsureStrict(typed.Strict, false, hookResult.Canceled ? CommandRunFailureKind.Canceled : CommandRunFailureKind.Exception, hookResult.Message);
                return;
            }

            if (outcome.EndKind == ConversationSessionEndKind.Completed)
                return;

            EnsureStrict(typed.Strict, false, ToFailureKind(outcome.EndKind), outcome.Message);
        }

        static async UniTask<ConversationRunOutcome> RunSessionLoopAsync(
            IConversationRuntimeSession session,
            IDialogueService dialogue,
            CommandContext ctx,
            int maxNodeStepsOverride,
            CancellationToken ct)
        {
            var maxNodeSteps = maxNodeStepsOverride > 0
                ? maxNodeStepsOverride
                : session.FlowPreset.MaxNodeSteps;

            for (var step = 0; step < maxNodeSteps; step++)
            {
                if (!session.IsActive)
                    return ConversationRunOutcome.Failed("[CONV-260] Conversation session is not active.");

                if (!session.TryGetCurrentNode(out var node) || node == null)
                    return ConversationRunOutcome.Failed($"[CONV-261] Current node was not found. nodeId={session.CurrentNodeId}");

                session.IncrementTurn();

                var onEnterResult = await ExecuteBranchCommandsAsync(node.OnEnterCommands, ctx, ct, "node.onEnter");
                if (!onEnterResult.Success)
                    return onEnterResult.Canceled
                        ? ConversationRunOutcome.Canceled(onEnterResult.Message)
                        : ConversationRunOutcome.Failed(onEnterResult.Message);

                var executeResult = await ExecuteNodeAsync(session, node, dialogue, ctx, ct);
                if (!executeResult.Success)
                    return executeResult.Canceled
                        ? ConversationRunOutcome.Canceled(executeResult.Message)
                        : ConversationRunOutcome.Failed(executeResult.Message);

                var onExitResult = await ExecuteBranchCommandsAsync(node.OnExitCommands, ctx, ct, "node.onExit");
                if (!onExitResult.Success)
                    return onExitResult.Canceled
                        ? ConversationRunOutcome.Canceled(onExitResult.Message)
                        : ConversationRunOutcome.Failed(onExitResult.Message);

                session.MarkNodeCompleted(node.NodeId);

                var nextNodeId = executeResult.NextNodeId;
                if (nextNodeId <= 0)
                    return ConversationRunOutcome.Completed();

                if (!session.TrySetCurrentNode(nextNodeId))
                    return ConversationRunOutcome.Failed($"[CONV-262] Next node was not found. nextNodeId={nextNodeId}");
            }

            return ConversationRunOutcome.Failed($"[CONV-263] Max node steps exceeded. max={maxNodeSteps}");
        }

        static async UniTask<ConversationNodeExecuteResult> ExecuteNodeAsync(
            IConversationRuntimeSession session,
            ConversationNodePresetBase node,
            IDialogueService dialogue,
            CommandContext ctx,
            CancellationToken ct)
        {
            switch (node)
            {
                case ConversationStartNodePreset startNode:
                    {
                        var startNextNodeId = ConversationNodeJointUtility.ResolveFirstConnectedNodeId(startNode.NextNodeJoints);
                        return ConversationNodeExecuteResult.FromSuccess(startNextNodeId, hasNextNode: startNextNodeId > 0);
                    }

                case ConversationMessageNodePreset messageNode:
                    {
                        if (!TryResolveDialogueTag(session, messageNode.Slot, messageNode.DialogueTagOverride, out var dialogueTag))
                            return ConversationNodeExecuteResult.Failed($"[CONV-270] Dialogue tag resolve failed for message node. nodeId={messageNode.NodeId}");

                        var messageHookSettings = ResolveMessageHookSettings(session.FlowPreset.Settings, messageNode);
                        var beforeHookResult = await ExecuteBranchCommandsAsync(messageHookSettings.BeforeCommands, ctx, ct, "node.message.before");
                        if (!beforeHookResult.Success)
                        {
                            return beforeHookResult.Canceled
                                ? ConversationNodeExecuteResult.FromCanceled(beforeHookResult.Message)
                                : ConversationNodeExecuteResult.Failed(beforeHookResult.Message);
                        }

                        var applyCharacterResult = await ApplyMessageCharacterFrameAsync(messageNode, dialogueTag, dialogue, ctx, ct);
                        if (!applyCharacterResult.Success)
                            return applyCharacterResult;

                        var request = BuildDialogueMessageRequest(messageNode, session.FlowPreset.Settings);
                        var messageResult = await dialogue.ShowMessageAsync(dialogueTag, request, ct);
                        if (!messageResult.Success)
                            return ConversationNodeExecuteResult.Failed(
                                string.IsNullOrWhiteSpace(messageResult.Message)
                                    ? $"[CONV-271] Dialogue message failed. nodeId={messageNode.NodeId}"
                                    : messageResult.Message);

                        var afterHookResult = await ExecuteBranchCommandsAsync(messageHookSettings.AfterCommands, ctx, ct, "node.message.after");
                        if (!afterHookResult.Success)
                        {
                            return afterHookResult.Canceled
                                ? ConversationNodeExecuteResult.FromCanceled(afterHookResult.Message)
                                : ConversationNodeExecuteResult.Failed(afterHookResult.Message);
                        }

                        var nextNodeId = ConversationNodeJointUtility.ResolveFirstConnectedNodeId(messageNode.NextNodeJoints);
                        return ConversationNodeExecuteResult.FromSuccess(nextNodeId, hasNextNode: nextNodeId > 0);
                    }

                case ConversationChoiceNodePreset choiceNode:
                    {
                        if (!TryResolveDialogueTag(session, choiceNode.Slot, choiceNode.DialogueTagOverride, out var dialogueTag))
                            return ConversationNodeExecuteResult.Failed($"[CONV-272] Dialogue tag resolve failed for choice node. nodeId={choiceNode.NodeId}");

                        var request = choiceNode.ChoiceRequest?.CreateRuntimeCopy() ?? new DialogueChoiceRequest();
                        var choiceResult = await dialogue.ShowChoiceAndWaitAsync(dialogueTag, request, ct);
                        if (!choiceResult.Success)
                            return ConversationNodeExecuteResult.Failed(
                                string.IsNullOrWhiteSpace(choiceResult.Message)
                                    ? $"[CONV-273] Dialogue choice failed. nodeId={choiceNode.NodeId}"
                                    : choiceResult.Message);

                        var completion = choiceResult.SourceResult.CompletionKind;
                        switch (completion)
                        {
                            case GridObjectChoiceCompletionKind.Selected:
                                {
                                    var selectedIndex = choiceResult.SourceResult.SelectedIndex;
                                    if (selectedIndex < 0)
                                        return ConversationNodeExecuteResult.Failed($"[CONV-274] Invalid selected index. nodeId={choiceNode.NodeId} index={selectedIndex}");

                                    session.RecordChoice(choiceNode.NodeId, selectedIndex, DateTime.UtcNow.Ticks);
                                    WriteSelectedIndexToVarsIfNeeded(choiceNode, selectedIndex, ctx);

                                    var entryResult = await ApplySelectedEntryAsync(request, selectedIndex, ctx, ct);
                                    if (!entryResult.Success)
                                        return entryResult;

                                    var selectedHook = await ExecuteBranchCommandsAsync(choiceNode.OnChoiceSelectedCommands, ctx, ct, "node.choice.onSelected");
                                    if (!selectedHook.Success)
                                    {
                                        return selectedHook.Canceled
                                            ? ConversationNodeExecuteResult.FromCanceled(selectedHook.Message)
                                            : ConversationNodeExecuteResult.Failed(selectedHook.Message);
                                    }

                                    var nextNodeId = ConversationNodeJointUtility.ResolveFirstConnectedNodeId(choiceNode.NextNodeJoints);
                                    if (choiceNode.TryResolveChoiceJoint(selectedIndex, out var routeJoint) && routeJoint != null)
                                    {
                                        var routeResult = await ExecuteBranchCommandsAsync(routeJoint.OnMatchedCommands, ctx, ct, "node.choice.route.onMatched");
                                        if (!routeResult.Success)
                                        {
                                            return routeResult.Canceled
                                                ? ConversationNodeExecuteResult.FromCanceled(routeResult.Message)
                                                : ConversationNodeExecuteResult.Failed(routeResult.Message);
                                        }

                                        nextNodeId = routeJoint.SelectedNextNodeId;
                                    }

                                    return ConversationNodeExecuteResult.FromSuccess(nextNodeId, hasNextNode: nextNodeId > 0);
                                }

                            case GridObjectChoiceCompletionKind.Canceled:
                                {
                                    var canceledHook = await ExecuteBranchCommandsAsync(choiceNode.OnChoiceCanceledCommands, ctx, ct, "node.choice.onCanceled");
                                    if (!canceledHook.Success)
                                    {
                                        return canceledHook.Canceled
                                            ? ConversationNodeExecuteResult.FromCanceled(canceledHook.Message)
                                            : ConversationNodeExecuteResult.Failed(canceledHook.Message);
                                    }

                                    return ConversationNodeExecuteResult.FromCanceled(
                                        string.IsNullOrWhiteSpace(choiceResult.SourceResult.Message)
                                            ? $"[CONV-275] Choice canceled. nodeId={choiceNode.NodeId}"
                                            : choiceResult.SourceResult.Message);
                                }

                            case GridObjectChoiceCompletionKind.Timeout:
                                {
                                    var timeoutHook = await ExecuteBranchCommandsAsync(choiceNode.OnChoiceTimeoutCommands, ctx, ct, "node.choice.onTimeout");
                                    if (!timeoutHook.Success)
                                    {
                                        return timeoutHook.Canceled
                                            ? ConversationNodeExecuteResult.FromCanceled(timeoutHook.Message)
                                            : ConversationNodeExecuteResult.Failed(timeoutHook.Message);
                                    }

                                    return ConversationNodeExecuteResult.FromCanceled(
                                        string.IsNullOrWhiteSpace(choiceResult.SourceResult.Message)
                                            ? $"[CONV-276] Choice timeout. nodeId={choiceNode.NodeId}"
                                            : choiceResult.SourceResult.Message);
                                }

                            case GridObjectChoiceCompletionKind.Replaced:
                                {
                                    var replacedHook = await ExecuteBranchCommandsAsync(choiceNode.OnChoiceReplacedCommands, ctx, ct, "node.choice.onReplaced");
                                    if (!replacedHook.Success)
                                    {
                                        return replacedHook.Canceled
                                            ? ConversationNodeExecuteResult.FromCanceled(replacedHook.Message)
                                            : ConversationNodeExecuteResult.Failed(replacedHook.Message);
                                    }

                                    return ConversationNodeExecuteResult.FromCanceled(
                                        string.IsNullOrWhiteSpace(choiceResult.SourceResult.Message)
                                            ? $"[CONV-277] Choice replaced. nodeId={choiceNode.NodeId}"
                                            : choiceResult.SourceResult.Message);
                                }

                            default:
                                return ConversationNodeExecuteResult.Failed(
                                    string.IsNullOrWhiteSpace(choiceResult.SourceResult.Message)
                                        ? $"[CONV-278] Choice completion failed. nodeId={choiceNode.NodeId} completion={completion}"
                                        : choiceResult.SourceResult.Message);
                        }
                    }

                case ConversationIfNodePreset ifNode:
                    {
                        var conditionContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, ctx.Scope);
                        var condition = ifNode.ConditionValue.GetOrDefault(conditionContext, false);
                        if (!ifNode.TryResolveConditionJoint(condition, out var ifJoint) || ifJoint == null)
                            return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

                        var branchResult = await ExecuteBranchCommandsAsync(ifJoint.OnMatchedCommands, ctx, ct, "node.if.joint.onMatched");
                        if (!branchResult.Success)
                        {
                            return branchResult.Canceled
                                ? ConversationNodeExecuteResult.FromCanceled(branchResult.Message)
                                : ConversationNodeExecuteResult.Failed(branchResult.Message);
                        }

                        var ifNextNodeId = ifJoint.SelectedNextNodeId;
                        return ConversationNodeExecuteResult.FromSuccess(ifNextNodeId, hasNextNode: ifNextNodeId > 0);
                    }

                case ConversationSwitchNodePreset switchNode:
                    {
                        var switchContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, ctx.Scope);
                        if (!switchNode.TryResolveSwitchJoint(switchContext, out var switchJoint) || switchJoint == null)
                            return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

                        var branchResult = await ExecuteBranchCommandsAsync(switchJoint.OnMatchedCommands, ctx, ct, "node.switch.joint.onMatched");
                        if (!branchResult.Success)
                        {
                            return branchResult.Canceled
                                ? ConversationNodeExecuteResult.FromCanceled(branchResult.Message)
                                : ConversationNodeExecuteResult.Failed(branchResult.Message);
                        }

                        var switchNextNodeId = switchJoint.SelectedNextNodeId;
                        return ConversationNodeExecuteResult.FromSuccess(switchNextNodeId, hasNextNode: switchNextNodeId > 0);
                    }

                case ConversationJumpNodePreset jumpNode:
                    {
                        var jumpNextNodeId = ConversationNodeJointUtility.ResolveFirstConnectedNodeId(jumpNode.NextNodeJoints);
                        return ConversationNodeExecuteResult.FromSuccess(jumpNextNodeId, hasNextNode: jumpNextNodeId > 0);
                    }

                case ConversationCommandOnlyNodePreset commandOnlyNode:
                    {
                        var commandNextNodeId = ConversationNodeJointUtility.ResolveFirstConnectedNodeId(commandOnlyNode.NextNodeJoints);
                        return ConversationNodeExecuteResult.FromSuccess(commandNextNodeId, hasNextNode: commandNextNodeId > 0);
                    }

                default:
                    return ConversationNodeExecuteResult.Failed($"[CONV-279] Unsupported node preset type: {node.GetType().Name}");
            }
        }

        static DialogueMessageRequest BuildDialogueMessageRequest(
            ConversationMessageNodePreset messageNode,
            ConversationFlowSettingsPreset? settings)
        {
            var request = new DialogueMessageRequest();

            settings?.DefaultMessageDetailSettings?.ApplyTo(request);
            if (messageNode.OverrideDetailSettings)
                messageNode.DetailSettingsOverride?.ApplyTo(request);

            request.BodyLines.Clear();
            request.BodyLines.Add(new DialogueMessageLine
            {
                DialogueTag = "default",
                ChannelTag = "default",
                Text = messageNode.BodyText,
            });

            return request;
        }

        static async UniTask<ConversationNodeExecuteResult> ApplyMessageCharacterFrameAsync(
            ConversationMessageNodePreset messageNode,
            string dialogueTag,
            IDialogueService dialogue,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (messageNode.CharacterDataId <= 0)
                return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

            var frame = new DialogueCharacterFrameRequest
            {
                RefreshLayout = true,
            };

            var entry = new DialogueCharacterEntryRequest
            {
                CharacterId = ResolveSpeakerRuntimeKey(messageNode),
                CharacterDataId = messageNode.CharacterDataId,
                Anchor = ToDialogueCharacterAnchor(messageNode.Slot),
                ExpressionKey = messageNode.ExpressionKey,
                UseDefaultImageFallback = messageNode.UseDefaultImageFallback,
            };

            frame.Entries.Add(entry);
            var applied = await dialogue.ApplyCharactersAsync(dialogueTag, frame, ct);
            if (applied)
                return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

            return ConversationNodeExecuteResult.Failed($"[CONV-269] Character apply failed for message node. nodeId={messageNode.NodeId}");
        }

        static string ResolveSpeakerRuntimeKey(ConversationMessageNodePreset messageNode)
        {
            if (messageNode.CharacterDataId > 0)
                return $"character-db-{messageNode.CharacterDataId}";

            return "speaker";
        }

        static DialogueCharacterAnchor ToDialogueCharacterAnchor(ConversationCharacterSlot slot)
        {
            return slot switch
            {
                ConversationCharacterSlot.FarLeft => DialogueCharacterAnchor.Left,
                ConversationCharacterSlot.Left => DialogueCharacterAnchor.Left,
                ConversationCharacterSlot.MidLeft => DialogueCharacterAnchor.Left,
                ConversationCharacterSlot.Center => DialogueCharacterAnchor.Center,
                ConversationCharacterSlot.MidRight => DialogueCharacterAnchor.Right,
                ConversationCharacterSlot.Right => DialogueCharacterAnchor.Right,
                ConversationCharacterSlot.FarRight => DialogueCharacterAnchor.Right,
                _ => DialogueCharacterAnchor.None,
            };
        }

        static MessageHookSettings ResolveMessageHookSettings(
            ConversationFlowSettingsPreset? flowSettings,
            ConversationMessageNodePreset messageNode)
        {
            var defaultHooks = flowSettings?.DefaultMessageHooks;
            var nodeHooks = messageNode.MessageHooksOverride;

            var before = MergeMessageHookCommands(
                defaultHooks?.OnBeforeMessageCommands,
                nodeHooks?.OnBeforeMessageCommands,
                messageNode.OverrideMessageHooks,
                messageNode.MessageHookMergeMode);

            var after = MergeMessageHookCommands(
                defaultHooks?.OnAfterMessageCommands,
                nodeHooks?.OnAfterMessageCommands,
                messageNode.OverrideMessageHooks,
                messageNode.MessageHookMergeMode);

            return new MessageHookSettings(before, after);
        }

        static CommandListData? MergeMessageHookCommands(
            CommandListData? defaults,
            CommandListData? overrides,
            bool useOverride,
            ConversationMessageHookMergeMode mode)
        {
            if (!useOverride || overrides == null || overrides.Count == 0)
                return defaults;

            var merged = defaults?.CreateRuntimeCopy() ?? new CommandListData();
            var operation = mode == ConversationMessageHookMergeMode.Override
                ? CommandListMutationOperation.Override
                : CommandListMutationOperation.Append;
            merged.ApplyRuntimeMutation(operation, overrides);
            return merged;
        }

        readonly struct MessageHookSettings
        {
            public CommandListData? BeforeCommands { get; }
            public CommandListData? AfterCommands { get; }

            public MessageHookSettings(CommandListData? beforeCommands, CommandListData? afterCommands)
            {
                BeforeCommands = beforeCommands;
                AfterCommands = afterCommands;
            }
        }

        static void WriteSelectedIndexToVarsIfNeeded(ConversationChoiceNodePreset node, int selectedIndex, CommandContext ctx)
        {
            if (!node.WriteSelectedIndexToVars)
                return;

            var varId = ConversationExecutorUtility.ResolveVarId(node.SelectedIndexVar, 0);
            if (varId <= 0)
                return;

            var destination = ctx.Vars ?? NullVarStore.Instance;
            destination.TrySetVariant(varId, DynamicVariant.FromInt(selectedIndex));
        }

        static void WriteChoiceDisplayNameToVars(GridObjectChoiceEntry? entry, CommandContext ctx)
        {
            if (entry == null)
                return;

            var destination = ctx.Vars ?? NullVarStore.Instance;
            destination.TrySetVariant(
                VarIds.GameLib.UI.DialogueChannel.Choice.DisplayName,
                DynamicVariant.FromString(entry.DisplayName));
        }

        static async UniTask<ConversationNodeExecuteResult> ApplySelectedEntryAsync(
            DialogueChoiceRequest choiceRequest,
            int selectedIndex,
            CommandContext ctx,
            CancellationToken ct)
        {
            var gridRequest = choiceRequest?.GridChoiceRequest;
            if (gridRequest == null || gridRequest.Entries == null)
                return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

            if (selectedIndex >= gridRequest.Entries.Count)
                return ConversationNodeExecuteResult.Failed($"[CONV-280] Selected index is out of range. index={selectedIndex} count={gridRequest.Entries.Count}");

            var selectedEntry = gridRequest.Entries[selectedIndex];
            if (selectedEntry == null)
                return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);

            var destination = ctx.Vars ?? NullVarStore.Instance;
            WriteChoiceDisplayNameToVars(selectedEntry, ctx);
            selectedEntry.SelectedVars?.ApplyTo(destination, ctx, overwrite: true);
            WriteChoiceDisplayNameToVars(selectedEntry, ctx);

            var selectedResult = await ExecuteBranchCommandsAsync(selectedEntry.SelectedCommands, ctx, ct, "choice.entry.onSelected");
            if (!selectedResult.Success)
            {
                return selectedResult.Canceled
                    ? ConversationNodeExecuteResult.FromCanceled(selectedResult.Message)
                    : ConversationNodeExecuteResult.Failed(selectedResult.Message);
            }

            return ConversationNodeExecuteResult.FromSuccess(0, hasNextNode: false);
        }

        static bool TryResolveDialogueTag(IConversationRuntimeSession session, ConversationCharacterSlot slot, string? overrideTag, out string dialogueTag)
        {
            var normalizedOverride = ConversationTagUtility.NormalizeNullable(overrideTag);
            if (!string.IsNullOrEmpty(normalizedOverride))
            {
                dialogueTag = normalizedOverride;
                return true;
            }

            if (session.TryResolveDialogueTag(slot, out dialogueTag))
                return !string.IsNullOrWhiteSpace(dialogueTag);

            dialogueTag = string.Empty;
            return false;
        }

        static async UniTask<ConversationBranchResult> ExecuteFlowEndHookAsync(
            ConversationFlowHookPreset hooks,
            ConversationSessionEndKind endKind,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (hooks == null)
                return ConversationBranchResult.SuccessResult;

            var commands = endKind switch
            {
                ConversationSessionEndKind.Completed => hooks.OnCompletedCommands,
                ConversationSessionEndKind.Canceled => hooks.OnCanceledCommands,
                ConversationSessionEndKind.Failed => hooks.OnFailedCommands,
                _ => null,
            };

            if (commands == null)
                return ConversationBranchResult.SuccessResult;

            return await ExecuteBranchCommandsAsync(commands, ctx, ct, $"flow.end.{endKind}");
        }

        static async UniTask<ConversationBranchResult> ExecuteBranchCommandsAsync(
            CommandListData? commands,
            CommandContext ctx,
            CancellationToken ct,
            string branch)
        {
            if (commands == null || commands.Count == 0)
                return ConversationBranchResult.SuccessResult;

            if (ctx.Runner == null)
                return ConversationBranchResult.SuccessResult;

            var runResult = await ctx.Runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            if (runResult.Status == CommandRunStatus.Canceled)
                return ConversationBranchResult.CanceledResult($"[CONV-290] Branch canceled. branch='{branch}'");

            if (runResult.Status == CommandRunStatus.Error)
                return ConversationBranchResult.FailedResult($"[CONV-291] Branch failed. branch='{branch}' message={runResult.Message}");

            return ConversationBranchResult.SuccessResult;
        }

        static CommandRunFailureKind ToFailureKind(ConversationSessionEndKind endKind)
        {
            return endKind switch
            {
                ConversationSessionEndKind.Canceled => CommandRunFailureKind.Canceled,
                ConversationSessionEndKind.Failed => CommandRunFailureKind.ResolveFailed,
                _ => CommandRunFailureKind.None,
            };
        }

        static async UniTask<bool> TryCloseDialogueAsync(
            IConversationRuntimeSession session,
            IDialogueService dialogue,
            CancellationToken ct)
        {
            if (session == null || dialogue == null)
                return false;

            if (!dialogue.TryGetSnapshot(session.DialogueChannelTag, out var snapshot))
                return false;

            if (!snapshot.IsVisible && !snapshot.IsActive)
                return true;

            return await dialogue.EndAsync(session.DialogueChannelTag, new DialogueEndRequest(), ct);
        }

        static void EnsureStrict(bool strict, bool success, CommandRunFailureKind kind, string message)
        {
            if (!strict || success)
                return;

            throw new CommandExecutionException(kind, message);
        }

        readonly struct ConversationRunOutcome
        {
            public ConversationSessionEndKind EndKind { get; }
            public string Message { get; }

            ConversationRunOutcome(ConversationSessionEndKind endKind, string message)
            {
                EndKind = endKind;
                Message = message ?? string.Empty;
            }

            public static ConversationRunOutcome Completed() => new(ConversationSessionEndKind.Completed, string.Empty);
            public static ConversationRunOutcome Failed(string message) => new(ConversationSessionEndKind.Failed, message);
            public static ConversationRunOutcome Canceled(string message) => new(ConversationSessionEndKind.Canceled, message);
        }

        readonly struct ConversationNodeExecuteResult
        {
            public bool Success { get; }
            public bool Canceled { get; }
            public string Message { get; }
            public int NextNodeId { get; }

            ConversationNodeExecuteResult(bool success, bool canceled, string message, int nextNodeId)
            {
                Success = success;
                Canceled = canceled;
                Message = message ?? string.Empty;
                NextNodeId = nextNodeId;
            }

            public static ConversationNodeExecuteResult FromSuccess(int nextNodeId, bool hasNextNode = true)
                => new(true, false, string.Empty, hasNextNode ? nextNodeId : 0);

            public static ConversationNodeExecuteResult Failed(string message)
                => new(false, false, message, 0);

            public static ConversationNodeExecuteResult FromCanceled(string message)
                => new(false, true, message, 0);
        }

        readonly struct ConversationBranchResult
        {
            public static readonly ConversationBranchResult SuccessResult = new(true, false, string.Empty);

            public bool Success { get; }
            public bool Canceled { get; }
            public string Message { get; }

            ConversationBranchResult(bool success, bool canceled, string message)
            {
                Success = success;
                Canceled = canceled;
                Message = message ?? string.Empty;
            }

            public static ConversationBranchResult FailedResult(string message) => new(false, false, message);
            public static ConversationBranchResult CanceledResult(string message) => new(false, true, message);
        }
    }

    public sealed class ConversationInFlowExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ConversationInFlow;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ConversationInFlowCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ConversationInFlowCommandData is required.");

            var targetScope = await ConversationExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            ConversationExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!ConversationExecutorUtility.TryResolve(targetScope, out IConversationChannelHubService? hub) || hub == null)
            {
                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, "IConversationChannelHubService is missing on target scope.");
                return;
            }

            var tag = typed.NormalizedConversationTag;
            if (!hub.TryGetSession(tag, out var session) || session == null || !session.IsActive)
            {
                EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, $"[CONV-300] Active conversation session was not found. tag='{tag}'");
                return;
            }

            switch (typed.Operation)
            {
                case ConversationInFlowOperation.ShowMessage:
                    {
                        var (dialogue, dialogueError) = await ConversationExecutorUtility.ResolveDialogueServiceAsync(session, targetScope, ctx, ct);
                        if (dialogue == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, dialogueError);
                            return;
                        }

                        var request = ResolveMessageRequest(typed, session, out var slot, out var overrideTag);
                        if (!TryResolveDialogueTag(session, slot, overrideTag, out var dialogueTag))
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, "[CONV-301] Dialogue tag resolve failed for ShowMessage.");
                            return;
                        }

                        var result = await dialogue.ShowMessageAsync(dialogueTag, request, ct);
                        EnsureStrict(
                            typed.Strict,
                            result.Success,
                            CommandRunFailureKind.ResolveFailed,
                            string.IsNullOrWhiteSpace(result.Message) ? "[CONV-302] ShowMessage failed." : result.Message);
                        return;
                    }

                case ConversationInFlowOperation.ShowChoiceAndWait:
                    {
                        var (dialogue, dialogueError) = await ConversationExecutorUtility.ResolveDialogueServiceAsync(session, targetScope, ctx, ct);
                        if (dialogue == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, dialogueError);
                            return;
                        }

                        var request = ResolveChoiceRequest(typed, session, out var slot, out var overrideTag);
                        if (!TryResolveDialogueTag(session, slot, overrideTag, out var dialogueTag))
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, "[CONV-303] Dialogue tag resolve failed for ShowChoiceAndWait.");
                            return;
                        }

                        var result = await dialogue.ShowChoiceAndWaitAsync(dialogueTag, request, ct);
                        EnsureStrict(
                            typed.Strict,
                            result.Success,
                            CommandRunFailureKind.ResolveFailed,
                            string.IsNullOrWhiteSpace(result.Message) ? "[CONV-304] ShowChoiceAndWait failed." : result.Message);

                        if (!result.Success)
                            return;

                        var completion = result.SourceResult.CompletionKind;
                        if (completion == GridObjectChoiceCompletionKind.Selected)
                        {
                            var selectedIndex = result.SourceResult.SelectedIndex;
                            var nodeId = session.CurrentNodeId;
                            session.RecordChoice(nodeId, selectedIndex, DateTime.UtcNow.Ticks);

                            if (typed.WriteSelectedIndexToVars)
                                WriteSelectedIndexToVars(typed.SelectedIndexVar, selectedIndex, ctx);

                            await ApplySelectedEntryAsync(request, selectedIndex, ctx, ct);
                            return;
                        }

                        if (typed.FailWhenChoiceNotSelected)
                        {
                            EnsureStrict(
                                typed.Strict,
                                false,
                                CommandRunFailureKind.ResolveFailed,
                                $"[CONV-305] Choice completed without selection. completion={completion} message={result.SourceResult.Message}");
                        }

                        return;
                    }

                case ConversationInFlowOperation.JumpToNode:
                    {
                        var moved = session.TrySetCurrentNode(typed.JumpNodeId);
                        EnsureStrict(typed.Strict, moved, CommandRunFailureKind.ResolveFailed, $"[CONV-306] JumpToNode failed. nodeId={typed.JumpNodeId}");
                        return;
                    }

                case ConversationInFlowOperation.WriteSnapshotToVars:
                    {
                        var snapshot = session.Snapshot;
                        WriteIntToVars(typed.CurrentNodeIdVar, snapshot.CurrentNodeId, ctx);
                        WriteIntToVars(typed.TurnCountVar, snapshot.TurnCount, ctx);
                        WriteIntToVars(typed.LastSelectedIndexVar, snapshot.LastSelectedIndex, ctx);
                        return;
                    }

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported Conversation in-flow operation: {typed.Operation}");
            }
        }

        static DialogueMessageRequest ResolveMessageRequest(
            ConversationInFlowCommandData typed,
            IConversationRuntimeSession session,
            out ConversationCharacterSlot slot,
            out string? overrideTag)
        {
            if (typed.UseCurrentNodeRequest &&
                session.TryGetCurrentNode(out var node) &&
                node is ConversationMessageNodePreset messageNode)
            {
                slot = messageNode.Slot;
                overrideTag = messageNode.DialogueTagOverride;

                var request = new DialogueMessageRequest();
                session.FlowPreset.Settings?.DefaultMessageDetailSettings?.ApplyTo(request);
                if (messageNode.OverrideDetailSettings)
                    messageNode.DetailSettingsOverride?.ApplyTo(request);

                request.BodyLines.Clear();
                request.BodyLines.Add(new DialogueMessageLine
                {
                    DialogueTag = "default",
                    ChannelTag = "default",
                    Text = messageNode.BodyText,
                });

                return request;
            }

            slot = typed.Slot;
            overrideTag = typed.DialogueTagOverride;
            return typed.MessageRequest?.CreateRuntimeCopy() ?? new DialogueMessageRequest();
        }

        static DialogueChoiceRequest ResolveChoiceRequest(
            ConversationInFlowCommandData typed,
            IConversationRuntimeSession session,
            out ConversationCharacterSlot slot,
            out string? overrideTag)
        {
            if (typed.UseCurrentNodeRequest &&
                session.TryGetCurrentNode(out var node) &&
                node is ConversationChoiceNodePreset choiceNode)
            {
                slot = choiceNode.Slot;
                overrideTag = choiceNode.DialogueTagOverride;
                return choiceNode.ChoiceRequest?.CreateRuntimeCopy() ?? new DialogueChoiceRequest();
            }

            slot = typed.Slot;
            overrideTag = typed.DialogueTagOverride;
            return typed.ChoiceRequest?.CreateRuntimeCopy() ?? new DialogueChoiceRequest();
        }

        static void WriteSelectedIndexToVars(VarKeyRef keyRef, int value, CommandContext ctx)
        {
            WriteIntToVars(keyRef, value, ctx);
        }

        static void WriteChoiceDisplayNameToVars(GridObjectChoiceEntry? entry, CommandContext ctx)
        {
            if (entry == null)
                return;

            var destination = ctx.Vars ?? NullVarStore.Instance;
            destination.TrySetVariant(
                VarIds.GameLib.UI.DialogueChannel.Choice.DisplayName,
                DynamicVariant.FromString(entry.DisplayName));
        }

        static async UniTask ApplySelectedEntryAsync(
            DialogueChoiceRequest choiceRequest,
            int selectedIndex,
            CommandContext ctx,
            CancellationToken ct)
        {
            var gridRequest = choiceRequest?.GridChoiceRequest;
            if (gridRequest == null || gridRequest.Entries == null)
                return;

            if (selectedIndex < 0 || selectedIndex >= gridRequest.Entries.Count)
                return;

            var selectedEntry = gridRequest.Entries[selectedIndex];
            if (selectedEntry == null)
                return;

            var destination = ctx.Vars ?? NullVarStore.Instance;
            WriteChoiceDisplayNameToVars(selectedEntry, ctx);
            selectedEntry.SelectedVars?.ApplyTo(destination, ctx, overwrite: true);
            WriteChoiceDisplayNameToVars(selectedEntry, ctx);

            if (selectedEntry.SelectedCommands == null || selectedEntry.SelectedCommands.Count == 0 || ctx.Runner == null)
                return;

            var runResult = await ctx.Runner.ExecuteListAsync(selectedEntry.SelectedCommands, ctx, ct, ctx.Options);
            if (runResult.Status == CommandRunStatus.Canceled)
                throw new CommandExecutionException(CommandRunFailureKind.Canceled, "[CONV-307] Selected entry commands were canceled.");

            if (runResult.Status == CommandRunStatus.Error)
            {
                throw new CommandExecutionException(
                    runResult.FailureKind,
                    $"[CONV-308] Selected entry commands failed. message={runResult.Message}");
            }
        }

        static void WriteIntToVars(VarKeyRef keyRef, int value, CommandContext ctx)
        {
            var varId = ConversationExecutorUtility.ResolveVarId(keyRef, 0);
            if (varId <= 0)
                return;

            var destination = ctx.Vars ?? NullVarStore.Instance;
            destination.TrySetVariant(varId, DynamicVariant.FromInt(value));
        }

        static bool TryResolveDialogueTag(IConversationRuntimeSession session, ConversationCharacterSlot slot, string? overrideTag, out string dialogueTag)
        {
            var normalizedOverride = ConversationTagUtility.NormalizeNullable(overrideTag);
            if (!string.IsNullOrEmpty(normalizedOverride))
            {
                dialogueTag = normalizedOverride;
                return true;
            }

            if (session.TryResolveDialogueTag(slot, out dialogueTag))
                return !string.IsNullOrWhiteSpace(dialogueTag);

            dialogueTag = string.Empty;
            return false;
        }

        static void EnsureStrict(bool strict, bool success, CommandRunFailureKind kind, string message)
        {
            if (!strict || success)
                return;

            throw new CommandExecutionException(kind, message);
        }
    }

    static class ConversationExecutorUtility
    {
        public static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[ConversationExecutor] Target resolve failed: {error} Falling back to current scope.");
                return ctx.Scope;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        public static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }

        public static async UniTask<(IDialogueService? dialogue, string error)> ResolveDialogueServiceAsync(
            IConversationRuntimeSession session,
            IScopeNode targetScope,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (session == null)
                return (null, "[CONV-239] Conversation session is missing.");

            var dialogueContext = new CommandContext(
                targetScope,
                ctx.Vars ?? NullVarStore.Instance,
                ctx.Runner,
                actor: targetScope,
                options: ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.CallerActor,
                sourceContext: ctx);

            var (dialogueScope, scopeError) = await ActorScopeResolver.ResolveAsync(session.DialogueChannelSource, dialogueContext, ct);
            if (dialogueScope == null)
            {
                var message = string.IsNullOrWhiteSpace(scopeError)
                    ? $"[CONV-240] Dialogue channel scope was not found. source={session.DialogueChannelSource.Kind} tag='{session.DialogueChannelTag}'"
                    : scopeError;
                return (null, message);
            }

            if (!TryResolve(dialogueScope, out IDialogueService? dialogue) || dialogue == null)
            {
                return (null, $"[CONV-241] IDialogueService is missing on dialogue channel scope. source={session.DialogueChannelSource.Kind} tag='{session.DialogueChannelTag}'");
            }

            return (dialogue, string.Empty);
        }

        public static int ResolveVarId(VarKeyRef keyRef, int fallback)
        {
            if (!string.IsNullOrEmpty(keyRef.StableKey) && VarIdResolver.TryResolve(keyRef.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return keyRef.VarId > 0 ? keyRef.VarId : fallback;
        }

        static bool AllowFallback(CommandRunOptions options)
        {
            if (!options.AllowActorFallback)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }
    }
}
