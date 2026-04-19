using System;
using Game.Common;
using UnityEngine;

namespace Game.Scalar
{
    /// <summary>
    /// 評価結果を指定桁で四捨五入する Mod。
    /// </summary>
    public sealed class RoundScalarModifier : IScalarGetModifier
    {
        const int MinDigits = 0;
        const int MaxDigits = 6;

        int _digits;
        readonly System.Action _invalidate;

        public RoundScalarModifier(int digits, System.Action invalidate)
        {
            _digits = NormalizeDigits(digits);
            _invalidate = invalidate ?? (() => { });
        }

        public int Digits
        {
            get => _digits;
            set
            {
                var normalized = NormalizeDigits(value);
                if (_digits == normalized)
                    return;

                _digits = normalized;
                _invalidate();
            }
        }

        public void OnAfterEvaluate(ref ScalarGetContext ctx)
        {
            if (_digits <= 0)
            {
                ctx.Value = DynamicVariant.NormalizeFloatValue(Mathf.Round(ctx.Value));
                return;
            }

            var factor = Mathf.Pow(10f, _digits);
            ctx.Value = DynamicVariant.NormalizeFloatValue(Mathf.Round(ctx.Value * factor) / factor);
        }

        static int NormalizeDigits(int digits)
            => Mathf.Clamp(digits, MinDigits, MaxDigits);
    }
}