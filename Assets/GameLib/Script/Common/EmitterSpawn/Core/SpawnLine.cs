#nullable enable
using System;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// スポーンラインの定義。
    /// 点の配列を保持し、SpawnPoint を生成する基盤。
    /// </summary>
    [Serializable]
    public struct SpawnLine
    {
        /// <summary>ライン上の点（ローカル座標）</summary>
        public Vector3[] Points;

        /// <summary>各点での正規化された位置 (0.0 ~ 1.0)</summary>
        public float[] NormalizedPositions;

        /// <summary>ラインの総長</summary>
        public float TotalLength;

        public static SpawnLine Empty => new()
        {
            Points = Array.Empty<Vector3>(),
            NormalizedPositions = Array.Empty<float>(),
            TotalLength = 0f
        };
    }
}
