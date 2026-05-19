#nullable enable
using System;

namespace Game.Kernel.Contributions
{
    public enum ContributionEnvironment
    {
        All = 0,
        Editor = 10,
        Test = 20,
        Release = 30,
    }

    public readonly struct ContributionAvailability : IEquatable<ContributionAvailability>
    {
        public ContributionAvailability(string? profileId, string? buildTarget, string? platformFamily, ContributionEnvironment environment = ContributionEnvironment.All)
        {
            ValidateOptionalValue(profileId, nameof(profileId));
            ValidateOptionalValue(buildTarget, nameof(buildTarget));
            ValidateOptionalValue(platformFamily, nameof(platformFamily));

            ProfileId = profileId;
            BuildTarget = buildTarget;
            PlatformFamily = platformFamily;
            Environment = environment;
        }

        public string? ProfileId { get; }

        public string? BuildTarget { get; }

        public string? PlatformFamily { get; }

        public ContributionEnvironment Environment { get; }

        public bool Equals(ContributionAvailability other)
        {
            return StringComparer.Ordinal.Equals(ProfileId, other.ProfileId)
                && StringComparer.Ordinal.Equals(BuildTarget, other.BuildTarget)
                && StringComparer.Ordinal.Equals(PlatformFamily, other.PlatformFamily)
                && Environment == other.Environment;
        }

        public override bool Equals(object? obj)
        {
            return obj is ContributionAvailability other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = GetNullableStringHashCode(ProfileId);
                hash = (hash * 397) ^ GetNullableStringHashCode(BuildTarget);
                hash = (hash * 397) ^ GetNullableStringHashCode(PlatformFamily);
                hash = (hash * 397) ^ (int)Environment;
                return hash;
            }
        }

        public override string ToString()
        {
            return "ContributionAvailability(ProfileId=" + FormatValue(ProfileId) + ", BuildTarget=" + FormatValue(BuildTarget) + ", PlatformFamily=" + FormatValue(PlatformFamily) + ", Environment=" + Environment + ")";
        }

        public static bool operator ==(ContributionAvailability left, ContributionAvailability right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContributionAvailability left, ContributionAvailability right)
        {
            return !left.Equals(right);
        }

        static void ValidateOptionalValue(string? value, string parameterName)
        {
            if (value != null && value.Trim().Length == 0)
                throw new ArgumentException("Availability values must be null or non-empty.", parameterName);
        }

        static int GetNullableStringHashCode(string? value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        static string FormatValue(string? value)
        {
            return value ?? "<all>";
        }
    }
}