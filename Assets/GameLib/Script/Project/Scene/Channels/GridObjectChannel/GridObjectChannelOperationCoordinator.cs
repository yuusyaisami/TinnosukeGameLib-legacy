#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Channel
{
    internal readonly struct GridObjectChannelOperationLockState
    {
        public GridObjectChannelOperationLockState(bool entered, int previousStamp, int currentStamp)
        {
            Entered = entered;
            PreviousStamp = previousStamp;
            CurrentStamp = currentStamp;
        }

        public bool Entered { get; }
        public int PreviousStamp { get; }
        public int CurrentStamp { get; }
    }

    internal sealed class GridObjectChannelOperationCoordinator
    {
        readonly string _tag;
        readonly SemaphoreSlim _mutex = new(1, 1);

        bool _queueWorkerActive;
        bool _refreshQueued;
        GridObjectChannelRefreshMode _queuedRefreshMode;
        bool _deferredClearRequested;
        bool _deferredClearKeepBinding = true;
        int _activeOperationStamp;
        int _operationStampSeed;

        public GridObjectChannelOperationCoordinator(string tag)
        {
            _tag = tag;
        }

        public async UniTask<GridObjectChannelOperationLockState> TryEnterAsync(
            GridObjectChannelRuntimeState state,
            CancellationToken ct,
            string operationName,
            bool reentrantIsError = true)
        {
            if (IsReentrantOperationCall(state))
            {
                var message = $"[GridObjectChannel] Re-entrant '{operationName}' was blocked to avoid deadlock. Tag='{_tag}'";
                if (reentrantIsError)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
                return new GridObjectChannelOperationLockState(false, 0, 0);
            }

            await _mutex.WaitAsync(ct);

            var previousStamp = state.OperationContextStamp.Value;
            var currentStamp = Interlocked.Increment(ref _operationStampSeed);
            state.OperationContextStamp.Value = currentStamp;
            Volatile.Write(ref _activeOperationStamp, currentStamp);
            return new GridObjectChannelOperationLockState(true, previousStamp, currentStamp);
        }

        public void Exit(GridObjectChannelRuntimeState state, GridObjectChannelOperationLockState lockState)
        {
            if (lockState.CurrentStamp != 0 && Volatile.Read(ref _activeOperationStamp) == lockState.CurrentStamp)
                Volatile.Write(ref _activeOperationStamp, 0);

            state.OperationContextStamp.Value = lockState.PreviousStamp;
            _mutex.Release();
        }

        public void ResetQueueState()
        {
            _queueWorkerActive = false;
            _refreshQueued = false;
            _queuedRefreshMode = GridObjectChannelRefreshMode.Incremental;
            _deferredClearRequested = false;
            _deferredClearKeepBinding = true;
        }

        public void RequestDeferredClear(bool keepBinding)
        {
            _deferredClearRequested = true;
            if (!keepBinding)
                _deferredClearKeepBinding = false;
        }

        public async UniTask FlushDeferredClearRequestsInsideLockAsync(Func<bool, CancellationToken, UniTask> clearAsync, CancellationToken ct)
        {
            const int maxFlushCount = 4;
            var flushCount = 0;

            while (_deferredClearRequested && flushCount < maxFlushCount)
            {
                var keepBinding = _deferredClearKeepBinding;
                _deferredClearRequested = false;
                _deferredClearKeepBinding = true;
                await clearAsync(keepBinding, ct);
                flushCount++;
            }

            if (!_deferredClearRequested)
                return;

            Debug.LogWarning($"[GridObjectChannel] Deferred clear was dropped after max flush count. Tag='{_tag}'");
            _deferredClearRequested = false;
            _deferredClearKeepBinding = true;
        }

        public void QueueRefresh(
            GridObjectChannelRuntimeState state,
            GridObjectChannelRefreshMode mode,
            Func<GridObjectChannelRefreshMode, CancellationToken, UniTask> refreshAsync)
        {
            if (!state.IsActive || !state.HasBinding)
                return;

            _queuedRefreshMode = _refreshQueued ? CombineRefreshModes(_queuedRefreshMode, mode) : mode;
            _refreshQueued = true;
            if (_queueWorkerActive)
                return;

            _queueWorkerActive = true;
            UniTask.Void(async () =>
            {
                try
                {
                    while (state.IsActive && state.HasBinding)
                    {
                        if (!_refreshQueued)
                            break;

                        var modeToRun = _queuedRefreshMode;
                        _refreshQueued = false;
                        var debounceFrames = Mathf.Max(0, state.ItemSourceRuntime?.DebounceFrames ?? state.ResolvedPlayerPreset.DebounceFrames);
                        if (debounceFrames > 0)
                            await UniTask.DelayFrame(debounceFrames, cancellationToken: state.LifecycleCts?.Token ?? CancellationToken.None);

                        await refreshAsync(modeToRun, CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GridObjectChannel] Queued refresh failed. Tag='{_tag}' Message={ex.Message}");
                }
                finally
                {
                    _queueWorkerActive = false;
                }
            });
        }

        public bool IsReentrantOperationCall(GridObjectChannelRuntimeState state)
        {
            var activeStamp = Volatile.Read(ref _activeOperationStamp);
            var contextStamp = state.OperationContextStamp.Value;
            return activeStamp != 0 &&
                   contextStamp != 0 &&
                   activeStamp == contextStamp &&
                   _mutex.CurrentCount == 0;
        }

        static GridObjectChannelRefreshMode CombineRefreshModes(
            GridObjectChannelRefreshMode a,
            GridObjectChannelRefreshMode b)
        {
            return GetRefreshPriority(a) <= GetRefreshPriority(b) ? a : b;
        }

        static int GetRefreshPriority(GridObjectChannelRefreshMode mode)
        {
            return mode switch
            {
                GridObjectChannelRefreshMode.FullRebuild => 0,
                GridObjectChannelRefreshMode.Incremental => 1,
                GridObjectChannelRefreshMode.LayoutOnly => 2,
                _ => 3,
            };
        }
    }
}
