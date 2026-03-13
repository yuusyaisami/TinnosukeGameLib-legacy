#nullable enable
using System.Collections.Generic;
using Game.UI;
using VContainer;

namespace Game
{
    /// <summary>
    /// Helper extensions for IScopeNode hierarchy traversal and resolver access.
    /// Phase0 uses these to move hierarchy utilities out of BaseLifetimeScope.
    /// </summary>
    public static class ScopeNodeExtensions
    {
        public static IReadOnlyList<IScopeNode>? GetPathFromRootSafe(this IScopeNode? node)
            => node?.GetPathFromRoot();

        public static IScopeNode? GetRoot(this IScopeNode? node)
        {
            var path = node?.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return null;
            return path[0];
        }

        public static IEnumerable<IScopeNode> EnumerateAncestors(this IScopeNode? node, bool includeSelf = true)
        {
            if (node == null)
                yield break;

            var current = includeSelf ? node : node.Parent;
            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        public static bool TryResolveInAncestors<T>(this IScopeNode? node, out T service)
        {
            service = default!;
            if (node == null)
                return false;

            // GCアロケーションを回避するため、EnumerateAncestors を使わず直接ループ
            var current = node;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<T>(out var found))
                {
                    service = found;
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Try to compute a path from <paramref name="source"/> to <paramref name="target"/> using the
        /// IScopeNode hierarchy. Returns true and fills <paramref name="path"/> when a path exists.
        /// This mirrors BaseLifetimeScope.TryGetPathTo but for IScopeNode and is safe for mixed runtime/base scopes.
        /// </summary>
        public static bool TryGetPathTo(this IScopeNode source,
                                        IScopeNode? target,
                                        out List<IScopeNode>? path,
                                        bool includeSelf = true)
        {
            path = null;
            if (source == null || target == null)
                return false;

            if (ReferenceEquals(source, target))
            {
                path = includeSelf ? new List<IScopeNode> { source } : new List<IScopeNode>();
                return true;
            }

            // 1) check ancestors: source -> parent -> ...
            {
                var current = includeSelf ? source : source.Parent;
                var list = new List<IScopeNode>();
                while (current != null)
                {
                    list.Add(current);
                    if (ReferenceEquals(current, target))
                    {
                        path = list;
                        return true;
                    }
                    current = current.Parent;
                }
            }

            // 2) BFS on children
            var visited = new HashSet<IScopeNode>(ReferenceEqualityComparer<IScopeNode>.Instance);
            var queue = new Queue<IScopeNode>();
            var parentMap = new Dictionary<IScopeNode, IScopeNode?>();

            queue.Enqueue(source);
            visited.Add(source);
            parentMap[source] = null;

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (ReferenceEquals(node, target))
                {
                    // reconstruct path
                    var stack = new Stack<IScopeNode>();
                    var n = node;
                    while (n != null)
                    {
                        stack.Push(n);
                        parentMap.TryGetValue(n, out n);
                    }

                    path = new List<IScopeNode>();
                    if (!includeSelf && stack.Count > 0 && ReferenceEquals(stack.Peek(), source))
                    {
                        stack.Pop();
                    }
                    while (stack.Count > 0) path.Add(stack.Pop());
                    return true;
                }

                var children = ScopeNodeHierarchy.GetChildrenOrEmpty(node);
                for (int i = 0; i < children.Count; i++)
                {
                    var c = children[i];
                    if (c == null || visited.Contains(c)) continue;
                    visited.Add(c);
                    queue.Enqueue(c);
                    parentMap[c] = node;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolve IUIElementState/IUIElementStateController from the closest ancestor that exposes it.
        /// Convenience helpers: many UI subsystems used to call these on UIElementLifetimeScope.
        /// </summary>
        public static IUIElementState? GetUIElementState(this IScopeNode? node)
        {
            if (node == null) return null;
            if (node.TryResolveInAncestors<IUIElementState>(out var st)) return st;
            return null;
        }

        public static IUIElementStateController? GetUIElementStateController(this IScopeNode? node)
        {
            if (node == null) return null;
            if (node.TryResolveInAncestors<IUIElementStateController>(out var st)) return st;
            return null;
        }
    }
}

