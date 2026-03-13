#nullable enable
using System;
using Game.Movement;
using Game.Rotation;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    public sealed class FireOutputDirectionAdapter :
        IOutputFirePattern,
        IInputMovementAdapter,
        IInputDirectionAdapter,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        const string RotateChannelKey = "pattern";
        readonly IInputMovementService _movementService;
        readonly IObjectResolver _resolver;
        readonly int _directionPriority;
        readonly bool _enableDebugLog;

        IRotateChannelHub? _rotateHub;
        IRotateChannelHandle? _rotateChannel;
        bool _ownsRotateChannel;

        Vector2 _direction;
        bool _hasDirection;
        InputMovementInput _input;
        bool _registered;
        bool _disposed;

        public int DirectionPriority => _directionPriority;

        public FireOutputDirectionAdapter(
            IInputMovementService movementService,
            IObjectResolver resolver,
            int directionPriority = InputDirectionAdapterPriority.Dynamic,
            bool enableDebugLog = false)
        {
            _movementService = movementService ?? throw new ArgumentNullException(nameof(movementService));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _directionPriority = directionPriority;
            _enableDebugLog = enableDebugLog;
        }


        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _registered)
                return;

            _registered = true;
            _movementService.RegisterAdapter(this);
            _movementService.NotifyDirectionUpdated();

            // Rotation channel is created lazily only when RotationSpeed != 0.
            _rotateHub = null;

            //if (_enableDebugLog)
            //    Debug.Log("[FireOutputDirectionAdapter] OnAcquire: adapter registered.");

        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_registered)
                return;

            _registered = false;
            _movementService.UnregisterAdapter(this);
            ClearDirection();
            ResetRotation();

            //if (_enableDebugLog)
            //    Debug.Log("[FireOutputDirectionAdapter] OnRelease: adapter unregistered.");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_registered)
            {
                _registered = false;
                _movementService.UnregisterAdapter(this);
            }

            ClearDirection();
            ResetRotation();
        }

        public void OnFireContextReceived(in FireContext context)
        {
            if (_disposed)
                return;

            var d = new Vector2(context.FinalDirection.x, context.FinalDirection.y);
            if (d.sqrMagnitude <= 0.000001f)
            {
                ClearDirection();
                if (_enableDebugLog)
                    Debug.Log("[FireOutputDirectionAdapter] OnFireContextReceived: zero direction -> cleared.");
                return;
            }

            _hasDirection = true;
            _direction = d.normalized;
            var speedMultiplier = Mathf.Max(0f, context.Data.SpeedMultiplier);
            _input = new InputMovementInput(
                direction: _direction,
                speedMultiplier: speedMultiplier,
                hasSpeedMultiplier: !Mathf.Approximately(speedMultiplier, 1f),
                accel: 0f,
                decel: 0f,
                hasAccelerationOverride: false);
            _movementService.NotifyDirectionUpdated();

            if (_enableDebugLog)
            {
                Debug.Log(
                    $"[FireOutputDirectionAdapter] FireContext received: finalDir={context.FinalDirection} " +
                    $"speedMul={context.Data.SpeedMultiplier:F3} rotSpeed={context.Data.RotationSpeed:F3} " +
                    $"finalPos={context.FinalPosition}");
            }

            var rotSpeed = context.Data.RotationSpeed;
            if (Mathf.Abs(rotSpeed) <= 0.0001f)
            {
                if (_rotateChannel != null)
                    _rotateChannel.AngularVelocity = 0f;
                return;
            }

            if (_rotateHub == null)
                _resolver.TryResolve(out _rotateHub);
            EnsureRotateChannel();
            if (_rotateChannel != null)
                _rotateChannel.AngularVelocity = rotSpeed;
        }

        public bool TryGetDirection(out Vector2 direction)
        {
            direction = _direction;
            return _hasDirection;
        }

        public bool TryGetInput(out InputMovementInput input)
        {
            input = _input;
            return _hasDirection;
        }

        void ClearDirection()
        {
            _hasDirection = false;
            _direction = Vector2.zero;
            _input = InputMovementInput.FromDirection(Vector2.zero);
            _movementService.NotifyDirectionUpdated();
        }

        void EnsureRotateChannel()
        {
            if (_rotateHub == null || _rotateChannel != null)
                return;

            if (_rotateHub.TryGetChannel(RotateChannelKey, out var existing) && existing != null)
            {
                _rotateChannel = existing;
                _ownsRotateChannel = false;
                return;
            }

            _rotateChannel = _rotateHub.RegisterChannel(RotateChannelKey, RotateChannelDef.Pattern(RotateChannelKey));
            _ownsRotateChannel = true;
        }

        void ResetRotation()
        {
            if (_rotateChannel != null)
                _rotateChannel.AngularVelocity = 0f;

            if (_ownsRotateChannel && _rotateHub != null)
                _rotateHub.UnregisterChannel(RotateChannelKey);

            _rotateChannel = null;
            _rotateHub = null;
            _ownsRotateChannel = false;
        }
    }
}
