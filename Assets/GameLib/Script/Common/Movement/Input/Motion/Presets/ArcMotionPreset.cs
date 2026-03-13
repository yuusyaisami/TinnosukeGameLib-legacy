#nullable enable
// Game.Movement
// ================================================================================
// ArcMotionPreset - 弧状移動モーション
// ================================================================================
//
// 【概要】
// 進行方向に対して弧を描くような横方向成分を加える。
// 一定方向へ曲がりながら進む移動パターンを表現。
// 波と違い、一定の横方向バイアスを持つ。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 弧状移動モーション。
    /// GuidanceDirection に対して横方向のバイアス速度を加算する。
    /// </summary>
    [Serializable]
    public sealed class ArcMotionPreset : MotionPreset
    {
        [Header("Arc Parameters")]
        [LabelText("Lateral Bias")]
        [Tooltip("横方向のバイアス速度（正=左、負=右）")]
        public float LateralBias = 1f;

        [LabelText("Use Curve")]
        [Tooltip("時間経過でバイアスを変化させるか")]
        public bool UseCurve = false;

        [LabelText("Bias Curve")]
        [Tooltip("時間→バイアス倍率のカーブ（0..1）")]
        [ShowIf(nameof(UseCurve))]
        public AnimationCurve BiasCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [LabelText("Curve Duration")]
        [Tooltip("カーブの再生時間（秒）")]
        [ShowIf(nameof(UseCurve))]
        [Min(0.01f)]
        public float CurveDuration = 1f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("基本速度への倍率")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new ArcMotionRuntime(this);
    }

    /// <summary>
    /// ArcMotion のランタイム。
    /// </summary>
    public sealed class ArcMotionRuntime : MotionRuntime
    {
        readonly ArcMotionPreset _source;

        public ArcMotionRuntime(ArcMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            // バイアス値を計算
            float bias = _source.LateralBias;

            if (_source.UseCurve && _source.BiasCurve != null)
            {
                // カーブから倍率を取得（ループせず clamp）
                float normalizedTime = Mathf.Clamp01(ElapsedTime / _source.CurveDuration);
                float curveValue = _source.BiasCurve.Evaluate(normalizedTime);
                bias *= curveValue;
            }

            // GuidanceDirection に対して垂直な方向を取得
            var guide = frame.GuidanceDirection;
            if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                guide = Vector2.up;

            var perpendicular = MovementMath.GetPerpendicular(guide);

            return new MotionOutput(
                direction: guide,
                speedMul: _source.SpeedMultiplier,
                additiveVelocity: perpendicular * bias
            );
        }
    }
}
