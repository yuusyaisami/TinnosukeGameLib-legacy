#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Editor.Foundation
{
    /// <summary>
    /// ドット区切りなどのパスを元に構築する汎用ツリーノード。
    /// TEntry にはカタログの行など任意の型を格納できる。
    /// </summary>
    public sealed class PathTreeNode<TEntry>
    {
        public string Segment;   // 例: "Attack"
        public string FullPath;  // 例: "Entity.Player.Attack"
        public bool HasEntry;
        public TEntry Entry;

        public readonly List<PathTreeNode<TEntry>> Children = new();

        public bool IsLeaf => Children.Count == 0;
    }

    public static class PathTreeBuilder
    {
        /// <summary>
        /// エントリ列と「パス取得関数」からツリーを構築する。ルートノードは常に空パス ""。
        /// </summary>
        public static PathTreeNode<TEntry> Build<TEntry>(
            IEnumerable<TEntry> entries,
            Func<TEntry, string> getPath,
            char separator = '.',
            bool sort = true)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (getPath == null) throw new ArgumentNullException(nameof(getPath));

            var root = new PathTreeNode<TEntry>
            {
                Segment = string.Empty,
                FullPath = string.Empty,
                HasEntry = false
            };

            var dict = new Dictionary<string, PathTreeNode<TEntry>>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };

            foreach (var entry in entries)
            {
                var path = getPath(entry);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var parts = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                string currentPath = string.Empty;
                var parent = root;

                for (int i = 0; i < parts.Length; i++)
                {
                    var segment = parts[i];
                    currentPath = string.IsNullOrEmpty(currentPath)
                        ? segment
                        : $"{currentPath}{separator}{segment}";

                    if (!dict.TryGetValue(currentPath, out var node))
                    {
                        node = new PathTreeNode<TEntry>
                        {
                            Segment = segment,
                            FullPath = currentPath
                        };
                        dict[currentPath] = node;
                        parent.Children.Add(node);
                    }

                    parent = node;
                }

                parent.HasEntry = true;
                parent.Entry = entry;
            }

            if (sort)
                SortRecursive(root);
            return root;
        }

        static void SortRecursive<TEntry>(PathTreeNode<TEntry> node)
        {
            node.Children.Sort((a, b) =>
                string.Compare(a.Segment, b.Segment, StringComparison.Ordinal));

            foreach (var child in node.Children)
            {
                SortRecursive(child);
            }
        }
    }
}
#endif
