using System;
using Game.Common;

namespace Game.Scalar
{
    /// <summary>
    /// 評価結果を min/max でクランプする Mod。
    /// </summary>
    public sealed class ClampScalarModifier : IScalarGetModifier
    {
        ScalarClamp _clamp;
        readonly System.Action _invalidate;

        public ClampScalarModifier(ScalarClamp clamp, System.Action invalidate)
        {
            _clamp = clamp;
            _invalidate = invalidate ?? (() => { });
        }

        public ScalarClamp Clamp
        {
            get => _clamp;
            set
            {
                _clamp = value;
                _invalidate();
            }
        }

        public bool UsesDynamicBounds => _clamp.UsesDynamicBounds;

        public float Min
        {
            get => TryGetLiteralValue(_clamp.Min, out var value) ? value : 0f;
            set
            {
                _clamp.Min = DynamicValueExtensions.FromLiteral(value);
                _clamp.UseMin = true;
                _invalidate();
            }
        }

        public float Max
        {
            get => TryGetLiteralValue(_clamp.Max, out var value) ? value : 0f;
            set
            {
                _clamp.Max = DynamicValueExtensions.FromLiteral(value);
                _clamp.UseMax = true;
                _invalidate();
            }
        }

        public void DisableMin()
        {
            _clamp.UseMin = false;
            _invalidate();
        }

        public void DisableMax()
        {
            _clamp.UseMax = false;
            _invalidate();
        }

        public void OnAfterEvaluate(ref ScalarGetContext ctx)
        {
            ctx.Value = _clamp.Apply(ctx.Value, null);
        }

        static bool TryGetLiteralValue(DynamicValue<float> value, out float result)
        {
            if (value.TryGetSource<LiteralFloatSource>(out var literalFloat))
            {
                result = literalFloat.Evaluate(null).AsFloat;
                return true;
            }

            if (value.TryGetSource<LiteralSource>(out var literal))
            {
                result = literal.Evaluate(null).AsFloat;
                return true;
            }

            result = default;
            return false;
        }
    }
}
