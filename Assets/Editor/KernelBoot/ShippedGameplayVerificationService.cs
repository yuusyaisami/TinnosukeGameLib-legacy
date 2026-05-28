#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.Kernel.Authoring;
using Game.Kernel.Diagnostics;
using Game.Kernel.Validation;
using UnityEditor;
using UnityEngine;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class ShippedGameplayVerificationService
    {
        const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        const string GameScenePath = "Assets/Scenes/GameScene.unity";
        const string ServiceInventoryPath = "Assets/Docs/v2.1/Index/ServiceCutoverInventory.md";
        const string CommandInventoryPath = "Assets/Docs/v2.1/Index/CommandCutoverInventory.md";
        const string ValueInventoryPath = "Assets/Docs/v2.1/Index/ValueScalarQueryInventory.md";
        const string DynamicInventoryPath = "Assets/Docs/v2.1/Index/DynamicSourceCutoverInventory.md";

        static readonly string[] RequiredSceneTargets =
        {
            TitleScenePath,
            GameScenePath,
        };

        static readonly Regex SummaryLineRegex = new Regex(
            @"^\s*-\s*`?(?<label>[^`:\r\n]+)`?\s*:\s*(?<count>\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        [MenuItem("Tools/Kernel/M11.5/Print Shipped Gameplay Verification Report")]
        static void PrintWorkspaceVerificationReportMenu()
        {
            ShippedGameplayVerificationReport report = BuildWorkspaceReport();
            AuthoringValidationReport validation = Validate(report);

            Debug.Log($"[M11.5] executable={report.IsExecutable}, verified={report.IsVerified}, unresolved={report.UnresolvedItemCount}, entryGateClosed={report.EntryGate.IsClosed}, targets={report.Targets.Count}, directPlayProofs={report.DirectPlayProofs.Count}, unexpectedPrefabs={report.UnexpectedPrefabPaths.Count}");
            for (int index = 0; index < report.Targets.Count; index++)
            {
                ShippedGameplayProofTargetRecord target = report.Targets[index];
                Debug.Log($"[M11.5] target={target.AssetPath}, status={target.Status}, unresolved={target.UnresolvedItemCount}, missingRequired={target.MissingRequiredAnchorCount}, legacy={target.LegacyAnchorCount}, hasRoots={target.HasRoots}");
            }

            for (int index = 0; index < report.DirectPlayProofs.Count; index++)
            {
                ShippedGameplayDirectPlayProofRecord proof = report.DirectPlayProofs[index];
                Debug.Log($"[M11.5] directPlay={proof.AssetPath}, status={proof.Status}, failedStage={proof.FailedStage}, diagnostics={proof.DiagnosticCount}, warnings={proof.WarningCount}, errors={proof.ErrorCount}, fatals={proof.FatalCount}, truncated={proof.WasTruncated}");
            }

            for (int index = 0; index < validation.Issues.Count; index++)
                Debug.LogError("[M11.5] " + validation.Issues[index]);
        }

        public static ShippedGameplayVerificationReport BuildWorkspaceReport()
        {
            SceneAssetMigrationReport migrationReport = SceneAssetMigrationReportService.BuildWorkspaceBaselineReport();
            ShippedGameplayInventoryGateSnapshot gate = ReadInventoryGateSnapshot();
            return BuildReport(migrationReport, gate);
        }

        public static ShippedGameplayVerificationReport BuildReport(SceneAssetMigrationReport migrationReport, ShippedGameplayInventoryGateSnapshot entryGate)
        {
            return BuildReport(migrationReport, entryGate, null);
        }

        public static ShippedGameplayVerificationReport BuildReport(
            SceneAssetMigrationReport migrationReport,
            ShippedGameplayInventoryGateSnapshot entryGate,
            IReadOnlyList<ShippedGameplayDirectPlayProofRecord>? directPlayProofs)
        {
            if (migrationReport == null)
                throw new ArgumentNullException(nameof(migrationReport));

            if (entryGate == null)
                throw new ArgumentNullException(nameof(entryGate));

            Dictionary<string, SceneAssetMigrationAssetRecord> recordsByPath = new Dictionary<string, SceneAssetMigrationAssetRecord>(StringComparer.Ordinal);
            IReadOnlyList<SceneAssetMigrationAssetRecord> assetRecords = migrationReport.AssetRecords;
            for (int index = 0; index < assetRecords.Count; index++)
            {
                SceneAssetMigrationAssetRecord record = assetRecords[index];
                recordsByPath[record.Target.AssetPath] = record;
            }

            List<ShippedGameplayProofTargetRecord> targets = new List<ShippedGameplayProofTargetRecord>(RequiredSceneTargets.Length);
            for (int index = 0; index < RequiredSceneTargets.Length; index++)
            {
                string scenePath = RequiredSceneTargets[index];
                targets.Add(CreateSceneTargetRecord(scenePath, recordsByPath, entryGate));
            }

            return new ShippedGameplayVerificationReport(entryGate, targets, directPlayProofs, migrationReport.UnexpectedPrefabPaths);
        }

        public static ShippedGameplayDirectPlayProofRecord SummarizeDirectPlayProof(string assetPath, AuthoringDirectPlayResult result, int diagnosticCapacity = 256)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Direct-play proof summaries must provide an asset path.", nameof(assetPath));

            if (result == null)
                throw new ArgumentNullException(nameof(result));

            InMemoryDiagnosticSink sink = new InMemoryDiagnosticSink(diagnosticCapacity);
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            AuthoringDirectPlayDiagnostics.Emit(diagnosticService, result);

            int warningCount = 0;
            int errorCount = 0;
            int fatalCount = 0;
            IReadOnlyList<KernelDiagnostic> diagnostics = sink.Diagnostics;
            for (int index = 0; index < diagnostics.Count; index++)
            {
                switch (diagnostics[index].Severity)
                {
                    case DiagnosticSeverity.Warning:
                        warningCount++;
                        break;
                    case DiagnosticSeverity.Error:
                        errorCount++;
                        break;
                    case DiagnosticSeverity.Fatal:
                        fatalCount++;
                        break;
                }
            }

            List<string> blockingCodes = new List<string>(2);
            bool proofFailed = !result.IsSuccessful || sink.WasTruncated || errorCount > 0 || fatalCount > 0;
            if (proofFailed)
                blockingCodes.Add(ShippedGameplayVerificationCodes.DirectPlayProofFailed);

            if (sink.WasTruncated)
                blockingCodes.Add(ShippedGameplayVerificationCodes.DirectPlayDiagnosticCaptureTruncated);

            return new ShippedGameplayDirectPlayProofRecord(
                assetPath,
                result.FailedStage,
                diagnostics.Count,
                warningCount,
                errorCount,
                fatalCount,
                sink.WasTruncated,
                proofFailed ? ShippedGameplayDirectPlayProofStatus.Failed : ShippedGameplayDirectPlayProofStatus.Succeeded,
                blockingCodes);
        }

        public static AuthoringValidationReport Validate(ShippedGameplayVerificationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            List<AuthoringValidationIssue> issues = new List<AuthoringValidationIssue>();
            AppendGateIssues(issues, report.EntryGate);
            AppendTargetIssues(issues, report.Targets, report.EntryGate);
            AppendDirectPlayIssues(issues, report.DirectPlayProofs);
            AppendUnexpectedPrefabIssues(issues, report.UnexpectedPrefabPaths);
            return new AuthoringValidationReport(issues.ToArray());
        }

        static ShippedGameplayProofTargetRecord CreateSceneTargetRecord(
            string scenePath,
            IReadOnlyDictionary<string, SceneAssetMigrationAssetRecord> recordsByPath,
            ShippedGameplayInventoryGateSnapshot entryGate)
        {
            if (!recordsByPath.TryGetValue(scenePath, out SceneAssetMigrationAssetRecord? record))
            {
                List<string> missingCodes = new List<string>(2)
                {
                    ShippedGameplayVerificationCodes.SceneTargetMissing,
                };

                if (!entryGate.IsClosed)
                    missingCodes.Add(ShippedGameplayVerificationCodes.InventoryGateBlocked);

                return new ShippedGameplayProofTargetRecord(
                    ShippedGameplayProofTargetKind.Scene,
                    scenePath,
                    existsInMigrationReport: false,
                    hasRoots: false,
                    missingRequiredAnchorCount: 0,
                    legacyAnchorCount: 0,
                    unresolvedItemCount: 1,
                    ShippedGameplayProofStatus.Blocked,
                    missingCodes);
            }

            int missingRequiredAnchorCount = record.MissingRequiredAnchorTypeNames.Count;
            int legacyAnchorCount = record.LegacyAnchors.Count;
            int unresolvedItemCount = record.UnresolvedItemCount;

            List<string> blockingCodes = new List<string>(5);
            if (!entryGate.IsClosed)
                blockingCodes.Add(ShippedGameplayVerificationCodes.InventoryGateBlocked);

            if (!record.HasRoots)
                blockingCodes.Add(ShippedGameplayVerificationCodes.SceneAssetRootsEmpty);

            if (missingRequiredAnchorCount > 0)
                blockingCodes.Add(ShippedGameplayVerificationCodes.SceneRequiredAnchorMissing);

            if (legacyAnchorCount > 0)
                blockingCodes.Add(ShippedGameplayVerificationCodes.SceneLegacyAnchorPresent);

            if (unresolvedItemCount > 0 && blockingCodes.Count == 0)
                blockingCodes.Add(ShippedGameplayVerificationCodes.SceneMigrationUnresolved);

            ShippedGameplayProofStatus status = entryGate.IsClosed && unresolvedItemCount == 0
                ? ShippedGameplayProofStatus.Ready
                : ShippedGameplayProofStatus.Blocked;

            return new ShippedGameplayProofTargetRecord(
                ShippedGameplayProofTargetKind.Scene,
                scenePath,
                existsInMigrationReport: true,
                hasRoots: record.HasRoots,
                missingRequiredAnchorCount,
                legacyAnchorCount,
                unresolvedItemCount,
                status,
                blockingCodes);
        }

        static void AppendGateIssues(List<AuthoringValidationIssue> issues, ShippedGameplayInventoryGateSnapshot gate)
        {
            if (!gate.IsClosed)
            {
                issues.Add(new AuthoringValidationIssue(
                    ShippedGameplayVerificationCodes.InventoryGateBlocked,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LegacyBoundary,
                    default,
                    "M11.5 shipped gameplay verification cannot execute while upstream M11.2-M11.4 gates remain open.",
                    subjectName: "M11.5 Entry Gate",
                    suggestedFix: "Close service/command/value/dynamic residual inventory debt and rerun the shipped gameplay verification report."));
            }

            if (gate.HasSummaryParseFailure)
            {
                issues.Add(new AuthoringValidationIssue(
                    ShippedGameplayVerificationCodes.InventorySummaryParseFailure,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LocalNode,
                    default,
                    "One or more inventory summary sections could not be parsed. Entry-gate evaluation is fail-closed.",
                    subjectName: "Inventory Summary",
                    suggestedFix: "Repair inventory summary formatting and rerun the M11.5 report."));
            }

            if (gate.ServiceTodoCount > 0)
                issues.Add(CreateGateCountIssue(ShippedGameplayVerificationCodes.InventoryGateServiceTodo, "Service inventory still has replacement debt.", gate.ServiceTodoCount, "ServiceTodoCount"));

            if (gate.CommandTodoCount > 0)
                issues.Add(CreateGateCountIssue(ShippedGameplayVerificationCodes.InventoryGateCommandTodo, "Command inventory still has replacement debt.", gate.CommandTodoCount, "CommandTodoCount"));

            if (gate.ValueBoundaryDebtCount > 0)
                issues.Add(CreateGateCountIssue(ShippedGameplayVerificationCodes.InventoryGateValueBoundaryDebt, "Value/scalar/query inventory still has unresolved migration debt.", gate.ValueBoundaryDebtCount, "ValueBoundaryDebtCount"));

            if (gate.DynamicTodoCount > 0)
                issues.Add(CreateGateCountIssue(ShippedGameplayVerificationCodes.InventoryGateDynamicTodo, "Dynamic source inventory still has replacement debt.", gate.DynamicTodoCount, "DynamicTodoCount"));
        }

        static AuthoringValidationIssue CreateGateCountIssue(string code, string message, int count, string payloadKey)
        {
            return new AuthoringValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.LegacyBoundary,
                default,
                message,
                subjectName: "M11.5 Entry Gate",
                suggestedFix: "Drive the corresponding inventory residual count to zero before running shipped gameplay proof.",
                additionalPayloadEntries: new[]
                {
                    new DiagnosticPayloadEntry(payloadKey, DiagnosticPayloadValue.FromInt32(count)),
                });
        }

        static void AppendTargetIssues(
            List<AuthoringValidationIssue> issues,
            IReadOnlyList<ShippedGameplayProofTargetRecord> targets,
            ShippedGameplayInventoryGateSnapshot entryGate)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                ShippedGameplayProofTargetRecord target = targets[index];

                if (!target.ExistsInMigrationReport)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.SceneTargetMissing,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Required shipped scene target is missing from the migration report.",
                        subjectName: target.AssetPath,
                        suggestedFix: "Include the shipped scene in the migration report target set before running M11.5 verification.",
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }

                if (!target.HasRoots)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.SceneAssetRootsEmpty,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Shipped scene target could not resolve root objects.",
                        subjectName: target.AssetPath,
                        suggestedFix: "Open and repair the scene asset, then rerun the migration report.",
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }

                if (target.MissingRequiredAnchorCount > 0)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.SceneRequiredAnchorMissing,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Shipped scene target is missing required new-path anchors.",
                        subjectName: target.AssetPath,
                        suggestedFix: "Complete M11.4 field-preserving migration for the missing successor anchors.",
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }

                if (target.LegacyAnchorCount > 0)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.SceneLegacyAnchorPresent,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LegacyBoundary,
                        default,
                        "Shipped scene target still serializes legacy runtime authority anchors.",
                        subjectName: target.AssetPath,
                        suggestedFix: "Migrate legacy runtime fields into successor authoring and remove legacy anchors.",
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }

                if (target.UnresolvedItemCount > 0 && target.MissingRequiredAnchorCount == 0 && target.LegacyAnchorCount == 0 && target.HasRoots)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.SceneMigrationUnresolved,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Shipped scene target still has unresolved migration items.",
                        subjectName: target.AssetPath,
                        suggestedFix: "Resolve scene migration report blockers before running M11.5 proof.",
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }

                if (!entryGate.IsClosed && target.Status == ShippedGameplayProofStatus.Blocked)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.InventoryGateBlocked,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LegacyBoundary,
                        default,
                        "Shipped scene proof target is blocked by the open entry gate.",
                        subjectName: target.AssetPath,
                        additionalPayloadEntries: CreateTargetPayload(target)));
                }
            }
        }

        static void AppendUnexpectedPrefabIssues(List<AuthoringValidationIssue> issues, IReadOnlyList<string> unexpectedPrefabPaths)
        {
            for (int index = 0; index < unexpectedPrefabPaths.Count; index++)
            {
                string prefabPath = unexpectedPrefabPaths[index];
                issues.Add(new AuthoringValidationIssue(
                    ShippedGameplayVerificationCodes.PrefabBaselineDrift,
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LegacyBoundary,
                    default,
                    "Unexpected prefab assets were found while M11.5 uses a frozen prefab baseline of zero.",
                    subjectName: prefabPath,
                    suggestedFix: "Update scene/prefab inventory and migration contract before executing M11.5 proof.",
                    additionalPayloadEntries: new[]
                    {
                        new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(prefabPath)),
                    }));
            }
        }

        static void AppendDirectPlayIssues(List<AuthoringValidationIssue> issues, IReadOnlyList<ShippedGameplayDirectPlayProofRecord> directPlayProofs)
        {
            for (int index = 0; index < directPlayProofs.Count; index++)
            {
                ShippedGameplayDirectPlayProofRecord proof = directPlayProofs[index];
                if (proof.Status == ShippedGameplayDirectPlayProofStatus.Failed)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.DirectPlayProofFailed,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Shipped gameplay direct-play proof did not complete successfully.",
                        subjectName: proof.AssetPath,
                        suggestedFix: "Repair the failing direct-play stage and rerun the shipped gameplay proof bundle.",
                        additionalPayloadEntries: CreateDirectPlayPayload(proof)));
                }

                if (proof.WasTruncated)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ShippedGameplayVerificationCodes.DirectPlayDiagnosticCaptureTruncated,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        default,
                        "Direct-play diagnostic capture truncated the evidence bundle and is treated as a proof failure.",
                        subjectName: proof.AssetPath,
                        suggestedFix: "Increase diagnostic capture capacity before rerunning the shipped gameplay proof bundle.",
                        additionalPayloadEntries: CreateDirectPlayPayload(proof)));
                }
            }
        }

        static DiagnosticPayloadEntry[] CreateTargetPayload(ShippedGameplayProofTargetRecord target)
        {
            return new[]
            {
                new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(target.AssetPath)),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(target.TargetKind.ToString())),
                new DiagnosticPayloadEntry("MissingRequiredAnchorCount", DiagnosticPayloadValue.FromInt32(target.MissingRequiredAnchorCount)),
                new DiagnosticPayloadEntry("LegacyAnchorCount", DiagnosticPayloadValue.FromInt32(target.LegacyAnchorCount)),
                new DiagnosticPayloadEntry("UnresolvedItemCount", DiagnosticPayloadValue.FromInt32(target.UnresolvedItemCount)),
            };
        }

        static DiagnosticPayloadEntry[] CreateDirectPlayPayload(ShippedGameplayDirectPlayProofRecord proof)
        {
            return new[]
            {
                new DiagnosticPayloadEntry("AssetPath", DiagnosticPayloadValue.FromString(proof.AssetPath)),
                new DiagnosticPayloadEntry("FailedStage", DiagnosticPayloadValue.FromString(proof.FailedStage.ToString())),
                new DiagnosticPayloadEntry("DiagnosticCount", DiagnosticPayloadValue.FromInt32(proof.DiagnosticCount)),
                new DiagnosticPayloadEntry("WarningCount", DiagnosticPayloadValue.FromInt32(proof.WarningCount)),
                new DiagnosticPayloadEntry("ErrorCount", DiagnosticPayloadValue.FromInt32(proof.ErrorCount)),
                new DiagnosticPayloadEntry("FatalCount", DiagnosticPayloadValue.FromInt32(proof.FatalCount)),
                new DiagnosticPayloadEntry("WasTruncated", DiagnosticPayloadValue.FromBoolean(proof.WasTruncated)),
            };
        }

        static ShippedGameplayInventoryGateSnapshot ReadInventoryGateSnapshot()
        {
            bool parseFailure = false;

            int serviceTodoCount = ReadSummaryCount(ServiceInventoryPath, "要差し替え", ref parseFailure);
            int commandTodoCount = ReadSummaryCount(CommandInventoryPath, "要差し替え", ref parseFailure);
            int valueInProgressCount = ReadSummaryCount(ValueInventoryPath, "進行中", ref parseFailure);
            int valueLegacyCount = ReadSummaryCount(ValueInventoryPath, "隔離/削除対象", ref parseFailure);
            int dynamicTodoCount = ReadSummaryCount(DynamicInventoryPath, "要差し替え", ref parseFailure);

            return new ShippedGameplayInventoryGateSnapshot(
                serviceTodoCount,
                commandTodoCount,
                valueInProgressCount + valueLegacyCount,
                dynamicTodoCount,
                parseFailure);
        }

        static int ReadSummaryCount(string relativeMarkdownPath, string label, ref bool parseFailure)
        {
            if (!TryReadSummaryCount(relativeMarkdownPath, label, out int count))
            {
                parseFailure = true;
                return 1;
            }

            return count;
        }

        static bool TryReadSummaryCount(string relativeMarkdownPath, string label, out int count)
        {
            count = 0;

            string absolutePath = ResolveProjectPath(relativeMarkdownPath);
            if (!File.Exists(absolutePath))
                return false;

            string markdown = File.ReadAllText(absolutePath);
            string expectedLabel = NormalizeSummaryLabel(label);

            MatchCollection matches = SummaryLineRegex.Matches(markdown);
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                string foundLabel = NormalizeSummaryLabel(match.Groups["label"].Value);
                if (!StringComparer.Ordinal.Equals(foundLabel, expectedLabel))
                    continue;

                if (!int.TryParse(match.Groups["count"].Value, out int parsedCount))
                    return false;

                count = parsedCount;
                return true;
            }

            return false;
        }

        static string ResolveProjectPath(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        static string NormalizeSummaryLabel(string value)
        {
            return value.Replace("`", string.Empty).Trim();
        }
    }
}