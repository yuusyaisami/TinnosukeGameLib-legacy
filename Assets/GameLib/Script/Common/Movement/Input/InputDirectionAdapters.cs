#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.ActionBlock.Keys;
using Game.Input;
using Game.Scalar;
using Game.Scalar.Generated;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Movement
{
    /// <summary>
    /// Direction adapter priority constants. Larger values take precedence.
    /// </summary>
    public static class InputDirectionAdapterPriority
    {
        public const int Dynamic = 2000;
        public const int User = 1000;
    }

    /// <summary>
    /// Contract for the movement service that accepts direction adapters.
    /// v0.2: Homing/Motion 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｸ縺ｮ繧｢繧ｯ繧ｻ繧ｹ繧定ｿｽ蜉縲・
    /// </summary>
    public interface IInputMovementService
    {
        /// <summary>Direction Adapter 繧堤匳骭ｲ</summary>
        void RegisterAdapter(IInputDirectionAdapter adapter);

        /// <summary>Direction Adapter 繧定ｧ｣髯､</summary>
        void UnregisterAdapter(IInputDirectionAdapter adapter);

        /// <summary>譁ｹ蜷代′譖ｴ譁ｰ縺輔ｌ縺溘％縺ｨ繧帝夂衍</summary>
        void NotifyDirectionUpdated();

        // ================================================================
        // v0.2 諡｡蠑ｵ
        // ================================================================

        /// <summary>迴ｾ蝨ｨ縺ｮ BaseDirection・郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・/summary>
        Vector2 CurrentBaseDirection { get; }

        /// <summary>迴ｾ蝨ｨ縺ｮ GuidanceDirection・郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・/summary>
        Vector2 CurrentGuidanceDirection { get; }

        /// <summary>Homing 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｸ縺ｮ繧｢繧ｯ繧ｻ繧ｹ・井ｻｻ諢擾ｼ・/summary>
        IHomingMovement? Homing { get; }

        /// <summary>Motion 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｸ縺ｮ繧｢繧ｯ繧ｻ繧ｹ・井ｻｻ諢擾ｼ・/summary>
        IMotionMovement? Motion { get; }
    }

    /// <summary>
    /// Adapter that can provide a movement direction.
    /// </summary>
    public interface IInputDirectionAdapter
    {
        /// <summary>Higher values override lower ones.</summary>
        int DirectionPriority { get; }

        /// <summary>
        /// Try to fetch the current direction. Returns true when this adapter wants to drive movement.
        /// </summary>
        bool TryGetDirection(out Vector2 direction);
    }

    /// <summary>
    /// Adapter that can provide direction + movement input metadata (speed/accel).
    /// </summary>
    public interface IInputMovementAdapter : IInputDirectionAdapter
    {
        bool TryGetInput(out InputMovementInput input);
    }

    /// <summary>
    /// Adapter that supports runtime direction settings updates.
    /// </summary>
    public interface IInputDirectionSettingsAdapter
    {
        InputDirectionAdapterSettings CurrentSettings { get; }
        void ApplySettings(in InputDirectionAdapterSettings settings);
    }

    /// <summary>
    /// Adapter that consumes raw InputSystem frames and exposes a direction.
    /// </summary>
    public interface IUserInputAdapter : IInputDirectionAdapter, IInputConsumer
    {
    }

    /// <summary>
    /// Adapter that external systems use to inject a direction programmatically.
    /// </summary>
    public interface IDynamicInputAdapter : IInputDirectionAdapter
    {
        void SetDirection(Vector2 direction);

        void ClearDirection();
    }

    /// <summary>
    /// Movement input payload from adapters.
    /// </summary>
    public readonly struct InputMovementInput
    {
        public readonly Vector2 Direction;
        public readonly float SpeedMultiplier;
        public readonly bool HasSpeedMultiplier;
        public readonly float Accel;
        public readonly float Decel;
        public readonly bool HasAccelerationOverride;

        public InputMovementInput(
            Vector2 direction,
            float speedMultiplier,
            bool hasSpeedMultiplier,
            float accel,
            float decel,
            bool hasAccelerationOverride)
        {
            Direction = direction;
            SpeedMultiplier = speedMultiplier;
            HasSpeedMultiplier = hasSpeedMultiplier;
            Accel = accel;
            Decel = decel;
            HasAccelerationOverride = hasAccelerationOverride;
        }

        public static InputMovementInput FromDirection(Vector2 direction)
            => new InputMovementInput(direction, 1f, false, 0f, 0f, false);
    }

    [Serializable]
    public struct InputDirectionBias
    {
        [SerializeField] public bool Enabled;
        [SerializeField] public Vector2 Direction;
        [SerializeField] public float Strength;
    }

    [Serializable]
    public struct InputAccelerationSettings
    {
        [SerializeField] public bool Enabled;
        [SerializeField, Min(0f), ShowIf(nameof(Enabled))] public float Accel;
        [SerializeField, Min(0f), ShowIf(nameof(Enabled))] public float Decel;
    }

    [Serializable]
    public struct InputDirectionLayer
    {
        [SerializeField] public string Name;
        [SerializeField] public bool Enabled;

        [SerializeField, LabelText("Weight X+ (Right)"), Min(0f)] public float WeightXPlus;
        [SerializeField, LabelText("Weight X- (Left)"), Min(0f)] public float WeightXMinus;
        [SerializeField, LabelText("Weight Y+ (Up)"), Min(0f)] public float WeightYPlus;
        [SerializeField, LabelText("Weight Y- (Down)"), Min(0f)] public float WeightYMinus;

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DraggableItems = true)] public List<InputDirectionBias> Biases;

        [SerializeField] public bool NormalizeAfterLayer;
        [SerializeField, Min(0f)] public float ClampMagnitude;

    }

    [Serializable]
    public struct InputDirectionAdapterSettings
    {
        [SerializeField] public bool Enabled;
        [SerializeField, Min(0f)] public float DeadZone;
        [SerializeField] public bool NormalizeAfterAll;

        [SerializeField, Min(0f)] public float SpeedMultiplier;

        [SerializeField] public InputAccelerationSettings Acceleration;
        [SerializeField, ListDrawerSettings(ShowFoldout = true, DraggableItems = true)] public List<InputDirectionLayer> Layers;

        public static InputDirectionAdapterSettings Default => new InputDirectionAdapterSettings
        {
            Enabled = true,
            DeadZone = 0f,
            NormalizeAfterAll = true,
            SpeedMultiplier = 1f,
            Acceleration = new InputAccelerationSettings
            {
                Enabled = false,
                Accel = 0f,
                Decel = 0f,
            },
            Layers = new List<InputDirectionLayer>(),
        };
    }

    /// <summary>
    /// Default adapter that bridges InputRouter (InputSystem) to the movement service.
    /// </summary>
    public sealed class UserInputAdapter :
        IUserInputAdapter,
        IInputMovementAdapter,
        IInputDirectionSettingsAdapter,
        IInputDirectionTelemetry,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        readonly IInputRouter _inputRouter;
        readonly IInputMovementService _movementService;
        readonly IBaseScalarService? _scalarService;
        readonly IActionBlockService? _actionBlockService;
        readonly List<IInputDirectionSource> _sources;
        readonly IRuntimeResolver _resolver;
        readonly CommandListData _onInputCommands;
        readonly VarStore _commandVars = new();
        readonly VarStore _commandVarsBuffer = new();
        readonly InputConsumerPriority _consumerPriority;
        readonly int _directionPriority;
        InputDirectionAdapterSettings _settings;
        InputMovementInput _movementInput;
        Vector2 _rawDirection;
        Vector2 _direction;
        bool _hasDirection;
        bool _registered;
        bool _disposed;
        string _lastSourceName = "(none)";
        float _weightSpeedMultiplier = 1f;
        bool _hadDirection;
        ICommandRunner? _commandRunner;

        public UserInputAdapter(
            IInputRouter inputRouter,
            IInputMovementService movementService,
            IRuntimeResolver resolver,
            InputDirectionAdapterSettings settings,
            List<IInputDirectionSource> sources,
            CommandListData onInputCommands,
            InputConsumerPriority consumerPriority = InputConsumerPriority.Gameplay,
            int directionPriority = InputDirectionAdapterPriority.User)
        {
            _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
            _movementService = movementService ?? throw new ArgumentNullException(nameof(movementService));
            _scalarService = ResolveScalarService(resolver);
            _actionBlockService = ResolveActionBlockService(resolver);
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _settings = settings;
            _sources = sources ?? new List<IInputDirectionSource>();
            _onInputCommands = onInputCommands ?? new CommandListData();
            _consumerPriority = consumerPriority;
            _directionPriority = directionPriority;

            if (resolver != null && resolver.TryResolve<InputDirectionDebugView>(out var debugView) && debugView != null)
            {
                debugView.Bind(this);
            }
        }

        /// <inheritdoc/>
        public InputConsumerPriority Priority => _consumerPriority;

        /// <inheritdoc/>
        public int DirectionPriority => _directionPriority;

        public InputDirectionAdapterSettings CurrentSettings => _settings;

        public bool HasDirection => _hasDirection;
        public Vector2 RawDirection => _rawDirection;
        public Vector2 ProcessedDirection => _direction;
        public InputMovementInput LastMovementInput => _movementInput;
        public string LastSourceName => _lastSourceName;
        public VarStore CommandVars => _commandVars;

        public void ApplySettings(in InputDirectionAdapterSettings settings)
        {
            _settings = settings;

            if (_disposed)
                return;

            if (_hasDirection)
            {
                _direction = ProcessDirection(_rawDirection);
                _movementInput = BuildMovementInput(_direction);
                _movementService.NotifyDirectionUpdated();
            }
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _registered)
                return;

            _registered = true;
            _movementService.RegisterAdapter(this);
            _inputRouter.RegisterConsumer(this);
            TryResolveCommandRunner();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_registered)
                return;

            _registered = false;
            _movementService.UnregisterAdapter(this);
            _inputRouter.UnregisterConsumer(this);

            _hasDirection = false;
            _direction = Vector2.zero;
            _movementService.NotifyDirectionUpdated();
            _hadDirection = false;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_registered)
            {
                _registered = false;
                _movementService.UnregisterAdapter(this);
                _inputRouter.UnregisterConsumer(this);
            }

            _hasDirection = false;
            _direction = Vector2.zero;
            _movementService.NotifyDirectionUpdated();
            _hadDirection = false;
        }

        /// <inheritdoc/>
        public void UpdateInput(ref InputFrame frame)
        {
            if (_disposed) return;
            if (IsUserMovementBlocked())
                return;

            var hasDirection = false;
            var raw = Vector2.zero;

            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null)
                    continue;
                if (source.TryGetDirection(ref frame, out var dir) && dir != Vector2.zero)
                {
                    hasDirection = true;
                    raw = dir;
                    _lastSourceName = source.GetType().Name;
                    break;
                }
            }

            _rawDirection = hasDirection ? raw : Vector2.zero;
            _direction = hasDirection ? ProcessDirection(_rawDirection) : Vector2.zero;

            if (_direction == Vector2.zero)
            {
                _hasDirection = false;
                _rawDirection = Vector2.zero;
                _movementInput = InputMovementInput.FromDirection(Vector2.zero);
                _lastSourceName = "(none)";
                _weightSpeedMultiplier = 0f;
            }
            else
            {
                _hasDirection = true;
                _movementInput = BuildMovementInput(_direction);
            }

            // Notify the movement service that direction state changed this frame.
            _movementService.NotifyDirectionUpdated();

            if (_hasDirection && !_hadDirection)
            {
                TryExecuteInputCommands(_rawDirection, _direction, _movementInput.SpeedMultiplier);
            }

            _hadDirection = _hasDirection;
        }

        /// <inheritdoc/>
        public bool TryGetDirection(out Vector2 direction)
        {
            direction = _direction;
            return _hasDirection;
        }

        public bool TryGetInput(out InputMovementInput input)
        {
            input = _movementInput;
            return _hasDirection;
        }

        static IBaseScalarService? ResolveScalarService(IRuntimeResolver resolver)
        {
            if (resolver == null)
                return null;
            return resolver.TryResolve<IBaseScalarService>(out var scalar) ? scalar : null;
        }

        static IActionBlockService? ResolveActionBlockService(IRuntimeResolver resolver)
        {
            if (resolver == null)
                return null;
            return resolver.TryResolve<IActionBlockService>(out var service) ? service : null;
        }

        bool IsUserMovementBlocked()
        {
            return _actionBlockService != null &&
                   _actionBlockService.IsBlocked(ActionBlockKeys.Entity.UserMovement);
        }

        void TryExecuteInputCommands(Vector2 rawDirection, Vector2 outputDirection, float speedMultiplier)
        {
            if (_onInputCommands == null || _onInputCommands.Count == 0)
                return;

            if (!TryResolveCommandRunner(out var runner))
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

                    _commandVarsBuffer.Clear();
                    _commandVars.MergeInto(_commandVarsBuffer, overwrite: true);
                    ApplyInputVars(_commandVarsBuffer, rawDirection, outputDirection, speedMultiplier);

                    var ctx = new CommandContext(runner.Scope, _commandVarsBuffer, runner);
                    await runner.ExecuteListAsync(_onInputCommands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        bool TryResolveCommandRunner()
        {
            return TryResolveCommandRunner(out _);
        }

        bool TryResolveCommandRunner(out ICommandRunner runner)
        {
            if (_commandRunner != null)
            {
                runner = _commandRunner;
                return true;
            }

            if (_resolver.TryResolve<ICommandRunner>(out var resolved) && resolved != null)
            {
                _commandRunner = resolved;
                runner = resolved;
                return true;
            }

            runner = null!;
            return false;
        }

        void ApplyInputVars(VarStore vars, Vector2 rawDirection, Vector2 outputDirection, float speedMultiplier)
        {
            if (vars == null)
                return;

            vars.TrySetVariant(VarIds.GameLib.Movement.UserInputAdapter.inputDirection, DynamicVariant.FromVector2(rawDirection));
            vars.TrySetVariant(VarIds.GameLib.Movement.UserInputAdapter.outputDirection, DynamicVariant.FromVector2(outputDirection));
            vars.TrySetVariant(VarIds.GameLib.Movement.UserInputAdapter.SpeedMultiplier, DynamicVariant.FromFloat(speedMultiplier));
        }

        InputMovementInput BuildMovementInput(Vector2 direction)
        {
            var speedMul = Mathf.Max(0f, _settings.SpeedMultiplier);
            bool hasSpeedMul = !Mathf.Approximately(speedMul, 1f);

            if (_weightSpeedMultiplier <= 0f)
            {
                speedMul = 0f;
                hasSpeedMul = true;
            }
            else if (!Mathf.Approximately(_weightSpeedMultiplier, 1f))
            {
                speedMul *= _weightSpeedMultiplier;
                hasSpeedMul = true;
            }

            var accelSettings = _settings.Acceleration;
            if (!accelSettings.Enabled)
            {
                return new InputMovementInput(direction, speedMul, hasSpeedMul, 0f, 0f, false);
            }

            float accel = accelSettings.Accel;
            float decel = accelSettings.Decel;

            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.Accel, out var scalarAccel))
                accel = scalarAccel;
            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.Decel, out var scalarDecel))
                decel = scalarDecel;

            accel = Mathf.Max(0f, accel);
            decel = Mathf.Max(0f, decel);

            return new InputMovementInput(direction, speedMul, hasSpeedMul, accel, decel, true);
        }

        Vector2 ProcessDirection(Vector2 raw)
        {
            if (!_settings.Enabled)
                return raw;

            if (raw.sqrMagnitude <= 0.0001f)
                return Vector2.zero;

            _weightSpeedMultiplier = 1f;

            if (raw.sqrMagnitude > 0f && _settings.DeadZone > 0f)
            {
                if (raw.magnitude < _settings.DeadZone)
                    return Vector2.zero;
            }

            var dir = raw;
            var layers = _settings.Layers;
            if (layers != null && layers.Count > 0)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (!layer.Enabled)
                        continue;

                    dir = ApplyLayer(dir, layer);
                }
            }

            if (_settings.NormalizeAfterAll && dir.sqrMagnitude > 0.0001f)
                dir = dir.normalized;

            _weightSpeedMultiplier = Mathf.Clamp01(_weightSpeedMultiplier);
            return dir;
        }

        Vector2 ApplyLayer(Vector2 dir, in InputDirectionLayer layer)
        {
            var x = dir.x;
            var y = dir.y;

            var wx = x >= 0f
                ? ResolveWeight(layer.WeightXPlus, ScalarKeys.GameLib.Movement.Input.WeightXPlusScalar)
                : ResolveWeight(layer.WeightXMinus, ScalarKeys.GameLib.Movement.Input.WeightXMinusScalar);

            var wy = y >= 0f
                ? ResolveWeight(layer.WeightYPlus, ScalarKeys.GameLib.Movement.Input.WeightYPlusScalar)
                : ResolveWeight(layer.WeightYMinus, ScalarKeys.GameLib.Movement.Input.WeightYMinusScalar);

            dir = new Vector2(x * wx, y * wy);

            _weightSpeedMultiplier = ComputeWeightMultiplier(x, y, wx, wy, _weightSpeedMultiplier);

            var biases = layer.Biases;
            if (biases != null && biases.Count > 0)
            {
                for (int i = 0; i < biases.Count; i++)
                {
                    var bias = biases[i];
                    if (!bias.Enabled)
                        continue;

                    var biasDir = bias.Direction;
                    if (biasDir.sqrMagnitude <= 0.0001f)
                        continue;

                    var strength = ResolveBiasStrength(bias);
                    if (Mathf.Approximately(strength, 0f))
                        continue;

                    dir += biasDir.normalized * strength;
                }
            }

            var clampMagnitude = ResolveClampMagnitude(layer);
            if (clampMagnitude > 0f)
            {
                dir = Vector2.ClampMagnitude(dir, clampMagnitude);
            }

            if (layer.NormalizeAfterLayer && dir.sqrMagnitude > 0.0001f)
                dir = dir.normalized;

            return dir;
        }

        static float ComputeWeightMultiplier(float x, float y, float wx, float wy, float current)
        {
            var absX = Mathf.Abs(x);
            var absY = Mathf.Abs(y);
            var denom = absX + absY;
            if (denom <= 0.0001f)
                return current;

            var weighted = (absX * wx + absY * wy) / denom;
            return current * Mathf.Clamp01(weighted);
        }

        float ResolveWeight(float weight, ScalarKey scalarKey)
        {
            var resolved = weight;
            if (TryGetScalar(scalarKey, out var scalarValue))
                resolved = scalarValue;

            return resolved;
        }

        float ResolveBiasStrength(in InputDirectionBias bias)
        {
            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.BiasStrengthScalar, out var scalarValue))
                return scalarValue;
            return bias.Strength;
        }

        float ResolveClampMagnitude(in InputDirectionLayer layer)
        {
            var magnitude = layer.ClampMagnitude;
            if (TryGetScalar(ScalarKeys.GameLib.Movement.Input.ClampMagnitudeScalar, out var scalarValue))
                magnitude = scalarValue;
            return Mathf.Max(0f, magnitude);
        }

        bool TryGetScalar(ScalarKey key, out float value)
        {
            value = 0f;
            if (_scalarService == null)
                return false;

            if (!IsScalarKeyValid(key))
                return false;

            return _scalarService.GlobalTryGet(key, out value);
        }

        static bool IsScalarKeyValid(ScalarKey key)
        {
            return !string.IsNullOrEmpty(key.Name);
        }
    }

    /// <summary>
    /// Adapter for services that drive movement direction directly (e.g. AI or scripted control).
    /// </summary>
    public sealed class DynamicInputAdapter :
        IDynamicInputAdapter,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        readonly IInputMovementService _movementService;
        readonly int _directionPriority;
        Vector2 _direction;
        bool _hasDirection;
        bool _registered;
        bool _disposed;

        public DynamicInputAdapter(
            IInputMovementService movementService,
            int directionPriority = InputDirectionAdapterPriority.Dynamic)
        {
            _movementService = movementService ?? throw new ArgumentNullException(nameof(movementService));
            _directionPriority = directionPriority;
        }

        /// <inheritdoc/>
        public int DirectionPriority => _directionPriority;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _registered)
                return;

            _registered = true;
            _movementService.RegisterAdapter(this);
            _movementService.NotifyDirectionUpdated();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_registered)
                return;

            _registered = false;
            _movementService.UnregisterAdapter(this);
            _hasDirection = false;
            _direction = Vector2.zero;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_registered)
            {
                _registered = false;
                _movementService.UnregisterAdapter(this);
            }

            _hasDirection = false;
            _direction = Vector2.zero;
        }

        /// <inheritdoc/>
        public bool TryGetDirection(out Vector2 direction)
        {
            direction = _direction;
            return _hasDirection;
        }

        /// <inheritdoc/>
        public void SetDirection(Vector2 direction)
        {
            _direction = direction;
            _hasDirection = direction != Vector2.zero;
            _movementService.NotifyDirectionUpdated();
        }

        /// <inheritdoc/>
        public void ClearDirection()
        {
            _direction = Vector2.zero;
            _hasDirection = false;
            _movementService.NotifyDirectionUpdated();
        }
    }
}
