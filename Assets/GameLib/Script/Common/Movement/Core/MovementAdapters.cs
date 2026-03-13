// Game.Movement.MovementAdapters.cs
//
// Movement 出力アダプタ（Rigidbody2D, Transform）。

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Rigidbody2D への速度反映モード。
    /// </summary>
    public enum Rigidbody2DVelocityApplyMode
    {
        Override = 0,
        Additive = 1,
    }

    [Serializable]
    public struct Rigidbody2DAdditiveControlSettings
    {
        [LabelText("Enabled")]
        public bool Enabled;

        [LabelText("Axis Weight +"), ShowIf("Enabled")]
        public Vector2 AxisWeightPos;

        [LabelText("Axis Weight -"), ShowIf("Enabled")]
        public Vector2 AxisWeightNeg;

        [LabelText("Speed Soft Limit +"), ShowIf("Enabled")]
        public Vector2 SpeedSoftLimitPos;

        [LabelText("Speed Soft Limit -"), ShowIf("Enabled")]
        public Vector2 SpeedSoftLimitNeg;

        [LabelText("Speed Hard Limit +"), ShowIf("Enabled")]
        public Vector2 SpeedHardLimitPos;

        [LabelText("Speed Hard Limit -"), ShowIf("Enabled")]
        public Vector2 SpeedHardLimitNeg;

        [LabelText("Accel Soft Limit +"), ShowIf("Enabled")]
        public Vector2 AccelSoftLimitPos;

        [LabelText("Accel Soft Limit -"), ShowIf("Enabled")]
        public Vector2 AccelSoftLimitNeg;

        [LabelText("Accel Hard Limit +"), ShowIf("Enabled")]
        public Vector2 AccelHardLimitPos;

        [LabelText("Accel Hard Limit -"), ShowIf("Enabled")]
        public Vector2 AccelHardLimitNeg;

        [LabelText("Clamp To Hard Limit"), ShowIf("Enabled")]
        public bool ClampResultToHardLimit;

        [LabelText("Min Scale"), ShowIf("Enabled")]
        [MinValue(0f)]
        public float MinScale;

        [LabelText("Axis Override"), ShowIf("Enabled"), InlineProperty]
        public Rigidbody2DAdditiveOverrideSettings AxisOverride;

        public static Rigidbody2DAdditiveControlSettings Default => new Rigidbody2DAdditiveControlSettings
        {
            Enabled = false,
            AxisWeightPos = Vector2.one,
            AxisWeightNeg = Vector2.one,
            SpeedSoftLimitPos = Vector2.zero,
            SpeedSoftLimitNeg = Vector2.zero,
            SpeedHardLimitPos = Vector2.zero,
            SpeedHardLimitNeg = Vector2.zero,
            AccelSoftLimitPos = Vector2.zero,
            AccelSoftLimitNeg = Vector2.zero,
            AccelHardLimitPos = Vector2.zero,
            AccelHardLimitNeg = Vector2.zero,
            ClampResultToHardLimit = false,
            MinScale = 0f,
            AxisOverride = Rigidbody2DAdditiveOverrideSettings.Default,
        };
    }

    [Serializable]
    public struct Rigidbody2DAdditiveOverrideSettings
    {
        [LabelText("Enabled")]
        public bool Enabled;

        [LabelText("Override X")]
        public bool OverrideX;

        [LabelText("Override Y")]
        public bool OverrideY;

        [LabelText("Min Current Speed")]
        [MinValue(0f)]
        public float MinCurrentSpeed;

        [LabelText("Min Input")]
        [MinValue(0f)]
        public float MinInput;

        [LabelText("Velocity Change Epsilon")]
        [MinValue(0f)]
        public float VelocityChangeEpsilon;

        public static Rigidbody2DAdditiveOverrideSettings Default => new Rigidbody2DAdditiveOverrideSettings
        {
            Enabled = false,
            OverrideX = false,
            OverrideY = false,
            MinCurrentSpeed = 0.1f,
            MinInput = 0.01f,
            VelocityChangeEpsilon = 0f,
        };
    }

    [Serializable]
    public struct Rigidbody2DGravityClampSettings
    {
        [LabelText("Enabled")]
        public bool Enabled;

        [LabelText("Min Fall Speed (Y)")]
        [MinValue(0f)]
        public float MinFallSpeed;

        public static Rigidbody2DGravityClampSettings Default => new Rigidbody2DGravityClampSettings
        {
            Enabled = false,
            MinFallSpeed = 0f,
        };
    }

    /// <summary>
    /// Movement アダプタの基底インターフェース。
    /// </summary>
    public interface IMovementAdapter : IDisposable
    {
        /// <summary>更新</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Rigidbody2D への Movement 出力アダプタ。
    /// Output.Value を Rigidbody2D.linearVelocity に反映。
    /// </summary>
    public sealed class Rigidbody2DMovementAdapter : IMovementAdapter
    {
        const float GravityAxisInputDeadZoneY = 0.01f;

        readonly Rigidbody2D _rb;
        readonly IMovementOutput _output;
        readonly Rigidbody2DVelocityApplyMode _applyMode;
        readonly Rigidbody2DAdditiveControlSettings _additiveSettings;
        readonly Rigidbody2DGravityClampSettings _gravityClamp;
        bool _disposed;
        bool _hasLastVelocity;
        Vector2 _lastVelocity;
        float _lastOverrideSignX;
        float _lastOverrideSignY;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Rigidbody2DMovementAdapter(Rigidbody2D rb, IMovementOutput output)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _applyMode = Rigidbody2DVelocityApplyMode.Override;
        }

        public Rigidbody2DMovementAdapter(
            Rigidbody2D rb,
            IMovementOutput output,
            Rigidbody2DVelocityApplyMode applyMode)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _applyMode = applyMode;
            _additiveSettings = Rigidbody2DAdditiveControlSettings.Default;
            _gravityClamp = Rigidbody2DGravityClampSettings.Default;
        }

        public Rigidbody2DMovementAdapter(
            Rigidbody2D rb,
            IMovementOutput output,
            Rigidbody2DVelocityApplyMode applyMode,
            Rigidbody2DAdditiveControlSettings additiveSettings)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _applyMode = applyMode;
            _additiveSettings = additiveSettings;
            _gravityClamp = Rigidbody2DGravityClampSettings.Default;
        }

        public Rigidbody2DMovementAdapter(
            Rigidbody2D rb,
            IMovementOutput output,
            Rigidbody2DVelocityApplyMode applyMode,
            Rigidbody2DAdditiveControlSettings additiveSettings,
            Rigidbody2DGravityClampSettings gravityClamp)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _applyMode = applyMode;
            _additiveSettings = additiveSettings;
            _gravityClamp = gravityClamp;
        }

        /// <summary>
        /// 更新（変更があった場合のみ Rigidbody に反映）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _rb == null) return;

            var vel = _output.Value;
            // In additive mode, tiny Y drift can accumulate into noticeable upward acceleration.
            // When gravity is active, treat near-zero Y input as zero to avoid runaway.
            if (Mathf.Abs(_rb.gravityScale) > 0.0001f && Mathf.Abs(vel.y) < GravityAxisInputDeadZoneY)
            {
                vel.y = 0f;
            }

            var current = _rb.linearVelocity;
            switch (_applyMode)
            {
                case Rigidbody2DVelocityApplyMode.Additive:
                    if (_additiveSettings.Enabled)
                    {
                        var accel = ResolveAcceleration(current, deltaTime);
                        var scaleX = ResolveAxisScale(
                            current.x,
                            accel.x,
                            vel.x,
                            _additiveSettings.AxisWeightPos.x,
                            _additiveSettings.AxisWeightNeg.x,
                            _additiveSettings.SpeedSoftLimitPos.x,
                            _additiveSettings.SpeedSoftLimitNeg.x,
                            _additiveSettings.SpeedHardLimitPos.x,
                            _additiveSettings.SpeedHardLimitNeg.x,
                            _additiveSettings.AccelSoftLimitPos.x,
                            _additiveSettings.AccelSoftLimitNeg.x,
                            _additiveSettings.AccelHardLimitPos.x,
                            _additiveSettings.AccelHardLimitNeg.x,
                            _additiveSettings.MinScale);
                        var scaleY = ResolveAxisScale(
                            current.y,
                            accel.y,
                            vel.y,
                            _additiveSettings.AxisWeightPos.y,
                            _additiveSettings.AxisWeightNeg.y,
                            _additiveSettings.SpeedSoftLimitPos.y,
                            _additiveSettings.SpeedSoftLimitNeg.y,
                            _additiveSettings.SpeedHardLimitPos.y,
                            _additiveSettings.SpeedHardLimitNeg.y,
                            _additiveSettings.AccelSoftLimitPos.y,
                            _additiveSettings.AccelSoftLimitNeg.y,
                            _additiveSettings.AccelHardLimitPos.y,
                            _additiveSettings.AccelHardLimitNeg.y,
                            _additiveSettings.MinScale);

                        var scaledAdd = new Vector2(
                            LimitAddByHardCap(current.x, vel.x * scaleX, _additiveSettings.SpeedHardLimitPos.x, _additiveSettings.SpeedHardLimitNeg.x),
                            LimitAddByHardCap(current.y, vel.y * scaleY, _additiveSettings.SpeedHardLimitPos.y, _additiveSettings.SpeedHardLimitNeg.y));
                        var next = current + scaledAdd;
                        ApplyAxisOverride(ref next, current, vel, _lastVelocity);
                        if (_additiveSettings.ClampResultToHardLimit)
                        {
                            next.x = ClampAxis(next.x, _additiveSettings.SpeedHardLimitPos.x, _additiveSettings.SpeedHardLimitNeg.x);
                            next.y = ClampAxis(next.y, _additiveSettings.SpeedHardLimitPos.y, _additiveSettings.SpeedHardLimitNeg.y);
                        }
                        _rb.linearVelocity = next;
                    }
                    else
                    {
                        _rb.linearVelocity = current + vel;
                    }
                    break;

                case Rigidbody2DVelocityApplyMode.Override:
                default:
                    if (Mathf.Abs(_rb.gravityScale) > 0.0001f)
                    {
                        // Preserve gravity on Y while applying input velocity.
                        _rb.linearVelocity = new Vector2(vel.x, current.y + vel.y);
                    }
                    else
                    {
                        _rb.linearVelocity = vel;
                    }
                    break;
            }

            if (_gravityClamp.Enabled && _gravityClamp.MinFallSpeed > 0f)
            {
                var v = _rb.linearVelocity;
                var minY = -_gravityClamp.MinFallSpeed;
                if (v.y < minY)
                    v.y = minY;
                _rb.linearVelocity = v;
            }

            _lastVelocity = _rb.linearVelocity;
            _hasLastVelocity = true;
        }

        Vector2 ResolveAcceleration(Vector2 current, float deltaTime)
        {
            if (!_hasLastVelocity || deltaTime <= 0f)
                return Vector2.zero;

            return (current - _lastVelocity) / deltaTime;
        }

        static float ResolveAxisScale(
            float velocity,
            float acceleration,
            float inputAdd,
            float weightPos,
            float weightNeg,
            float speedSoftPos,
            float speedSoftNeg,
            float speedHardPos,
            float speedHardNeg,
            float accelSoftPos,
            float accelSoftNeg,
            float accelHardPos,
            float accelHardNeg,
            float minScale)
        {
            var sign = Mathf.Abs(inputAdd) > 0.0001f ? Mathf.Sign(inputAdd) : Mathf.Sign(velocity);
            var weight = sign >= 0f ? weightPos : weightNeg;
            if (weight <= 0f)
                return 0f;

            var signedSpeed = velocity * sign;
            var speedValue = signedSpeed > 0f ? signedSpeed : 0f;
            var speedScale = ComputeAttenuation(speedValue,
                sign >= 0f ? speedSoftPos : speedSoftNeg,
                sign >= 0f ? speedHardPos : speedHardNeg);

            var signedAccel = acceleration * sign;
            var accelValue = signedAccel > 0f ? signedAccel : 0f;
            var accelScale = ComputeAttenuation(accelValue,
                sign >= 0f ? accelSoftPos : accelSoftNeg,
                sign >= 0f ? accelHardPos : accelHardNeg);
            var scale = weight * Mathf.Min(speedScale, accelScale);

            if (minScale > 0f)
                scale = Mathf.Max(scale, minScale);

            return Mathf.Clamp01(scale);
        }

        static float ComputeAttenuation(float value, float softLimit, float hardLimit)
        {
            if (hardLimit <= 0f)
                return 1f;

            var soft = Mathf.Max(0f, softLimit);
            var hard = Mathf.Max(soft, hardLimit);

            if (value <= soft)
                return 1f;
            if (value >= hard)
                return 0f;

            return 1f - (value - soft) / (hard - soft);
        }

        static float ClampAxis(float value, float hardLimit)
        {
            if (hardLimit <= 0f)
                return value;

            return Mathf.Clamp(value, -hardLimit, hardLimit);
        }

        static float ClampAxis(float value, float hardLimitPos, float hardLimitNeg)
        {
            var pos = Mathf.Max(0f, hardLimitPos);
            var neg = Mathf.Max(0f, hardLimitNeg);
            if (pos <= 0f && neg <= 0f)
                return value;

            var min = neg > 0f ? -neg : float.NegativeInfinity;
            var max = pos > 0f ? pos : float.PositiveInfinity;
            return Mathf.Clamp(value, min, max);
        }

        static float LimitAddByHardCap(float current, float add, float hardLimitPos, float hardLimitNeg)
        {
            if (Mathf.Abs(add) <= 0.000001f)
                return 0f;

            var sign = Mathf.Sign(add);
            var hard = sign >= 0f ? Mathf.Max(0f, hardLimitPos) : Mathf.Max(0f, hardLimitNeg);
            if (hard <= 0f)
                return add;

            var signedCurrent = current * sign;
            var remaining = hard - signedCurrent;
            if (remaining <= 0f)
                return 0f;

            var maxAdd = remaining;
            var desired = Mathf.Abs(add);
            var clamped = desired > maxAdd ? maxAdd : desired;
            return clamped * sign;
        }

        void ApplyAxisOverride(ref Vector2 next, Vector2 current, Vector2 inputAdd, Vector2 prev)
        {
            var settings = _additiveSettings.AxisOverride;
            if (!settings.Enabled)
                return;

            var inputX = inputAdd.x;
            if (settings.OverrideX)
            {
                var sign = Mathf.Abs(inputX) > settings.MinInput ? Mathf.Sign(inputX) : 0f;
                if (sign == 0f)
                {
                    _lastOverrideSignX = 0f;
                }
                else if (sign != _lastOverrideSignX)
                {
                    var currentAbs = Mathf.Abs(current.x);
                    var changed = settings.VelocityChangeEpsilon <= 0f || Mathf.Abs(current.x - prev.x) > settings.VelocityChangeEpsilon;
                    if (currentAbs >= settings.MinCurrentSpeed && changed)
                    {
                        next.x = inputAdd.x;
                        _lastOverrideSignX = sign;
                    }
                }
            }

            var inputY = inputAdd.y;
            if (settings.OverrideY)
            {
                var sign = Mathf.Abs(inputY) > settings.MinInput ? Mathf.Sign(inputY) : 0f;
                if (sign == 0f)
                {
                    _lastOverrideSignY = 0f;
                }
                else if (sign != _lastOverrideSignY)
                {
                    var currentAbs = Mathf.Abs(current.y);
                    var changed = settings.VelocityChangeEpsilon <= 0f || Mathf.Abs(current.y - prev.y) > settings.VelocityChangeEpsilon;
                    if (currentAbs >= settings.MinCurrentSpeed && changed)
                    {
                        next.y = inputAdd.y;
                        _lastOverrideSignY = sign;
                    }
                }
            }
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Transform への Movement 出力アダプタ。
    /// Output.Value * deltaTime を Transform.position に加算。
    /// </summary>
    public sealed class TransformMovementAdapter : IMovementAdapter
    {
        readonly Transform _transform;
        readonly IMovementOutput _output;
        uint _lastVersion;
        bool _disposed;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public TransformMovementAdapter(Transform transform, IMovementOutput output)
        {
            _transform = transform ?? throw new ArgumentNullException(nameof(transform));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// 更新（変更があった場合のみ Transform に反映）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _transform == null) return;

            // Apply every frame: velocity is per-second and should accumulate each frame
            var vel = _output.Value;
            if (vel != Vector2.zero)
            {
                _transform.position += (Vector3)vel * deltaTime;
            }
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// RectTransform への Movement 出力アダプタ。
    /// Output.Value * deltaTime を RectTransform.anchoredPosition に加算。
    /// </summary>
    public sealed class RectTransformMovementAdapter : IMovementAdapter
    {
        readonly RectTransform _rectTransform;
        readonly IMovementOutput _output;
        bool _disposed;

        public RectTransformMovementAdapter(RectTransform rectTransform, IMovementOutput output)
        {
            _rectTransform = rectTransform ?? throw new ArgumentNullException(nameof(rectTransform));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void Tick(float deltaTime)
        {
            if (_disposed || _rectTransform == null) return;

            var vel = _output.Value;
            if (vel != Vector2.zero)
            {
                _rectTransform.anchoredPosition += vel * deltaTime;
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// CharacterController への Movement 出力アダプタ。
    /// Output.Value * deltaTime を CharacterController.Move に渡す。
    /// </summary>
    public sealed class CharacterControllerMovementAdapter : IMovementAdapter
    {
        readonly CharacterController _controller;
        readonly IMovementOutput _output;
        uint _lastVersion;
        bool _disposed;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public CharacterControllerMovementAdapter(CharacterController controller, IMovementOutput output)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// 更新（変更があった場合のみ CharacterController に反映）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _controller == null) return;
            // Apply movement per-frame so continuous velocity has visible effect
            Vector3 move = new Vector3(_output.Value.x, _output.Value.y, 0f) * deltaTime;
            if (move != Vector3.zero)
            {
                _controller.Move(move);
            }
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
