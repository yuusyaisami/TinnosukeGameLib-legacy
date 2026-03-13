// Assets/GameLib/Script/Common/LTS/Core/ScopeNodeHierarchy.cs
#nullable enable
using System;
using System.Collections.Generic;

namespace Game
{
    public static class ScopeNodeHierarchy
    {
        sealed class ChildBucket
        {
            public readonly List<IScopeNode> List = new(4);

            // child -> index (swap-back remove 用)
            public readonly Dictionary<IScopeNode, int> Index =
                new(ReferenceEqualityComparer<IScopeNode>.Instance);
        }

        // parent -> children
        static readonly Dictionary<IScopeNode, ChildBucket> Children =
            new(ReferenceEqualityComparer<IScopeNode>.Instance);

        // child -> parent (Unregister を O(1) にする核心)
        static readonly Dictionary<IScopeNode, IScopeNode?> ParentMap =
            new(ReferenceEqualityComparer<IScopeNode>.Instance);

        public static void Register(IScopeNode node, IScopeNode? parent)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            // 元コードは「return」して終わり（＝古い登録が残り得る）
            // ここは正しさ優先で「デタッチ扱い」にしている。
            if (parent == null || ReferenceEquals(parent, node))
            {
                // ↓ 元の挙動を厳密に維持したいなら、この2行を消して return に戻す
                Unregister(node);
                return;
            }

            // 既登録なら「親が変わった時だけ」付け替え
            if (ParentMap.TryGetValue(node, out var oldParent))
            {
                if (ReferenceEquals(oldParent, parent))
                    return;

                if (oldParent != null)
                    RemoveChild(oldParent, node);

                ParentMap[node] = parent;
            }
            else
            {
                ParentMap.Add(node, parent);
            }

            AddChild(parent, node);
        }

        public static void Unregister(IScopeNode node)
        {
            if (node == null)
                return;

            // (1) 自分が「子」である関係を外す（全探索しない）
            if (ParentMap.TryGetValue(node, out var parent))
            {
                if (parent != null)
                    RemoveChild(parent, node);

                ParentMap.Remove(node);
            }

            // (2) 自分が「親」として持っている子リストを消す（元コード Children.Remove(node) 相当）
            //     ついでに ParentMap 側の参照も掃除しておく（幽霊参照/メモリ保持を避ける）
            if (Children.TryGetValue(node, out var bucket))
            {
                var list = bucket.List;
                for (int i = 0; i < list.Count; i++)
                {
                    var child = list[i];
                    if (child != null)
                        ParentMap.Remove(child);
                }

                Children.Remove(node);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 保険：もしバグで「複数親のリストに入る」状態が発生してた場合の掃除。
            // （本来は起きない設計だが、移行期の安全弁として）
            foreach (var kv in Children)
            {
                RemoveChildIfPresent(kv.Value, node);
            }
#endif
        }

        public static IReadOnlyList<IScopeNode> GetChildrenOrEmpty(IScopeNode parent)
        {
            if (parent == null)
                return Array.Empty<IScopeNode>();

            return Children.TryGetValue(parent, out var bucket) && bucket != null
                ? bucket.List
                : Array.Empty<IScopeNode>();
        }

        public static IEnumerable<IScopeNode> EnumerateSubtree(IScopeNode root, bool includeSelf = true)
        {
            if (root == null)
                yield break;

            if (includeSelf)
                yield return root;

            var queue = new Queue<IScopeNode>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var children = GetChildrenOrEmpty(node);
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null)
                        continue;

                    queue.Enqueue(child);
                    yield return child;
                }
            }
        }

        public static IScopeNode? FindNearestAncestorByKind(IScopeNode origin, LifetimeScopeKind kind, bool includeSelf = false)
        {
            if (origin == null || kind == LifetimeScopeKind.None)
                return null;

            var current = includeSelf ? origin : origin.Parent;
            while (current != null)
            {
                if (current.Kind == kind)
                    return current;

                current = current.Parent;
            }

            return null;
        }

        public static IScopeNode? FindNearestAncestorByMask(IScopeNode origin, LifetimeScopeMask allowedMask, bool includeSelf = false)
        {
            if (origin == null || allowedMask == LifetimeScopeMask.None)
                return null;

            var current = includeSelf ? origin : origin.Parent;
            while (current != null)
            {
                if (LifetimeScopeMaskUtility.IsKindAllowed(current.Kind, allowedMask))
                    return current;

                current = current.Parent;
            }

            return null;
        }

        public static IScopeNode? FindNearestGameLogicRoot(IScopeNode? origin, bool includeSelf = true)
        {
            var current = includeSelf ? origin : origin?.Parent;
            while (current != null)
            {
                if (current is BaseLifetimeScope baseScope && baseScope.UseAsGameLogicRoot)
                    return current;

                current = current.Parent;
            }

            return null;
        }

        // ---------- internal helpers ----------

        static void AddChild(IScopeNode parent, IScopeNode child)
        {
            if (!Children.TryGetValue(parent, out var bucket))
            {
                bucket = new ChildBucket();
                Children.Add(parent, bucket);
            }

            if (bucket.Index.ContainsKey(child))
                return;

            bucket.Index.Add(child, bucket.List.Count);
            bucket.List.Add(child);
        }

        static void RemoveChild(IScopeNode parent, IScopeNode child)
        {
            if (!Children.TryGetValue(parent, out var bucket))
                return;

            if (!bucket.Index.TryGetValue(child, out var idx))
                return;

            var list = bucket.List;
            var lastIdx = list.Count - 1;
            var last = list[lastIdx];

            // swap-back remove (O(1))
            list[idx] = last;
            list.RemoveAt(lastIdx);

            bucket.Index.Remove(child);
            if (idx != lastIdx)
                bucket.Index[last] = idx;

            if (list.Count == 0)
                Children.Remove(parent);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void RemoveChildIfPresent(ChildBucket bucket, IScopeNode child)
        {
            if (bucket == null) return;
            if (!bucket.Index.TryGetValue(child, out var idx)) return;

            var list = bucket.List;
            var lastIdx = list.Count - 1;
            var last = list[lastIdx];

            list[idx] = last;
            list.RemoveAt(lastIdx);

            bucket.Index.Remove(child);
            if (idx != lastIdx)
                bucket.Index[last] = idx;
        }
#endif

        internal static void Reset()
        {
            Children.Clear();
            ParentMap.Clear();
        }
    }
}
