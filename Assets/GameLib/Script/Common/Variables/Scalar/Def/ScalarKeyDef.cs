using System;
using UnityEngine;
using ScalarSpace = Game.LifetimeScopeKind; // ScalarSpace を LifetimeScopeKind に統一

namespace Game.Scalar
{
    /// <summary>
    /// Verified scalar identity.
    /// </summary>
    [Serializable]
    public readonly struct ScalarKeyId : IEquatable<ScalarKeyId>
    {
        public ScalarKeyId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ScalarKeyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ScalarKeyId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"ScalarKeyId({Value})";

        public static bool operator ==(ScalarKeyId left, ScalarKeyId right) => left.Equals(right);
        public static bool operator !=(ScalarKeyId left, ScalarKeyId right) => !left.Equals(right);
    }

    public enum ScalarOwnerKind
    {
        Unknown = 0,
        Application = 10,
        Platform = 20,
        Global = 30,
        Scene = 40,
        Field = 50,
        Entity = 60,
        UI = 70,
        UIElement = 80,
        Runtime = 90,
    }

    public readonly struct ScalarOwnerId : IEquatable<ScalarOwnerId>
    {
        readonly string _value;

        public ScalarOwnerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Scalar owner ids must be non-empty.", nameof(value));

            _value = value.Trim();
        }

        public string Value => _value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(ScalarOwnerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ScalarOwnerId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static bool TryParse(string value, out ScalarOwnerId ownerId)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                ownerId = new ScalarOwnerId(value);
                return true;
            }

            ownerId = default;
            return false;
        }

        public static bool operator ==(ScalarOwnerId left, ScalarOwnerId right) => left.Equals(right);
        public static bool operator !=(ScalarOwnerId left, ScalarOwnerId right) => !left.Equals(right);
    }

    public readonly struct ScalarOwnerIdentity : IEquatable<ScalarOwnerIdentity>
    {
        public ScalarOwnerIdentity(ScalarOwnerKind kind, ScalarOwnerId ownerId)
        {
            if (kind == ScalarOwnerKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind), "Scalar owner kind must be explicit.");
            if (ownerId.IsEmpty)
                throw new ArgumentException("Scalar owner identity requires a non-empty owner id.", nameof(ownerId));

            Kind = kind;
            OwnerId = ownerId;
        }

        public ScalarOwnerKind Kind { get; }
        public ScalarOwnerId OwnerId { get; }
        public bool IsValid => Kind != ScalarOwnerKind.Unknown && !OwnerId.IsEmpty;

        public bool Equals(ScalarOwnerIdentity other) => Kind == other.Kind && OwnerId == other.OwnerId;
        public override bool Equals(object obj) => obj is ScalarOwnerIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, OwnerId);
        public override string ToString() => Kind + ":" + OwnerId;

        public static bool operator ==(ScalarOwnerIdentity left, ScalarOwnerIdentity right) => left.Equals(right);
        public static bool operator !=(ScalarOwnerIdentity left, ScalarOwnerIdentity right) => !left.Equals(right);
    }

    public readonly struct ScalarBindingEndpoint : IEquatable<ScalarBindingEndpoint>
    {
        public ScalarBindingEndpoint(ScalarOwnerIdentity owner, ScalarKeyId keyId)
        {
            if (!owner.IsValid)
                throw new ArgumentException("Scalar binding endpoint requires an explicit owner identity.", nameof(owner));
            if (keyId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(keyId), "Scalar binding endpoint requires a verified key id.");

            Owner = owner;
            KeyId = keyId;
        }

        public ScalarOwnerIdentity Owner { get; }
        public ScalarKeyId KeyId { get; }
        public bool IsValid => Owner.IsValid && KeyId.Value > 0;

        public bool Equals(ScalarBindingEndpoint other) => Owner == other.Owner && KeyId == other.KeyId;
        public override bool Equals(object obj) => obj is ScalarBindingEndpoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, KeyId);
        public override string ToString() => Owner + "/" + KeyId.Value;

        public static bool operator ==(ScalarBindingEndpoint left, ScalarBindingEndpoint right) => left.Equals(right);
        public static bool operator !=(ScalarBindingEndpoint left, ScalarBindingEndpoint right) => !left.Equals(right);
    }

    public readonly struct ScalarBindingEdge : IEquatable<ScalarBindingEdge>
    {
        public ScalarBindingEdge(ScalarBindingEndpoint source, ScalarBindingEndpoint target)
        {
            if (!source.IsValid)
                throw new ArgumentException("Scalar binding edge requires a verified source endpoint.", nameof(source));
            if (!target.IsValid)
                throw new ArgumentException("Scalar binding edge requires a verified target endpoint.", nameof(target));

            Source = source;
            Target = target;
        }

        public ScalarBindingEndpoint Source { get; }
        public ScalarBindingEndpoint Target { get; }
        public bool IsValid => Source.IsValid && Target.IsValid;

        public bool Equals(ScalarBindingEdge other) => Source == other.Source && Target == other.Target;
        public override bool Equals(object obj) => obj is ScalarBindingEdge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Source, Target);
        public override string ToString() => Source + " -> " + Target;

        public static bool operator ==(ScalarBindingEdge left, ScalarBindingEdge right) => left.Equals(right);
        public static bool operator !=(ScalarBindingEdge left, ScalarBindingEdge right) => !left.Equals(right);
    }

    /// <summary>
    /// スカラーの修正種別。Add: 加算, Mul: 乗算。
    /// </summary>
    public enum ScalarModKind
    {
        Add = 10,
        Mul = 20,
        Clamp = 30,
    }

    [Serializable]
    public struct ScalarRef
    {
        public ScalarSpace Space;
        public ScalarKey Key;

        public ScalarRef(ScalarSpace space, ScalarKey key)
        {
            Space = space;
            Key = key;
        }

        public override string ToString() => $"{Space}:{Key}";
    }


    [Serializable]
    public struct ScalarKey : IEquatable<ScalarKey>, ISerializationCallbackReceiver
    {
        [SerializeField] public int Id;
        [SerializeField] public string Name;

        public ScalarKey(string name)
        {
            Name = name ?? string.Empty;
            Id = ScalarKeyIdResolver.ResolveOrZero(Name);
        }

        public bool IsVerified => Id > 0;
        public ScalarKeyId KeyId => new ScalarKeyId(Id);

        public void OnBeforeSerialize() => Id = ScalarKeyIdResolver.ResolveOrZero(Name ?? string.Empty);
        public void OnAfterDeserialize() => Id = ScalarKeyIdResolver.ResolveOrZero(Name ?? string.Empty);

        public bool Equals(ScalarKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ScalarKey o && Equals(o);
        public override int GetHashCode() => Id;
        public override string ToString() => Name;

        /// <summary>
        /// Returns a human-friendly, hierarchical label for the key suitable for editors and debug views.
        /// Example: "Player.Attack.Power" -> "Player → Attack → Power  (#123456)"
        /// </summary>
        /// <param name="includeId">Include the key id in the returned label when true (default true).</param>
        public string FormatLabel(bool includeId = true)
        {
            if (Id == 0 && string.IsNullOrEmpty(Name))
                return "(empty)";

            var name = Name ?? string.Empty;
            var parts = name.Split(new[] { '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                return string.IsNullOrEmpty(name) ? (includeId ? $"#{Id}" : string.Empty) : name;

            var joined = string.Join(" → ", parts);
            return includeId ? joined + $"  (#{Id})" : joined;
        }

        public static implicit operator ScalarKey(string name) => new ScalarKey(name);
    }


}
