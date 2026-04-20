#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.TransformSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class VisualBoundsChangeCommandConfig
    {
        public CommandListData Commands = new();
        public bool RebuildBeforeCheck = true;
        public float PositionEpsilon = 0.1f;
        public float SizeEpsilon = 0.1f;
        public bool RunInLateUpdate = true;
        public bool ExecuteOnAcquire = true;
        public bool EnableDebugLog = false;
    }

    public sealed class VisualBoundsChangeCommandService :
        IScopeTickHandler,
        IScopeLateTickHandler,
        ITickPhase,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly VisualBoundsChangeCommandConfig _config;

        IVisualBoundsService? _boundsService;
        IVisualBoundsOutput? _boundsOutput;
        ICommandRunner? _runner;
        IVarStore? _vars;
        CancellationTokenSource? _commandCts;

        bool _acquired;
        bool _hasLastRect;
        Rect _lastRect;

        public VisualBoundsChangeCommandService(IScopeNode owner, VisualBoundsChangeCommandConfig config)
        {
            _owner = owner;
            _config = config;
        }

        public TickPhase Phase => _config.RunInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _acquired = true;
            _hasLastRect = false;

            var resolver = scope.Resolver;
            if (resolver == null)
            {
                Trace("OnAcquire skipped: resolver is null");
                return;
            }

            resolver.TryResolve(out _boundsService);
            if (!scope.TryResolveInAncestors(out _runner))
                resolver.TryResolve(out _runner);
            if (!scope.TryResolveInAncestors(out _vars))
                resolver.TryResolve(out _vars);

            if (resolver.TryResolve<IVisualBoundsOutput>(out var output))
                _boundsOutput = output;
            else
                _boundsOutput = _boundsService as IVisualBoundsOutput;

            Trace(
                $"OnAcquire: boundsService={(_boundsService != null)} output={(_boundsOutput != null)} runner={(_runner != null)} vars={(_vars != null)}");

            if (_config.ExecuteOnAcquire)
            {
                if (_config.RebuildBeforeCheck)
                    _boundsService?.RebuildNow();

                var initialOutput = _boundsOutput;
                if (initialOutput != null && initialOutput.HasBounds)
                {
                    _lastRect = initialOutput.LocalRect;
                    _hasLastRect = true;
                    Trace($"ExecuteOnAcquire fired: rect={_lastRect}");
                    ExecuteChangedCommandsAsync().Forget();
                }
                else
                {
                    Trace("ExecuteOnAcquire skipped: no bounds");
                }
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _acquired = false;
            _hasLastRect = false;

            StopCommands();
            _boundsService = null;
            _boundsOutput = null;
            _runner = null;
            _vars = null;
        }

        public void Tick()
        {
            if (!_acquired || _config.RunInLateUpdate)
                return;

            TickInternal();
        }

        public void LateTick()
        {
            if (!_acquired || !_config.RunInLateUpdate)
                return;

            TickInternal();
        }

        void TickInternal()
        {
            if (_config.Commands == null || _config.Commands.Count == 0)
            {
                Trace("Tick skipped: commands empty");
                return;
            }

            if (_config.RebuildBeforeCheck)
                _boundsService?.RebuildNow();

            var output = _boundsOutput;
            if (output == null || !output.HasBounds)
            {
                _hasLastRect = false;
                Trace("Tick skipped: output has no bounds");
                return;
            }

            var currentRect = output.LocalRect;
            if (!_hasLastRect)
            {
                _lastRect = currentRect;
                _hasLastRect = true;
                Trace($"Tick initialized baseline rect={currentRect}");
                return;
            }

            if (!HasMeaningfulChange(_lastRect, currentRect, _config.PositionEpsilon, _config.SizeEpsilon))
                return;

            _lastRect = currentRect;
            Trace($"Bounds changed: rect={currentRect}");
            ExecuteChangedCommandsAsync().Forget();
        }

        async UniTaskVoid ExecuteChangedCommandsAsync()
        {
            var runner = _runner;
            if (runner == null)
            {
                Trace("Execute skipped: ICommandRunner is null");
                return;
            }

            ResetCommandState();
            if (_commandCts == null)
                return;

            var vars = _vars ?? NullVarStore.Instance;
            var ctx = new CommandContext(_owner, vars, runner, _owner, CommandRunOptions.Default);

            try
            {
                Trace($"Execute commands start: count={_config.Commands.Count}");
                var result = await runner.ExecuteListAsync(_config.Commands, ctx, _commandCts.Token, ctx.Options);
                Trace($"Execute commands completed: status={result.Status}");
                if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                    Debug.LogError($"[VisualBounds] OnBoundsChanged commands failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualBounds] OnBoundsChanged commands exception: {ex.Message}");
            }
        }

        static bool HasMeaningfulChange(in Rect previous, in Rect current, float posEpsilon, float sizeEpsilon)
        {
            var prevCenter = previous.center;
            var currCenter = current.center;
            var prevSize = previous.size;
            var currSize = current.size;

            return
                Mathf.Abs(currCenter.x - prevCenter.x) > posEpsilon ||
                Mathf.Abs(currCenter.y - prevCenter.y) > posEpsilon ||
                Mathf.Abs(currSize.x - prevSize.x) > sizeEpsilon ||
                Mathf.Abs(currSize.y - prevSize.y) > sizeEpsilon;
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

        void Trace(string message)
        {
            if (!_config.EnableDebugLog)
                return;

            Debug.Log($"[VisualBounds] {message}");
        }
    }
}
