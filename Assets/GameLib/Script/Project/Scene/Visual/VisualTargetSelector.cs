#nullable enable

using System;

namespace Game.Visual
{
    public enum VisualTargetSelectorKind
    {
        All = 0,
        ByKind = 1,
        ByTag = 2,
        ByKindAndTag = 3,
    }

    /// <summary>
    /// VisualSystem の送信先指定。
    /// v1 は最小限（All / ByKind / ByKindAndTag / ByTag）。
    /// </summary>
    public readonly struct VisualTargetSelector : IEquatable<VisualTargetSelector>
    {
        public readonly VisualTargetSelectorKind Kind;
        public readonly VisualHubKind HubKind;
        public readonly string Tag;

        VisualTargetSelector(VisualTargetSelectorKind kind, VisualHubKind hubKind, string tag)
        {
            Kind = kind;
            HubKind = hubKind;
            Tag = tag ?? string.Empty;
        }

        public static VisualTargetSelector All() => new(VisualTargetSelectorKind.All, default, string.Empty);

        public static VisualTargetSelector ByKind(VisualHubKind kind) => new(VisualTargetSelectorKind.ByKind, kind, string.Empty);

        public static VisualTargetSelector ByTag(string tag) => new(VisualTargetSelectorKind.ByTag, default, tag ?? string.Empty);

        public static VisualTargetSelector ByKindAndTag(VisualHubKind kind, string tag) => new(VisualTargetSelectorKind.ByKindAndTag, kind, tag ?? string.Empty);

        public bool Matches(IVisualHub hub)
        {
            if (hub == null) return false;

            switch (Kind)
            {
                case VisualTargetSelectorKind.All:
                    return true;
                case VisualTargetSelectorKind.ByKind:
                    return hub.Kind == HubKind;
                case VisualTargetSelectorKind.ByTag:
                    return string.Equals(hub.HubTag ?? string.Empty, Tag, StringComparison.Ordinal);
                case VisualTargetSelectorKind.ByKindAndTag:
                    return hub.Kind == HubKind && string.Equals(hub.HubTag ?? string.Empty, Tag, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public int SpecificityOrder
        {
            get
            {
                // v1 の推奨: All → ByKind → ByKindAndTag
                // ByTag は必要時のみなので中間に置く。
                return Kind switch
                {
                    VisualTargetSelectorKind.All => 0,
                    VisualTargetSelectorKind.ByKind => 1,
                    VisualTargetSelectorKind.ByTag => 2,
                    VisualTargetSelectorKind.ByKindAndTag => 3,
                    _ => 99,
                };
            }
        }

        public bool Equals(VisualTargetSelector other)
        {
            return Kind == other.Kind && HubKind == other.HubKind && string.Equals(Tag, other.Tag, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is VisualTargetSelector other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = (int)Kind;
                h = (h * 397) ^ (int)HubKind;
                h = (h * 397) ^ (Tag != null ? StringComparer.Ordinal.GetHashCode(Tag) : 0);
                return h;
            }
        }

        public override string ToString()
        {
            return Kind switch
            {
                VisualTargetSelectorKind.All => "All",
                VisualTargetSelectorKind.ByKind => $"ByKind({HubKind})",
                VisualTargetSelectorKind.ByTag => $"ByTag({Tag})",
                VisualTargetSelectorKind.ByKindAndTag => $"ByKindAndTag({HubKind},{Tag})",
                _ => $"Unknown({(int)Kind})",
            };
        }
    }
}
