#nullable enable
using System;

namespace Game.RoomMap
{
    public static class RoomMapAutoTileEvaluator
    {
        public readonly struct RuleMatch
        {
            public readonly RoomMapAutoTileRule Rule;
            public readonly int Priority;
            public readonly int Specificity;
            public readonly int OrderIndex;

            public RuleMatch(RoomMapAutoTileRule rule, int priority, int specificity, int orderIndex)
            {
                Rule = rule;
                Priority = priority;
                Specificity = specificity;
                OrderIndex = orderIndex;
            }
        }

        public static bool TryFindBestRule(
            RoomMapAutoTileRuleSetSO ruleSet,
            RoomMapInstance instance,
            RoomMapTileRegistry tileRegistry,
            int layerIndex,
            int x,
            int y,
            int centerTileId,
            out RoomMapAutoTileRule? bestRule)
        {
            bestRule = null;

            if (ruleSet == null || instance == null || tileRegistry == null)
                return false;

            var rules = ruleSet.Rules;
            if (rules == null || rules.Length == 0)
                return false;

            RuleMatch best = default;
            var hasBest = false;

            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r == null || !r.Enabled)
                    continue;

                r.EnsureDefaults();

                if (!TryMatchAnyTransform(r, instance, tileRegistry, layerIndex, x, y, centerTileId, out var specificity))
                    continue;

                var match = new RuleMatch(r, r.Priority, specificity, i);
                if (!hasBest)
                {
                    best = match;
                    hasBest = true;
                    continue;
                }

                if (Compare(match, best) > 0)
                    best = match;
            }

            if (!hasBest)
                return false;

            bestRule = best.Rule;
            return true;
        }

        static int Compare(in RuleMatch a, in RuleMatch b)
        {
            if (a.Priority != b.Priority)
                return a.Priority.CompareTo(b.Priority);

            if (a.Specificity != b.Specificity)
                return a.Specificity.CompareTo(b.Specificity);

            // tie-break: later wins
            return a.OrderIndex.CompareTo(b.OrderIndex);
        }

        static bool TryMatchAnyTransform(
            RoomMapAutoTileRule rule,
            RoomMapInstance instance,
            RoomMapTileRegistry tileRegistry,
            int layerIndex,
            int x,
            int y,
            int centerTileId,
            out int bestSpecificity)
        {
            bestSpecificity = 0;

            // Identity
            if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.Identity, out var spec))
            {
                bestSpecificity = spec;
                return true;
            }

            // Rotate 90/180/270
            if (rule.AllowRotate90)
            {
                if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.Rotate90, out spec)) { bestSpecificity = spec; return true; }
                if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.Rotate180, out spec)) { bestSpecificity = spec; return true; }
                if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.Rotate270, out spec)) { bestSpecificity = spec; return true; }
            }

            if (rule.AllowMirrorX)
            {
                if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.MirrorX, out spec)) { bestSpecificity = spec; return true; }
            }

            if (rule.AllowMirrorY)
            {
                if (TryMatch(rule.Pattern, instance, tileRegistry, layerIndex, x, y, centerTileId, TransformKind.MirrorY, out spec)) { bestSpecificity = spec; return true; }
            }

            return false;
        }

        enum TransformKind
        {
            Identity = 0,
            Rotate90 = 1,
            Rotate180 = 2,
            Rotate270 = 3,
            MirrorX = 4,
            MirrorY = 5,
        }

        static bool TryMatch(
            RoomMapAutoTilePattern pattern,
            RoomMapInstance instance,
            RoomMapTileRegistry tileRegistry,
            int layerIndex,
            int cx,
            int cy,
            int centerTileId,
            TransformKind transform,
            out int specificity)
        {
            specificity = 0;

            if (pattern == null)
                return false;

            pattern.EnsureSize();

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    var (tx, ty) = TransformOffset(ox, oy, transform);
                    var cond = pattern.GetAt(tx + 1, ty + 1);

                    if (!TryGetNeighborTileId(instance, layerIndex, cx + ox, cy + oy, out var neighborTileId, out var isOob))
                    {
                        neighborTileId = 0;
                        isOob = true;
                    }

                    if (!IsCondMatched(cond, tileRegistry, centerTileId, neighborTileId, isOob))
                        return false;

                    specificity += GetSpecificity(cond);
                }
            }

            return true;
        }

        static (int x, int y) TransformOffset(int x, int y, TransformKind t)
        {
            return t switch
            {
                TransformKind.Identity => (x, y),
                TransformKind.Rotate90 => (-y, x),
                TransformKind.Rotate180 => (-x, -y),
                TransformKind.Rotate270 => (y, -x),
                TransformKind.MirrorX => (-x, y),
                TransformKind.MirrorY => (x, -y),
                _ => (x, y),
            };
        }

        static bool TryGetNeighborTileId(RoomMapInstance instance, int layerIndex, int x, int y, out int tileId, out bool isOob)
        {
            if (instance.TryGet(layerIndex, x, y, out var cell))
            {
                tileId = cell.TileId;
                isOob = false;
                return true;
            }

            tileId = 0;
            isOob = true;
            return false;
        }

        static bool IsCondMatched(
            in RoomMapAutoTileCellCond cond,
            RoomMapTileRegistry tileRegistry,
            int centerTileId,
            int neighborTileId,
            bool isOob)
        {
            switch (cond.Kind)
            {
                case RoomMapAutoTileCondKind.Any:
                    return true; // includes OOB

                case RoomMapAutoTileCondKind.OutOfBounds:
                    return isOob;

                case RoomMapAutoTileCondKind.ExactTileId:
                    return !isOob && neighborTileId == cond.TileId;

                case RoomMapAutoTileCondKind.HasTag:
                    {
                        if (isOob)
                            return false;

                        if (!tileRegistry.TryGetTags(neighborTileId, out var tags))
                            return false;

                        return (tags & cond.Tag) != 0;
                    }

                case RoomMapAutoTileCondKind.SameAsCenter:
                    return !isOob && neighborTileId == centerTileId;

                case RoomMapAutoTileCondKind.DifferentFromCenter:
                    // Spec: OOB is treated as Different.
                    return isOob || neighborTileId != centerTileId;

                default:
                    return true;
            }
        }

        static int GetSpecificity(in RoomMapAutoTileCellCond cond)
        {
            return cond.Kind switch
            {
                RoomMapAutoTileCondKind.Any => 0,
                RoomMapAutoTileCondKind.OutOfBounds => 1,
                RoomMapAutoTileCondKind.SameAsCenter => 1,
                RoomMapAutoTileCondKind.DifferentFromCenter => 1,
                RoomMapAutoTileCondKind.HasTag => 2,
                RoomMapAutoTileCondKind.ExactTileId => 3,
                _ => 0,
            };
        }
    }
}
