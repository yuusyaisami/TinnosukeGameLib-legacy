#nullable enable
using System;
using Game.TransformSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer; // 縺薙ｌ繧呈ｶ医＆縺ｪ縺・〒縺上□縺輔＞ resolver.TryResolve 逕ｨ
namespace Game.Channel
{
    [Serializable]
    public enum TransformFollowVelocitySourceType
    {
        TransformChannel = 0,
        Rigidbody2D = 1,
    }

    [Serializable]
    public struct TransformFollowOptions
    {
        [LabelText("Smooth Time")]
        [MinValue(0f)]
        public float SmoothTime;

        [LabelText("Follow X")]
        public bool FollowX;

        [LabelText("Follow Y")]
        public bool FollowY;

        [LabelText("Max Speed")]
        [MinValue(0f)]
        public float MaxSpeed;

        [LabelText("Use Velocity Offset")]
        public bool UseVelocityOffset;

        [LabelText("Base Target Offset")]
        [Tooltip("Inspector setting.")]
        public Vector3 BaseTargetOffset;

        [LabelText("Velocity Offset Scale")]
        [ShowIf(nameof(UseVelocityOffset))]
        public Vector2 VelocityOffsetScale;

        [LabelText("Velocity Offset Weight By Speed")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UseVelocityOffset))]
        [InlineProperty]
        public TransformFollowVelocityWeightSettings VelocityWeight;

        [LabelText("Velocity Source")]
        [ShowIf(nameof(UseVelocityOffset))]
        public TransformFollowVelocitySourceType VelocitySourceType;

        [LabelText("Limit Turn Rate")]
        public bool LimitTurnRate;

        [LabelText("Turn Rate (deg/s)")]
        [ShowIf(nameof(LimitTurnRate))]
        [MinValue(0f)]
        public float TurnRate;
    }

    [Serializable]
    public struct TransformFollowVelocityWeightSettings
    {
        [LabelText("Enable Speed-Based Weight")]
        [Tooltip("Inspector setting.")]
        public bool Enabled;

        [LabelText("+ Soft Speed Limit (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 SoftLimitPos;

        [LabelText("+ Hard Speed Limit (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 HardLimitPos;

        [LabelText("- Soft Speed Limit (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 SoftLimitNeg;

        [LabelText("- Hard Speed Limit (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 HardLimitNeg;

        [LabelText("+ Min Weight (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MinWeightPos;

        [LabelText("+ Max Weight (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MaxWeightPos;

        [LabelText("- Min Weight (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MinWeightNeg;

        [LabelText("- Max Weight (X,Y)")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MaxWeightNeg;

        public static TransformFollowVelocityWeightSettings Default => new TransformFollowVelocityWeightSettings
        {
            Enabled = false,
            SoftLimitPos = Vector2.zero,
            HardLimitPos = Vector2.one,
            SoftLimitNeg = Vector2.zero,
            HardLimitNeg = Vector2.one,
            MinWeightPos = Vector2.one,
            MaxWeightPos = Vector2.one,
            MinWeightNeg = Vector2.one,
            MaxWeightNeg = Vector2.one,
        };
    }

    public sealed class TransformFollowService
    {
        Transform? _targetTransform;
        Vector3 _targetPosition;
        bool _useTransformTarget;
        Vector3 _smoothVelocity;
        Vector3 _currentDirection;
        ITransformChannelPoseReader? _velocitySource;
        Rigidbody2D? _rigidbodyVelocitySource;

        public bool UseTransformTarget => _useTransformTarget;
        public Transform? TargetTransform => _targetTransform;
        public Vector3 TargetPosition => _targetPosition;
        public Vector3 SmoothVelocity => _smoothVelocity;
        public Vector3 CurrentDirection => _currentDirection;
        public bool HasTransformChannelVelocitySource => _velocitySource != null;
        public bool HasRigidbody2DVelocitySource => _rigidbodyVelocitySource != null;

        public void SetTarget(Transform target, in TransformFollowOptions options)
        {
            _useTransformTarget = true;
            _targetTransform = target;
            _targetPosition = default;
            _smoothVelocity = Vector3.zero;
            _currentDirection = Vector3.zero;
            _velocitySource = null;
            _rigidbodyVelocitySource = null;

            if (options.UseVelocityOffset)
            {
                switch (options.VelocitySourceType)
                {
                    case TransformFollowVelocitySourceType.Rigidbody2D:
                        _rigidbodyVelocitySource = ResolveRigidbodySource(target);
                        break;
                    case TransformFollowVelocitySourceType.TransformChannel:
                    default:
                        _velocitySource = ResolveVelocitySource(target);
                        break;
                }
            }
        }

        public void SetTarget(Vector3 position)
        {
            _useTransformTarget = false;
            _targetTransform = null;
            _targetPosition = position;
            _smoothVelocity = Vector3.zero;
            _currentDirection = Vector3.zero;
            _velocitySource = null;
            _rigidbodyVelocitySource = null;
        }

        public Vector3 Update(Vector3 currentPosition, float deltaTime, in TransformFollowOptions options)
        {
            if (deltaTime <= 0f)
                return currentPosition;

            var targetPosition = ResolveTargetPosition(options);
            if (!options.FollowX)
                targetPosition.x = currentPosition.x;
            if (!options.FollowY)
                targetPosition.y = currentPosition.y;
            var desired = targetPosition - currentPosition;

            if (options.LimitTurnRate && options.TurnRate > 0f && desired.sqrMagnitude > 0.0001f)
            {
                var desiredDir = desired.normalized;
                var currentDir = _currentDirection.sqrMagnitude > 0.0001f
                    ? _currentDirection.normalized
                    : desiredDir;

                var maxRadians = Mathf.Deg2Rad * options.TurnRate * deltaTime;
                var newDir = Vector3.RotateTowards(currentDir, desiredDir, maxRadians, 0f);
                _currentDirection = newDir;
                targetPosition = currentPosition + newDir * desired.magnitude;
            }
            else
            {
                _currentDirection = desired;
            }

            if (options.SmoothTime > 0f)
            {
                var maxSpeed = options.MaxSpeed > 0f ? options.MaxSpeed : Mathf.Infinity;
                return Vector3.SmoothDamp(currentPosition, targetPosition, ref _smoothVelocity, options.SmoothTime, maxSpeed, deltaTime);
            }

            if (options.MaxSpeed > 0f)
            {
                var t = 1f - Mathf.Exp(-options.MaxSpeed * deltaTime);
                return Vector3.Lerp(currentPosition, targetPosition, t);
            }

            return targetPosition;
        }

        public Vector3 ResolveCurrentTargetPosition(Vector3 currentPosition, in TransformFollowOptions options)
        {
            var targetPosition = ResolveTargetPosition(options);
            if (!options.FollowX)
                targetPosition.x = currentPosition.x;
            if (!options.FollowY)
                targetPosition.y = currentPosition.y;
            return targetPosition;
        }

        Vector3 ResolveTargetPosition(in TransformFollowOptions options)
        {
            if (_useTransformTarget)
            {
                if (_targetTransform == null)
                    return _targetPosition;

                var targetPosition = _targetTransform.position;
                targetPosition += options.BaseTargetOffset;
                if (options.UseVelocityOffset)
                {
                    var offset = ResolveVelocityOffset(options);
                    targetPosition += offset;
                }

                _targetPosition = targetPosition;
                return targetPosition;
            }

            return _targetPosition + options.BaseTargetOffset;
        }

        static ITransformChannelPoseReader? ResolveVelocitySource(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                var scope = TryGetScopeNode(current);
                if (scope?.Resolver == null)
                    continue;

                if (scope.Resolver.TryResolve<ITransformChannelPoseReader>(out var poseReader) && poseReader != null)
                    return poseReader;
            }

            return null;
        }

        static Rigidbody2D? ResolveRigidbodySource(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                var rb = current.GetComponent<Rigidbody2D>();
                if (rb != null)
                    return rb;
            }

            return null;
        }

        Vector3 ResolveVelocityOffset(in TransformFollowOptions options)
        {
            var weightSettings = options.VelocityWeight;
            switch (options.VelocitySourceType)
            {
                case TransformFollowVelocitySourceType.Rigidbody2D:
                    if (_rigidbodyVelocitySource != null)
                    {
                        var v = _rigidbodyVelocitySource.linearVelocity;
                        var weightX = ResolveAxisWeight(v.x, in weightSettings, true);
                        var weightY = ResolveAxisWeight(v.y, in weightSettings, false);
                        return new Vector3(
                            v.x * options.VelocityOffsetScale.x * weightX,
                            v.y * options.VelocityOffsetScale.y * weightY,
                            0f);
                    }
                    break;
                case TransformFollowVelocitySourceType.TransformChannel:
                default:
                    if (_velocitySource != null)
                    {
                        var v = _velocitySource.CurrentVelocity;
                        var weightX = ResolveAxisWeight(v.x, in weightSettings, true);
                        var weightY = ResolveAxisWeight(v.y, in weightSettings, false);
                        return new Vector3(
                            v.x * options.VelocityOffsetScale.x * weightX,
                            v.y * options.VelocityOffsetScale.y * weightY,
                            0f);
                    }
                    break;
            }

            return Vector3.zero;
        }

        static float ResolveAxisWeight(float velocity, in TransformFollowVelocityWeightSettings settings, bool isX)
        {
            if (!settings.Enabled)
                return 1f;

            var speed = Mathf.Abs(velocity);
            var isPositive = velocity >= 0f;

            var soft = isPositive ? settings.SoftLimitPos : settings.SoftLimitNeg;
            var hard = isPositive ? settings.HardLimitPos : settings.HardLimitNeg;
            var min = isPositive ? settings.MinWeightPos : settings.MinWeightNeg;
            var max = isPositive ? settings.MaxWeightPos : settings.MaxWeightNeg;

            var softLimit = Mathf.Abs(isX ? soft.x : soft.y);
            var hardLimit = Mathf.Abs(isX ? hard.x : hard.y);
            var minWeight = isX ? min.x : min.y;
            var maxWeight = isX ? max.x : max.y;

            if (hardLimit <= softLimit)
                return Mathf.Max(minWeight, maxWeight);

            if (speed <= softLimit)
                return minWeight;

            if (speed >= hardLimit)
                return maxWeight;

            var t = Mathf.InverseLerp(softLimit, hardLimit, speed);
            return Mathf.Lerp(minWeight, maxWeight, t);
        }

        static IScopeNode? TryGetScopeNode(Transform t)
        {
            if (t == null)
                return null;

            return ScopeFeatureInstallerUtility.TryGetScopeNode(t, includeInactive: true, out var node)
                ? node
                : null;
        }
    }
}
