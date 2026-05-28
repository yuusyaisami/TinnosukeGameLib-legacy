#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class SceneAssetMigrationCodes
    {
        public const string TargetInvalid = "UNITY_ASSET_MIGRATION_TARGET_INVALID";
        public const string AssetRootsEmpty = "UNITY_ASSET_MIGRATION_ASSET_ROOTS_EMPTY";
        public const string MissingRequiredAnchor = "UNITY_ASSET_MIGRATION_REQUIRED_ANCHOR_MISSING";
        public const string LegacyAnchorPresent = "UNITY_ASSET_MIGRATION_LEGACY_ANCHOR_PRESENT";
        public const string PrefabBaselineDrift = "UNITY_ASSET_MIGRATION_PREFAB_BASELINE_DRIFT";
    }

    public enum SceneAssetMigrationAssetKind
    {
        Unknown = 0,
        Scene = 10,
        Prefab = 20,
    }

    public sealed class SceneAssetMigrationTarget
    {
        readonly string[] requiredAnchorTypeNames;
        readonly string[] legacyAnchorTypeNames;

        public SceneAssetMigrationTarget(
            SceneAssetMigrationAssetKind assetKind,
            string assetPath,
            string? assetGuid,
            IReadOnlyList<string>? requiredAnchorTypeNames = null,
            IReadOnlyList<string>? legacyAnchorTypeNames = null)
        {
            if (assetKind == SceneAssetMigrationAssetKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Scene asset migration targets must declare a concrete asset kind.");

            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Scene asset migration targets must provide an asset path.", nameof(assetPath));

            AssetKind = assetKind;
            AssetPath = assetPath.Trim();
            AssetGuid = NormalizeOptional(assetGuid);
            this.requiredAnchorTypeNames = CloneTypeNames(requiredAnchorTypeNames, nameof(requiredAnchorTypeNames));
            this.legacyAnchorTypeNames = CloneTypeNames(legacyAnchorTypeNames, nameof(legacyAnchorTypeNames));
        }

        public SceneAssetMigrationAssetKind AssetKind { get; }

        public string AssetPath { get; }

        public string? AssetGuid { get; }

        public IReadOnlyList<string> RequiredAnchorTypeNames => requiredAnchorTypeNames;

        public IReadOnlyList<string> LegacyAnchorTypeNames => legacyAnchorTypeNames;

        static string[] CloneTypeNames(IReadOnlyList<string>? typeNames, string paramName)
        {
            if (typeNames == null || typeNames.Count == 0)
                return Array.Empty<string>();

            List<string> normalized = new List<string>(typeNames.Count);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < typeNames.Count; index++)
            {
                string? candidate = typeNames[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    throw new ArgumentException("Scene asset migration targets must not contain null or empty type names.", paramName);

                string trimmed = candidate.Trim();
                if (seen.Add(trimmed))
                    normalized.Add(trimmed);
            }

            return normalized.ToArray();
        }

        static string? NormalizeOptional(string? value)
        {
            if (value == null)
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }

    public sealed class SceneAssetMigrationAnchorRecord : IEquatable<SceneAssetMigrationAnchorRecord>
    {
        public SceneAssetMigrationAnchorRecord(string typeName, string gameObjectPath, UnitySourceLocation sourceLocation)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Scene asset migration anchor records must provide a type name.", nameof(typeName));

            if (string.IsNullOrWhiteSpace(gameObjectPath))
                throw new ArgumentException("Scene asset migration anchor records must provide a game object path.", nameof(gameObjectPath));

            TypeName = typeName.Trim();
            GameObjectPath = gameObjectPath.Trim();
            SourceLocation = sourceLocation;
        }

        public string TypeName { get; }

        public string GameObjectPath { get; }

        public UnitySourceLocation SourceLocation { get; }

        public bool Equals(SceneAssetMigrationAnchorRecord? other)
        {
            return other != null
                && StringComparer.Ordinal.Equals(TypeName, other.TypeName)
                && StringComparer.Ordinal.Equals(GameObjectPath, other.GameObjectPath)
                && SourceLocation.Equals(other.SourceLocation);
        }

        public override bool Equals(object? obj)
        {
            return obj is SceneAssetMigrationAnchorRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(TypeName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GameObjectPath);
                hash = (hash * 397) ^ SourceLocation.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class SceneAssetMigrationAssetRecord
    {
        readonly SceneAssetMigrationAnchorRecord[] requiredAnchors;
        readonly SceneAssetMigrationAnchorRecord[] legacyAnchors;
        readonly string[] missingRequiredAnchorTypeNames;

        public SceneAssetMigrationAssetRecord(
            SceneAssetMigrationTarget target,
            IReadOnlyList<SceneAssetMigrationAnchorRecord>? requiredAnchors,
            IReadOnlyList<SceneAssetMigrationAnchorRecord>? legacyAnchors,
            IReadOnlyList<string>? missingRequiredAnchorTypeNames,
            bool hasRoots)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            this.requiredAnchors = CloneAndSortAnchors(requiredAnchors, nameof(requiredAnchors));
            this.legacyAnchors = CloneAndSortAnchors(legacyAnchors, nameof(legacyAnchors));
            this.missingRequiredAnchorTypeNames = CloneAndSortTypeNames(missingRequiredAnchorTypeNames, nameof(missingRequiredAnchorTypeNames));
            HasRoots = hasRoots;
        }

        public SceneAssetMigrationTarget Target { get; }

        public bool HasRoots { get; }

        public IReadOnlyList<SceneAssetMigrationAnchorRecord> RequiredAnchors => requiredAnchors;

        public IReadOnlyList<SceneAssetMigrationAnchorRecord> LegacyAnchors => legacyAnchors;

        public IReadOnlyList<string> MissingRequiredAnchorTypeNames => missingRequiredAnchorTypeNames;

        public int UnresolvedItemCount => (HasRoots ? 0 : 1) + missingRequiredAnchorTypeNames.Length + legacyAnchors.Length;

        public bool IsValid => HasRoots && missingRequiredAnchorTypeNames.Length == 0 && legacyAnchors.Length == 0;

        static SceneAssetMigrationAnchorRecord[] CloneAndSortAnchors(IReadOnlyList<SceneAssetMigrationAnchorRecord>? anchors, string paramName)
        {
            if (anchors == null || anchors.Count == 0)
                return Array.Empty<SceneAssetMigrationAnchorRecord>();

            SceneAssetMigrationAnchorRecord[] snapshot = new SceneAssetMigrationAnchorRecord[anchors.Count];
            for (int index = 0; index < anchors.Count; index++)
                snapshot[index] = anchors[index] ?? throw new ArgumentException("Scene asset migration records must not contain null anchors.", paramName);

            Array.Sort(snapshot, CompareAnchors);
            return snapshot;
        }

        static string[] CloneAndSortTypeNames(IReadOnlyList<string>? typeNames, string paramName)
        {
            if (typeNames == null || typeNames.Count == 0)
                return Array.Empty<string>();

            string[] snapshot = new string[typeNames.Count];
            for (int index = 0; index < typeNames.Count; index++)
            {
                string? candidate = typeNames[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    throw new ArgumentException("Scene asset migration records must not contain null or empty type names.", paramName);

                snapshot[index] = candidate.Trim();
            }

            Array.Sort(snapshot, StringComparer.Ordinal);
            return snapshot;
        }

        static int CompareAnchors(SceneAssetMigrationAnchorRecord left, SceneAssetMigrationAnchorRecord right)
        {
            int result = StringComparer.Ordinal.Compare(left.TypeName, right.TypeName);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.GameObjectPath, right.GameObjectPath);
            if (result != 0)
                return result;

            return left.SourceLocation.LocalFileId.CompareTo(right.SourceLocation.LocalFileId);
        }
    }

    public sealed class SceneAssetMigrationReport
    {
        readonly SceneAssetMigrationAssetRecord[] assetRecords;
        readonly string[] unexpectedPrefabPaths;

        public SceneAssetMigrationReport(IReadOnlyList<SceneAssetMigrationAssetRecord>? assetRecords, IReadOnlyList<string>? unexpectedPrefabPaths = null)
        {
            this.assetRecords = CloneAndSortRecords(assetRecords);
            this.unexpectedPrefabPaths = CloneAndSortPaths(unexpectedPrefabPaths, nameof(unexpectedPrefabPaths));

            int unresolvedCount = this.unexpectedPrefabPaths.Length;
            for (int index = 0; index < this.assetRecords.Length; index++)
                unresolvedCount += this.assetRecords[index].UnresolvedItemCount;

            UnresolvedItemCount = unresolvedCount;
        }

        public IReadOnlyList<SceneAssetMigrationAssetRecord> AssetRecords => assetRecords;

        public IReadOnlyList<string> UnexpectedPrefabPaths => unexpectedPrefabPaths;

        public int UnresolvedItemCount { get; }

        public bool IsValid => UnresolvedItemCount == 0;

        static SceneAssetMigrationAssetRecord[] CloneAndSortRecords(IReadOnlyList<SceneAssetMigrationAssetRecord>? records)
        {
            if (records == null || records.Count == 0)
                return Array.Empty<SceneAssetMigrationAssetRecord>();

            SceneAssetMigrationAssetRecord[] snapshot = new SceneAssetMigrationAssetRecord[records.Count];
            for (int index = 0; index < records.Count; index++)
                snapshot[index] = records[index] ?? throw new ArgumentException("Scene asset migration reports must not contain null asset records.", nameof(records));

            Array.Sort(snapshot, CompareRecords);
            return snapshot;
        }

        static string[] CloneAndSortPaths(IReadOnlyList<string>? paths, string paramName)
        {
            if (paths == null || paths.Count == 0)
                return Array.Empty<string>();

            string[] snapshot = new string[paths.Count];
            for (int index = 0; index < paths.Count; index++)
            {
                string? candidate = paths[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    throw new ArgumentException("Scene asset migration reports must not contain null or empty paths.", paramName);

                snapshot[index] = candidate.Trim();
            }

            Array.Sort(snapshot, StringComparer.Ordinal);
            return snapshot;
        }

        static int CompareRecords(SceneAssetMigrationAssetRecord left, SceneAssetMigrationAssetRecord right)
        {
            int result = left.Target.AssetKind.CompareTo(right.Target.AssetKind);
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(left.Target.AssetPath, right.Target.AssetPath);
        }
    }
}