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
