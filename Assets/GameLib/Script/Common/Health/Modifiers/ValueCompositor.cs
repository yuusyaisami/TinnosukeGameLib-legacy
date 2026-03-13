// Game.Health.ValueCompositor.cs
//
// 数値の合成方法を指定して計算するヘルパー
// v0.2: 複数入力の重み付きブレンド機能を追加

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Health
{
    /// <summary>数値の合成モード。</summary>
    public enum ValueCompositorMode
    {
        Avg,
        Max,
        Min,
        Add,
        Mul,
        /// <summary>重み付き分布ブレンド。Weight で左から右への分布を制御。</summary>
        WeightedDistribution,
    }

    /// <summary>
    /// 複数の数値を指定したモードで合成する小さなユーティリティ。
    /// CriticalHitModifier などでクリティカル率や倍率を組み合わせる際に使用する。
    /// 
    /// v0.2: WeightedDistribution モードを追加。
    /// - Weight=0: 左端の値のみ
    /// - Weight=0.5: 全入力を均等にブレンド
    /// - Weight=1: 右端の値のみ
    /// </summary>
    public sealed class ValueCompositor
    {
        public ValueCompositorMode Mode { get; set; }

        /// <summary>
        /// 重み付き分布の分布位置 (0=左端, 0.5=均等, 1=右端)。
        /// WeightedDistribution モード時に使用。
        /// </summary>
        public float Weight { get; set; } = 0.5f;

        /// <summary>
        /// ブレンド幅の倍率。1.0 = デフォルト（隣接入力間でスムーズブレンド）。
        /// 大きくすると幅が広がり、複数入力が同時に影響する。
        /// </summary>
        public float BlendWidth { get; set; } = 1f;

        public ValueCompositor(ValueCompositorMode mode = ValueCompositorMode.Avg)
        {
            Mode = mode;
        }

        public ValueCompositor(ValueCompositorMode mode, float weight) : this(mode)
        {
            Weight = weight;
        }

        public float Combine(params float[] values)
        {
            if (values == null || values.Length == 0)
                return 0f;

            switch (Mode)
            {
                case ValueCompositorMode.Max:
                    return Max(values);
                case ValueCompositorMode.Min:
                    return Min(values);
                case ValueCompositorMode.Add:
                    return Add(values);
                case ValueCompositorMode.Mul:
                    return Mul(values);
                case ValueCompositorMode.WeightedDistribution:
                    return WeightedDistribution(values, Weight, BlendWidth);
                case ValueCompositorMode.Avg:
                default:
                    return Avg(values);
            }
        }

        /// <summary>
        /// 重み付き分布ブレンド（入力配列 + Weight）。
        /// </summary>
        public float CombineWeighted(float[] values, float weight)
        {
            if (values == null || values.Length == 0)
                return 0f;
            return WeightedDistribution(values, weight, BlendWidth);
        }

        /// <summary>
        /// 重み付き分布ブレンド（入力配列 + Weight + BlendWidth）。
        /// </summary>
        public float CombineWeighted(float[] values, float weight, float blendWidth)
        {
            if (values == null || values.Length == 0)
                return 0f;
            return WeightedDistribution(values, weight, blendWidth);
        }

        static float Avg(ReadOnlySpan<float> values)
        {
            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        static float Max(ReadOnlySpan<float> values)
        {
            float max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max) max = values[i];
            }
            return max;
        }

        static float Min(ReadOnlySpan<float> values)
        {
            float min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
            }
            return min;
        }

        static float Add(ReadOnlySpan<float> values)
        {
            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }

        static float Mul(ReadOnlySpan<float> values)
        {
            float product = 1f;
            for (int i = 0; i < values.Length; i++)
            {
                product *= values[i];
            }
            return product;
        }

        /// <summary>
        /// 重み付き分布ブレンド。
        /// Weight (0-1) で分布の中心位置を制御。
        /// BlendWidth で影響範囲を制御。
        /// 
        /// 動作:
        /// - 各入力に「位置」(0, 1/n, 2/n, ..., 1) を割り当て
        /// - Weight から各入力への距離で重みを計算
        /// - BlendWidth を考慮して滑らかにブレンド
        /// </summary>
        static float WeightedDistribution(ReadOnlySpan<float> values, float weight, float blendWidth)
        {
            int count = values.Length;

            // 1入力の場合はそのまま返す
            if (count == 1)
                return values[0];

            // Weight を 0-1 にクランプ
            weight = Mathf.Clamp01(weight);

            // 各入力の位置を計算 (0, 1/(n-1), 2/(n-1), ..., 1)
            // BlendWidth=1 の場合、隣接入力間の距離 = 1/(n-1)
            float spacing = 1f / (count - 1);
            float effectiveWidth = spacing * Mathf.Max(0.01f, blendWidth);

            float totalWeight = 0f;
            float result = 0f;

            for (int i = 0; i < count; i++)
            {
                // この入力の位置
                float position = i * spacing;

                // Weight との距離
                float distance = Mathf.Abs(position - weight);

                // 重みを計算（ガウシアン風の減衰）
                // effectiveWidth 以内なら影響あり
                float influence;
                if (distance >= effectiveWidth)
                {
                    influence = 0f;
                }
                else
                {
                    // コサイン補間で滑らかに減衰
                    float t = distance / effectiveWidth;
                    influence = (1f + Mathf.Cos(t * Mathf.PI)) * 0.5f;
                }

                totalWeight += influence;
                result += values[i] * influence;
            }

            // 重みの合計が0に近い場合（極端な BlendWidth）は均等ブレンド
            if (totalWeight < 0.0001f)
            {
                return Avg(values);
            }

            return result / totalWeight;
        }
    }
}
