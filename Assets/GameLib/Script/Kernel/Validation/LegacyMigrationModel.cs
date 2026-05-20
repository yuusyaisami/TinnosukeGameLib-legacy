#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;

namespace Game.Kernel.Validation
{
    public static class LegacyCompatBoundaryCodes
    {
        public const string BridgeUsed = "LEGACY_BRIDGE_USED";
        public const string RuntimeAdapterUsed = "LEGACY_RUNTIME_ADAPTER_USED";
        public const string FallbackForbidden = "LEGACY_FALLBACK_FORBIDDEN";
        public const string ProfileForbidden = "LEGACY_PROFILE_FORBIDDEN";
        public const string AdapterExpired = "LEGACY_ADAPTER_EXPIRED";
    }

    public sealed class LegacyMigrationReportHeader
    {
        public LegacyMigrationReportHeader(
            string sourceSystemName,
            int sourceVersion,
            string targetSubsystemName,
            ValidationPhase phase,
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            string? compatibilityHash = null)
        {
            if (string.IsNullOrWhiteSpace(sourceSystemName))
                throw new ArgumentException("Legacy migration report headers must provide a source system name.", nameof(sourceSystemName));

            if (sourceVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceVersion), sourceVersion, "Legacy migration report headers must provide a positive source version.");

            if (string.IsNullOrWhiteSpace(targetSubsystemName))
                throw new ArgumentException("Legacy migration report headers must provide a target subsystem name.", nameof(targetSubsystemName));

            if (phase == default)
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Legacy migration report headers must provide a defined validation phase.");

            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Legacy migration report headers must provide a selected profile.", nameof(selectedProfile));

            if (selectedProfileMask == KernelProfileMask.None)
                throw new ArgumentException("Legacy migration report headers must provide a non-empty selected profile mask.", nameof(selectedProfileMask));

            if (compatibilityHash != null && string.IsNullOrWhiteSpace(compatibilityHash))
                throw new ArgumentException("Legacy migration report compatibility hashes must be null or non-empty.", nameof(compatibilityHash));

            SourceSystemName = sourceSystemName;
            SourceVersion = sourceVersion;
            TargetSubsystemName = targetSubsystemName;
            Phase = phase;
            SelectedProfile = selectedProfile;
            SelectedProfileMask = selectedProfileMask;
            CompatibilityHash = compatibilityHash;
        }

        public string SourceSystemName { get; }

        public int SourceVersion { get; }

        public string TargetSubsystemName { get; }

        public ValidationPhase Phase { get; }

        public string SelectedProfile { get; }

        public KernelProfileMask SelectedProfileMask { get; }

        public string? CompatibilityHash { get; }
    }

    public sealed class LegacyMigrationReport
    {
        readonly LegacyMigrationReportHeader header;
        readonly ReadOnlyCollection<LegacyAdapterDescriptor> adapters;
        readonly ReadOnlyCollection<DependencyValidationIssue> issues;

        LegacyMigrationReport(
            LegacyMigrationReportHeader header,
            IReadOnlyList<LegacyAdapterDescriptor> adapters,
            IReadOnlyList<DependencyValidationIssue> issues)
        {
            this.header = header ?? throw new ArgumentNullException(nameof(header));
            if (adapters == null)
                throw new ArgumentNullException(nameof(adapters));

            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            SelectedProfile = header.SelectedProfile;
            this.adapters = new ReadOnlyCollection<LegacyAdapterDescriptor>(CloneAndSortAdapters(adapters));
            this.issues = new ReadOnlyCollection<DependencyValidationIssue>(CloneIssues(issues));

            (InfoCount, WarningCount, ErrorCount, FatalCount) = CountSeverity(this.issues);
            Status = DeriveStatus(InfoCount, WarningCount, ErrorCount, FatalCount);
        }

        public LegacyMigrationReportHeader Header => header;

        public string SelectedProfile { get; }

        public IReadOnlyList<LegacyAdapterDescriptor> Adapters => adapters;

        public IReadOnlyList<DependencyValidationIssue> Issues => issues;

        public ValidationResultStatus Status { get; }

        public int InfoCount { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public int FatalCount { get; }

        public bool IsValid => ErrorCount == 0 && FatalCount == 0;

        public static LegacyMigrationReport Validate(IReadOnlyList<LegacyAdapterDescriptor> adapters, string selectedProfile, KernelProfileMask selectedProfileMask)
        {
            return Validate(new LegacyMigrationReportHeader("LegacyCompatBoundary", 1, "LegacyCompatBoundary", ValidationPhase.Build, selectedProfile, selectedProfileMask), adapters);
        }

        public static LegacyMigrationReport Validate(LegacyMigrationReportHeader header, IReadOnlyList<LegacyAdapterDescriptor> adapters)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (adapters == null)
                throw new ArgumentNullException(nameof(adapters));

            LegacyAdapterDescriptor[] sortedAdapters = CloneAndSortAdapters(adapters);
            List<DependencyValidationIssue> issues = new List<DependencyValidationIssue>();
            for (int index = 0; index < sortedAdapters.Length; index++)
            {
                LegacyAdapterDescriptor adapter = sortedAdapters[index] ?? throw new ArgumentException("Legacy migration adapter collections must not contain null items.", nameof(adapters));
                ValidateAdapter(header, adapter, issues);
            }

            return new LegacyMigrationReport(header, sortedAdapters, issues);
        }

        public static LegacyMigrationReport Validate(DependencyValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            LegacyMigrationReportHeader header = new LegacyMigrationReportHeader(
                "DependencyValidationInput",
                1,
                "LegacyCompatBoundary",
                ValidationPhase.Build,
                input.SelectedProfile,
                input.SelectedProfileMask,
                null);

            List<LegacyAdapterDescriptor> adapters = new List<LegacyAdapterDescriptor>();
            List<DependencyValidationIssue> issues = new List<DependencyValidationIssue>();

            ReadOnlySpan<ModuleIR> modules = input.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                ModuleIR module = modules[index];
                LegacyCompatDescriptorIR? legacyCompat = module.LegacyCompat;

                if (legacyCompat == null)
                {
                    if (module.Kind == ModuleKind.MigrationAdapter)
                    {
                        issues.Add(CreateIssue(
                            module.Id,
                            module.Source,
                            LegacyCompatBoundaryCodes.ProfileForbidden,
                            ValidationSeverity.Error,
                            input.SelectedProfile,
                            "Migration-adapter modules must declare explicit legacy compatibility metadata.",
                            "Attach explicit legacy compatibility classification and policy metadata to the adapter module."));
                    }

                    continue;
                }

                if (legacyCompat.Kind == LegacyCompatKind.ForbiddenFallback)
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.FallbackForbidden,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy fallback bridges are forbidden by default.",
                        "Replace fallback behavior with an explicit migrated target-kernel dependency or fail deterministically."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(legacyCompat.DiagnosticsCode))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        "LEGACY_ADAPTER_DIAGNOSTICS_MISSING",
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must declare a stable diagnostics code.",
                        "Provide a stable diagnostics code for the legacy bridge descriptor."));
                }

                if (RequiresRemovalCondition(legacyCompat.Kind) && string.IsNullOrWhiteSpace(legacyCompat.RemovalCondition))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        "LEGACY_ADAPTER_REMOVAL_POLICY_MISSING",
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Runtime-capable legacy bridges must declare a removal condition.",
                        "Declare how and when the legacy bridge will be removed."));
                }

                if (IsProfileForbidden(legacyCompat, input.SelectedProfileMask))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.ProfileForbidden,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge kind is forbidden for the selected profile.",
                        "Remove the live runtime legacy bridge from this profile or migrate the dependency fully into v2."));
                    continue;
                }

                LegacyRemovalPolicy removalPolicy = new LegacyRemovalPolicy(
                    module.Id,
                    legacyCompat.RemovalStatus,
                    legacyCompat.Profiles,
                    "Legacy bridge declared in dependency input.",
                    legacyCompat.TargetSubsystem,
                    legacyCompat.RemovalCondition ?? "Complete migration to v2-owned normalized data.",
                    legacyCompat.DiagnosticsCode ?? "LEGACY_ADAPTER_DIAGNOSTICS_MISSING",
                    legacyCompat.RemovalCondition ?? "Complete migration to v2-owned normalized data.");

                adapters.Add(new LegacyAdapterDescriptor(
                    legacyCompat.Kind,
                    module.Id,
                    legacyCompat.LegacySystemName,
                    legacyCompat.TargetSubsystem,
                    legacyCompat.Profiles,
                    module.Source,
                    removalPolicy));
            }

            LegacyMigrationReport adapterReport = Validate(header, adapters);
            if (issues.Count == 0)
                return adapterReport;

            List<DependencyValidationIssue> combinedIssues = new List<DependencyValidationIssue>(issues.Count + adapterReport.Issues.Count);
            combinedIssues.AddRange(issues);
            for (int index = 0; index < adapterReport.Issues.Count; index++)
                combinedIssues.Add(adapterReport.Issues[index]);

            return new LegacyMigrationReport(header, adapters, combinedIssues);
        }

        public KernelDiagnostic[] ToKernelDiagnostics()
        {
            KernelDiagnostic[] diagnostics = new KernelDiagnostic[issues.Count];
            for (int index = 0; index < issues.Count; index++)
                diagnostics[index] = issues[index].ToKernelDiagnostic(DiagnosticFailureBoundary.Build);

            return diagnostics;
        }

        public void Emit(KernelDiagnosticService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            service.ReportBatch(ToKernelDiagnostics());
        }

        static void ValidateAdapter(LegacyMigrationReportHeader header, LegacyAdapterDescriptor adapter, List<DependencyValidationIssue> issues)
        {
            if (adapter.Kind == LegacyCompatKind.ForbiddenFallback)
            {
                issues.Add(CreateIssue(
                    adapter.OwnerModule,
                    adapter.Source,
                    LegacyCompatBoundaryCodes.FallbackForbidden,
                    ValidationSeverity.Error,
                    header.SelectedProfile,
                    "Legacy fallback bridges are forbidden by default.",
                    "Replace fallback behavior with an explicit migrated target-kernel dependency or fail deterministically."));
                return;
            }

            if (!adapter.IsAllowedFor(header.SelectedProfileMask))
            {
                issues.Add(CreateIssue(
                    adapter.OwnerModule,
                    adapter.Source,
                    LegacyCompatBoundaryCodes.ProfileForbidden,
                    ValidationSeverity.Error,
                    header.SelectedProfile,
                    "Legacy bridge kind is forbidden for the selected profile.",
                    "Remove the live legacy bridge from this profile or migrate the dependency fully into v2."));
                return;
            }

            if (adapter.RemovalStatus == LegacyRemovalStatus.Deprecated || adapter.RemovalStatus == LegacyRemovalStatus.Forbidden)
            {
                issues.Add(CreateIssue(
                    adapter.OwnerModule,
                    adapter.Source,
                    LegacyCompatBoundaryCodes.AdapterExpired,
                    ValidationSeverity.Error,
                    header.SelectedProfile,
                    "Legacy adapter removal policy has expired.",
                    "Remove the adapter or refresh its removal policy before shipping."));
                return;
            }

            issues.Add(CreateIssue(
                adapter.OwnerModule,
                adapter.Source,
                adapter.IsRuntimeCapable ? LegacyCompatBoundaryCodes.RuntimeAdapterUsed : LegacyCompatBoundaryCodes.BridgeUsed,
                ValidationSeverity.Warning,
                header.SelectedProfile,
                "Legacy bridge remains active and must stay explicit, observable, and removable.",
                "Continue migration until the legacy bridge can be removed from the verified graph."));
        }

        static DependencyValidationIssue CreateIssue(
            ModuleId ownerModule,
            SourceLocationId source,
            string code,
            ValidationSeverity severity,
            string profile,
            string message,
            string suggestedFix)
        {
            return new DependencyValidationIssue(
                code,
                severity,
                ValidationIssueCategory.LegacyBoundary,
                new DependencyNodeIR(ownerModule),
                null,
                ValidationPhase.EditorOnly,
                ownerModule,
                source,
                profile,
                message,
                suggestedFix,
                Array.Empty<DiagnosticPayloadEntry>());
        }

        static LegacyAdapterDescriptor[] CloneAndSortAdapters(IReadOnlyList<LegacyAdapterDescriptor> source)
        {
            LegacyAdapterDescriptor[] clone = new LegacyAdapterDescriptor[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Legacy migration adapter collections must not contain null items.", nameof(source));
            }

            Array.Sort(clone, CompareAdapters);

            return clone;
        }

        static int CompareAdapters(LegacyAdapterDescriptor left, LegacyAdapterDescriptor right)
        {
            if (left == null)
                throw new ArgumentNullException(nameof(left));

            if (right == null)
                throw new ArgumentNullException(nameof(right));

            int comparison = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.Value.CompareTo(right.Source.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.TargetSubsystemName, right.TargetSubsystemName);
            if (comparison != 0)
                return comparison;

            return StringComparer.Ordinal.Compare(left.LegacySystemName, right.LegacySystemName);
        }

        static DependencyValidationIssue[] CloneIssues(IReadOnlyList<DependencyValidationIssue> source)
        {
            DependencyValidationIssue[] clone = new DependencyValidationIssue[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Legacy migration issue collections must not contain null items.", nameof(source));
            }

            return clone;
        }

        static (int InfoCount, int WarningCount, int ErrorCount, int FatalCount) CountSeverity(IReadOnlyList<DependencyValidationIssue> source)
        {
            int infoCount = 0;
            int warningCount = 0;
            int errorCount = 0;
            int fatalCount = 0;

            for (int index = 0; index < source.Count; index++)
            {
                switch (source[index].Severity)
                {
                    case ValidationSeverity.Info:
                        infoCount++;
                        break;
                    case ValidationSeverity.Warning:
                        warningCount++;
                        break;
                    case ValidationSeverity.Error:
                        errorCount++;
                        break;
                    case ValidationSeverity.Fatal:
                        fatalCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(source), source[index].Severity, "Legacy migration reports must contain only defined severities.");
                }
            }

            return (infoCount, warningCount, errorCount, fatalCount);
        }

        static ValidationResultStatus DeriveStatus(int infoCount, int warningCount, int errorCount, int fatalCount)
        {
            if (fatalCount > 0)
                return ValidationResultStatus.Fatal;

            if (errorCount > 0)
                return ValidationResultStatus.Failed;

            if (warningCount > 0 || infoCount > 0)
                return ValidationResultStatus.PassedWithWarnings;

            return ValidationResultStatus.Passed;
        }
    }
}