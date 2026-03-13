#nullable enable
using System;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// スポーンポイント固有の動的データ。
    /// Layer 2 Data（二層データ）。DynamicValue から計算される。
    /// </summary>
    [Serializable]
    public struct SpawnData
    {
        public Vector3 Direction;
        public float Speed;
        public float DelayTime;
        public float RandomVerticalOffset;
        public float RandomHorizontalOffset;
        public float CustomFloat0;
        public float CustomFloat1;

        public Vector3 ApplyRandomOffset(Vector3 basePosition, Vector3 tangentDirection)
        {
            // TangentDirection の法線（2D の場合は 90度回転）
            var normal = new Vector3(-tangentDirection.y, tangentDirection.x, tangentDirection.z);

            float vertOffset = (UnityEngine.Random.value - 0.5f) * RandomVerticalOffset;
            float horizOffset = (UnityEngine.Random.value - 0.5f) * RandomHorizontalOffset;

            return basePosition + normal * vertOffset + tangentDirection * horizOffset;
        }
    }
}
