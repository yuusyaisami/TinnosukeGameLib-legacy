#nullable enable

namespace Game.UI
{
    internal static class ModalStackChannelDebugLabelUtility
    {
        public static string DescribeRoot(IUIModalRoot? root)
        {
            if (root == null)
                return "(none)";

            return $"{root.ModalId} [{DescribeScope(root.OwnerScope)}]";
        }

        public static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "(null)";

            return scope.Identity?.SelfTransform?.name ?? "(unknown)";
        }
    }
}