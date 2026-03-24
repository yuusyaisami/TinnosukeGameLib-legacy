#nullable enable
using System;
using Game.BuildConsole;
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Scalar;
using Game.Input;
using Game.Direction;
using Game.Scalar.Generated;

namespace Game.Movement
{
    [DisallowMultipleComponent]
    public sealed class MoveToInputPointController : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Profile")]
        [SerializeField] MoveToInputPointProfileSO profile = null!;

        [BoxGroup("Monitor")]
        [LabelText("Use Speed Monitor")]
        [SerializeField] bool useSpeedMonitor;

        [BoxGroup("Monitor")]
        [LabelText("Speed Variable Key")]
        [ShowIf(nameof(useSpeedMonitor))]
        [SerializeField] string speedVariableKey = "moveTo.speed";

        [BoxGroup("Monitor")]
        [LabelText("Evaluation Mode")]
        [ShowIf(nameof(useSpeedMonitor))]
        [SerializeField] MonitorEvaluationMode monitorEvaluationMode = MonitorEvaluationMode.Polling;

        [BoxGroup("Monitor")]
        [LabelText("Default Execution Behavior")]
        [ShowIf(nameof(useSpeedMonitor))]
        [SerializeField] ExecutionBehavior defaultExecutionBehavior = ExecutionBehavior.SkipIfRunning;

        [BoxGroup("Monitor")]
        [LabelText("Monitor Rules")]
        [ShowIf(nameof(useSpeedMonitor))]
        [SerializeField] MonitorRule[]? monitorRules;

        [BoxGroup("Output")]
        [LabelText("Write To Movement Channel")]
        [SerializeField] bool writeToMovementChannel = true;

        [BoxGroup("Output")]
        [LabelText("Channel Key")]
        [ShowIf(nameof(writeToMovementChannel))]
        [SerializeField] string channelKey = "moveToInputPoint";

        [BoxGroup("Output")]
        [LabelText("Channel Def (optional)")]
        [ShowIf(nameof(writeToMovementChannel))]
        [SerializeField] MovementChannelDef? channelDef;

        [BoxGroup("Output")]
        [LabelText("Output Direction (optional)")]
        [SerializeField] bool outputDirection;

        [BoxGroup("Output")]
        [LabelText("Direction Key")]
        [ShowIf(nameof(outputDirection))]
        [SerializeField] string directionKey = "moveToInputPoint";

        [BoxGroup("Output")]
        [LabelText("Direction Priority")]
        [ShowIf(nameof(outputDirection))]
        [SerializeField] int directionPriority;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (monitorRules == null || monitorRules.Length == 0)
                return;

            for (int i = 0; i < monitorRules.Length; i++)
            {
                var rule = monitorRules[i];
                rule.EnsureDefaults();
                monitorRules[i] = rule;
            }
        }
#endif

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (profile == null)
            {
                profile = new MoveToInputPointProfileSO();
            }
            builder.RegisterInstance(profile);

            builder.Register<IMoveToInputPointService>(resolver =>
            {
                resolver.TryResolve(out IBaseScalarService? scalarSvc);
                resolver.TryResolve(out IActionBlockService? actionBlockSvc);

                return new MoveToInputPointService(profile, scalarService: scalarSvc, actionBlockService: actionBlockSvc);
            }, Lifetime.Singleton)
                .As<IMoveToInputPointService>()
                .As<IDisposable>();

            builder.Register<MoveToInputPointEntryPoint>(Lifetime.Singleton)
                .WithParameter(transform)
                .WithParameter(new MoveToInputPointOutputOptions(
                    useSpeedMonitor: useSpeedMonitor,
                    speedVariableKey: speedVariableKey,
                    monitorEvaluationMode: monitorEvaluationMode,
                    defaultExecutionBehavior: defaultExecutionBehavior,
                    monitorRules: monitorRules,
                    writeToMovementChannel: writeToMovementChannel,
                    channelKey: channelKey,
                    channelDef: channelDef ?? MovementChannelDef.Default(channelKey),
                    outputDirection: outputDirection,
                    directionKey: directionKey,
                    directionPriority: directionPriority
                ))
                .AsSelf()
                .As<ITickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>();
        }
    }

    /// <summary>
    /// Entry point that calls MoveToInputPointService.Tick each frame using the captured transform and writes the result to a MovementChannel.
    /// </summary>
    public readonly struct MoveToInputPointOutputOptions
    {
        public readonly bool UseSpeedMonitor;
        public readonly string SpeedVariableKey;
        public readonly MonitorEvaluationMode MonitorEvaluationMode;
        public readonly ExecutionBehavior DefaultExecutionBehavior;
        public readonly MonitorRule[]? MonitorRules;

        public readonly bool WriteToMovementChannel;
        public readonly string ChannelKey;
        public readonly MovementChannelDef? ChannelDef;
        public readonly bool OutputDirection;
        public readonly string DirectionKey;
        public readonly int DirectionPriority;

        public MoveToInputPointOutputOptions(
            bool useSpeedMonitor,
            string speedVariableKey,
            MonitorEvaluationMode monitorEvaluationMode,
            ExecutionBehavior defaultExecutionBehavior,
            MonitorRule[]? monitorRules,
            bool writeToMovementChannel,
            string channelKey,
            MovementChannelDef? channelDef,
            bool outputDirection,
            string directionKey,
            int directionPriority)
        {
            UseSpeedMonitor = useSpeedMonitor;
            SpeedVariableKey = speedVariableKey;
            MonitorEvaluationMode = monitorEvaluationMode;
            DefaultExecutionBehavior = defaultExecutionBehavior;
            MonitorRules = monitorRules;

            WriteToMovementChannel = writeToMovementChannel;
            ChannelKey = channelKey;
            ChannelDef = channelDef;
            OutputDirection = outputDirection;
            DirectionKey = directionKey;
            DirectionPriority = directionPriority;
        }
    }

    public sealed class MoveToInputPointEntryPoint : IScopeAcquireHandler, IScopeReleaseHandler, ITickable, IDisposable
    {
        readonly IMoveToInputPointService _service;
        readonly Transform _transform;
        readonly MoveToInputPointOutputOptions _options;
        readonly IObjectResolver _resolver;

        IMovementChannelHub? _hub;
        IMovementChannelHandle? _handle;
        IDirectionChannelHub? _directionHub;
        IDirectionChannelHandle? _directionLayer;
        IMonitorChannelHub? _monitorHub;
        IBaseScalarService? _scalarService;
        IScopeNode? _scope;
        VarStore? _vars;
        bool _initialized;
        bool _lastMissingHandleLogged;
        bool _lastHadTarget;
        int _lastOutputState = -1;
        bool _lastZeroVelocityLogged;

        public MoveToInputPointEntryPoint(
            IMoveToInputPointService service,
            Transform transform,
            MoveToInputPointOutputOptions options,
            IObjectResolver resolver)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _transform = transform ? transform : throw new ArgumentNullException(nameof(transform));
            _options = options;
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _scope = scope;
            _ = isReset;
            ReleaseOutputs();
            ResolveDependencies();
            InitializeOutputs();
            BuildConsoleLog.Scope(_scope,
                $"MoveTo acquire | Channel={_options.ChannelKey} Hub={(_hub != null)} Handle={(_handle != null)} Direction={(_directionLayer != null)} Monitor={(_monitorHub != null)}",
                LogType.Log);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            BuildConsoleLog.Scope(_scope, "MoveTo release", LogType.Log);
            ReleaseOutputs();
            _scope = null;
        }

        void ResolveDependencies()
        {
            _resolver.TryResolve(out IMovementChannelHub? hub);
            _hub = hub;
            _resolver.TryResolve(out IDirectionChannelHub? directionHub);
            _directionHub = directionHub;
            _resolver.TryResolve(out IMonitorChannelHub? monitorHub);
            _monitorHub = monitorHub;
            _resolver.TryResolve(out IBaseScalarService? scalarService);
            _scalarService = scalarService;
        }

        void InitializeOutputs()
        {
            if (_options.WriteToMovementChannel && _hub != null && !string.IsNullOrEmpty(_options.ChannelKey))
            {
                if (!_hub.TryGetChannel(_options.ChannelKey, out _handle))
                {
                    if (_options.ChannelDef != null)
                        _handle = _hub.RegisterChannel(_options.ChannelKey, _options.ChannelDef);
                }
            }

            if (_options.OutputDirection && _directionHub != null && !string.IsNullOrEmpty(_options.DirectionKey))
            {
                if (!_directionHub.TryGetLayer(_options.DirectionKey, out _directionLayer))
                {
                    _directionLayer = _directionHub.RegisterLayer(
                        _options.DirectionKey,
                        new DirectionLayerDef(
                            tag: _options.DirectionKey,
                            priority: _options.DirectionPriority));
                }
            }

            if (_options.UseSpeedMonitor && _monitorHub != null)
            {
                _vars = new VarStore();
                _monitorHub.EvaluationMode = _options.MonitorEvaluationMode;
                _monitorHub.DefaultExecutionBehavior = _options.DefaultExecutionBehavior;
                _monitorHub.AttachToVars(_vars);

                var rules = _options.MonitorRules;
                if (rules != null)
                {
                    for (int i = 0; i < rules.Length; i++)
                    {
                        var rule = rules[i];
                        if (!string.IsNullOrEmpty(rule.RuleName))
                            _monitorHub.RemoveRule(rule.RuleName);
                        _monitorHub.AddRule(rule);
                    }
                }
            }
            _initialized = true;
        }

        public void Tick()
        {
            if (!_initialized)
            {
                ResolveDependencies();
                InitializeOutputs();
            }

            // Avoid accessing a destroyed Unity object (MissingReferenceException).
            if (!_transform)
            {
                // Clear channels/vars to avoid leaving stale values.
                Dispose();
                return;
            }

            var pos = (Vector2)_transform.position;
            var output = _service.Tick(Time.deltaTime, pos, Vector2.zero);
            if (_service.HasTarget && _handle == null)
            {
                if (!_lastMissingHandleLogged)
                {
                    _lastMissingHandleLogged = true;
                    BuildConsoleLog.Scope(_scope,
                        $"MoveTo missing movement channel handle | Channel={_options.ChannelKey}",
                        LogType.Error);
                }
            }
            else
            {
                _lastMissingHandleLogged = false;
            }

            if (_options.WriteToMovementChannel && _handle != null)
            {
                _handle.Velocity = output.DesiredVelocity;
            }

            if (_options.OutputDirection && _directionLayer != null)
            {
                var dir = output.DesiredVelocity.sqrMagnitude > 0.0001f
                    ? output.DesiredVelocity.normalized
                    : Vector2.zero;
                _directionLayer.TrySetDirection(dir);
            }

            if (_options.UseSpeedMonitor && _monitorHub != null && !string.IsNullOrEmpty(_options.SpeedVariableKey))
            {
                var speed = output.DesiredVelocity.magnitude;
                _monitorHub.SetVariable(_options.SpeedVariableKey, speed);
            }

            var hasTarget = _service.HasTarget;
            var outputState = (int)output.State;
            if (hasTarget != _lastHadTarget || outputState != _lastOutputState)
            {
                var target = _service.Target;
                BuildConsoleLog.Scope(_scope,
                    $"MoveTo tick | State={output.State} HasTarget={hasTarget} Target=({target.x:F2},{target.y:F2}) Dist={output.DistanceToTarget:F2} Velocity=({output.DesiredVelocity.x:F2},{output.DesiredVelocity.y:F2}) Handle={(_handle != null)}",
                    output.State == MoveToInputPointState.Blocked ? LogType.Warning : LogType.Log);
                _lastHadTarget = hasTarget;
                _lastOutputState = outputState;
            }

            var isZeroVelocityWhileMoving =
                output.State == MoveToInputPointState.Moving &&
                hasTarget &&
                output.DesiredVelocity.sqrMagnitude <= 0.000001f;

            if (isZeroVelocityWhileMoving && !_lastZeroVelocityLogged)
            {
                float defaultSpeed = 0f;
                float speedMultiplier = 1f;
                var hasDefaultSpeed = _scalarService != null &&
                                      _scalarService.GlobalTryGet(ScalarKeys.GameLib.Movement.DefaultSpeed, out defaultSpeed);
                var hasSpeedMultiplier = _scalarService != null &&
                                         _scalarService.GlobalTryGet(ScalarKeys.GameLib.Movement.SpeedMultiplier, out speedMultiplier);
                var multiplierApplied = _service.CurrentRequestInputType == MovementInputType.Player;

                BuildConsoleLog.Scope(_scope,
                    $"MoveTo zero velocity while moving | InputType={_service.CurrentRequestInputType} MultiplierApplied={multiplierApplied} DefaultSpeed={(hasDefaultSpeed ? defaultSpeed.ToString("F2") : "<none>")} SpeedMultiplier={(hasSpeedMultiplier ? speedMultiplier.ToString("F2") : "<none>")} Target=({_service.Target.x:F2},{_service.Target.y:F2})",
                    LogType.Warning);
            }

            _lastZeroVelocityLogged = isZeroVelocityWhileMoving;
        }

        public void Dispose()
        {
            ReleaseOutputs();
        }

        void ReleaseOutputs()
        {
            if (_handle != null)
            {
                _handle.SetImmediateVelocity(Vector2.zero);
            }

            if (_directionLayer != null)
            {
                _directionLayer.TrySetDirection(Vector2.zero);
            }

            if (_monitorHub != null && _vars != null)
            {
                _monitorHub.DetachFromVars(_vars);
            }

            _vars = null;
            _handle = null;
            _directionLayer = null;
            _initialized = false;
            _lastMissingHandleLogged = false;
            _lastHadTarget = false;
            _lastOutputState = -1;
            _lastZeroVelocityLogged = false;
        }
    }
}
