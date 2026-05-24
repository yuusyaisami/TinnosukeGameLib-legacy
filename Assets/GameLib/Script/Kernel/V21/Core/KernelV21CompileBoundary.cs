#nullable enable
using System;

namespace Game.Kernel.V21
{
    public static class KernelV21CompileBoundary
    {
        public const string CoreAssemblyName = "GameLib.Kernel.V21.Core";
        public const string QuarantineAssemblyName = "GameLib.Kernel.V21.Quarantine";
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
