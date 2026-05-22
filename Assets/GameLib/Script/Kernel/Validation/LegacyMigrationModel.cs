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
        public const string RuntimeAdapterReleaseForbidden = "LEGACY_RUNTIME_ADAPTER_RELEASE_FORBIDDEN";
        public const string FallbackForbidden = "LEGACY_FALLBACK_FORBIDDEN";
        public const string ResolverComponentFallbackForbidden = "LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN";
        public const string ProfileForbidden = "LEGACY_PROFILE_FORBIDDEN";
        public const string AdapterExpired = "LEGACY_ADAPTER_EXPIRED";
        public const string AdapterSurfaceMissing = "LEGACY_ADAPTER_SURFACE_MISSING";
        public const string AdapterSourceTypeMissing = "LEGACY_ADAPTER_SOURCE_TYPE_MISSING";
        public const string AdapterKindSurfaceMismatch = "LEGACY_ADAPTER_KIND_SURFACE_MISMATCH";
        public const string AdapterTargetMissing = "LEGACY_ADAPTER_TARGET_MISSING";
        public const string AdapterTrackingMissing = "LEGACY_ADAPTER_TRACKING_MISSING";
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
        readonly ReadOnlyCollection<LegacyRemovalPolicy> removalPolicies;
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
            removalPolicies = new ReadOnlyCollection<LegacyRemovalPolicy>(CloneRemovalPolicies(this.adapters));
            this.issues = new ReadOnlyCollection<DependencyValidationIssue>(CloneIssues(issues));

            (InfoCount, WarningCount, ErrorCount, FatalCount) = CountSeverity(this.issues);
            Status = DeriveStatus(InfoCount, WarningCount, ErrorCount, FatalCount);
        }

        public LegacyMigrationReportHeader Header => header;

        public string SelectedProfile { get; }

        public IReadOnlyList<LegacyAdapterDescriptor> Adapters => adapters;

        public IReadOnlyList<DependencyValidationIssue> Issues => issues;

        public IReadOnlyList<LegacyRemovalPolicy> RemovalPolicies => removalPolicies;

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

                DiagnosticPayloadEntry[] legacyPayload = CreatePayloadEntries(legacyCompat);

                if (legacyCompat.Kind == LegacyCompatKind.ForbiddenFallback)
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.FallbackForbidden,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy fallback bridges are forbidden by default.",
                            "Replace fallback behavior with an explicit migrated target-kernel dependency or fail deterministically.",
                            legacyPayload));
                    continue;
                }

                bool canCreateAdapter = true;
                bool hasBlockingMetadataIssues = false;

                if (string.IsNullOrWhiteSpace(legacyCompat.DiagnosticsCode))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        "LEGACY_ADAPTER_DIAGNOSTICS_MISSING",
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must declare a stable diagnostics code.",
                            "Provide a stable diagnostics code for the legacy bridge descriptor.",
                            legacyPayload));
                }

                if (string.IsNullOrWhiteSpace(legacyCompat.TrackingIssueOrBlockingCondition))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.AdapterTrackingMissing,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must declare a tracking issue or blocking condition.",
                        "Provide a stable issue reference or blocking condition for removal tracking.",
                        legacyPayload));
                    hasBlockingMetadataIssues = true;
                }

                if (string.IsNullOrWhiteSpace(legacyCompat.RemovalCondition))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        "LEGACY_ADAPTER_REMOVAL_POLICY_MISSING",
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must declare a removal condition.",
                            "Declare how and when the legacy bridge will be removed.",
                            legacyPayload));
                    hasBlockingMetadataIssues = true;
                }

                if (IsReleaseProfile(input.SelectedProfileMask) && IsReleaseRuntimeAdapterKind(legacyCompat.Kind))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Release profile forbids live runtime legacy adapters.",
                        "Replace the runtime adapter with prevalidated migrated input or a non-runtime migration artifact before shipping Release.",
                        legacyPayload));
                    continue;
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
                            "Remove the live runtime legacy bridge from this profile or migrate the dependency fully into v2.",
                            legacyPayload));
                    continue;
                }

                if (legacyCompat.Surface == LegacyAdapterSurface.None)
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.AdapterSurfaceMissing,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must classify which compatibility surface they isolate.",
                        "Declare whether this adapter isolates installer, resolver, command, value, lifecycle, or authoring migration behavior.",
                        legacyPayload));
                    canCreateAdapter = false;
                }

                if (string.IsNullOrWhiteSpace(legacyCompat.LegacySourceType))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.AdapterSourceTypeMissing,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        "Legacy bridge modules must declare the concrete legacy source type they adapt.",
                        "Record the stable legacy runtime or authoring source type behind this compatibility seam.",
                        legacyPayload));
                    canCreateAdapter = false;
                }

                if (canCreateAdapter && !IsSurfaceCompatible(legacyCompat.Kind, legacyCompat.Surface))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.AdapterKindSurfaceMismatch,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        GetKindSurfaceMismatchMessage(legacyCompat.Surface),
                        GetKindSurfaceMismatchFix(legacyCompat.Surface),
                        legacyPayload));
                    canCreateAdapter = false;
                }

                if (canCreateAdapter && !TryValidateExplicitTargets(input, module.Id, legacyCompat.Surface, legacyCompat.ExplicitTargets, out string targetValidationMessage, out string targetValidationFix))
                {
                    issues.Add(CreateIssue(
                        module.Id,
                        module.Source,
                        LegacyCompatBoundaryCodes.AdapterTargetMissing,
                        ValidationSeverity.Error,
                        input.SelectedProfile,
                        targetValidationMessage,
                        targetValidationFix,
                        legacyPayload));
                    canCreateAdapter = false;
                }

                if (!canCreateAdapter || hasBlockingMetadataIssues)
                    continue;

                LegacyRemovalPolicy removalPolicy = new LegacyRemovalPolicy(
                    module.Id,
                    legacyCompat.RemovalStatus,
                    legacyCompat.Profiles,
                    "Legacy bridge declared in dependency input.",
                    legacyCompat.TargetSubsystem,
                    legacyCompat.RemovalCondition!,
                    legacyCompat.DiagnosticsCode ?? "LEGACY_ADAPTER_DIAGNOSTICS_MISSING",
                    legacyCompat.TrackingIssueOrBlockingCondition!);

                adapters.Add(new LegacyAdapterDescriptor(
                    legacyCompat.Kind,
                    module.Id,
                    legacyCompat.LegacySystemName,
                    legacyCompat.LegacySourceType!,
                    legacyCompat.TargetSubsystem,
                    legacyCompat.Surface,
                    legacyCompat.Profiles,
                    module.Source,
                        removalPolicy,
                        explicitTargets: CopyTargets(legacyCompat.ExplicitTargets)));
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
                    "Replace fallback behavior with an explicit migrated target-kernel dependency or fail deterministically.",
                    CreatePayloadEntries(adapter)));
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
                    "Remove the live legacy bridge from this profile or migrate the dependency fully into v2.",
                    CreatePayloadEntries(adapter)));
                return;
            }

            if (IsReleaseProfile(header.SelectedProfileMask) && IsReleaseRuntimeAdapterKind(adapter.Kind))
            {
                issues.Add(CreateIssue(
                    adapter.OwnerModule,
                    adapter.Source,
                    LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden,
                    ValidationSeverity.Error,
                    header.SelectedProfile,
                    "Release profile forbids live runtime legacy adapters.",
                    "Replace the runtime adapter with prevalidated migrated input or a non-runtime migration artifact before shipping Release.",
                    CreatePayloadEntries(adapter)));
                return;
            }

            if (adapter.RemovalPolicy.IsExpired)
            {
                issues.Add(CreateIssue(
                    adapter.OwnerModule,
                    adapter.Source,
                    LegacyCompatBoundaryCodes.AdapterExpired,
                    ValidationSeverity.Error,
                    header.SelectedProfile,
                    "Legacy adapter removal policy has expired.",
                    "Remove the adapter or refresh its removal policy before shipping.",
                    CreatePayloadEntries(adapter)));
                return;
            }

            issues.Add(CreateIssue(
                adapter.OwnerModule,
                adapter.Source,
                adapter.IsRuntimeCapable ? LegacyCompatBoundaryCodes.RuntimeAdapterUsed : LegacyCompatBoundaryCodes.BridgeUsed,
                ValidationSeverity.Warning,
                header.SelectedProfile,
                "Legacy bridge remains active as quarantine-only residue and must stay explicit, observable, profile-bounded, removable, and non-authoritative.",
                "Continue migration until the legacy bridge can be removed from the verified graph.",
                CreatePayloadEntries(adapter)));
        }

        static DependencyValidationIssue CreateIssue(
            ModuleId ownerModule,
            SourceLocationId source,
            string code,
            ValidationSeverity severity,
            string profile,
            string message,
            string suggestedFix,
            DiagnosticPayloadEntry[]? payloadEntries = null)
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
                payloadEntries ?? Array.Empty<DiagnosticPayloadEntry>());
        }

        static bool IsReleaseProfile(KernelProfileMask selectedProfileMask)
        {
            return (selectedProfileMask & KernelProfileMask.Release) != KernelProfileMask.None;
        }

        static bool IsReleaseRuntimeAdapterKind(LegacyCompatKind kind)
        {
            return kind == LegacyCompatKind.RuntimeAdapter
                || kind == LegacyCompatKind.TemporaryBridge;
        }

        static bool IsProfileForbidden(LegacyCompatDescriptorIR legacyCompat, KernelProfileMask selectedProfileMask)
        {
            if (legacyCompat == null)
                throw new ArgumentNullException(nameof(legacyCompat));

            return (legacyCompat.Profiles & selectedProfileMask) == KernelProfileMask.None;
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

        static LegacyRemovalPolicy[] CloneRemovalPolicies(IReadOnlyList<LegacyAdapterDescriptor> source)
        {
            LegacyRemovalPolicy[] clone = new LegacyRemovalPolicy[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index].RemovalPolicy;
            }

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

            comparison = left.Surface.CompareTo(right.Surface);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.TargetSubsystemName, right.TargetSubsystemName);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.LegacySourceType, right.LegacySourceType);
            if (comparison != 0)
                return comparison;

            return CompareTargets(left.ExplicitTargets, right.ExplicitTargets);
        }

        static DiagnosticPayloadEntry[] CreatePayloadEntries(LegacyCompatDescriptorIR legacyCompat)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(10)
            {
                new DiagnosticPayloadEntry("LegacySystemName", DiagnosticPayloadValue.FromString(legacyCompat.LegacySystemName)),
                new DiagnosticPayloadEntry("BridgeKind", DiagnosticPayloadValue.FromString(legacyCompat.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetSubsystem", DiagnosticPayloadValue.FromString(legacyCompat.TargetSubsystem)),
                new DiagnosticPayloadEntry("Profiles", DiagnosticPayloadValue.FromString(legacyCompat.Profiles.ToString())),
                new DiagnosticPayloadEntry("RemovalStatus", DiagnosticPayloadValue.FromString(legacyCompat.RemovalStatus.ToString())),
                new DiagnosticPayloadEntry("ResidueState", DiagnosticPayloadValue.FromString(GetResidueState(legacyCompat.Kind))),
                new DiagnosticPayloadEntry("AuthorityState", DiagnosticPayloadValue.FromString("NonAuthoritative")),
            };

            if (legacyCompat.Surface != LegacyAdapterSurface.None)
                payloadEntries.Add(new DiagnosticPayloadEntry("AdapterSurface", DiagnosticPayloadValue.FromString(legacyCompat.Surface.ToString())));

            if (!string.IsNullOrWhiteSpace(legacyCompat.LegacySourceType))
                payloadEntries.Add(new DiagnosticPayloadEntry("LegacySourceType", DiagnosticPayloadValue.FromString(legacyCompat.LegacySourceType)));

            if (!string.IsNullOrWhiteSpace(legacyCompat.DiagnosticsCode))
                payloadEntries.Add(new DiagnosticPayloadEntry("LegacyDiagnosticsCode", DiagnosticPayloadValue.FromString(legacyCompat.DiagnosticsCode)));

            if (!string.IsNullOrWhiteSpace(legacyCompat.RemovalCondition))
                payloadEntries.Add(new DiagnosticPayloadEntry("RemovalCondition", DiagnosticPayloadValue.FromString(legacyCompat.RemovalCondition)));

            if (!string.IsNullOrWhiteSpace(legacyCompat.TrackingIssueOrBlockingCondition))
                payloadEntries.Add(new DiagnosticPayloadEntry("TrackingIssueOrBlockingCondition", DiagnosticPayloadValue.FromString(legacyCompat.TrackingIssueOrBlockingCondition)));

            if (legacyCompat.ExplicitTargets.Length > 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("ExplicitTargets", DiagnosticPayloadValue.FromString(FormatTargets(legacyCompat.ExplicitTargets))));

            return payloadEntries.ToArray();
        }

        static DiagnosticPayloadEntry[] CreatePayloadEntries(LegacyAdapterDescriptor adapter)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(16)
            {
                new DiagnosticPayloadEntry("LegacySystemName", DiagnosticPayloadValue.FromString(adapter.LegacySystemName)),
                new DiagnosticPayloadEntry("BridgeKind", DiagnosticPayloadValue.FromString(adapter.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetSubsystem", DiagnosticPayloadValue.FromString(adapter.TargetSubsystemName)),
                new DiagnosticPayloadEntry("Profiles", DiagnosticPayloadValue.FromString(adapter.Profiles.ToString())),
                new DiagnosticPayloadEntry("RemovalStatus", DiagnosticPayloadValue.FromString(adapter.RemovalStatus.ToString())),
                new DiagnosticPayloadEntry("ResidueState", DiagnosticPayloadValue.FromString(GetResidueState(adapter.Kind))),
                new DiagnosticPayloadEntry("AuthorityState", DiagnosticPayloadValue.FromString("NonAuthoritative")),
                new DiagnosticPayloadEntry("AdapterSurface", DiagnosticPayloadValue.FromString(adapter.Surface.ToString())),
                new DiagnosticPayloadEntry("LegacySourceType", DiagnosticPayloadValue.FromString(adapter.LegacySourceType)),
                new DiagnosticPayloadEntry("LegacyDiagnosticsCode", DiagnosticPayloadValue.FromString(adapter.DiagnosticsCode)),
                new DiagnosticPayloadEntry("RemovalCondition", DiagnosticPayloadValue.FromString(adapter.RemovalCondition)),
                new DiagnosticPayloadEntry("TrackingIssueOrBlockingCondition", DiagnosticPayloadValue.FromString(adapter.TrackingIssueOrBlockingCondition)),
                new DiagnosticPayloadEntry("ExplicitTargets", DiagnosticPayloadValue.FromString(FormatTargets(adapter.ExplicitTargets))),
            };

            payloadEntries.AddRange(adapter.RemovalPolicy.ToDiagnosticPayloadEntries());

            return payloadEntries.ToArray();
        }

        static string GetResidueState(LegacyCompatKind kind)
        {
            return IsReleaseRuntimeAdapterKind(kind) || kind == LegacyCompatKind.ForbiddenFallback
                ? "QuarantineOnly"
                : "ExplicitCompatOnly";
        }

        static bool IsSurfaceCompatible(LegacyCompatKind kind, LegacyAdapterSurface surface)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.Installer:
                case LegacyAdapterSurface.Authoring:
                    return kind == LegacyCompatKind.AuthoringMigration;

                case LegacyAdapterSurface.Resolver:
                case LegacyAdapterSurface.Command:
                case LegacyAdapterSurface.Lifecycle:
                    return kind == LegacyCompatKind.RuntimeAdapter
                        || kind == LegacyCompatKind.TemporaryBridge
                        || kind == LegacyCompatKind.TestAdapter;

                case LegacyAdapterSurface.Value:
                    return kind == LegacyCompatKind.DataMigration
                        || kind == LegacyCompatKind.RuntimeAdapter
                        || kind == LegacyCompatKind.TemporaryBridge
                        || kind == LegacyCompatKind.TestAdapter;

                case LegacyAdapterSurface.None:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static string GetKindSurfaceMismatchMessage(LegacyAdapterSurface surface)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.Installer:
                case LegacyAdapterSurface.Authoring:
                    return "Installer and authoring migration surfaces must use the AuthoringMigration bridge kind.";

                case LegacyAdapterSurface.Resolver:
                case LegacyAdapterSurface.Command:
                case LegacyAdapterSurface.Lifecycle:
                    return "Resolver, command, and lifecycle migration surfaces cannot use DataMigration; reserve DataMigration for the Value surface.";

                case LegacyAdapterSurface.Value:
                    return "Value migration surfaces cannot use the AuthoringMigration bridge kind.";

                case LegacyAdapterSurface.None:
                    return "Legacy bridge modules must classify which compatibility surface they isolate.";

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static string GetKindSurfaceMismatchFix(LegacyAdapterSurface surface)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.Installer:
                case LegacyAdapterSurface.Authoring:
                    return "Reclassify this adapter as AuthoringMigration or move the runtime seam into a runtime migration surface.";

                case LegacyAdapterSurface.Resolver:
                case LegacyAdapterSurface.Command:
                case LegacyAdapterSurface.Lifecycle:
                    return "Use RuntimeAdapter, TemporaryBridge, or TestAdapter on this surface, or move DataMigration to the Value surface.";

                case LegacyAdapterSurface.Value:
                    return "Use DataMigration, RuntimeAdapter, TemporaryBridge, or TestAdapter for value migration.";

                case LegacyAdapterSurface.None:
                    return "Declare whether this adapter isolates installer, resolver, command, value, lifecycle, or authoring migration behavior.";

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static bool TryValidateExplicitTargets(DependencyValidationInput input, ModuleId ownerModule, LegacyAdapterSurface surface, ReadOnlySpan<DependencyNodeIR> explicitTargets, out string message, out string suggestedFix)
        {
            if (explicitTargets.Length == 0)
            {
                message = "Legacy bridge surface '" + surface + "' requires explicit target nodes in its descriptor.";
                suggestedFix = "Declare at least one " + GetSurfaceTargetRequirement(surface) + " in the legacy adapter descriptor.";
                return false;
            }

            for (int index = 0; index < explicitTargets.Length; index++)
            {
                DependencyNodeIR target = explicitTargets[index];
                if (!IsTargetKindAllowed(surface, target.Kind))
                {
                    message = "Legacy bridge surface '" + surface + "' cannot target dependency node kind '" + target.Kind + "'.";
                    suggestedFix = "Restrict explicit targets to " + GetSurfaceTargetRequirement(surface) + ".";
                    return false;
                }

                if (!IsOwnedExplicitTarget(input, ownerModule, target))
                {
                    message = "Legacy bridge target '" + DescribeDependencyNode(target) + "' must resolve to v2-owned IR emitted by the same adapter module.";
                    suggestedFix = "Emit the declared target from module " + ownerModule.Value + " or correct the adapter descriptor's explicit targets.";
                    return false;
                }
            }

            message = string.Empty;
            suggestedFix = string.Empty;
            return true;
        }

        static string GetSurfaceTargetRequirement(LegacyAdapterSurface surface)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.Installer:
                    return "explicit contribution target nodes (scope, service, command, value, lifecycle step, or runtime query)";

                case LegacyAdapterSurface.Authoring:
                    return "explicit authoring-emitted target nodes (scope, service, command, value, lifecycle step, or runtime query)";

                case LegacyAdapterSurface.Resolver:
                    return "explicit ServiceId targets";

                case LegacyAdapterSurface.Command:
                    return "explicit CommandTypeId targets";

                case LegacyAdapterSurface.Value:
                    return "explicit ValueKeyId targets";

                case LegacyAdapterSurface.Lifecycle:
                    return "explicit LifecycleStepId targets";

                case LegacyAdapterSurface.None:
                    return "classified legacy adapter target";

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static bool IsTargetKindAllowed(LegacyAdapterSurface surface, DependencyNodeKind kind)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.Installer:
                case LegacyAdapterSurface.Authoring:
                    return kind == DependencyNodeKind.Scope
                        || kind == DependencyNodeKind.Service
                        || kind == DependencyNodeKind.Command
                        || kind == DependencyNodeKind.ValueKey
                        || kind == DependencyNodeKind.LifecycleStep
                        || kind == DependencyNodeKind.RuntimeQuery;

                case LegacyAdapterSurface.Resolver:
                    return kind == DependencyNodeKind.Service;

                case LegacyAdapterSurface.Command:
                    return kind == DependencyNodeKind.Command;

                case LegacyAdapterSurface.Value:
                    return kind == DependencyNodeKind.ValueKey;

                case LegacyAdapterSurface.Lifecycle:
                    return kind == DependencyNodeKind.LifecycleStep;

                case LegacyAdapterSurface.None:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static bool IsOwnedExplicitTarget(DependencyValidationInput input, ModuleId ownerModule, DependencyNodeIR target)
        {
            switch (target.Kind)
            {
                case DependencyNodeKind.Scope:
                    return HasOwnedScope(input.Scopes, ownerModule, target.ScopePlanId);

                case DependencyNodeKind.Service:
                    return HasOwnedService(input.Services, ownerModule, target.ServiceId);

                case DependencyNodeKind.Command:
                    return HasOwnedCommand(input.Commands, ownerModule, target.CommandTypeId);

                case DependencyNodeKind.ValueKey:
                    return HasOwnedValueKey(input.ValueKeys, ownerModule, target.ValueKeyId);

                case DependencyNodeKind.LifecycleStep:
                    return HasOwnedLifecycleStep(input.Lifecycles, ownerModule, target.LifecycleStepId);

                case DependencyNodeKind.RuntimeQuery:
                    return HasOwnedRuntimeQuery(input.RuntimeQueries, ownerModule, target.RuntimeQueryId);

                default:
                    return false;
            }
        }

        static bool HasOwnedScope(ReadOnlySpan<ScopeIR> scopes, ModuleId ownerModule, ScopePlanId scopePlanId)
        {
            for (int index = 0; index < scopes.Length; index++)
            {
                if (scopes[index].OwnerModule == ownerModule && scopes[index].PlanId == scopePlanId)
                    return true;
            }

            return false;
        }

        static bool HasOwnedService(ReadOnlySpan<ServiceIR> services, ModuleId ownerModule, ServiceId serviceId)
        {
            for (int index = 0; index < services.Length; index++)
            {
                if (services[index].OwnerModule == ownerModule && services[index].Id == serviceId)
                    return true;
            }

            return false;
        }

        static bool HasOwnedCommand(ReadOnlySpan<CommandIR> commands, ModuleId ownerModule, CommandTypeId commandTypeId)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                if (commands[index].OwnerModule == ownerModule && commands[index].TypeId == commandTypeId)
                    return true;
            }

            return false;
        }

        static bool HasOwnedValueKey(ReadOnlySpan<ValueKeyIR> valueKeys, ModuleId ownerModule, ValueKeyId valueKeyId)
        {
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (valueKeys[index].OwnerModule == ownerModule && valueKeys[index].Id == valueKeyId)
                    return true;
            }

            return false;
        }

        static bool HasOwnedLifecycleStep(ReadOnlySpan<LifecycleIR> lifecycles, ModuleId ownerModule, LifecycleStepId lifecycleStepId)
        {
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                if (lifecycles[lifecycleIndex].OwnerModule != ownerModule)
                    continue;

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (steps[stepIndex].Id == lifecycleStepId)
                        return true;
                }
            }

            return false;
        }

        static bool HasOwnedRuntimeQuery(ReadOnlySpan<RuntimeQueryIR> runtimeQueries, ModuleId ownerModule, RuntimeQueryId runtimeQueryId)
        {
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (runtimeQueries[index].OwnerModule == ownerModule && runtimeQueries[index].Id == runtimeQueryId)
                    return true;
            }

            return false;
        }

        static DependencyNodeIR[] CopyTargets(ReadOnlySpan<DependencyNodeIR> source)
        {
            if (source.Length == 0)
                return Array.Empty<DependencyNodeIR>();

            DependencyNodeIR[] clone = new DependencyNodeIR[source.Length];
            for (int index = 0; index < source.Length; index++)
                clone[index] = source[index];

            return clone;
        }

        static int CompareTargets(ReadOnlySpan<DependencyNodeIR> left, ReadOnlySpan<DependencyNodeIR> right)
        {
            int lengthComparison = left.Length.CompareTo(right.Length);
            int maxLength = left.Length < right.Length ? left.Length : right.Length;
            for (int index = 0; index < maxLength; index++)
            {
                int comparison = left[index].Kind.CompareTo(right[index].Kind);
                if (comparison != 0)
                    return comparison;

                comparison = DescribeDependencyNode(left[index]).CompareTo(DescribeDependencyNode(right[index]));
                if (comparison != 0)
                    return comparison;
            }

            if (lengthComparison != 0)
                return lengthComparison;

            return 0;
        }

        static string FormatTargets(ReadOnlySpan<DependencyNodeIR> targets)
        {
            if (targets.Length == 0)
                return "<none>";

            string[] parts = new string[targets.Length];
            for (int index = 0; index < targets.Length; index++)
                parts[index] = DescribeDependencyNode(targets[index]);

            return string.Join(",", parts);
        }

        static string DescribeDependencyNode(DependencyNodeIR node)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Scope:
                    return "Scope:" + node.ScopePlanId.Value;

                case DependencyNodeKind.Service:
                    return "Service:" + node.ServiceId.Value;

                case DependencyNodeKind.Command:
                    return "Command:" + node.CommandTypeId.Value;

                case DependencyNodeKind.ValueKey:
                    return "ValueKey:" + node.ValueKeyId.Value;

                case DependencyNodeKind.LifecycleStep:
                    return "LifecycleStep:" + node.LifecycleStepId.Value;

                case DependencyNodeKind.RuntimeQuery:
                    return "RuntimeQuery:" + node.RuntimeQueryId.Value;

                case DependencyNodeKind.Module:
                    return "Module:" + node.ModuleId.Value;

                default:
                    return node.Kind + ":unknown";
            }
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
                        throw new ArgumentOutOfRangeException(nameof(source), source[index].Severity, "Unsupported validation severity.");
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