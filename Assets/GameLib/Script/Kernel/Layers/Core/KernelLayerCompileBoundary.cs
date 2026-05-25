#nullable enable
using System;

namespace Game.Kernel.Layers
{
    public static class KernelLayerCompileBoundary
    {
        public const string CoreAssemblyName = "GameLib.Kernel.Layers.Core";
        public const string QuarantineAssemblyName = "GameLib.Kernel.Layers.Quarantine";
        public const bool CoreDirectLegacyReferencesForbidden = true;
        public const bool QuarantineAutoReferenceForbidden = true;

        public static bool IsCoreAssembly(string assemblyName)
        {
            return StringComparer.Ordinal.Equals(assemblyName, CoreAssemblyName);
        }

        public static bool IsQuarantineAssembly(string assemblyName)
        {
            return StringComparer.Ordinal.Equals(assemblyName, QuarantineAssemblyName);
        }
    }
}
