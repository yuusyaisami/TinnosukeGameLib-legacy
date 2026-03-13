#nullable enable
// Game.Movement
// ================================================================================
// WaveMotionPreset - 波状移動モーション
// ================================================================================
//
// 【概要】
// 進行方向に対して垂直に正弦波の揺れを加える。
// 蛇行移動や波打ち移動を表現。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 波状移動モーション。
    /// GuidanceDirection に対して垂直に正弦波の速度を加算する。
    /// </summary>
    [Serializable]
    public sealed class WaveMotionPreset : MotionPreset
    {
        [Header("Wave Parameters")]
        [LabelText("Amplitude")]
        [Tooltip("横方向の振幅（速度）")]
        [Min(0f)]
        public float Amplitude = 2f;

        [LabelText("Frequency")]
        [Tooltip("振動周波数（Hz）")]
        [Min(0.01f)]
        public float Frequency = 1f;

        [LabelText("Phase Offset")]
        [Tooltip("初期位相（ラジアン）")]
        [Range(-Mathf.PI, Mathf.PI)]
        public float PhaseOffset = 0f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("基本速度への倍率")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new WaveMotionRuntime(this);
    }

    /// <summary>
    /// WaveMotion のランタイム。
    /// </summary>
    public sealed class WaveMotionRuntime : MotionRuntime
    {
        readonly WaveMotionPreset _source;

        public WaveMotionRuntime(WaveMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            // 位相を計算
            float phase = ElapsedTime * _source.Frequency * Mathf.PI * 2f + _source.PhaseOffset;

            // 横方向の速度を計算
            float lateral = Mathf.Sin(phase) * _source.Amplitude;

            // GuidanceDirection に対して垂直な方向を取得
            var guide = frame.GuidanceDirection;
            if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                guide = Vector2.up;

            var perpendicular = MovementMath.GetPerpendicular(guide);

            return new MotionOutput(
                direction: guide,
                speedMul: _source.SpeedMultiplier,
                additiveVelocity: perpendicular * lateral
            );
        }
    }
}
