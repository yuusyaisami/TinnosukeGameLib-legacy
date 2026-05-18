#nullable enable
using System;

namespace Game.Kernel.IR
{
    public enum SourceLocationKind
    {
        Unknown = 0,
        Unity = 10,
        Legacy = 20,
        Generated = 30,
    }

    public readonly struct SourceLocationIR : IEquatable<SourceLocationIR>
    {
        public SourceLocationIR(SourceLocationKind kind, UnitySourceLocation? unitySource, LegacySourceLocation? legacySource, GeneratedSourceLocation? generatedSource)
        {
            ValidateVariantCombination(kind, unitySource, legacySource, generatedSource);
            Kind = kind;
            UnitySource = unitySource;
            LegacySource = legacySource;
            GeneratedSource = generatedSource;
        }

        public SourceLocationIR(UnitySourceLocation unitySource)
            : this(SourceLocationKind.Unity, unitySource, null, null)
        {
        }

        public SourceLocationIR(LegacySourceLocation legacySource)
            : this(SourceLocationKind.Legacy, null, legacySource, null)
        {
        }

        public SourceLocationIR(GeneratedSourceLocation generatedSource)
            : this(SourceLocationKind.Generated, null, null, generatedSource)
        {
        }

        public SourceLocationKind Kind { get; }

        public UnitySourceLocation? UnitySource { get; }

        public LegacySourceLocation? LegacySource { get; }

        public GeneratedSourceLocation? GeneratedSource { get; }

        public bool IsSpecified => Kind != SourceLocationKind.Unknown
            && (UnitySource.HasValue ? 1 : 0)
            + (LegacySource.HasValue ? 1 : 0)
            + (GeneratedSource.HasValue ? 1 : 0) == 1;

        public bool Equals(SourceLocationIR other)
        {
            return Kind == other.Kind
                && Nullable.Equals(UnitySource, other.UnitySource)
                && Nullable.Equals(LegacySource, other.LegacySource)
                && Nullable.Equals(GeneratedSource, other.GeneratedSource);
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceLocationIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (UnitySource.HasValue ? UnitySource.Value.GetHashCode() : 0);
                hash = (hash * 397) ^ (LegacySource.HasValue ? LegacySource.Value.GetHashCode() : 0);
                hash = (hash * 397) ^ (GeneratedSource.HasValue ? GeneratedSource.Value.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case SourceLocationKind.Unity:
                    return "SourceLocationIR(Unity, " + (UnitySource.HasValue ? UnitySource.Value.ToString() : "<missing>") + ")";
                case SourceLocationKind.Legacy:
                    return "SourceLocationIR(Legacy, " + (LegacySource.HasValue ? LegacySource.Value.ToString() : "<missing>") + ")";
                case SourceLocationKind.Generated:
                    return "SourceLocationIR(Generated, " + (GeneratedSource.HasValue ? GeneratedSource.Value.ToString() : "<missing>") + ")";
                default:
                    return "SourceLocationIR(<invalid>)";
            }
        }

        public static bool operator ==(SourceLocationIR left, SourceLocationIR right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SourceLocationIR left, SourceLocationIR right)
        {
            return !left.Equals(right);
        }

        static void ValidateVariantCombination(SourceLocationKind kind, UnitySourceLocation? unitySource, LegacySourceLocation? legacySource, GeneratedSourceLocation? generatedSource)
        {
            bool hasUnitySource = unitySource.HasValue;
            bool hasLegacySource = legacySource.HasValue;
            bool hasGeneratedSource = generatedSource.HasValue;
            int variantCount = 0;
            if (hasUnitySource)
                variantCount++;

            if (hasLegacySource)
                variantCount++;

            if (hasGeneratedSource)
                variantCount++;

            if (kind == SourceLocationKind.Unknown)
                throw new ArgumentException("Unknown source locations are reserved for invalid/default state and cannot be constructed.", nameof(kind));

            if (variantCount != 1)
                throw new ArgumentException("Source locations must carry exactly one variant payload.", nameof(kind));

            switch (kind)
            {
                case SourceLocationKind.Unity:
                    if (!hasUnitySource)
                        throw new ArgumentException("Unity source locations must provide a UnitySourceLocation payload.", nameof(kind));

                    unitySource!.Value.ValidateRequiredProvenance();

                    break;
                case SourceLocationKind.Legacy:
                    if (!hasLegacySource)
                        throw new ArgumentException("Legacy source locations must provide a LegacySourceLocation payload.", nameof(kind));

                    legacySource!.Value.ValidateRequiredProvenance();

                    break;
                case SourceLocationKind.Generated:
                    if (!hasGeneratedSource)
                        throw new ArgumentException("Generated source locations must provide a GeneratedSourceLocation payload.", nameof(kind));

                    generatedSource!.Value.ValidateRequiredProvenance();

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported source location kind.");
            }
        }
    }

    public readonly struct UnitySourceLocation : IEquatable<UnitySourceLocation>
    {
        public UnitySourceLocation(string? assetGuid, string? assetPath, long localFileId, string? scenePath, string? gameObjectPath, string? componentType, string? propertyPath)
        {
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            LocalFileId = localFileId;
            ScenePath = scenePath;
            GameObjectPath = gameObjectPath;
            ComponentType = componentType;
            PropertyPath = propertyPath;
        }

        public string? AssetGuid { get; }

        public string? AssetPath { get; }

        public long LocalFileId { get; }

        public string? ScenePath { get; }

        public string? GameObjectPath { get; }

        public string? ComponentType { get; }

        public string? PropertyPath { get; }

        public bool Equals(UnitySourceLocation other)
        {
            return StringComparer.Ordinal.Equals(AssetGuid, other.AssetGuid)
                && StringComparer.Ordinal.Equals(AssetPath, other.AssetPath)
                && LocalFileId == other.LocalFileId
                && StringComparer.Ordinal.Equals(ScenePath, other.ScenePath)
                && StringComparer.Ordinal.Equals(GameObjectPath, other.GameObjectPath)
                && StringComparer.Ordinal.Equals(ComponentType, other.ComponentType)
                && StringComparer.Ordinal.Equals(PropertyPath, other.PropertyPath);
        }

        public override bool Equals(object? obj)
        {
            return obj is UnitySourceLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = GetNullableStringHashCode(AssetGuid);
                hash = (hash * 397) ^ GetNullableStringHashCode(AssetPath);
                hash = (hash * 397) ^ LocalFileId.GetHashCode();
                hash = (hash * 397) ^ GetNullableStringHashCode(ScenePath);
                hash = (hash * 397) ^ GetNullableStringHashCode(GameObjectPath);
                hash = (hash * 397) ^ GetNullableStringHashCode(ComponentType);
                hash = (hash * 397) ^ GetNullableStringHashCode(PropertyPath);
                return hash;
            }
        }

        public override string ToString()
        {
            return "UnitySourceLocation(AssetGuid=" + FormatValue(AssetGuid) + ", AssetPath=" + FormatValue(AssetPath) + ", LocalFileId=" + LocalFileId + ", ScenePath=" + FormatValue(ScenePath) + ", GameObjectPath=" + FormatValue(GameObjectPath) + ", ComponentType=" + FormatValue(ComponentType) + ", PropertyPath=" + FormatValue(PropertyPath) + ")";
        }

        public static bool operator ==(UnitySourceLocation left, UnitySourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnitySourceLocation left, UnitySourceLocation right)
        {
            return !left.Equals(right);
        }

        internal void ValidateRequiredProvenance()
        {
            RequireNonEmpty(AssetGuid, nameof(AssetGuid));
            RequireNonEmpty(AssetPath, nameof(AssetPath));

            if (LocalFileId == 0)
                throw new ArgumentException("Unity source locations must provide a non-zero LocalFileId.", nameof(LocalFileId));

            RequireNonEmpty(GameObjectPath, nameof(GameObjectPath));
            RequireNonEmpty(ComponentType, nameof(ComponentType));
            RequireNonEmpty(PropertyPath, nameof(PropertyPath));
        }

        static int GetNullableStringHashCode(string? value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        static string FormatValue(string? value)
        {
            return value ?? "<missing>";
        }

        static void RequireNonEmpty(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Unity source locations must provide " + fieldName + ".", fieldName);
        }
    }

    public readonly struct LegacySourceLocation : IEquatable<LegacySourceLocation>
    {
        public LegacySourceLocation(string? legacySystemName, string? legacyOrigin, string? migrationAdapter)
        {
            LegacySystemName = legacySystemName;
            LegacyOrigin = legacyOrigin;
            MigrationAdapter = migrationAdapter;
        }

        public string? LegacySystemName { get; }

        public string? LegacyOrigin { get; }

        public string? MigrationAdapter { get; }

        public bool Equals(LegacySourceLocation other)
        {
            return StringComparer.Ordinal.Equals(LegacySystemName, other.LegacySystemName)
                && StringComparer.Ordinal.Equals(LegacyOrigin, other.LegacyOrigin)
                && StringComparer.Ordinal.Equals(MigrationAdapter, other.MigrationAdapter);
        }

        public override bool Equals(object? obj)
        {
            return obj is LegacySourceLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = GetNullableStringHashCode(LegacySystemName);
                hash = (hash * 397) ^ GetNullableStringHashCode(LegacyOrigin);
                hash = (hash * 397) ^ GetNullableStringHashCode(MigrationAdapter);
                return hash;
            }
        }

        public override string ToString()
        {
            return "LegacySourceLocation(LegacySystemName=" + FormatValue(LegacySystemName) + ", LegacyOrigin=" + FormatValue(LegacyOrigin) + ", MigrationAdapter=" + FormatValue(MigrationAdapter) + ")";
        }

        public static bool operator ==(LegacySourceLocation left, LegacySourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LegacySourceLocation left, LegacySourceLocation right)
        {
            return !left.Equals(right);
        }

        internal void ValidateRequiredProvenance()
        {
            RequireNonEmpty(LegacySystemName, nameof(LegacySystemName));
            RequireNonEmpty(LegacyOrigin, nameof(LegacyOrigin));
        }

        static int GetNullableStringHashCode(string? value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        static string FormatValue(string? value)
        {
            return value ?? "<missing>";
        }

        static void RequireNonEmpty(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Legacy source locations must provide " + fieldName + ".", fieldName);
        }
    }

    public readonly struct GeneratedSourceLocation : IEquatable<GeneratedSourceLocation>
    {
        public GeneratedSourceLocation(string? generatorName, string? generatedFrom, string? generationPhase)
        {
            GeneratorName = generatorName;
            GeneratedFrom = generatedFrom;
            GenerationPhase = generationPhase;
        }

        public string? GeneratorName { get; }

        public string? GeneratedFrom { get; }

        public string? GenerationPhase { get; }

        public bool Equals(GeneratedSourceLocation other)
        {
            return StringComparer.Ordinal.Equals(GeneratorName, other.GeneratorName)
                && StringComparer.Ordinal.Equals(GeneratedFrom, other.GeneratedFrom)
                && StringComparer.Ordinal.Equals(GenerationPhase, other.GenerationPhase);
        }

        public override bool Equals(object? obj)
        {
            return obj is GeneratedSourceLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = GetNullableStringHashCode(GeneratorName);
                hash = (hash * 397) ^ GetNullableStringHashCode(GeneratedFrom);
                hash = (hash * 397) ^ GetNullableStringHashCode(GenerationPhase);
                return hash;
            }
        }

        public override string ToString()
        {
            return "GeneratedSourceLocation(GeneratorName=" + FormatValue(GeneratorName) + ", GeneratedFrom=" + FormatValue(GeneratedFrom) + ", GenerationPhase=" + FormatValue(GenerationPhase) + ")";
        }

        public static bool operator ==(GeneratedSourceLocation left, GeneratedSourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeneratedSourceLocation left, GeneratedSourceLocation right)
        {
            return !left.Equals(right);
        }

        internal void ValidateRequiredProvenance()
        {
            RequireNonEmpty(GeneratorName, nameof(GeneratorName));
            RequireNonEmpty(GeneratedFrom, nameof(GeneratedFrom));
        }

        static int GetNullableStringHashCode(string? value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        static string FormatValue(string? value)
        {
            return value ?? "<missing>";
        }

        static void RequireNonEmpty(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Generated source locations must provide " + fieldName + ".", fieldName);
        }
    }
}