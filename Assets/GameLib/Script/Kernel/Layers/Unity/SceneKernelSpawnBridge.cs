#nullable enable

using Game.Kernel.Layers;

namespace Game.Kernel.Layers.Unity
{
    public static class SceneKernelSpawnBridge
    {
        public static bool TryGetCurrentSpawnBoundary(out ISceneKernelSpawnBoundary spawnBoundary)
        {
            if (ApplicationKernelHostMB.TryGetInstance(out ApplicationKernelHostMB? applicationKernelHost)
                && applicationKernelHost?.CurrentSceneKernelHost != null)
            {
                return applicationKernelHost.CurrentSceneKernelHost.RuntimeKernel.TryGetSpawnBoundary(out spawnBoundary);
            }

            spawnBoundary = null!;
            return false;
        }
    }
}