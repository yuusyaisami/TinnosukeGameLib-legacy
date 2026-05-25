#nullable enable

using System;

namespace Game.Kernel.Abstractions
{
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        readonly string? value;

        public EntityRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Entity refs must be non-empty.", nameof(value));

            this.value = value.Trim();
        }

        public string Value => value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(value);

        public bool Equals(EntityRef other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is EntityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool TryParse(string? value, out EntityRef entityRef)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                entityRef = new EntityRef(value);
                return true;
            }

            entityRef = default;
            return false;
        }

        public static bool operator ==(EntityRef left, EntityRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EntityRef left, EntityRef right)
        {
            return !left.Equals(right);
        }
    }
}
