#nullable enable
// Game.Movement
// ================================================================================
// BounceToTargetMotionPreset - バウンドしてターゲット位置で停止するモーション
// ================================================================================
//
// 【概要】
// - ターゲット座標へ向かって擬似2Dバウンドし、最終的に制止する。
// - 重力は下方向。
// - InitialAngle は GuidanceDirection に対する初期発射角のオフセット（度）。
//
// 【実装方針】
// - 速度を内部状態として積分し、出力は AdditiveVelocity で“最終速度そのもの”を返す。
// - ヒット床は TargetPosition.y を基準にした水平面として扱い、反発係数で跳ね返る。
// - X 方向はターゲットに吸い寄せる（バウンド中に自然に寄る）
// - 近傍 + 低速で停止。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class BounceToTargetMotionPreset : MotionPreset
    {
        [Header("Launch")]
        [LabelText("Initial Angle")]
        [Tooltip("初期角度オフセット（度）。GuidanceDirection をこの角度だけ回転して初速方向にする。")]
        public float InitialAngle = 0f;

        [LabelText("Initial Speed Multiplier")]
        [Tooltip("初速 = SpeedBase * この倍率")]
        [Min(0f)]
        public float InitialSpeedMultiplier = 1f;

        [Header("Gravity")]
        [LabelText("Gravity")]
        [Tooltip("重力加速度（下方向）。")]
        [Min(0f)]
        public float Gravity = 30f;

        [LabelText("Air Drag")]
        [Tooltip("空気抵抗（1/秒）。大きいほど早く減速する。")]
        [Min(0f)]
        public float AirDrag = 0.5f;

        [Header("Bounce")]
        [LabelText("Restitution")]
        [Tooltip("反発係数（0..1）。大きいほどよく跳ねる。")]
        [Range(0f, 1f)]
        public float Restitution = 0.6f;

        [LabelText("Restitution Decay")]
        [Tooltip("バウンドのたびに Restitution に掛ける減衰（0..1）。")]
        [Range(0f, 1f)]
        public float RestitutionDecay = 0.85f;

        [LabelText("Bounce Friction")]
        [Tooltip("接地バウンド時の水平摩擦（0..1）。X 速度に (1-friction) を掛ける。")]
        [Range(0f, 1f)]
        public float BounceFriction = 0.15f;

        [Header("Attraction (Horizontal)")]
        [LabelText("Attraction")]
        [Tooltip("ターゲットXへの吸引（加速度係数）。")]
        [Min(0f)]
        public float HorizontalAttraction = 20f;

        [LabelText("Damping")]
        [Tooltip("ターゲットXへの吸引の減衰（速度係数）。")]
        [Min(0f)]
        public float HorizontalDamping = 6f;

        [Header("Stop")]
        [LabelText("Stop Distance")]
        [Tooltip("ターゲットに十分近いとみなす距離。")]
        [Min(0f)]
        public float StopDistance = 0.05f;

        [LabelText("Stop Speed")]
        [Tooltip("十分に遅いとみなす速度。")]
        [Min(0f)]
        public float StopSpeed = 0.05f;

        public override MotionRuntime CreateRuntime() => new BounceToTargetMotionRuntime(this);
    }

    public sealed class BounceToTargetMotionRuntime : MotionRuntime
    {
        readonly BounceToTargetMotionPreset _source;

        Vector2 _velocity;
        bool _hasInitialVelocity;
        float _restitution;

        public BounceToTargetMotionRuntime(BounceToTargetMotionPreset source)
        {
            _source = source;
        }

        protected override void OnInitialize()
        {
            _velocity = Vector2.zero;
            _hasInitialVelocity = false;
            _restitution = Mathf.Clamp01(_source.Restitution);
        }

        protected override void OnReset()
        {
            _velocity = Vector2.zero;
            _hasInitialVelocity = false;
            _restitution = Mathf.Clamp01(_source.Restitution);
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            if (!frame.Target.HasTarget)
                return MotionOutput.Default(frame.GuidanceDirection);

            float dt = Mathf.Max(0f, frame.DeltaTime);
            if (dt <= 0f)
                return MotionOutput.Default(frame.GuidanceDirection);

            Vector2 ownerPos = frame.Target.OwnerPosition;
            Vector2 targetPos = frame.Target.TargetPosition;

            // 初速
            if (!_hasInitialVelocity)
            {
                var guide = frame.GuidanceDirection;
                if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                    guide = Vector2.up;

                var dir = MovementMath.RotateDirection(guide, _source.InitialAngle);
                dir = MovementMath.NormalizeDirection(dir);

                float initialSpeed = Mathf.Max(0f, frame.SpeedBase) * Mathf.Max(0f, _source.InitialSpeedMultiplier);
                _velocity = dir * initialSpeed;
                _hasInitialVelocity = true;
            }

            // X 方向はターゲットへ吸い寄せる（自然に“寄っていく”ため）
            float dx = targetPos.x - ownerPos.x;
            float ax = _source.HorizontalAttraction * dx - _source.HorizontalDamping * _velocity.x;

            // 重力
            Vector2 accel = new(ax, -_source.Gravity);
            _velocity += accel * dt;

            // 空気抵抗（指数減衰で dt に安定）
            float drag = Mathf.Max(0f, _source.AirDrag);
            if (drag > 0f)
            {
                float damp = Mathf.Exp(-drag * dt);
                _velocity *= damp;
            }

            // 擬似床（targetY）でバウンド
            float groundY = targetPos.y;
            float nextY = ownerPos.y + _velocity.y * dt;
            bool willCrossGround = ownerPos.y > groundY && nextY <= groundY;
            bool alreadyBelow = ownerPos.y <= groundY;

            if ((_velocity.y < 0f) && (willCrossGround || alreadyBelow))
            {
                // 反射
                _velocity.y = -_velocity.y * _restitution;

                // 摩擦
                float friction = Mathf.Clamp01(_source.BounceFriction);
                _velocity.x *= (1f - friction);

                // バウンドごとに弱く
                _restitution *= Mathf.Clamp01(_source.RestitutionDecay);
            }

            // 停止判定（近傍 + 低速）
            float stopDist = Mathf.Max(0f, _source.StopDistance);
            float stopSpeed = Mathf.Max(0f, _source.StopSpeed);

            if ((ownerPos - targetPos).sqrMagnitude <= stopDist * stopDist &&
                _velocity.sqrMagnitude <= stopSpeed * stopSpeed)
            {
                _velocity = Vector2.zero;
                return MotionOutput.Zero;
            }

            // AdditiveVelocity として“最終速度そのもの”を出す
            return new MotionOutput(
                direction: Vector2.zero,
                speedMul: 0f,
                additiveVelocity: _velocity
            );
        }
    }
}
