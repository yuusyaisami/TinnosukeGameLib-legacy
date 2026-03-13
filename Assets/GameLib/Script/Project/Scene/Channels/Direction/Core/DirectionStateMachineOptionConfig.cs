#nullable enable
using System;

namespace Game.Direction
{
    public enum ZeroSpeedOptionPolicy
    {
        KeepLast = 0,
        Clear = 1,
    }

    public enum DiagonalOptionPolicy
    {
        /// <summary>斜め入力でも 1 つだけ立てる。前回の方向（hold閾値を満たす限り）を優先。</summary>
        SinglePreferPrevious = 0,

        /// <summary>斜め入力でも 1 つだけ立てる。左右（水平）を優先。</summary>
        SinglePreferHorizontal = 1,

        /// <summary>斜め入力時に水平・垂直の 2 つの Option を同時に立てる（OptionKey が別である必要がある）。</summary>
        DualAxis = 2,
    }

    [Serializable]
    public readonly struct DirectionCardinalAngleConfig
    {
        public readonly bool Enabled;
        public readonly float UpCenterDeg;
        public readonly float UpHalfRangeDeg;
        public readonly float LeftCenterDeg;
        public readonly float LeftHalfRangeDeg;
        public readonly float RightCenterDeg;
        public readonly float RightHalfRangeDeg;
        public readonly float DownCenterDeg;
        public readonly float DownHalfRangeDeg;

        public DirectionCardinalAngleConfig(
            bool enabled,
            float upCenterDeg,
            float upHalfRangeDeg,
            float leftCenterDeg,
            float leftHalfRangeDeg,
            float rightCenterDeg,
            float rightHalfRangeDeg,
            float downCenterDeg,
            float downHalfRangeDeg)
        {
            Enabled = enabled;
            UpCenterDeg = upCenterDeg;
            UpHalfRangeDeg = upHalfRangeDeg;
            LeftCenterDeg = leftCenterDeg;
            LeftHalfRangeDeg = leftHalfRangeDeg;
            RightCenterDeg = rightCenterDeg;
            RightHalfRangeDeg = rightHalfRangeDeg;
            DownCenterDeg = downCenterDeg;
            DownHalfRangeDeg = downHalfRangeDeg;
        }
    }

    [Serializable]
    public readonly struct DirectionStateMachineOptionConfig
    {
        public readonly float ActivationThreshold;
        public readonly float HoldThreshold;
        public readonly float ZeroHoldThreshold;
        public readonly ZeroSpeedOptionPolicy ZeroSpeedPolicy;
        public readonly DiagonalOptionPolicy DiagonalPolicy;
        public readonly bool OutputToGlobal;
        public readonly DirectionCardinalAngleConfig CardinalAngleConfig;

        public DirectionStateMachineOptionConfig(
            float activationThreshold,
            float holdThreshold,
            float zeroHoldThreshold,
            ZeroSpeedOptionPolicy zeroSpeedPolicy,
            DiagonalOptionPolicy diagonalPolicy,
            bool outputToGlobal = false,
            DirectionCardinalAngleConfig cardinalAngleConfig = default)
        {
            ActivationThreshold = activationThreshold;
            HoldThreshold = holdThreshold;
            ZeroHoldThreshold = zeroHoldThreshold;
            ZeroSpeedPolicy = zeroSpeedPolicy;
            DiagonalPolicy = diagonalPolicy;
            OutputToGlobal = outputToGlobal;
            CardinalAngleConfig = cardinalAngleConfig;
        }
    }
}
