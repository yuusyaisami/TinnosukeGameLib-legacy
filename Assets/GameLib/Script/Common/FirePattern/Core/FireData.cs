#nullable enable
using System;

namespace Game.Fire
{
    [Serializable]
    public struct FireData
    {
        public float SpeedMultiplier;
        /// <summary>角速度（degrees/sec）。0 のときは回転出力しない。</summary>
        public float RotationSpeed;
        public float DelayTime;
        public float AngleOffset;
        public float DistanceOffset;
        public float CustomFloat0;
        public float CustomFloat1;
    }
}
