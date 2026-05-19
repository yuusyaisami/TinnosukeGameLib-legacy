#nullable enable
using System;

namespace Game.Kernel.Abstractions
{
    public readonly struct ManifestId : IEquatable<ManifestId>
    {
        public ManifestId(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "ManifestId must be positive.");

            Value = value;
        }

        public int Value { get; }

        public bool Equals(ManifestId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ManifestId(" + Value + ")";
        }

        public static bool operator ==(ManifestId left, ManifestId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ManifestId left, ManifestId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct KernelProfileId : IEquatable<KernelProfileId>
    {
        public KernelProfileId(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "KernelProfileId must be positive.");

            Value = value;
        }

        public int Value { get; }

        public bool Equals(KernelProfileId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelProfileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "KernelProfileId(" + Value + ")";
        }

        public static bool operator ==(KernelProfileId left, KernelProfileId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelProfileId left, KernelProfileId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct BootPolicyId : IEquatable<BootPolicyId>
    {
        public BootPolicyId(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "BootPolicyId must be positive.");

            Value = value;
        }

        public int Value { get; }

        public bool Equals(BootPolicyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is BootPolicyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "BootPolicyId(" + Value + ")";
        }

        public static bool operator ==(BootPolicyId left, BootPolicyId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BootPolicyId left, BootPolicyId right)
        {
            return !left.Equals(right);
        }
    }
}