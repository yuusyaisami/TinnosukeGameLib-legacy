using System;

namespace Game.Scalar
{
    /// <summary>
    /// Default runtime config provider that supplies no baseline/config.
    /// Initial values must be provided via ProfileRegistry.
    /// </summary>
    public sealed class NullScalarRuntimeConfigProvider : IScalarRuntimeConfigProvider
    {
        public bool TryGetBase(ScalarKey key, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetConfig(ScalarKey key, out ScalarRuntimeConfig config)
        {
            config = null;
            return false;
        }
    }
}
