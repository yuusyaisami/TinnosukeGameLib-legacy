#nullable enable
// Game.Movement
// ================================================================================
// GravityPullMotionPreset - 重力で落ちていくモーション
// ================================================================================
//
// 【概要】
// - 初期角度（InitialAngle）で進み始め、時間とともに重力で下へ引っ張られる。
// - GuidanceDirection は「初期の前方向」の基準として使い、そこから角度オフセットを与える。
// - 出力は BaseVelocity（direction*speedBase*speedMul）+ 重力による AdditiveVelocity。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class GravityPullMotionPreset : MotionPreset
    {
        [Header("Direction")]
        [LabelText("Initial Angle")]
        [Tooltip("初期角度オフセット（度）。GuidanceDirection をこの角度だけ回転して初期の進行方向にする。")]
        public float InitialAngle = 0f;

        [Header("Gravity")]
        [LabelText("Gravity")]
        [Tooltip("重力加速度（下方向）。")]
        [Min(0f)]
        public float Gravity = 25f;

        [LabelText("Air Drag")]
        [Tooltip("重力成分に対する空気抵抗（1/秒）。")]
        [Min(0f)]
        public float AirDrag = 0.2f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("基本速度への倍率")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        [Header("Debug")]
        [LabelText("Enable Debug Log")]
        public bool EnableDebugLog = false;

        [LabelText("Debug Log Interval Frames")]
        [Min(1)]
        [ShowIf(nameof(EnableDebugLog))]
        public int DebugLogIntervalFrames = 20;

        public override MotionRuntime CreateRuntime() => new GravityPullMotionRuntime(this);
    }

    public sealed class GravityPullMotionRuntime : MotionRuntime
    {
        readonly GravityPullMotionPreset _source;
        Vector2 _gravityVelocity;
        Vector2 _initialDirection;
        bool _hasInitialDirection;
        int _lastDebugLogFrame;

        public GravityPullMotionRuntime(GravityPullMotionPreset source)
        {
            _source = source;
        }

        protected override void OnInitialize()
        {
            _gravityVelocity = Vector2.zero;
            _initialDirection = Vector2.zero;
            _hasInitialDirection = false;
            _lastDebugLogFrame = -99999;
        }

        protected override void OnReset()
        {
            _gravityVelocity = Vector2.zero;
            _initialDirection = Vector2.zero;
            _hasInitialDirection = false;
            _lastDebugLogFrame = -99999;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            float dt = Mathf.Max(0f, frame.DeltaTime);
            if (dt <= 0f)
                return MotionOutput.Default(frame.GuidanceDirection);

            // 初期方向は最初に有効だった GuidanceDirection を固定値として使う。
            // Guidance が未設定の場合は BaseDirection を使い、それも未設定なら
            // 「推進なし（重力のみ）」にする。
            if (!_hasInitialDirection && frame.GuidanceDirection.sqrMagnitude >= MovementMath.NormalizeEpsilon)
            {
                _initialDirection = MovementMath.NormalizeDirection(frame.GuidanceDirection);
                _hasInitialDirection = true;
            }

            if (!_hasInitialDirection && frame.BaseDirection.sqrMagnitude >= MovementMath.NormalizeEpsilon)
            {
                _initialDirection = MovementMath.NormalizeDirection(frame.BaseDirection);
                _hasInitialDirection = true;
            }

            var guide = _hasInitialDirection ? _initialDirection : Vector2.zero;

            var dir = MovementMath.RotateDirection(guide, _source.InitialAngle);
            if (dir.sqrMagnitude >= MovementMath.NormalizeEpsilon)
                dir = MovementMath.NormalizeDirection(dir);
            else
                dir = Vector2.zero;

            // 重力速度を積分
            _gravityVelocity += Vector2.down * Mathf.Max(0f, _source.Gravity) * dt;

            // 空気抵抗（指数減衰）
            float drag = Mathf.Max(0f, _source.AirDrag);
            if (drag > 0f)
            {
                float damp = Mathf.Exp(-drag * dt);
                _gravityVelocity *= damp;
            }

            var output = new MotionOutput(
                direction: dir,
                speedMul: Mathf.Max(0f, _source.SpeedMultiplier),
                additiveVelocity: _gravityVelocity
            );

            if (_source.EnableDebugLog)
            {
                var interval = Mathf.Max(1, _source.DebugLogIntervalFrames);
                if (Time.frameCount - _lastDebugLogFrame >= interval)
                {
                    _lastDebugLogFrame = Time.frameCount;
                    Debug.Log(
                        $"[GravityPullMotion] frame={Time.frameCount} dt={dt:F4} " +
                        $"base={frame.BaseDirection} guidance={frame.GuidanceDirection} hasInit={_hasInitialDirection} init={_initialDirection} " +
                        $"dir={output.Direction} speedMul={output.SpeedMul:F3} addVel={output.AdditiveVelocity}");
                }
            }

            return output;
        }
    }
}
