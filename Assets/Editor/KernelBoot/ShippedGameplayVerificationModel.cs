#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class ShippedGameplayVerificationCodes
    {
        public const string InventoryGateBlocked = "UNITY_SHIPPED_GAMEPLAY_GATE_BLOCKED";
        public const string InventorySummaryParseFailure = "UNITY_SHIPPED_GAMEPLAY_INVENTORY_SUMMARY_PARSE_FAILURE";
        public const string InventoryGateServiceTodo = "UNITY_SHIPPED_GAMEPLAY_GATE_SERVICE_TODO";
        public const string InventoryGateCommandTodo = "UNITY_SHIPPED_GAMEPLAY_GATE_COMMAND_TODO";
        public const string InventoryGateValueBoundaryDebt = "UNITY_SHIPPED_GAMEPLAY_GATE_VALUE_BOUNDARY_DEBT";
        public const string InventoryGateDynamicTodo = "UNITY_SHIPPED_GAMEPLAY_GATE_DYNAMIC_TODO";
        public const string SceneTargetMissing = "UNITY_SHIPPED_GAMEPLAY_SCENE_TARGET_MISSING";
        public const string SceneAssetRootsEmpty = "UNITY_SHIPPED_GAMEPLAY_SCENE_ROOTS_EMPTY";
        public const string SceneRequiredAnchorMissing = "UNITY_SHIPPED_GAMEPLAY_SCENE_REQUIRED_ANCHOR_MISSING";
        public const string SceneLegacyAnchorPresent = "UNITY_SHIPPED_GAMEPLAY_SCENE_LEGACY_ANCHOR_PRESENT";
        public const string SceneMigrationUnresolved = "UNITY_SHIPPED_GAMEPLAY_SCENE_MIGRATION_UNRESOLVED";
        public const string PrefabBaselineDrift = "UNITY_SHIPPED_GAMEPLAY_PREFAB_BASELINE_DRIFT";
        public const string DirectPlayProofFailed = "UNITY_SHIPPED_GAMEPLAY_DIRECT_PLAY_PROOF_FAILED";
        public const string DirectPlayDiagnosticCaptureTruncated = "UNITY_SHIPPED_GAMEPLAY_DIRECT_PLAY_DIAGNOSTICS_TRUNCATED";
    }

    public enum ShippedGameplayProofTargetKind
    {
        Unknown = 0,
        Scene = 10,
        Prefab = 20,
    }

    public enum ShippedGameplayProofStatus
    {
        Unknown = 0,
        Ready = 10,
        Blocked = 20,
    }

    public enum ShippedGameplayDirectPlayProofStatus
    {
        Unknown = 0,
        Succeeded = 10,
        Failed = 20,
    }

    public sealed class ShippedGameplayInventoryGateSnapshot
    {
        public ShippedGameplayInventoryGateSnapshot(
            int serviceTodoCount,
            int commandTodoCount,
            int valueBoundaryDebtCount,
            int dynamicTodoCount,
            bool hasSummaryParseFailure = false)
        {
            if (serviceTodoCount < 0)
                throw new ArgumentOutOfRangeException(nameof(serviceTodoCount), serviceTodoCount, "Service todo counts must be non-negative.");

            if (commandTodoCount < 0)
                throw new ArgumentOutOfRangeException(nameof(commandTodoCount), commandTodoCount, "Command todo counts must be non-negative.");

            if (valueBoundaryDebtCount < 0)
                throw new ArgumentOutOfRangeException(nameof(valueBoundaryDebtCount), valueBoundaryDebtCount, "Value boundary debt counts must be non-negative.");

            if (dynamicTodoCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicTodoCount), dynamicTodoCount, "Dynamic todo counts must be non-negative.");

            ServiceTodoCount = serviceTodoCount;
            CommandTodoCount = commandTodoCount;
            ValueBoundaryDebtCount = valueBoundaryDebtCount;
            DynamicTodoCount = dynamicTodoCount;
            HasSummaryParseFailure = hasSummaryParseFailure;
        }

        public int ServiceTodoCount { get; }

        public int CommandTodoCount { get; }

        public int ValueBoundaryDebtCount { get; }

        public int DynamicTodoCount { get; }

        public bool HasSummaryParseFailure { get; }

        public int BlockingCategoryCount
        {
            get
            {
                int count = 0;
                if (HasSummaryParseFailure)
                    count++;

                if (ServiceTodoCount > 0)
                    count++;

                if (CommandTodoCount > 0)
                    count++;

                if (ValueBoundaryDebtCount > 0)
                    count++;

                if (DynamicTodoCount > 0)
                    count++;

                return count;
            }
        }

        public bool IsClosed => BlockingCategoryCount == 0;
    }

    public sealed class ShippedGameplayProofTargetRecord
    {
        readonly string[] blockingCodes;

        public ShippedGameplayProofTargetRecord(
            ShippedGameplayProofTargetKind targetKind,
            string assetPath,
            bool existsInMigrationReport,
            bool hasRoots,
            int missingRequiredAnchorCount,
            int legacyAnchorCount,
            int unresolvedItemCount,
            ShippedGameplayProofStatus status,
            IReadOnlyList<string>? blockingCodes = null)
        {
            if (targetKind == ShippedGameplayProofTargetKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Proof target records must declare a concrete target kind.");

            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Proof target records must provide an asset path.", nameof(assetPath));

            if (missingRequiredAnchorCount < 0)
                throw new ArgumentOutOfRangeException(nameof(missingRequiredAnchorCount), missingRequiredAnchorCount, "Missing-required-anchor counts must be non-negative.");

            if (legacyAnchorCount < 0)
                throw new ArgumentOutOfRangeException(nameof(legacyAnchorCount), legacyAnchorCount, "Legacy-anchor counts must be non-negative.");

            if (unresolvedItemCount < 0)
                throw new ArgumentOutOfRangeException(nameof(unresolvedItemCount), unresolvedItemCount, "Unresolved-item counts must be non-negative.");

            if (status == ShippedGameplayProofStatus.Unknown)
                throw new ArgumentOutOfRangeException(nameof(status), status, "Proof target records must use a concrete status.");

            TargetKind = targetKind;
            AssetPath = assetPath.Trim();
            ExistsInMigrationReport = existsInMigrationReport;
            HasRoots = hasRoots;
            MissingRequiredAnchorCount = missingRequiredAnchorCount;
            LegacyAnchorCount = legacyAnchorCount;
            UnresolvedItemCount = unresolvedItemCount;
            Status = status;
            this.blockingCodes = CloneCodes(blockingCodes);
        }

        public ShippedGameplayProofTargetKind TargetKind { get; }

        public string AssetPath { get; }

        public bool ExistsInMigrationReport { get; }

        public bool HasRoots { get; }

        public int MissingRequiredAnchorCount { get; }

        public int LegacyAnchorCount { get; }

        public int UnresolvedItemCount { get; }

        public ShippedGameplayProofStatus Status { get; }

        public IReadOnlyList<string> BlockingCodes => blockingCodes;

        static string[] CloneCodes(IReadOnlyList<string>? source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            List<string> normalized = new List<string>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                string? candidate = source[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string code = candidate.Trim();
                if (unique.Add(code))
                    normalized.Add(code);
            }

            normalized.Sort(StringComparer.Ordinal);
            return normalized.ToArray();
        }
    }

    public sealed class ShippedGameplayDirectPlayProofRecord
    {
        readonly string[] blockingCodes;

        public ShippedGameplayDirectPlayProofRecord(
            string assetPath,
            AuthoringDirectPlayStage failedStage,
            int diagnosticCount,
            int warningCount,
            int errorCount,
            int fatalCount,
            bool wasTruncated,
            ShippedGameplayDirectPlayProofStatus status,
            IReadOnlyList<string>? blockingCodes = null)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Direct-play proof records must provide an asset path.", nameof(assetPath));

            if (diagnosticCount < 0)
                throw new ArgumentOutOfRangeException(nameof(diagnosticCount), diagnosticCount, "Diagnostic counts must be non-negative.");

            if (warningCount < 0)
                throw new ArgumentOutOfRangeException(nameof(warningCount), warningCount, "Warning counts must be non-negative.");

            if (errorCount < 0)
                throw new ArgumentOutOfRangeException(nameof(errorCount), errorCount, "Error counts must be non-negative.");

            if (fatalCount < 0)
                throw new ArgumentOutOfRangeException(nameof(fatalCount), fatalCount, "Fatal counts must be non-negative.");

            if (status == ShippedGameplayDirectPlayProofStatus.Unknown)
                throw new ArgumentOutOfRangeException(nameof(status), status, "Direct-play proof records must use a concrete status.");

            if (warningCount + errorCount + fatalCount > diagnosticCount)
                throw new ArgumentException("Diagnostic severity counts must not exceed the total diagnostic count.", nameof(diagnosticCount));

            AssetPath = assetPath.Trim();
            FailedStage = failedStage;
            DiagnosticCount = diagnosticCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            FatalCount = fatalCount;
            WasTruncated = wasTruncated;
            Status = status;
            this.blockingCodes = CloneCodes(blockingCodes);
        }

        public string AssetPath { get; }

        public AuthoringDirectPlayStage FailedStage { get; }

        public int DiagnosticCount { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public int FatalCount { get; }

        public bool WasTruncated { get; }

        public ShippedGameplayDirectPlayProofStatus Status { get; }

        public IReadOnlyList<string> BlockingCodes => blockingCodes;

        public int BlockingDiagnosticCount => ErrorCount + FatalCount;

        public bool IsSuccessful => Status == ShippedGameplayDirectPlayProofStatus.Succeeded;
    }

    public sealed class ShippedGameplayVerificationReport
    {
        readonly ShippedGameplayProofTargetRecord[] targets;
        readonly ShippedGameplayDirectPlayProofRecord[] directPlayProofs;
        readonly string[] unexpectedPrefabPaths;

        public ShippedGameplayVerificationReport(
            ShippedGameplayInventoryGateSnapshot entryGate,
            IReadOnlyList<ShippedGameplayProofTargetRecord>? targets,
            IReadOnlyList<ShippedGameplayDirectPlayProofRecord>? directPlayProofs = null,
            IReadOnlyList<string>? unexpectedPrefabPaths = null)
        {
            EntryGate = entryGate ?? throw new ArgumentNullException(nameof(entryGate));
            this.targets = CloneAndSortTargets(targets);
            this.directPlayProofs = CloneAndSortDirectPlayProofs(directPlayProofs);
            this.unexpectedPrefabPaths = CloneAndSortPaths(unexpectedPrefabPaths, nameof(unexpectedPrefabPaths));

            int unresolved = EntryGate.IsClosed ? 0 : EntryGate.BlockingCategoryCount;
            unresolved += this.unexpectedPrefabPaths.Length;
            for (int index = 0; index < this.targets.Length; index++)
                unresolved += this.targets[index].UnresolvedItemCount;

            for (int index = 0; index < this.directPlayProofs.Length; index++)
                unresolved += this.directPlayProofs[index].BlockingCodes.Count;

            UnresolvedItemCount = unresolved;
        }

        public ShippedGameplayInventoryGateSnapshot EntryGate { get; }

        public IReadOnlyList<ShippedGameplayProofTargetRecord> Targets => targets;

        public IReadOnlyList<ShippedGameplayDirectPlayProofRecord> DirectPlayProofs => directPlayProofs;

        public IReadOnlyList<string> UnexpectedPrefabPaths => unexpectedPrefabPaths;

        public int UnresolvedItemCount { get; }

        public bool IsExecutable
        {
            get
            {
                if (!EntryGate.IsClosed)
                    return false;

                if (unexpectedPrefabPaths.Length > 0)
                    return false;

                for (int index = 0; index < targets.Length; index++)
                {
                    ShippedGameplayProofTargetRecord target = targets[index];
                    if (target.Status != ShippedGameplayProofStatus.Ready)
                        return false;

                    if (target.UnresolvedItemCount != 0)
                        return false;
                }

                return true;
            }
        }

        public bool IsVerified
        {
            get
            {
                if (!IsExecutable)
                    return false;

                if (directPlayProofs.Length != targets.Length)
                    return false;

                for (int index = 0; index < targets.Length; index++)
                {
                    if (!HasSuccessfulDirectPlayProof(targets[index].AssetPath))
                        return false;
                }

                return true;
            }
        }

        static ShippedGameplayProofTargetRecord[] CloneAndSortTargets(IReadOnlyList<ShippedGameplayProofTargetRecord>? source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ShippedGameplayProofTargetRecord>();

            ShippedGameplayProofTargetRecord[] snapshot = new ShippedGameplayProofTargetRecord[source.Count];
            for (int index = 0; index < source.Count; index++)
                snapshot[index] = source[index] ?? throw new ArgumentException("Shipped gameplay verification reports must not contain null targets.", nameof(source));

            Array.Sort(snapshot, CompareTargets);
            return snapshot;
        }

        static int CompareTargets(ShippedGameplayProofTargetRecord left, ShippedGameplayProofTargetRecord right)
        {
            int result = left.TargetKind.CompareTo(right.TargetKind);
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
        }

        bool HasSuccessfulDirectPlayProof(string assetPath)
        {
            for (int index = 0; index < directPlayProofs.Length; index++)
            {
                ShippedGameplayDirectPlayProofRecord proof = directPlayProofs[index];
                if (!StringComparer.Ordinal.Equals(proof.AssetPath, assetPath))
                    continue;

                return proof.IsSuccessful;
            }

            return false;
        }

        static ShippedGameplayDirectPlayProofRecord[] CloneAndSortDirectPlayProofs(IReadOnlyList<ShippedGameplayDirectPlayProofRecord>? source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ShippedGameplayDirectPlayProofRecord>();

            ShippedGameplayDirectPlayProofRecord[] snapshot = new ShippedGameplayDirectPlayProofRecord[source.Count];
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                ShippedGameplayDirectPlayProofRecord item = source[index] ?? throw new ArgumentException("Shipped gameplay verification reports must not contain null direct-play proofs.", nameof(source));
                if (!seenPaths.Add(item.AssetPath))
                    throw new ArgumentException("Shipped gameplay verification reports must not contain duplicate direct-play proof asset paths.", nameof(source));

                snapshot[index] = item;
            }

            Array.Sort(snapshot, CompareDirectPlayProofs);
            return snapshot;
        }

        static int CompareDirectPlayProofs(ShippedGameplayDirectPlayProofRecord left, ShippedGameplayDirectPlayProofRecord right)
        {
            return StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
        }

        static string[] CloneAndSortPaths(IReadOnlyList<string>? source, string paramName)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            string[] snapshot = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                string? candidate = source[index];
                if (string.IsNullOrWhiteSpace(candidate))
                    throw new ArgumentException("Shipped gameplay verification reports must not contain null or empty paths.", paramName);

                snapshot[index] = candidate.Trim();
            }

            Array.Sort(snapshot, StringComparer.Ordinal);
            return snapshot;
        }
    }
}