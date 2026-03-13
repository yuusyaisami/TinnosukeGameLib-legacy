#nullable enable
namespace Game
{
    public static class ScopeNodeUtility
    {
        public static BaseLifetimeScope? FindNearestBaseLifetimeScope(IScopeNode? node)
        {
            while (node != null)
            {
                if (node is BaseLifetimeScope baseScope)
                    return baseScope;

                node = node.Parent;
            }

            return null;
        }
    }
}

