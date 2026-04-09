#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VNext = Game.Commands.VNext;
using VContainer;

namespace Game.UI
{
    public sealed class ModalStackLayerAffectCommandService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IModalStackLayerAffectCommandOptions _options;

        IModalStackChannelHubService? _hub;
        VNext.ICommandRunner? _runner;
        CancellationTokenSource? _commandCts;
        ModalRootResolvedState? _lastState;

        public ModalStackLayerAffectCommandService(IScopeNode owner, IModalStackLayerAffectCommandOptions options)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IModalStackChannelHubService>(out var hub) || hub == null)
                return;

            resolver.TryResolve<VNext.ICommandRunner>(out var runner);

            _hub = hub;
            _runner = runner;
            _hub.OnLayerStatesChanged += HandleLayerStatesChanged;

            _lastState = ResolveCurrentState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_hub != null)
                _hub.OnLayerStatesChanged -= HandleLayerStatesChanged;

            _hub = null;
            _runner = null;
            _lastState = null;
            StopCommands();
        }

        void HandleLayerStatesChanged(ModalLayerStatesChangedContext context)
        {
            var prev = _lastState;
            var curr = ResolveCurrentState();
            _lastState = curr;

            if (!prev.HasValue && !curr.HasValue)
                return;

            var prevVisible = prev.HasValue && prev.Value.Visible;
            var currVisible = curr.HasValue && curr.Value.Visible;
            var prevInput = prev.HasValue && prev.Value.InputActive;
            var currInput = curr.HasValue && curr.Value.InputActive;

            if (!prevVisible && currVisible)
                ExecuteCommands(_options.OnBecameVisible).Forget();
            else if (prevVisible && !currVisible)
                ExecuteCommands(_options.OnBecameHidden).Forget();

            if (!prevInput && currInput)
                ExecuteCommands(_options.OnBecameInputActive).Forget();
            else if (prevInput && !currInput)
                ExecuteCommands(_options.OnBecameInputInactive).Forget();

            if (curr.HasValue && (!prev.HasValue || prev.Value.InactiveReason != curr.Value.InactiveReason))
            {
                if (curr.Value.InactiveReason == ModalLayerRootInactiveReason.NotActiveInLayer)
                    ExecuteCommands(_options.OnBecameInactiveInLayer).Forget();
                else if (curr.Value.InactiveReason == ModalLayerRootInactiveReason.LayerSuppressedByOtherLayer)
                    ExecuteCommands(_options.OnBecameSuppressedByOtherLayer).Forget();
            }
        }

        ModalRootResolvedState? ResolveCurrentState()
        {
            if (_hub == null)
                return null;

            return _hub.TryGetRootState(_owner, out var state) ? state : null;
        }

        async UniTaskVoid ExecuteCommands(VNext.CommandListData commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (_runner == null)
                return;

            ResetCommandState();
            if (_commandCts == null)
                return;

            var options = VNext.CommandRunOptions.Default;
            var context = new VNext.CommandContext(_owner, NullVarStore.Instance, _runner, _owner, options);

            try
            {
                var result = await _runner.ExecuteListAsync(commands, context, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    UnityEngine.Debug.LogError($"[ModalStackLayerAffect] command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        void ResetCommandState()
        {
            StopCommands();
            _commandCts = new CancellationTokenSource();
        }

        void StopCommands()
        {
            if (_commandCts == null)
                return;

            _commandCts.Cancel();
            _commandCts.Dispose();
            _commandCts = null;
        }
    }
}
