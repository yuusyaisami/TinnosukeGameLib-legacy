using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Scalar
{
    public struct ScalarAddContext
    {
        public ScalarKey Key;
        public string Layer;
        public float Value;
        public object Source;
        public string Tag;

        internal ScalarKeyRuntime Runtime;
        internal IBaseScalarService Service;
    }

    public struct ScalarMulContext
    {
        public ScalarKey Key;
        public string Layer;
        public float Factor;
        public ScalarMulPhase Phase;
        public object Source;
        public string Tag;

        internal ScalarKeyRuntime Runtime;
        internal IBaseScalarService Service;
    }


    public struct ScalarGetContext
    {
        public ScalarKey Key;
        public bool IncludeAllLayers;
        public string Layer;
        public float Value;

        internal ScalarKeyRuntime Runtime;
        internal IBaseScalarService Service;
        internal IDynamicContext DynamicContext;
    }

    [Serializable]
    public struct ScalarClamp
    {
        [LabelText("Use Min")]
        [Tooltip("Inspector setting.")]
        public bool UseMin;

        [ShowIf(nameof(UseMin))]
        [LabelText("Min")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> Min;

        [LabelText("Use Max")]
        [Tooltip("Inspector setting.")]
        public bool UseMax;

        [ShowIf(nameof(UseMax))]
        [LabelText("Max")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> Max;

        public bool UsesDynamicBounds
            => UsesDynamicValue(Min) || UsesDynamicValue(Max);

        public bool TryCreateLiteralClamp(out ScalarClamp literalClamp)
        {
            literalClamp = default;

            if (UseMin)
            {
                if (!TryGetLiteralValue(Min, out var min))
                    return false;

                literalClamp.UseMin = true;
                literalClamp.Min = DynamicValueExtensions.FromLiteral(min);
            }

            if (UseMax)
            {
                if (!TryGetLiteralValue(Max, out var max))
                    return false;

                literalClamp.UseMax = true;
                literalClamp.Max = DynamicValueExtensions.FromLiteral(max);
            }

            return true;
        }

        public float Apply(float v, IDynamicContext context)
        {
            if (UseMin)
            {
                var min = Min.GetOrDefault(context, float.MinValue);
                if (v < min)
                    v = min;
            }

            if (UseMax)
            {
                var max = Max.GetOrDefault(context, float.MaxValue);
                if (v > max)
                    v = max;
            }

            return v;
        }

        static bool UsesDynamicValue(DynamicValue<float> value)
        {
            if (!value.HasSource)
                return false;

            if (value.TryGetSource<LiteralSource>(out _))
                return false;

            if (value.TryGetSource<LiteralFloatSource>(out _))
                return false;

            return true;
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

    public sealed class ScalarRuntimeConfig
    {
        public float BaseValue;
        public bool UseEffectMod;
        public bool UseRoundMod;
        public int RoundDigits;
        public bool UseClampMod;
        public ScalarClamp Clamp;
    }

    public interface IScalarRuntimeConfigProvider : IScalarBaseline
    {
        bool TryGetConfig(ScalarKey key, out ScalarRuntimeConfig config);
    }
}
