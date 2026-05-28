#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.Validation;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class SceneAssetMigrationValidationService
    {
        public static AuthoringValidationReport Validate(SceneAssetMigrationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            List<AuthoringValidationIssue> issues = new List<AuthoringValidationIssue>();

            IReadOnlyList<SceneAssetMigrationAssetRecord> assetRecords = report.AssetRecords;
            for (int index = 0; index < assetRecords.Count; index++)
                AppendAssetIssues(issues, assetRecords[index]);

            IReadOnlyList<string> unexpectedPrefabPaths = report.UnexpectedPrefabPaths;
            for (int index = 0; index < unexpectedPrefabPaths.Count; index++)
            {
                string prefabPath = unexpectedPrefabPaths[index];
                issues.Add(new AuthoringValidationIssue(
                    SceneAssetMigrationCodes.PrefabBaselineDrift,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LegacyBoundary,
                    default,
                    "Unexpected prefab assets were found while the prefab migration baseline is frozen at zero.",
                    subjectName: prefabPath,
                    suggestedFix: "Re-audit prefab assets and add them to the M11.4 migration contract before any scene rewrite proceeds.",
                    additionalPayloadEntries: new[]
                    {
                        new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(prefabPath)),
                        new DiagnosticPayloadEntry("AssetKind", DiagnosticPayloadValue.FromString(SceneAssetMigrationAssetKind.Prefab.ToString())),
                    }));
            }

            return new AuthoringValidationReport(issues.ToArray());
        }

        static void AppendAssetIssues(List<AuthoringValidationIssue> issues, SceneAssetMigrationAssetRecord record)
        {
            if (record.Target.AssetKind == SceneAssetMigrationAssetKind.Unknown)
            {
                issues.Add(new AuthoringValidationIssue(
                    SceneAssetMigrationCodes.TargetInvalid,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LocalNode,
                    default,
                    "Scene asset migration targets must declare a concrete asset kind.",
                    subjectName: record.Target.AssetPath,
                    additionalPayloadEntries: CreateAssetPayload(record.Target, null)));
            }

            if (!record.HasRoots)
            {
                issues.Add(new AuthoringValidationIssue(
                    SceneAssetMigrationCodes.AssetRootsEmpty,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LocalNode,
                    default,
                    "Scene asset migration targets must resolve at least one root object before rewrite is allowed.",
                    subjectName: record.Target.AssetPath,
                    suggestedFix: "Open the asset through the migration entry point and verify the shipped scene or prefab still resolves to live root objects.",
                    additionalPayloadEntries: CreateAssetPayload(record.Target, null)));
            }

            IReadOnlyList<string> missingRequiredAnchorTypeNames = record.MissingRequiredAnchorTypeNames;
            for (int index = 0; index < missingRequiredAnchorTypeNames.Count; index++)
            {
                string typeName = missingRequiredAnchorTypeNames[index];
                issues.Add(new AuthoringValidationIssue(
                    SceneAssetMigrationCodes.MissingRequiredAnchor,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LocalNode,
                    default,
                    "Required scene migration anchor is missing from the serialized asset.",
                    subjectName: record.Target.AssetPath,
                    suggestedFix: "Add the required SceneKernel host or declaration anchor before removing legacy runtime species.",
                    additionalPayloadEntries: CreateAssetPayload(record.Target, typeName)));
            }

            IReadOnlyList<SceneAssetMigrationAnchorRecord> legacyAnchors = record.LegacyAnchors;
            for (int index = 0; index < legacyAnchors.Count; index++)
            {
                SceneAssetMigrationAnchorRecord anchor = legacyAnchors[index];
                issues.Add(new AuthoringValidationIssue(
                    SceneAssetMigrationCodes.LegacyAnchorPresent,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LegacyBoundary,
                    default,
                    "Legacy runtime authority is still serialized in the target asset.",
                    anchor.SourceLocation,
                    subjectName: anchor.GameObjectPath,
                    suggestedFix: "Migrate serialized fields to the successor authoring surface, then remove the legacy component after reserialize.",
                    additionalPayloadEntries: CreateAssetPayload(record.Target, anchor.TypeName)));
            }
        }

        static DiagnosticPayloadEntry[] CreateAssetPayload(SceneAssetMigrationTarget target, string? typeName)
        {
            List<DiagnosticPayloadEntry> payload = new List<DiagnosticPayloadEntry>(4)
            {
                new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(target.AssetPath)),
                new DiagnosticPayloadEntry("AssetKind", DiagnosticPayloadValue.FromString(target.AssetKind.ToString())),
            };

            if (!string.IsNullOrWhiteSpace(target.AssetGuid))
                payload.Add(new DiagnosticPayloadEntry("AssetGuid", DiagnosticPayloadValue.FromString(target.AssetGuid!)));

            if (!string.IsNullOrWhiteSpace(typeName))
                payload.Add(new DiagnosticPayloadEntry("AnchorType", DiagnosticPayloadValue.FromString(typeName!)));

            return payload.ToArray();
        }
    }
}