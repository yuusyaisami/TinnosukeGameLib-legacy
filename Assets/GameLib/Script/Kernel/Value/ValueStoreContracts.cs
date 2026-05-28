#nullable enable
using System;
using Game.Kernel.IR;

namespace Game.Kernel.Value
{
    public enum ValueStoreScopeKind
    {
        Unknown = 0,
        Kernel = 10,
        Project = 20,
        Scene = 30,
        Scope = 40,
        Entity = 50,
        CommandLocal = 60,
        Test = 90,
    }

    public readonly struct ValueKeyMetadata : IEquatable<ValueKeyMetadata>
    {
        readonly string displayName;
        readonly string? saveChannel;

        public ValueKeyMetadata(
            ValueKeyId keyId,
            ValueSchemaId schemaId,
            ValueKind kind,
            string displayName,
            bool persists,
            bool saveAcrossProfiles,
            string? saveChannel)
        {
            if (keyId.Value == 0)
                throw new ArgumentException("Value metadata must provide a non-zero ValueKeyId.", nameof(keyId));

            if (schemaId.Value == 0)
                throw new ArgumentException("Value metadata must provide a non-zero ValueSchemaId.", nameof(schemaId));

            if (kind == ValueKind.Null)
                throw new ArgumentException("Value metadata must provide a non-null ValueKind.", nameof(kind));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Value metadata must provide a display name.", nameof(displayName));

            if (saveChannel != null && saveChannel.Trim().Length == 0)
                throw new ArgumentException("Save channels must be null or non-empty.", nameof(saveChannel));

            KeyId = keyId;
            SchemaId = schemaId;
            Kind = kind;
            this.displayName = displayName.Trim();
            Persists = persists;
            SaveAcrossProfiles = saveAcrossProfiles;
            this.saveChannel = saveChannel?.Trim();
        }

        public ValueKeyId KeyId { get; }

        public ValueSchemaId SchemaId { get; }

        public ValueKind Kind { get; }

        public string DisplayName => displayName;

        public bool Persists { get; }

        public bool SaveAcrossProfiles { get; }

        public string? SaveChannel => saveChannel;

        public bool Equals(ValueKeyMetadata other)
        {
            return KeyId == other.KeyId
                && SchemaId == other.SchemaId
                && Kind == other.Kind
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && Persists == other.Persists
                && SaveAcrossProfiles == other.SaveAcrossProfiles
                && string.Equals(SaveChannel, other.SaveChannel, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueKeyMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = KeyId.GetHashCode();
                hash = (hash * 397) ^ SchemaId.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(displayName);
                hash = (hash * 397) ^ Persists.GetHashCode();
                hash = (hash * 397) ^ SaveAcrossProfiles.GetHashCode();
                hash = (hash * 397) ^ (saveChannel != null ? StringComparer.Ordinal.GetHashCode(saveChannel) : 0);
                return hash;
            }
        }

        public static bool operator ==(ValueKeyMetadata left, ValueKeyMetadata right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueKeyMetadata left, ValueKeyMetadata right)
        {
            return !left.Equals(right);
        }
    }

    public interface IReadOnlyValueStore
    {
        ValueStoreScopeKind ScopeKind { get; }

        bool TryRead(ValueKeyId keyId, out ValueVariant value);

        uint GetRevision(ValueKeyId keyId);

        bool TryGetMetadata(ValueKeyId keyId, out ValueKeyMetadata metadata);
    }

    public interface IValueStore : IReadOnlyValueStore
    {
        bool TryWrite(ValueKeyId keyId, in ValueVariant value);
    }
}