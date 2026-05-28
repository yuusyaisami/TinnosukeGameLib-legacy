#nullable enable

using System;
using System.Collections.Generic;
using Game.Commands;
using Game.Common;
using Game.Kernel.Diagnostics;
using Game.Kernel.Validation;
using Game.Project.Scene.Runtime;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class M12_1AuditCodes
    {
        public const string AssetBridgeLingering = "M12_1_ASSET_BRIDGE_LINGERING";
    }

    public static class M12_1SceneAssetBridgeLingeringService
    {
        static readonly string[] BridgeAnchorTypeNames =
        {
            typeof(CommandRunnerMB).FullName!,
            typeof(BlackboardMB).FullName!,
            typeof(RuntimeManagerMB).FullName!,
            typeof(RuntimeLifetimeScope).FullName!,
        };

        public static IReadOnlyList<SceneAssetMigrationTarget> CreateAuditTargets()
        {
            IReadOnlyList<SceneAssetMigrationTarget> targets = SceneAssetMigrationReportService.CreateDefaultTargets();
            if (targets.Count == 0)
                return Array.Empty<SceneAssetMigrationTarget>();

            SceneAssetMigrationTarget[] snapshot = new SceneAssetMigrationTarget[targets.Count];
            for (int index = 0; index < targets.Count; index++)
            {
                SceneAssetMigrationTarget target = targets[index];
                snapshot[index] = new SceneAssetMigrationTarget(
                    target.AssetKind,
                    target.AssetPath,
                    target.AssetGuid,
                    target.RequiredAnchorTypeNames,
                    MergeLegacyAnchorTypeNames(target.LegacyAnchorTypeNames));
            }

            return snapshot;
        }

        public static SceneAssetMigrationReport BuildWorkspaceReport()
        {
            return SceneAssetMigrationReportService.BuildReport(CreateAuditTargets());
        }

        public static AuthoringValidationReport Validate(SceneAssetMigrationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            List<AuthoringValidationIssue> issues = new List<AuthoringValidationIssue>();
            IReadOnlyList<SceneAssetMigrationAssetRecord> assetRecords = report.AssetRecords;
            for (int assetIndex = 0; assetIndex < assetRecords.Count; assetIndex++)
            {
                SceneAssetMigrationAssetRecord record = assetRecords[assetIndex];
                IReadOnlyList<SceneAssetMigrationAnchorRecord> legacyAnchors = record.LegacyAnchors;
                for (int anchorIndex = 0; anchorIndex < legacyAnchors.Count; anchorIndex++)
                {
                    SceneAssetMigrationAnchorRecord anchor = legacyAnchors[anchorIndex];
                    if (!IsBridgeAnchorType(anchor.TypeName))
                        continue;

                    issues.Add(new AuthoringValidationIssue(
                        M12_1AuditCodes.AssetBridgeLingering,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LegacyBoundary,
                        default,
                        "M12.1 bridge or residual runtime authority is still serialized in the shipped asset.",
                        anchor.SourceLocation,
                        subjectName: anchor.GameObjectPath,
                        suggestedFix: "Remove the serialized bridge or demote it to diagnostics-only quarantine before M12.2 purge.",
                        additionalPayloadEntries: CreateAssetPayload(record.Target, anchor.TypeName)));
                }
            }

            return new AuthoringValidationReport(issues.ToArray());
        }

        static string[] MergeLegacyAnchorTypeNames(IReadOnlyList<string> existing)
        {
            HashSet<string> merged = new HashSet<string>(StringComparer.Ordinal);
            List<string> ordered = new List<string>(existing.Count + BridgeAnchorTypeNames.Length);

            for (int index = 0; index < existing.Count; index++)
            {
                string typeName = existing[index];
                if (merged.Add(typeName))
                    ordered.Add(typeName);
            }

            for (int index = 0; index < BridgeAnchorTypeNames.Length; index++)
            {
                string typeName = BridgeAnchorTypeNames[index];
                if (merged.Add(typeName))
                    ordered.Add(typeName);
            }

            return ordered.ToArray();
        }

        static bool IsBridgeAnchorType(string typeName)
        {
            for (int index = 0; index < BridgeAnchorTypeNames.Length; index++)
            {
                if (StringComparer.Ordinal.Equals(typeName, BridgeAnchorTypeNames[index]))
                    return true;
            }

            return false;
        }

        static DiagnosticPayloadEntry[] CreateAssetPayload(SceneAssetMigrationTarget target, string typeName)
        {
            List<DiagnosticPayloadEntry> payload = new List<DiagnosticPayloadEntry>(4)
            {
                new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(target.AssetPath)),
                new DiagnosticPayloadEntry("AssetKind", DiagnosticPayloadValue.FromString(target.AssetKind.ToString())),
                new DiagnosticPayloadEntry("AnchorType", DiagnosticPayloadValue.FromString(typeName)),
            };

            if (!string.IsNullOrWhiteSpace(target.AssetGuid))
                payload.Add(new DiagnosticPayloadEntry("AssetGuid", DiagnosticPayloadValue.FromString(target.AssetGuid!)));

            return payload.ToArray();
        }
    }
}