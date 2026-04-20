#nullable enable
namespace Game
{
    public static class ScopeNodeUtility
    {
        public static RuntimeLifetimeScopeBase? FindNearestRuntimeLifetimeScope(IScopeNode? node)
        {
            while (node != null)
            {
                if (node is RuntimeLifetimeScopeBase runtimeScope)
                    return runtimeScope;

                node = node.Parent;
            }

            return null;
        }
    }
}

