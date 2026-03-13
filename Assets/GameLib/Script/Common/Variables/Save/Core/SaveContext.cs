// Game.Save.SaveContext.cs
//
// SaveSystem v2 (Port/Adapter) transaction context

#nullable enable
using System;
using Game;

namespace Game.Save
{
    public readonly struct ScopeKey : IEquatable<ScopeKey>
    {
        public readonly LifetimeScopeKind Kind;
        public readonly string Id;

        public ScopeKey(LifetimeScopeKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }

        public bool Equals(ScopeKey other) => Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ScopeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Id);
        public override string ToString() => $"{Kind}:{Id}";
    }

    /// <summary>
    /// Save/Load/Clear operation context.
    /// - ProfileId: non-negative slot id
    /// - ScopeKey: structured scope identity
    /// - Layer: save layer
    /// </summary>
    public readonly struct SaveContext
    {
        public readonly int ProfileId;
        public readonly SaveLayer Layer;
        public readonly ScopeKey ScopeKey;

        public SaveContext(int profileId, SaveLayer layer, ScopeKey scopeKey)
        {
            ProfileId = profileId;
            Layer = layer;
            ScopeKey = scopeKey;
        }

        public bool TryValidate(out SaveResult error)
        {
            if (ProfileId < 0)
            {
                error = SaveResult.Failed(SaveError.InvalidKey, "ProfileId must be non-negative.");
                return false;
            }

            if (ScopeKey.Kind == LifetimeScopeKind.Runtime)
            {
                error = SaveResult.Failed(SaveError.InvalidKey, "Runtime scope is not allowed for persistence.");
                return false;
            }

            if (!SaveKeys.TryValidateSegment(ScopeKey.Id, out var segErr))
            {
                error = SaveResult.Failed(SaveError.InvalidKey, segErr);
                return false;
            }

            error = SaveResult.Success();
            return true;
        }

        public override string ToString() => $"[{ProfileId}/{ScopeKey}/{Layer}]";
    }
}
