using System;

namespace Game
{
    public static class LifetimeScopeMaskUtility
    {
        public static bool TryGetBit(LifetimeScopeKind kind, out LifetimeScopeMask bit)
        {
            switch (kind)
            {
                case LifetimeScopeKind.Project:
                    bit = LifetimeScopeMask.Project;
                    return true;
                case LifetimeScopeKind.Platform:
                    bit = LifetimeScopeMask.Platform;
                    return true;
                case LifetimeScopeKind.Global:
                    bit = LifetimeScopeMask.Global;
                    return true;
                case LifetimeScopeKind.Scene:
                    bit = LifetimeScopeMask.Scene;
                    return true;
                case LifetimeScopeKind.Field:
                    bit = LifetimeScopeMask.Field;
                    return true;
                case LifetimeScopeKind.Entity:
                    bit = LifetimeScopeMask.Entity;
                    return true;
                case LifetimeScopeKind.UI:
                    bit = LifetimeScopeMask.UI;
                    return true;
                case LifetimeScopeKind.UIElement:
                    bit = LifetimeScopeMask.UIElement;
                    return true;
                case LifetimeScopeKind.Runtime:
                    bit = LifetimeScopeMask.Runtime;
                    return true;
                default:
                    bit = LifetimeScopeMask.None;
                    return false;
            }
        }

        public static bool IsKindAllowed(LifetimeScopeKind kind, LifetimeScopeMask mask)
        {
            if (!TryGetBit(kind, out var bit))
                return false;

            return (mask & bit) != 0;
        }

        public static bool TryGetSingleKind(LifetimeScopeMask mask, out LifetimeScopeKind kind)
        {
            if (mask == LifetimeScopeMask.None)
            {
                kind = LifetimeScopeKind.None;
                return false;
            }

            int m = (int)mask;
            if ((m & (m - 1)) != 0)
            {
                kind = LifetimeScopeKind.None;
                return false;
            }

            if (mask == LifetimeScopeMask.Project)
            {
                kind = LifetimeScopeKind.Project;
                return true;
            }

            if (mask == LifetimeScopeMask.Platform)
            {
                kind = LifetimeScopeKind.Platform;
                return true;
            }

            if (mask == LifetimeScopeMask.Global)
            {
                kind = LifetimeScopeKind.Global;
                return true;
            }

            if (mask == LifetimeScopeMask.Scene)
            {
                kind = LifetimeScopeKind.Scene;
                return true;
            }

            if (mask == LifetimeScopeMask.Field)
            {
                kind = LifetimeScopeKind.Field;
                return true;
            }

            if (mask == LifetimeScopeMask.Entity)
            {
                kind = LifetimeScopeKind.Entity;
                return true;
            }

            if (mask == LifetimeScopeMask.UI)
            {
                kind = LifetimeScopeKind.UI;
                return true;
            }

            if (mask == LifetimeScopeMask.UIElement)
            {
                kind = LifetimeScopeKind.UIElement;
                return true;
            }

            if (mask == LifetimeScopeMask.Runtime)
            {
                kind = LifetimeScopeKind.Runtime;
                return true;
            }

            kind = LifetimeScopeKind.None;
            return false;
        }
    }
}
