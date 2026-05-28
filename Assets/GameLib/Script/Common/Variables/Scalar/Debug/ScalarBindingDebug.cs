// Assets/Game/Script/Core/Scalar/ScalarBindingTelemetry.cs
using System;
using System.Collections.Generic;

namespace Game.Scalar
{
    public readonly struct ScalarBindingDebugInfo
    {
        public readonly ScalarBindingEndpoint Source;
        public readonly ScalarBindingEndpoint Target;
        public readonly ScalarLinkMode Mode;
        public readonly float Factor;
        public readonly ScalarLinkClamp Clamp;
        public readonly string Tag;
        public readonly float BaseSource;
        public readonly float LastEffective;
        public readonly float LastModValue;

        public ScalarBindingDebugInfo(
            ScalarBindingEndpoint source,
            ScalarBindingEndpoint target,
            ScalarLinkMode mode,
            float factor,
            ScalarLinkClamp clamp,
            string tag,
            float baseSource,
            float lastEffective,
            float lastModValue)
        {
            Source = source;
            Target = target;
            Mode = mode;
            Factor = factor;
            Clamp = clamp;
            Tag = tag;
            BaseSource = baseSource;
            LastEffective = lastEffective;
            LastModValue = lastModValue;
        }
    }

    public interface IScalarBindingTelemetry
    {
        IReadOnlyList<ScalarBindingDebugInfo> GetBindings();
    }
}
