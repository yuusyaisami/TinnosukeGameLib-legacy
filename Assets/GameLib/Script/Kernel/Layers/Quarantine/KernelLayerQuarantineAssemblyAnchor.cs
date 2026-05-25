#nullable enable

namespace Game.Kernel.Layers.Quarantine
{
    /// <summary>
    /// Future legacy adapters must live in the quarantine assembly.
    /// Kernel layer runtime code must not reference legacy LTS surfaces directly.
    /// </summary>
    public static class KernelLayerQuarantineAssemblyAnchor
    {
    }
}
