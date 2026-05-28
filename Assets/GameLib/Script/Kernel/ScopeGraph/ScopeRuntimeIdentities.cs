#nullable enable
using System;

namespace Game.Kernel.ScopeGraph
{
    public readonly struct ScopeHandle : IEquatable<ScopeHandle>
    {
        public ScopeHandle(int index, int generation)
        {
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Scope handles must use a positive slot index.");

            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation), generation, "Scope handles must use a positive generation.");

            Index = index;
            Generation = generation;
        }

        public int Index { get; }

        public int Generation { get; }

        public bool IsDefault => Index == 0 && Generation == 0;

        public bool IsValid => Index > 0 && Generation > 0;

        public bool Equals(ScopeHandle other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is ScopeHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }

        public override string ToString()
        {
            return IsDefault ? "ScopeHandle(<default>)" : "ScopeHandle(" + Index + ", " + Generation + ")";
        }

        public static bool operator ==(ScopeHandle left, ScopeHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScopeHandle left, ScopeHandle right)
        {
            return !left.Equals(right);
        }
    }

    public enum UnityObjectLinkKind
    {
        Unknown = 0,
        Asset = 10,
        Scene = 20,
        Runtime = 30,
        Selection = 40,
    }

    public readonly struct UnityObjectLink : IEquatable<UnityObjectLink>
    {
        readonly string? sourceGuid;
        readonly string? debugName;

        public UnityObjectLink(
            UnityObjectLinkKind kind,
            string? sourceGuid,
            long localFileId,
            int runtimeInstanceId,
            string? debugName)
        {
            if (kind == UnityObjectLinkKind.Unknown)
            {
                if (!string.IsNullOrEmpty(sourceGuid) || localFileId != 0 || runtimeInstanceId != 0 || !string.IsNullOrEmpty(debugName))
                    throw new ArgumentException("Unity object links with unknown kind must be empty.", nameof(kind));

                Kind = kind;
                this.sourceGuid = string.IsNullOrWhiteSpace(sourceGuid) ? null : sourceGuid.Trim();
                LocalFileId = 0;
                RuntimeInstanceId = 0;
                this.debugName = string.Empty;
                return;
            }

            if (runtimeInstanceId < 0)
                throw new ArgumentOutOfRangeException(nameof(runtimeInstanceId), runtimeInstanceId, "Unity object links must use a non-negative runtime instance id.");

            if (!string.IsNullOrWhiteSpace(sourceGuid))
                sourceGuid = sourceGuid.Trim();

            if (!string.IsNullOrWhiteSpace(debugName))
                debugName = debugName.Trim();

            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Unity object links must provide a debug name.", nameof(debugName));

            if (!string.IsNullOrEmpty(sourceGuid) && localFileId <= 0)
                throw new ArgumentException("Unity object links with a source GUID must provide a positive local file id.", nameof(localFileId));

            if (localFileId < 0)
                throw new ArgumentOutOfRangeException(nameof(localFileId), localFileId, "Unity object links must use a non-negative local file id.");

            Kind = kind;
            this.sourceGuid = sourceGuid == null ? null : sourceGuid.Trim();
            LocalFileId = localFileId;
            RuntimeInstanceId = runtimeInstanceId;
            this.debugName = debugName.Trim();
        }

        public UnityObjectLinkKind Kind { get; }

        public string SourceGuid => sourceGuid ?? string.Empty;

        public long LocalFileId { get; }

        public int RuntimeInstanceId { get; }

        public string DebugName => debugName ?? string.Empty;

        public bool IsEmpty => Kind == UnityObjectLinkKind.Unknown
            && string.IsNullOrEmpty(sourceGuid)
            && LocalFileId == 0
            && RuntimeInstanceId == 0
            && string.IsNullOrEmpty(debugName);

        public bool HasPersistentSource => !string.IsNullOrEmpty(sourceGuid) && LocalFileId > 0;

        public bool Equals(UnityObjectLink other)
        {
            return Kind == other.Kind
                && string.Equals(SourceGuid, other.SourceGuid, StringComparison.Ordinal)
                && LocalFileId == other.LocalFileId
                && RuntimeInstanceId == other.RuntimeInstanceId
                && string.Equals(DebugName, other.DebugName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is UnityObjectLink other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (sourceGuid != null ? StringComparer.Ordinal.GetHashCode(sourceGuid) : 0);
                hash = (hash * 397) ^ LocalFileId.GetHashCode();
                hash = (hash * 397) ^ RuntimeInstanceId;
                hash = (hash * 397) ^ (debugName != null ? StringComparer.Ordinal.GetHashCode(debugName) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return IsEmpty
                ? "UnityObjectLink(<empty>)"
                : "UnityObjectLink(Kind=" + Kind + ", SourceGuid=" + (SourceGuid.Length == 0 ? "<none>" : SourceGuid) + ", LocalFileId=" + LocalFileId + ", RuntimeInstanceId=" + RuntimeInstanceId + ", DebugName=" + DebugName + ")";
        }

        public static bool operator ==(UnityObjectLink left, UnityObjectLink right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnityObjectLink left, UnityObjectLink right)
        {
            return !left.Equals(right);
        }
    }
}
