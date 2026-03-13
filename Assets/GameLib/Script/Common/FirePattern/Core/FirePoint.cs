#nullable enable
using System;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public readonly struct FirePoint
    {
        public readonly int Index;
        public readonly Vector3 Position;
        public readonly Vector3 BaseDirection;
        public readonly Vector3 TargetDirection;
        public readonly float TargetDistance;
        public readonly bool HasTarget;
        public readonly int TargetHitCount;
        public readonly float NormalizedPosition;
        public readonly int TotalCount;
        public readonly int FireRepeatIndex;

        public FirePoint(
            int index,
            Vector3 position,
            Vector3 baseDirection,
            Vector3 targetDirection,
            float targetDistance,
            bool hasTarget,
            int targetHitCount,
            float normalizedPosition,
            int totalCount,
            int fireRepeatIndex)
        {
            Index = index;
            Position = position;
            BaseDirection = baseDirection;
            TargetDirection = targetDirection;
            TargetDistance = targetDistance;
            HasTarget = hasTarget;
            TargetHitCount = targetHitCount;
            NormalizedPosition = normalizedPosition;
            TotalCount = totalCount;
            FireRepeatIndex = fireRepeatIndex;
        }
    }
}
