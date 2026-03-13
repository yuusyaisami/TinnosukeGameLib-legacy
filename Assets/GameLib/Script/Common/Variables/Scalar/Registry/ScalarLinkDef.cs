// Assets/Game/Script/Core/Scalar/ScalarLinkDef.cs
using System;
using UnityEngine;

namespace Game.Scalar
{
    /// <summary>
    /// ソース値の取り方 ＆ ターゲットへの適用方法。
    /// </summary>
    public enum ScalarLinkMode
    {
        // effective = clamp( current - base )
        // target.Add = factor * effective
        DeltaToAdd,

        // effective = clamp( current - base )
        // target.Mul = 1 + factor * effective
        DeltaToMul,

        // effective = clamp( current )
        // target.Add = factor * effective
        ValueToAdd,

        // effective = clamp( current )
        // target.Mul = 1 + factor * effective
        ValueToMul,
    }

    /// <summary>
    /// ソース側の「使う値」に対するクランプ（振れ幅制限）。
    /// </summary>
    [Serializable]
    public struct ScalarLinkClamp
    {
        public bool Enabled;
        public float Min;
        public float Max;

        public float Apply(float x)
        {
            if (!Enabled) return x;
            return Mathf.Clamp(x, Min, Max);
        }

        public static ScalarLinkClamp Disabled => new ScalarLinkClamp
        {
            Enabled = false,
            Min = 0f,
            Max = 0f
        };

        public static ScalarLinkClamp Range(float min, float max)
        {
            return new ScalarLinkClamp
            {
                Enabled = true,
                Min = min,
                Max = max
            };
        }
    }
}
