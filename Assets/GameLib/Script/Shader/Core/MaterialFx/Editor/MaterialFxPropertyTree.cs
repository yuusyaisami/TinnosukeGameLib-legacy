#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxPropertyRegistrySO からツリー構造を構築するユーティリティ。
    /// Dropdown や Explorer で共有。
    /// </summary>
    public static class MaterialFxPropertyTree
    {
        public sealed class Node
        {
            public string Segment;      // "Flash" etc.
            public string FullPath;     // "Flash/Amount"
            public string StableKey;    // actual key string (leaf only)
            public ValueKind ValueType; // leaf only
            public bool IsFolder;
            public List<Node> Children = new();
        }

        /// <summary>
        /// Registry から階層ツリーを構築。root.Children がトップレベル。
        /// </summary>
        public static Node Build(MaterialFxPropertyRegistrySO registry)
        {
            var root = new Node
            {
                Segment = string.Empty,
                FullPath = string.Empty,
                StableKey = null,
                IsFolder = true
            };

            if (registry == null)
                return root;

            // parentId -> children
            var parentMap = new Dictionary<string, List<MaterialFxPropertyNode>>(StringComparer.Ordinal);
            foreach (var n in registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<MaterialFxPropertyNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }

            void BuildChildren(Node parent, string parentId, string parentPath)
            {
                if (!parentMap.TryGetValue(parentId ?? string.Empty, out var children))
                    return;

                foreach (var regNode in children)
                {
                    var seg = regNode.Name ?? string.Empty;
                    var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

                    var treeNode = new Node
                    {
                        Segment = seg,
                        FullPath = path,
                        StableKey = regNode.IsFolder ? null : (string.IsNullOrEmpty(regNode.StableKey) ? path : regNode.StableKey),
                        ValueType = regNode.ValueType,
                        IsFolder = regNode.IsFolder
                    };

                    parent.Children.Add(treeNode);
                    BuildChildren(treeNode, regNode.Id, path);
                }
            }

            BuildChildren(root, string.Empty, string.Empty);
            return root;
        }

        /// <summary>
        /// StableKey から Node を検索（フラット探索）。
        /// </summary>
        public static Node FindByStableKey(Node root, string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey))
                return null;

            Node Search(Node node)
            {
                if (!node.IsFolder && string.Equals(node.StableKey, stableKey, StringComparison.Ordinal))
                    return node;

                foreach (var c in node.Children)
                {
                    var found = Search(c);
                    if (found != null)
                        return found;
                }
                return null;
            }

            return Search(root);
        }

        /// <summary>
        /// すべての leaf (property) をフラットリストで列挙。
        /// </summary>
        public static IEnumerable<Node> EnumerateLeaves(Node root)
        {
            if (root == null) yield break;

            var stack = new Stack<Node>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!node.IsFolder && !string.IsNullOrEmpty(node.StableKey))
                    yield return node;

                for (int i = node.Children.Count - 1; i >= 0; i--)
                    stack.Push(node.Children[i]);
            }
        }
    }
}
#endif
