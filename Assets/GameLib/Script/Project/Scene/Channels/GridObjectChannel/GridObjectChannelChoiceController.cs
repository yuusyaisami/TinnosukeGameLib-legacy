#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class GridObjectChannelChoiceController
    {
        sealed class ActiveChoiceSession
        {
            public readonly CancellationTokenSource CancelSource = new();
            public readonly UniTaskCompletionSource<GridObjectChoiceSessionResult> Completion = new();
            public GridObjectChoiceSessionResult CancelResult = GridObjectChoiceSessionResult.Canceled("Choice canceled.");
        }

        sealed class ChoiceOutputSubscription : IDisposable
        {
            readonly IButtonChannelOutput _output;
            readonly Action<ButtonChannelOutputSnapshot> _handler;

            public ChoiceOutputSubscription(
                int listIndex,
                IButtonChannelOutput output,
                Action<ButtonChannelOutputSnapshot> handler)
            {
                ListIndex = listIndex;
                _output = output;
                _handler = handler;
                HasLastPhase = true;
                LastPhase = output.Phase;
                _output.OnUpdated += _handler;
            }

            public int ListIndex { get; }
            public bool HasLastPhase { get; private set; }
            public ButtonChannelPhase LastPhase { get; private set; }

            public bool IsPhaseTransition(ButtonChannelPhase next)
            {
                var transitioned = !HasLastPhase || LastPhase != next;
                LastPhase = next;
                HasLastPhase = true;
                return transitioned;
            }

            public void Dispose()
            {
                _output.OnUpdated -= _handler;
            }
        }

        readonly string _tag;
        readonly GridObjectChannelRuntimeState _state;
        readonly Func<GridObjectChannelBindRequest, CancellationToken, UniTask<bool>> _bindAsync;
        readonly Func<bool, CancellationToken, UniTask<bool>> _clearAsync;
        readonly Func<GridObjectChoiceWaitOptions, float> _resolveTimeoutSeconds;
        readonly SemaphoreSlim _choiceSessionGate = new(1, 1);

        ActiveChoiceSession? _activeChoiceSession;

        public GridObjectChannelChoiceController(
            string tag,
            GridObjectChannelRuntimeState state,
            Func<GridObjectChannelBindRequest, CancellationToken, UniTask<bool>> bindAsync,
            Func<bool, CancellationToken, UniTask<bool>> clearAsync,
            Func<GridObjectChoiceWaitOptions, float> resolveTimeoutSeconds)
        {
            _tag = tag;
            _state = state;
            _bindAsync = bindAsync;
            _clearAsync = clearAsync;
            _resolveTimeoutSeconds = resolveTimeoutSeconds;
        }

        public bool IsChoiceSessionActive => _activeChoiceSession != null;

        public bool TryCancelActiveChoice(string reason = "")
        {
            return TryCancelActiveChoiceInternal(replaced: false, reason);
        }

        public bool TryReplaceActiveChoice(string reason = "")
        {
            return TryCancelActiveChoiceInternal(replaced: true, reason);
        }

        public async UniTask<GridObjectChoiceSessionResult> ShowChoiceAndWaitAsync(
            GridObjectChoiceRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return GridObjectChoiceSessionResult.Failed("[GOC-CHOICE-000] Choice request is null.");

            var runtimeRequest = request.CreateRuntimeCopy();
            if (runtimeRequest.Entries == null || runtimeRequest.Entries.Count == 0)
                return GridObjectChoiceSessionResult.Failed("[GOC-CHOICE-000] Choice entries are empty.");

            if (_state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Choice request received. Tag='{_tag}' Entries={runtimeRequest.Entries.Count} " +
                    $"BindSpawnCommands={runtimeRequest.BindRequest?.SpawnCommands?.Count ?? 0} " +
                    $"OverridePlayer={runtimeRequest.BindRequest?.OverridePlayerPreset} OverrideLayout={runtimeRequest.BindRequest?.OverrideLayoutPreset} " +
                    $"OverrideVisualizer={runtimeRequest.BindRequest?.OverrideVisualizerPreset} ForceChoiceCompatible={runtimeRequest.BindRequest?.ForceChoiceCompatible}",
                    _state.ListRoot);

                for (var i = 0; i < runtimeRequest.Entries.Count; i++)
                {
                    var entry = runtimeRequest.Entries[i];
                    if (entry == null)
                        continue;

                    Debug.Log(
                        $"[GridObjectChannel] Choice entry[{i}]. Tag='{_tag}' DisplayName='{entry.DisplayName}' " +
                        $"SpawnCommands={entry.SpawnCommands?.Count ?? 0} SelectedCommands={entry.SelectedCommands?.Count ?? 0} " +
                        $"SelectedVars={entry.SelectedVars.Entries?.Count ?? 0}",
                        _state.ListRoot);
                }
            }

            var waitOptions = runtimeRequest.WaitOptions?.CreateRuntimeCopy() ?? new GridObjectChoiceWaitOptions();
            ActiveChoiceSession? sessionToRun = null;

            await _choiceSessionGate.WaitAsync(ct);
            try
            {
                if (!_state.IsActive || _state.ActiveScope == null)
                    return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-001] GridObjectChannel is inactive. tag='{_tag}'");

                if (_activeChoiceSession != null)
                {
                    switch (waitOptions.ConcurrencyPolicy)
                    {
                        case GridObjectChoiceConcurrencyPolicy.ErrorIfActive:
                            Debug.LogError($"[GridObjectChannel] Choice session conflict. tag='{_tag}' policy={waitOptions.ConcurrencyPolicy}");
                            return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-004] Choice session is already active. tag='{_tag}'");

                        case GridObjectChoiceConcurrencyPolicy.CancelAndReplace:
                            Debug.LogWarning($"[GridObjectChannel] Choice session replaced. tag='{_tag}'");
                            TryCancelActiveChoiceInternal(replaced: true, $"[GOC-CHOICE-009] Replaced by another choice request. tag='{_tag}'");
                            break;

                        case GridObjectChoiceConcurrencyPolicy.Queue:
                            break;

                        default:
                            return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-004] Unsupported concurrency policy: {waitOptions.ConcurrencyPolicy}");
                    }

                    while (_activeChoiceSession != null)
                        await UniTask.DelayFrame(1, cancellationToken: ct);
                }

                sessionToRun = new ActiveChoiceSession();
                _activeChoiceSession = sessionToRun;
            }
            finally
            {
                _choiceSessionGate.Release();
            }

            return await RunChoiceSessionAsync(sessionToRun, runtimeRequest, waitOptions, ct);
        }

        async UniTask<GridObjectChoiceSessionResult> RunChoiceSessionAsync(
            ActiveChoiceSession session,
            GridObjectChoiceRequest runtimeRequest,
            GridObjectChoiceWaitOptions waitOptions,
            CancellationToken ct)
        {
            CancellationTokenSource? linkedLifecycleCts = null;
            CancellationTokenSource? sessionLinkedCts = null;
            List<ChoiceOutputSubscription>? subscriptions = null;

            try
            {
                linkedLifecycleCts = GridObjectChannelRuntimeUtility.CreateLinkedTokenSource(_state.LifecycleCts, ct);
                sessionLinkedCts = linkedLifecycleCts != null
                    ? CancellationTokenSource.CreateLinkedTokenSource(linkedLifecycleCts.Token, session.CancelSource.Token)
                    : CancellationTokenSource.CreateLinkedTokenSource(ct, session.CancelSource.Token);

                var sessionToken = sessionLinkedCts.Token;
                using var cancelRegistration = sessionToken.Register(() => session.Completion.TrySetResult(session.CancelResult));

                _state.ActiveChoiceEntries = runtimeRequest.Entries;

                var bindRequest = GridObjectChannelChoiceBindBuilder.Build(runtimeRequest);

                if (_state.EnableVerboseLayoutLog)
                {
                    Debug.Log(
                        $"[GridObjectChannel] Choice bind prepared. Tag='{_tag}' Entries={runtimeRequest.Entries.Count} " +
                        $"BindSpawnCommands={bindRequest.SpawnCommands?.Count ?? 0} OverrideLayout={bindRequest.OverrideLayoutPreset} " +
                        $"LayoutPreset={(bindRequest.OverrideLayoutPreset ? "bind-override" : "definition")}",
                        _state.ListRoot);
                }

                var bindSucceeded = await _bindAsync(bindRequest, sessionToken);
                if (!bindSucceeded)
                    return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-001] Choice bind failed. tag='{_tag}'");

                if (!_state.ResolvedVisualizerPreset.EnableChoiceInput)
                {
                    Debug.LogError($"[GridObjectChannel] Choice requested with non-choice visualizer. tag='{_tag}'");
                    return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-002] Visualizer preset is not choice-compatible. tag='{_tag}'");
                }

                subscriptions = BuildChoiceSubscriptions(runtimeRequest, session);
                if (subscriptions.Count == 0)
                {
                    Debug.LogError($"[GridObjectChannel] Choice button outputs were not found. tag='{_tag}' buttonTag='{_state.ResolvedVisualizerPreset.ChoiceButtonChannelTag}'");
                    return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-003] ButtonChannel output not found. tag='{_tag}' buttonTag='{_state.ResolvedVisualizerPreset.ChoiceButtonChannelTag}'");
                }

                var completionTask = session.Completion.Task.AsTask();
                var timeoutSeconds = _resolveTimeoutSeconds(waitOptions);
                if (timeoutSeconds > 0f)
                {
                    var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken: CancellationToken.None).AsTask();
                    var winner = await Task.WhenAny(completionTask, timeoutTask);
                    if (winner == timeoutTask)
                    {
                        Debug.LogWarning($"[GridObjectChannel] Choice timed out. tag='{_tag}' timeout={timeoutSeconds:0.###}");
                        session.Completion.TrySetResult(GridObjectChoiceSessionResult.Timeout($"[GOC-CHOICE-005] Choice timed out. tag='{_tag}' timeout={timeoutSeconds:0.###}"));
                    }
                }

                var result = await completionTask;
                if (result.CompletionKind == GridObjectChoiceCompletionKind.Canceled && !waitOptions.AllowCancel)
                    return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-006] Choice cancel is not allowed. tag='{_tag}'");

                return result;
            }
            finally
            {
                if (subscriptions != null)
                {
                    for (var i = 0; i < subscriptions.Count; i++)
                        subscriptions[i].Dispose();
                }

                if (!waitOptions.KeepAliveAfterCompletion)
                {
                    try
                    {
                        await _clearAsync(false, CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GridObjectChannel] Choice cleanup failed. Tag='{_tag}' Message={ex.Message}");
                    }
                }

                _state.ActiveChoiceEntries = null;
                if (ReferenceEquals(_activeChoiceSession, session))
                    _activeChoiceSession = null;

                sessionLinkedCts?.Dispose();
                linkedLifecycleCts?.Dispose();
                session.CancelSource.Dispose();
            }
        }

        List<ChoiceOutputSubscription> BuildChoiceSubscriptions(
            GridObjectChoiceRequest request,
            ActiveChoiceSession session)
        {
            var subscriptions = new List<ChoiceOutputSubscription>(_state.Visuals.Count);

            for (var i = 0; i < _state.Visuals.Count; i++)
            {
                var instance = _state.Visuals[i];
                if (instance == null)
                    continue;

                if (!GridObjectChannelRuntimeUtility.TryResolveFromScopeOrAncestors<IButtonChannelHubService>(instance.Scope, out var buttonHub) ||
                    buttonHub == null)
                {
                    continue;
                }

                if (!buttonHub.TryGetOutput(_state.ResolvedVisualizerPreset.ChoiceButtonChannelTag, out var output) || output == null)
                    continue;

                ChoiceOutputSubscription? subscription = null;
                subscription = new ChoiceOutputSubscription(
                    instance.ListIndex,
                    output,
                    snapshot => HandleChoiceOutputUpdated(subscription, snapshot, request, session));
                subscriptions.Add(subscription);
            }

            return subscriptions;
        }

        void HandleChoiceOutputUpdated(
            ChoiceOutputSubscription? subscription,
            ButtonChannelOutputSnapshot snapshot,
            GridObjectChoiceRequest request,
            ActiveChoiceSession session)
        {
            if (subscription == null)
                return;

            if (!ReferenceEquals(_activeChoiceSession, session))
                return;

            var phaseTransitioned = subscription.IsPhaseTransition(snapshot.Phase);
            if (_state.ResolvedVisualizerPreset.ChoiceRequirePhaseTransition && !phaseTransitioned)
                return;

            if (!_state.ResolvedVisualizerPreset.IsChoiceDecisionPhase(snapshot.Phase))
                return;

            var selectedIndex = subscription.ListIndex;
            if (selectedIndex < 0 || request.Entries == null || selectedIndex >= request.Entries.Count)
            {
                session.Completion.TrySetResult(GridObjectChoiceSessionResult.Failed(
                    $"[GOC-CHOICE-007] Selected index out of range. tag='{_tag}' index={selectedIndex}"));
                return;
            }

            session.Completion.TrySetResult(GridObjectChoiceSessionResult.Selected(selectedIndex, snapshot.Phase));
        }

        bool TryCancelActiveChoiceInternal(bool replaced, string reason)
        {
            var session = _activeChoiceSession;
            if (session == null)
                return false;

            session.CancelResult = replaced
                ? GridObjectChoiceSessionResult.Replaced(reason)
                : GridObjectChoiceSessionResult.Canceled(reason);

            if (!session.CancelSource.IsCancellationRequested)
                session.CancelSource.Cancel();

            return true;
        }
    }
}
