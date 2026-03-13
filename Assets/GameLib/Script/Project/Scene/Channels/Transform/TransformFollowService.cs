#nullable enable
using System;
using Game.TransformSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer; // これを消さないでください resolver.TryResolve 用
namespace Game.Channel
{
    [Serializable]
    public enum TransformFollowVelocitySourceType
    {
        TransformController = 0,
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
        [Tooltip("追従ターゲットの基準位置に常に加算する固定オフセットです。速度に依存せず常時適用されます。")]
        public Vector3 BaseTargetOffset;

        [LabelText("Velocity Offset Scale")]
        [ShowIf(nameof(UseVelocityOffset))]
        public Vector2 VelocityOffsetScale;

        [LabelText("Velocity Offset Weight By Speed")]
        [Tooltip("速度に応じて、Velocity Offset の寄与率を軸/方向ごとに補正します。")]
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
        [Tooltip("有効時のみ、速度に応じて Offset Weight を補正します。無効時は常に Weight=1 です。")]
        public bool Enabled;

        [LabelText("+ Soft Speed Limit (X,Y)")]
        [Tooltip("+方向(+X,+Y)の速度がこの値以下なら Min Weight を使用します。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 SoftLimitPos;

        [LabelText("+ Hard Speed Limit (X,Y)")]
        [Tooltip("+方向(+X,+Y)の速度がこの値以上なら Max Weight を使用します。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 HardLimitPos;

        [LabelText("- Soft Speed Limit (X,Y)")]
        [Tooltip("-方向(-X,-Y)の速度がこの値以下なら Min Weight を使用します。符号は自動で絶対値化されます。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 SoftLimitNeg;

        [LabelText("- Hard Speed Limit (X,Y)")]
        [Tooltip("-方向(-X,-Y)の速度がこの値以上なら Max Weight を使用します。符号は自動で絶対値化されます。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 HardLimitNeg;

        [LabelText("+ Min Weight (X,Y)")]
        [Tooltip("+方向(+X,+Y)の低速時に使う Weight。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MinWeightPos;

        [LabelText("+ Max Weight (X,Y)")]
        [Tooltip("+方向(+X,+Y)の高速時に使う Weight。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MaxWeightPos;

        [LabelText("- Min Weight (X,Y)")]
        [Tooltip("-方向(-X,-Y)の低速時に使う Weight。")]
        [ShowIf(nameof(Enabled))]
        public Vector2 MinWeightNeg;

        [LabelText("- Max Weight (X,Y)")]
        [Tooltip("-方向(-X,-Y)の高速時に使う Weight。")]
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
        TransformControllerService? _velocitySource;
        Rigidbody2D? _rigidbodyVelocitySource;

        public bool UseTransformTarget => _useTransformTarget;
        public Transform? TargetTransform => _targetTransform;
        public Vector3 TargetPosition => _targetPosition;
        public Vector3 SmoothVelocity => _smoothVelocity;
        public Vector3 CurrentDirection => _currentDirection;
        public bool HasTransformControllerVelocitySource => _velocitySource != null;
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
                    case TransformFollowVelocitySourceType.TransformController:
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

        static TransformControllerService? ResolveVelocitySource(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                var scope = TryGetScopeNode(current);
                if (scope?.Resolver == null)
                    continue;

                if (scope.Resolver.TryResolve<TransformControllerService>(out var service) && service != null)
                    return service;
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
                case TransformFollowVelocitySourceType.TransformController:
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

            var baseScope = t.GetComponent<BaseLifetimeScope>();
            if (baseScope != null)
                return baseScope;

            var runtimeScope = t.GetComponent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
                return runtimeScope;

            return null;
        }
    }
}
