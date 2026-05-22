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

            // child -> index (swap-back remove 逕ｨ)
            public readonly Dictionary<IScopeNode, int> Index =
                new(ReferenceEqualityComparer<IScopeNode>.Instance);
        }

        // parent -> children
        static readonly Dictionary<IScopeNode, ChildBucket> Children =
            new(ReferenceEqualityComparer<IScopeNode>.Instance);

        // child -> parent (Unregister 繧・O(1) 縺ｫ縺吶ｋ譬ｸ蠢・
        static readonly Dictionary<IScopeNode, IScopeNode?> ParentMap =
            new(ReferenceEqualityComparer<IScopeNode>.Instance);

        public static void Register(IScopeNode node, IScopeNode? parent)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            // 蜈・さ繝ｼ繝峨・縲罫eturn縲阪＠縺ｦ邨ゅｏ繧奇ｼ茨ｼ晏商縺・匳骭ｲ縺梧ｮ九ｊ蠕励ｋ・・
            // 縺薙％縺ｯ豁｣縺励＆蜆ｪ蜈医〒縲後ョ繧ｿ繝・メ謇ｱ縺・阪↓縺励※縺・ｋ縲・
            if (ReferenceEquals(parent, node))
            {
                // 竊・蜈・・謖吝虚繧貞宍蟇・↓邯ｭ謖√＠縺溘＞縺ｪ繧峨√％縺ｮ2陦後ｒ豸医＠縺ｦ return 縺ｫ謌ｻ縺・
                Unregister(node);
                return;
            }

            // 譌｢逋ｻ骭ｲ縺ｪ繧峨瑚ｦｪ縺悟､峨ｏ縺｣縺滓凾縺縺代堺ｻ倥￠譖ｿ縺・
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

            if (parent != null)
                AddChild(parent, node);
        }

        public static void Unregister(IScopeNode node)
        {
            if (node == null)
                return;

            // (1) 閾ｪ蛻・′縲悟ｭ舌阪〒縺ゅｋ髢｢菫ゅｒ螟悶☆・亥・謗｢邏｢縺励↑縺・ｼ・
            if (ParentMap.TryGetValue(node, out var parent))
            {
                if (parent != null)
                    RemoveChild(parent, node);

                ParentMap.Remove(node);
            }

            // (2) 閾ｪ蛻・′縲瑚ｦｪ縲阪→縺励※謖√▲縺ｦ縺・ｋ蟄舌Μ繧ｹ繝医ｒ豸医☆・亥・繧ｳ繝ｼ繝・Children.Remove(node) 逶ｸ蠖難ｼ・
            //     縺､縺・〒縺ｫ ParentMap 蛛ｴ縺ｮ蜿ら・繧よ祉髯､縺励※縺翫￥・亥ｹｽ髴雁盾辣ｧ/繝｡繝｢繝ｪ菫晄戟繧帝∩縺代ｋ・・
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
            // 菫晞匱・壹ｂ縺励ヰ繧ｰ縺ｧ縲瑚､・焚隕ｪ縺ｮ繝ｪ繧ｹ繝医↓蜈･繧九咲憾諷九′逋ｺ逕溘＠縺ｦ縺溷ｴ蜷医・謗・勁縲・
            // ・域悽譚･縺ｯ襍ｷ縺阪↑縺・ｨｭ險医□縺後∫ｧｻ陦梧悄縺ｮ螳牙・蠑√→縺励※・・
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
                if (current is KernelScopeHost runtimeScope && runtimeScope.UseAsGameLogicRoot)
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

