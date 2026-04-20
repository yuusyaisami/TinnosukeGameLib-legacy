#nullable enable
using System;
using System.Collections.Generic;
using Game.ActionBlock.Keys;
using Game.Commands;
using Game.Common;
using Game.Scalar;
using Game.Direction;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Scalar.Generated;
using Game.Profile;
using Game.Input;
using Game.DI;
namespace Game.Movement
{
    /// <summary>
    /// Movement service that gathers direction adapters (user input or external) and writes velocity into a movement channel.
    /// v0.2: Homing/Motion 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｫ蟇ｾ蠢懊・
    /// </summary>
    public sealed class InputMovementService :
        IDisposable,
        IActionBlockable,
        IInputMovementService,
        IInputMovementTelemetry,
        IEnabledService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        // ================================================================
        // Dependencies
        // ================================================================

        IMovementChannelHub _channelHub;
        readonly IActionBlockService? _actionBlockService;
        readonly Game.Commands.IMonitorChannelHub? _monitorHub;
        readonly InputMovementOptions _options;
        readonly IDirectionChannelHub? _directionHub;
        readonly List<IInputDirectionAdapter> _directionAdapters = new();
        readonly IBaseScalarService? _scalar;
        readonly ScalarKey? _speedScalarKey;
        readonly Transform? _ownerTransform;
        readonly IRuntimeResolver _resolver = null!;

        readonly float _defaultSpeedFallback;
        readonly float _defaultMultiplierFallback;

        // v0.2: Homing/Motion
        IHomingMovement? _homing;
        IMotionMovement? _motion;

        bool _enabled = true;

        // ================================================================
        // State
        // ================================================================

        VarStore? _vars;
        IMovementChannelHandle? _channel;
        IDirectionChannelHandle? _directionLayer;
        bool _disposed;
        bool _sortDirty;

        // v0.2: 譁ｹ蜷醍憾諷・
        Vector2 _currentBaseDirection;
        Vector2 _currentGuidanceDirection;

        // v0.3: 蜉騾・貂幃・
        Vector2 _targetVelocity;
        Vector2 _currentVelocity;
        bool _accelerationActive;
        float _activeAccel;
        float _activeDecel;

        // Telemetry
        Vector2 _lastRawDirection;
        Vector2 _lastProcessedDirection;
        InputMovementInput _lastInput;
        bool _lastHasInput;
        float _lastSpeedMultiplier;
        float _lastSpeedBase;
        float _lastFinalSpeedMul;
        Vector2 _lastFinalDirection;
        Vector2 _lastAdditiveVelocity;
        int _lastGravityPullDebugFrame;

        // ================================================================
        // Properties
        // ================================================================

        /// <inheritdoc/>
        public string ActionBlockKind => ActionBlockKeys.Entity.UserMovement;

        /// <inheritdoc/>
        public string BlockableId => _options.BlockableId;

        /// <inheritdoc/>
        public BoolLayer BlockLayer { get; } = new();

        /// <inheritdoc/>
        public Vector2 CurrentBaseDirection => _currentBaseDirection;

        /// <inheritdoc/>
        public Vector2 CurrentGuidanceDirection => _currentGuidanceDirection;

        public bool HasInput => _lastHasInput;
        public Vector2 RawDirection => _lastRawDirection;
        public Vector2 ProcessedDirection => _lastProcessedDirection;
        public InputMovementInput LastInput => _lastInput;
        public float SpeedMultiplier => _lastSpeedMultiplier;
        public bool AccelerationActive => _accelerationActive;
        public float ActiveAccel => _activeAccel;
        public float ActiveDecel => _activeDecel;
        public Vector2 TargetVelocity => _targetVelocity;
        public Vector2 CurrentVelocity => _currentVelocity;
        public Vector2 BaseDirection => _currentBaseDirection;
        public Vector2 GuidanceDirection => _currentGuidanceDirection;
        public float SpeedBase => _lastSpeedBase;
        public float FinalSpeedMul => _lastFinalSpeedMul;
        public Vector2 FinalDirection => _lastFinalDirection;
        public Vector2 AdditiveVelocity => _lastAdditiveVelocity;

        /// <inheritdoc/>
        public IHomingMovement? Homing
        {
            get
            {
                EnsureMovementModulesResolved();
                return _homing;
            }
        }

        /// <inheritdoc/>
        public IMotionMovement? Motion
        {
            get
            {
                EnsureMovementModulesResolved();
                return _motion;
            }
        }

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _enabled;

        // ================================================================
        // Constructor
        // ================================================================

        public InputMovementService(
            IMovementChannelHub channelHub,
            Game.Commands.IMonitorChannelHub? monitorHub,
            InputMovementOptions options,
            IRuntimeResolver resolver,
            IScopeNode scopeNode)
        {
            _channelHub = channelHub;
            _resolver = resolver;
            _resolver.TryResolve<IActionBlockService>(out var actionBlockService);
            _actionBlockService = actionBlockService;

            _monitorHub = monitorHub;
            _options = options ?? new InputMovementOptions();

            _resolver.TryResolve<IDirectionChannelHub>(out _directionHub);
            _resolver.TryResolve(out IBaseScalarService? scalarSvc);
            _scalar = scalarSvc;

            _speedScalarKey = ScalarKeys.GameLib.Movement.DefaultSpeed;

            _defaultSpeedFallback = 4f;
            _defaultMultiplierFallback = 1f;
            if (_resolver.TryResolve<IScopeBindingRegistry>(out var profileRegistry) && profileRegistry != null)
            {
                if (profileRegistry.TryResolveDefinition<MovementPreset>(out var movementPreset))
                {
                    _defaultSpeedFallback = movementPreset.DefaultSpeedFallback;
                    _defaultMultiplierFallback = movementPreset.DefaultMultiplierFallback;
                }
            }

            // v0.2: Homing/Motion 縺ｯ驕・ｻｶ隗｣豎ｺ縲・
            // Runtime 蛛ｴ縺ｧ縺ｯ譛ｪ逋ｻ骭ｲ繧ｵ繝ｼ繝薙せ隗｣豎ｺ譎ゅ↓繝ｭ繧ｰ/萓句､悶ヮ繧､繧ｺ縺悟・繧九◆繧√・
            // 蠢・ｦ√↓縺ｪ繧九∪縺ｧ Resolve 縺励↑縺・・
            _homing = null;
            _motion = null;
            if (scopeNode.Identity != null)
                _ownerTransform = scopeNode.Identity.SelfTransform;

            if (_resolver != null && _resolver.TryResolve<InputMovementDebugView>(out var debugView) && debugView != null)
            {
                debugView.Bind(this);
            }


        }

        // ================================================================
        // IScopeAcquireHandler / IScopeReleaseHandler
        // ================================================================

        public void OnAcquire(IScopeNode scopeNode, bool isReset)
        {
            EnsureMovementModulesResolved();
            EnsureScopedMovementHub();
            RegisterChannel();
            _actionBlockService?.RegisterBlockable(this);

            if (_options.UseSpeedBasedCommands && _monitorHub != null)
            {
                _vars = new VarStore();
                _monitorHub.EvaluationMode = _options.MonitorEvaluationMode;
                _monitorHub.AttachToVars(_vars);

                if (_directionHub != null && _options.OutputDirection)
                {
                    if (_directionLayer == null)
                    {
                        _directionHub.TryGetLayer(_options.DirectionKey, out var _);
                        if (_directionLayer == null)
                        {
                            _directionLayer = _directionHub.RegisterLayer(_options.DirectionKey, new DirectionLayerDef(_options.DirectionKey));
                        }
                    }
                }

                if (_options.MonitorRules != null)
                {
                    var sharedExpressionVariables = _options.SharedExpressionVariables;
                    for (int i = 0; i < _options.MonitorRules.Length; i++)
                    {
                        var rule = _options.MonitorRules[i];
                        if (sharedExpressionVariables != null && sharedExpressionVariables.Count > 0)
                        {
                            rule.Condition.TrySetExternalExpressionVariables(sharedExpressionVariables);
                        }
                        if (!string.IsNullOrEmpty(rule.RuleName))
                            _monitorHub.RemoveRule(rule.RuleName);
                        _monitorHub.AddRule(rule);
                    }
                }
            }

            ApplyDirectionFromAdapters();
        }

        public void OnRelease(IScopeNode scopeNode, bool isReset)
        {
            Reset();
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _actionBlockService?.UnregisterBlockable(this);
            _channel = null;
            _targetVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;
            _accelerationActive = false;
            _activeAccel = 0f;
            _activeDecel = 0f;
            _currentBaseDirection = Vector2.zero;
            _currentGuidanceDirection = Vector2.zero;

            if (_monitorHub != null && _vars != null)
            {
                _monitorHub.DetachFromVars(_vars);
            }
        }

        void RegisterChannel()
        {
            if (_channelHub == null)
                return;

            var key = _options.ChannelKey ?? InputMovementOptions.DefaultChannelKey;
            if (_channelHub.TryGetChannel(key, out var existing))
            {
                _channel = existing;
                return;
            }

            var def = MovementChannelDef.Input(key);
            def.SmoothingLambda = _options.SmoothingLambda;
            _channel = _channelHub.RegisterChannel(key, def);
        }

        void EnsureScopedMovementHub()
        {
            if (_resolver.TryResolve<IMovementChannelHub>(out var hub) && hub != null)
            {
                _channelHub = hub;
            }
        }

        bool IsBlocked() => BlockLayer.Value;

        // ================================================================
        // IInputMovementService
        // ================================================================

        public void RegisterAdapter(IInputDirectionAdapter adapter)
        {
            if (adapter == null || _disposed)
                return;

            if (_directionAdapters.Contains(adapter))
                return;

            _directionAdapters.Add(adapter);
            _sortDirty = true;
        }

        public void UnregisterAdapter(IInputDirectionAdapter adapter)
        {
            if (adapter == null)
                return;

            _directionAdapters.Remove(adapter);
        }

        public void NotifyDirectionUpdated()
        {
            if (_disposed)
                return;

            ApplyDirectionFromAdapters();
        }

        // ================================================================
        // Core Logic
        // ================================================================

        void ApplyDirectionFromAdapters()
        {
            EnsureChannel();
            if (_channel == null)
                return;

            if (!IsEnabled)
            {
                StopImmediate();
                return;
            }

            if (_sortDirty)
            {
                _directionAdapters.Sort((a, b) => b.DirectionPriority.CompareTo(a.DirectionPriority));
                _sortDirty = false;
            }

            InputMovementInput input = default;
            bool hasInput = false;

            for (int i = 0; i < _directionAdapters.Count; i++)
            {
                var adapter = _directionAdapters[i];
                if (adapter == null)
                    continue;

                if (adapter is IInputMovementAdapter movementAdapter && movementAdapter.TryGetInput(out input))
                {
                    hasInput = true;
                    break;
                }

                if (adapter.TryGetDirection(out var candidate))
                {
                    input = InputMovementInput.FromDirection(candidate);
                    hasInput = true;
                    break;
                }
            }

            ApplyMovementInput(input, hasInput);
        }

        void ApplyMovementInput(in InputMovementInput input, bool hasInput)
        {
            if (_channel == null)
                return;

            if (!IsEnabled || IsBlocked())
            {
                StopImmediate();
                return;
            }

            var wasAccelerationActive = _accelerationActive;

            if (hasInput)
            {
                _accelerationActive = ResolveAcceleration(input, out _activeAccel, out _activeDecel);
            }

            var direction = hasInput ? input.Direction : Vector2.zero;
            var speedMultiplier = hasInput && input.HasSpeedMultiplier
                ? Mathf.Max(0f, input.SpeedMultiplier)
                : 1f;

            _lastHasInput = hasInput;
            _lastInput = input;
            _lastRawDirection = direction;
            _lastProcessedDirection = direction;
            _lastSpeedMultiplier = speedMultiplier;

            _targetVelocity = hasInput
                ? ComputeTargetVelocity(direction, speedMultiplier)
                : Vector2.zero;

            if (!hasInput)
            {
                _currentBaseDirection = Vector2.zero;
                _currentGuidanceDirection = Vector2.zero;
            }

            if (!_accelerationActive)
            {
                _currentVelocity = _targetVelocity;
                _channel.Velocity = _currentVelocity;
                PushAuxOutputs(_currentVelocity);
                return;
            }

            if (!wasAccelerationActive)
            {
                _currentVelocity = _channel.CurrentVelocity;
            }

            if (!hasInput && _currentVelocity.sqrMagnitude <= 0.0001f)
            {
                _accelerationActive = false;
                _currentVelocity = Vector2.zero;
                _channel.Velocity = Vector2.zero;
                PushAuxOutputs(Vector2.zero);
            }
        }

        Vector2 ComputeTargetVelocity(Vector2 direction, float inputSpeedMultiplier)
        {
            EnsureMovementModulesResolved();

            // 1. BaseDirection 繧呈ｭ｣隕丞喧
            var normalizedDir = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.zero;
            _currentBaseDirection = normalizedDir;

            // 2. Owner 菴咲ｽｮ繧貞叙蠕・
            Vector2 ownerPos = _ownerTransform != null
                ? (Vector2)_ownerTransform.position
                : Vector2.zero;

            // 3. Homing 譖ｴ譁ｰ
            _currentGuidanceDirection = _currentBaseDirection;
            var homing = _homing;
            if (homing != null)
            {
                _currentGuidanceDirection = homing.Tick(_currentBaseDirection, ownerPos, Time.deltaTime);
            }

            // 4. SpeedBase 繧貞叙蠕・
            float speedBase;
            if (_scalar != null && _speedScalarKey.HasValue && _scalar.GlobalTryGet(_speedScalarKey.Value, out float scalarSpeed))
                speedBase = scalarSpeed;
            else
                speedBase = _defaultSpeedFallback;

            float speedMul;
            if (_scalar != null && _scalar.GlobalTryGet(ScalarKeys.GameLib.Movement.SpeedMultiplier, out speedMul))
                speedMul = Mathf.Max(0f, speedMul);
            else
                speedMul = _defaultMultiplierFallback;

            _lastSpeedBase = speedBase;
            _lastFinalSpeedMul = speedMul;

            speedBase *= speedMul;
            speedBase *= inputSpeedMultiplier;

            // 5. Motion 譖ｴ譁ｰ
            Vector2 finalDirection = _currentGuidanceDirection;
            float finalSpeedMul = 1f;
            Vector2 additiveVelocity = Vector2.zero;

            var motion = _motion;
            if (motion != null && motion.IsActive)
            {
                var frame = new MovementGuidanceFrame
                {
                    FrameCount = Time.frameCount,
                    DeltaTime = Time.deltaTime,
                    BaseDirection = _currentBaseDirection,
                    GuidanceDirection = _currentGuidanceDirection,
                    Target = homing?.CurrentTarget ?? TargetSnapshot.Invalid,
                    HomingEnabled = homing?.HomingEnabled ?? false,
                    SpeedBase = speedBase
                };
                var motionOutput = motion.Tick(frame);
                finalDirection = motionOutput.Direction;
                finalSpeedMul = motionOutput.SpeedMul;
                additiveVelocity = motionOutput.AdditiveVelocity;
            }

            _lastFinalDirection = finalDirection;
            _lastFinalSpeedMul = finalSpeedMul;
            _lastAdditiveVelocity = additiveVelocity;

            // 6. FinalVelocity 繧定ｨ育ｮ・
            var finalVelocity = finalDirection * speedBase * finalSpeedMul + additiveVelocity;

            if (motion?.CurrentMotion is GravityPullMotionPreset gravityPull && gravityPull.EnableDebugLog)
            {
                var interval = Mathf.Max(1, gravityPull.DebugLogIntervalFrames);
                if (Time.frameCount - _lastGravityPullDebugFrame >= interval)
                {
                    _lastGravityPullDebugFrame = Time.frameCount;
                    Debug.Log(
                        $"[InputMovementService/GravityPull] frame={Time.frameCount} owner={_ownerTransform?.name ?? "(no-owner)"} " +
                        $"rawDir={direction} baseDir={_currentBaseDirection} guidance={_currentGuidanceDirection} " +
                        $"inputSpeedMul={inputSpeedMultiplier:F3} speedBase={speedBase:F3} finalSpeedMul={finalSpeedMul:F3} " +
                        $"addVel={additiveVelocity} finalVel={finalVelocity} blocked={IsBlocked()} enabled={IsEnabled}");
                }
            }

            return finalVelocity;
        }

        void EnsureMovementModulesResolved()
        {
            if (_homing == null)
            {
                _resolver.TryResolve(out IHomingMovement? homing);
                _homing = homing;
            }

            if (_motion == null)
            {
                _resolver.TryResolve(out IMotionMovement? motion);
                _motion = motion;
            }
        }

        bool ResolveAcceleration(in InputMovementInput input, out float accel, out float decel)
        {
            accel = 0f;
            decel = 0f;

            if (input.HasAccelerationOverride)
            {
                accel = Mathf.Max(0f, input.Accel);
                decel = Mathf.Max(0f, input.Decel);
                return accel > 0f || decel > 0f;
            }

            var settings = _options.Acceleration;
            if (!settings.Enabled)
                return false;

            accel = settings.Accel;
            decel = settings.Decel;

            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.Accel, out var scalarAccel))
                accel = scalarAccel;
            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.Decel, out var scalarDecel))
                decel = scalarDecel;

            accel = Mathf.Max(0f, accel);
            decel = Mathf.Max(0f, decel);
            return accel > 0f || decel > 0f;
        }

        bool TryGetScalar(ScalarKey key, out float value)
        {
            value = 0f;
            if (_scalar == null)
                return false;

            if (string.IsNullOrEmpty(key.Name))
                return false;

            return _scalar.GlobalTryGet(key, out value);
        }

        void StopImmediate()
        {
            _accelerationActive = false;
            _activeAccel = 0f;
            _activeDecel = 0f;
            _targetVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;
            _currentBaseDirection = Vector2.zero;
            _currentGuidanceDirection = Vector2.zero;
            _lastRawDirection = Vector2.zero;
            _lastProcessedDirection = Vector2.zero;
            _lastInput = default;
            _lastHasInput = false;
            _lastSpeedMultiplier = 1f;
            _lastSpeedBase = 0f;
            _lastFinalSpeedMul = 1f;
            _lastFinalDirection = Vector2.zero;
            _lastAdditiveVelocity = Vector2.zero;

            if (_channel != null)
            {
                _channel.SetImmediateVelocity(Vector2.zero);
            }

            PushAuxOutputs(Vector2.zero);
        }

        void PushAuxOutputs(Vector2 velocity)
        {
            if (_directionLayer != null && _options.OutputDirection)
            {

                var dir = velocity.sqrMagnitude > 0.0001f
                    ? velocity.normalized
                    : Vector2.zero;
                _directionLayer.TrySetDirection(dir);
            }

            if (_options.UseSpeedBasedCommands && _monitorHub != null)
            {
                var speed = velocity.magnitude;
                _monitorHub.SetVariable(_options.SpeedVariableKey, speed);
            }
        }

        void EnsureChannel()
        {
            if (_channel != null || _disposed)
                return;
            RegisterChannel();
        }

        public void Tick()
        {
            if (_disposed || _channel == null)
                return;

            if (!IsEnabled || IsBlocked())
            {
                StopImmediate();
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            // Motion/Homing 縺ｯ譎る俣萓晏ｭ倥・縺溘ａ縲∝・蜉帙′邯咏ｶ壹＠縺ｦ縺・ｋ髢薙・豈弱ヵ繝ｬ繝ｼ繝蜀崎ｨ育ｮ励☆繧九・
            // (萓・ GravityPull 縺ｮ AdditiveVelocity 遨榊・)
            bool hasContinuousModule =
                (_motion != null && _motion.IsActive) ||
                (_homing != null && _homing.HomingEnabled);
            if (_lastHasInput && hasContinuousModule)
            {
                var inputSpeedMultiplier = _lastInput.HasSpeedMultiplier
                    ? Mathf.Max(0f, _lastInput.SpeedMultiplier)
                    : 1f;

                _targetVelocity = ComputeTargetVelocity(_lastInput.Direction, inputSpeedMultiplier);
            }

            if (!_accelerationActive)
            {
                _currentVelocity = _targetVelocity;
                _channel.SetImmediateVelocity(_currentVelocity);
                PushAuxOutputs(_currentVelocity);
                return;
            }

            var step = ResolveAccelerationStep(_currentVelocity, _targetVelocity);
            if (step <= 0f)
            {
                _currentVelocity = _targetVelocity;
            }
            else
            {
                _currentVelocity = Vector2.MoveTowards(_currentVelocity, _targetVelocity, step * deltaTime);
            }

            _channel.SetImmediateVelocity(_currentVelocity);
            PushAuxOutputs(_currentVelocity);

            if (_currentVelocity.sqrMagnitude <= 0.0001f && _targetVelocity.sqrMagnitude <= 0.0001f)
                _accelerationActive = false;
        }

        float ResolveAccelerationStep(Vector2 current, Vector2 target)
        {
            var currentSqr = current.sqrMagnitude;
            var targetSqr = target.sqrMagnitude;
            bool speedingUp = targetSqr > currentSqr + 0.0001f;
            var step = speedingUp ? _activeAccel : _activeDecel;

            if (!speedingUp && currentSqr > 0f && targetSqr > 0f && Vector2.Dot(current, target) < 0f)
                step = _activeDecel;

            return step;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;
            _currentBaseDirection = Vector2.zero;
            _currentGuidanceDirection = Vector2.zero;
            _targetVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;
            _accelerationActive = false;
            _activeAccel = 0f;
            _activeDecel = 0f;
            _lastRawDirection = Vector2.zero;
            _lastProcessedDirection = Vector2.zero;
            _lastInput = default;
            _lastHasInput = false;
            _lastSpeedMultiplier = 1f;
            _lastSpeedBase = 0f;
            _lastFinalSpeedMul = 1f;
            _lastFinalDirection = Vector2.zero;
            _lastAdditiveVelocity = Vector2.zero;
            _sortDirty = false;
            BlockLayer.Clear();

            if (_channel != null)
            {
                _channel.SetImmediateVelocity(Vector2.zero);
            }
            if (_directionLayer != null && _options.OutputDirection)
            {
                _directionLayer.TrySetDirection(Vector2.zero);
            }

            if (_homing is IResettableService resettableHoming)
            {
                resettableHoming.Reset();
            }
            if (_motion is IResettableService resettableMotion)
            {
                resettableMotion.Reset();
            }
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                StopImmediate();
            }
        }
    }

    /// <summary>
    /// InputMovementService 縺ｮ繧ｪ繝励す繝ｧ繝ｳ縲・
    /// </summary>
    public sealed class InputMovementOptions
    {
        public const string DefaultChannelKey = "userInput";
        public const string DefaultSpeedKey = "inputSpeed";
        public const string DefaultDirectionKey = "inputDirection";

        public string ChannelKey { get; set; } = DefaultChannelKey;

        public string BlockableId { get; set; } = nameof(InputMovementService);

        public bool OutputDirection { get; set; } = false;
        public string DirectionKey { get; set; } = DefaultDirectionKey;

        public bool UseSpeedBasedCommands { get; set; } = false;
        public string SpeedVariableKey { get; set; } = DefaultSpeedKey;
        public MonitorEvaluationMode MonitorEvaluationMode { get; set; } = Game.Commands.MonitorEvaluationMode.EventDriven;
        public Game.Commands.MonitorRule[] MonitorRules { get; set; } = Array.Empty<Game.Commands.MonitorRule>();
        public IReadOnlyList<ExpressionVariable>? SharedExpressionVariables { get; set; }
        public float SmoothingLambda { get; set; } = 0f;
        public InputAccelerationSettings Acceleration { get; set; } = new InputAccelerationSettings
        {
            Enabled = false,
            Accel = 0f,
            Decel = 0f,
        };
    }
}
