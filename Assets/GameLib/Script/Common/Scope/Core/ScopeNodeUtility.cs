#nullable enable
namespace Game
{
    public static class ScopeNodeUtility
    {
        public static KernelScopeHost? FindNearestRuntimeLifetimeScope(IScopeNode? node)
        {
            while (node != null)
            {
                if (node is KernelScopeHost runtimeScope)
                    return runtimeScope;

                node = node.Parent;
            }

            return null;
        }
    }
}


