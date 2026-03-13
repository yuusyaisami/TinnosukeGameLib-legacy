using System;

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

        public float Min
        {
            get => _clamp.Min;
            set
            {
                _clamp.Min = value;
                _clamp.UseMin = true;
                _invalidate();
            }
        }

        public float Max
        {
            get => _clamp.Max;
            set
            {
                _clamp.Max = value;
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
            ctx.Value = _clamp.Apply(ctx.Value);
        }
    }
}
