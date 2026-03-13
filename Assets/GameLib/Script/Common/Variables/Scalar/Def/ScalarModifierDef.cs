using System;

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
    }

    [Serializable]
    public struct ScalarClamp
    {
        public bool UseMin;
        public float Min;
        public bool UseMax;
        public float Max;

        public float Apply(float v)
        {
            if (UseMin && v < Min) v = Min;
            if (UseMax && v > Max) v = Max;
            return v;
        }
    }

    public sealed class ScalarRuntimeConfig
    {
        public float BaseValue;
        public bool UseEffectMod;
        public bool UseClampMod;
        public ScalarClamp Clamp;
    }

    public interface IScalarRuntimeConfigProvider : IScalarBaseline
    {
        bool TryGetConfig(ScalarKey key, out ScalarRuntimeConfig config);
    }
}
