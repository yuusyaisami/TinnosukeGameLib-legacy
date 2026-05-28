#nullable enable
using System;
using Game.Kernel.IR;

namespace Game.Kernel.Value
{
    public readonly struct ValueVariant : IEquatable<ValueVariant>
    {
        readonly long integerValue;
        readonly double floatingValue;
        readonly string? textValue;

        ValueVariant(ValueKind kind, long integerValue, double floatingValue, string? textValue)
        {
            Kind = kind;
            this.integerValue = integerValue;
            this.floatingValue = floatingValue;
            this.textValue = textValue;
        }

        public ValueKind Kind { get; }

        public bool HasValue => Kind != ValueKind.Null;

        public static ValueVariant Null => default;

        public static ValueVariant FromBool(bool value)
        {
            return new ValueVariant(ValueKind.Bool, value ? 1L : 0L, 0d, null);
        }

        public static ValueVariant FromInt(int value)
        {
            return new ValueVariant(ValueKind.Int, value, 0d, null);
        }

        public static ValueVariant FromLong(long value)
        {
            return new ValueVariant(ValueKind.Long, value, 0d, null);
        }

        public static ValueVariant FromFloat(float value)
        {
            return new ValueVariant(ValueKind.Float, 0L, value, null);
        }

        public static ValueVariant FromDouble(double value)
        {
            return new ValueVariant(ValueKind.Double, 0L, value, null);
        }

        public static ValueVariant FromString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ValueVariant(ValueKind.String, 0L, 0d, value);
        }

        public bool TryGetBool(out bool value)
        {
            if (Kind == ValueKind.Bool)
            {
                value = integerValue != 0L;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetInt(out int value)
        {
            if (Kind == ValueKind.Int)
            {
                value = (int)integerValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetLong(out long value)
        {
            if (Kind == ValueKind.Long)
            {
                value = integerValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetFloat(out float value)
        {
            if (Kind == ValueKind.Float)
            {
                value = (float)floatingValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetDouble(out double value)
        {
            if (Kind == ValueKind.Double)
            {
                value = floatingValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetString(out string? value)
        {
            if (Kind == ValueKind.String)
            {
                value = textValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool Equals(ValueVariant other)
        {
            if (Kind != other.Kind)
                return false;

            switch (Kind)
            {
                case ValueKind.Null:
                    return true;
                case ValueKind.Bool:
                case ValueKind.Int:
                case ValueKind.Long:
                    return integerValue == other.integerValue;
                case ValueKind.Float:
                case ValueKind.Double:
                    return floatingValue.Equals(other.floatingValue);
                case ValueKind.String:
                    return string.Equals(textValue, other.textValue, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueVariant other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ integerValue.GetHashCode();
                hash = (hash * 397) ^ floatingValue.GetHashCode();
                hash = (hash * 397) ^ (textValue != null ? StringComparer.Ordinal.GetHashCode(textValue) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case ValueKind.Null:
                    return "ValueVariant(Null)";
                case ValueKind.Bool:
                    return "ValueVariant(Bool, " + (integerValue != 0L) + ")";
                case ValueKind.Int:
                    return "ValueVariant(Int, " + (int)integerValue + ")";
                case ValueKind.Long:
                    return "ValueVariant(Long, " + integerValue + ")";
                case ValueKind.Float:
                    return "ValueVariant(Float, " + (float)floatingValue + ")";
                case ValueKind.Double:
                    return "ValueVariant(Double, " + floatingValue + ")";
                case ValueKind.String:
                    return "ValueVariant(String, " + textValue + ")";
                default:
                    return "ValueVariant(" + Kind + ")";
            }
        }

        public static bool operator ==(ValueVariant left, ValueVariant right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueVariant left, ValueVariant right)
        {
            return !left.Equals(right);
        }
    }
}