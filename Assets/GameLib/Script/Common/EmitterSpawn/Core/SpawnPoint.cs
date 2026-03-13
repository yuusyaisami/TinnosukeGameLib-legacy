#nullable enable
using System;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// 個々のスポーン位置とその付随データ。
    /// Layer 1 Data（一層データ）の一部。
    /// </summary>
    [Serializable]
    public readonly struct SpawnPoint
    {
        public readonly int Index;
        public readonly Vector3 Position;
        public readonly Vector3 DirectionFromOrigin;
        public readonly float DistanceFromOrigin;
        public readonly Vector3 TangentDirection;
        public readonly float NormalizedPosition;
        public readonly int SpawnCount; // この点のスポーン数
        public readonly int EmitCount; // Emitter repeat 対応用

        public SpawnPoint(
            int index,
            Vector3 position,
            Vector3 directionFromOrigin,
            float distanceFromOrigin,
            Vector3 tangentDirection,
            float normalizedPosition,
            int spawnCount,
            int emitCount) : this()
        {
            Index = index;
            Position = position;
            DirectionFromOrigin = directionFromOrigin;
            DistanceFromOrigin = distanceFromOrigin;
            TangentDirection = tangentDirection;
            NormalizedPosition = normalizedPosition;
            SpawnCount = spawnCount;
            EmitCount = emitCount;
        }
    }
}
