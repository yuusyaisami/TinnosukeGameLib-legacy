#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Validation;

namespace Game.Kernel.Boot
{
    public enum BootValidationGateKind
    {
        ManifestMissing = 10,
        ArtifactSetMissing = 20,
        SelectedProfileMissing = 30,
        ProfileMismatch = 40,
        ArtifactSetIncomplete = 50,
        ArtifactHeadersIncompatible = 60,
        ArtifactStale = 70,
        KernelIRHashMismatch = 80,
        RegistryHashMismatch = 90,
        ProfileHashMismatch = 100,
        DebugMapHashMismatch = 110,
        DebugMapMissing = 120,
        DependencyValidationFailed = 130,
        RequiredRootServiceMissing = 140,
        RequiredRootScopeMissing = 150,
        LegacyFallbackForbidden = 160,
        TestNonDeterministicPolicy = 170,
        RuntimeDiscoveryForbidden = 180,
        ResourcesFallbackForbidden = 190,
        DefaultRootCreationForbidden = 200,
        DuplicateRootCleanupForbidden = 210,
    }

    public static class BootValidationCodes
    {
        public const string ManifestMissing = "BOOT_MANIFEST_MISSING";
        public const string ArtifactSetMissing = "BOOT_ARTIFACT_SET_MISSING";
        public const string SelectedProfileMissing = "BOOT_SELECTED_PROFILE_MISSING";
        public const string ProfileMismatch = "BOOT_PROFILE_MISMATCH";
        public const string ArtifactSetIncomplete = "BOOT_ARTIFACT_SET_INCOMPLETE";
        public const string ArtifactHeadersIncompatible = "BOOT_ARTIFACT_HEADERS_INCOMPATIBLE";
        public const string ArtifactStale = "BOOT_ARTIFACT_STALE";
        public const string KernelIRHashMismatch = "BOOT_KERNEL_IR_HASH_MISMATCH";
        public const string RegistryHashMismatch = "BOOT_REGISTRY_HASH_MISMATCH";
        public const string ProfileHashMismatch = "BOOT_PROFILE_HASH_MISMATCH";
        public const string DebugMapHashMismatch = "BOOT_DEBUGMAP_HASH_MISMATCH";
        public const string DebugMapMissing = "BOOT_DEBUGMAP_MISSING";
        public const string DependencyValidationFailed = "BOOT_DEPENDENCY_VALIDATION_FAILED";
        public const string RequiredRootServiceMissing = "BOOT_REQUIRED_ROOT_SERVICE_MISSING";
        public const string RequiredRootScopeMissing = "BOOT_REQUIRED_ROOT_SCOPE_MISSING";
        public const string LegacyFallbackForbidden = "BOOT_LEGACY_FALLBACK_FORBIDDEN";
        public const string TestNonDeterministicPolicy = "BOOT_TEST_NON_DETERMINISTIC_POLICY";
        public const string RuntimeDiscoveryForbidden = "BOOT_RUNTIME_DISCOVERY_FORBIDDEN";
        public const string ResourcesFallbackForbidden = "BOOT_RESOURCES_FALLBACK_FORBIDDEN";
        public const string DefaultRootCreationForbidden = "BOOT_DEFAULT_ROOT_CREATION_FORBIDDEN";
        public const string DuplicateRootCleanupForbidden = "BOOT_DUPLICATE_ROOT_CLEANUP_FORBIDDEN";
    }

    public sealed class BootValidationIssue
    {
        public BootValidationIssue(
            string code,
            ValidationSeverity severity,
            BootValidationGateKind gate,
            string message,
            string? suggestedFix = null,
            RuntimeIdentityRef? subjectIdentity = null,
            string? expectedValue = null,
            string? actualValue = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Boot validation issues must provide a stable code.", nameof(code));

            if (severity == default)
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Boot validation issues must provide a defined severity.");

            if (gate == default)
                throw new ArgumentOutOfRangeException(nameof(gate), gate, "Boot validation issues must provide a defined gate kind.");

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Boot validation issues must provide a message.", nameof(message));

            if (suggestedFix != null && string.IsNullOrWhiteSpace(suggestedFix))
                throw new ArgumentException("Boot validation issue suggested fixes must be null or non-empty.", nameof(suggestedFix));

            if (expectedValue != null && string.IsNullOrWhiteSpace(expectedValue))
                throw new ArgumentException("Boot validation issue expected values must be null or non-empty.", nameof(expectedValue));

            if (actualValue != null && string.IsNullOrWhiteSpace(actualValue))
                throw new ArgumentException("Boot validation issue actual values must be null or non-empty.", nameof(actualValue));

            Code = code;
            Severity = severity;
            Gate = gate;
            Message = message;
            SuggestedFix = suggestedFix;
            SubjectIdentity = subjectIdentity;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
        }

        public string Code { get; }

        public ValidationSeverity Severity { get; }

        public BootValidationGateKind Gate { get; }

        public string Message { get; }

        public string? SuggestedFix { get; }

        public RuntimeIdentityRef? SubjectIdentity { get; }

        public string? ExpectedValue { get; }

        public string? ActualValue { get; }

        public KernelDiagnostic ToKernelDiagnostic(KernelBootManifest? manifest = null, KernelProfile? selectedProfile = null)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(2);
            if (SubjectIdentity.HasValue)
                runtimeIdentities.Add(SubjectIdentity.Value);

            DiagnosticContext context = new DiagnosticContext(
                runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(),
                artifact: manifest == null ? default : new ArtifactIdentityRef(manifest.ArtifactSet.ArtifactSetId.Value),
                profileId: selectedProfile == null ? 0 : selectedProfile.Id.Value,
                phase: "Boot");

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(8)
            {
                new DiagnosticPayloadEntry("BootGate", DiagnosticPayloadValue.FromString(Gate.ToString())),
            };

            if (manifest != null)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(manifest.ManifestId.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(manifest.ArtifactSet.ArtifactSetId.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(manifest.BootPolicyId.Value)));
            }

            if (selectedProfile != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("SelectedProfileId", DiagnosticPayloadValue.FromInt32(selectedProfile.Id.Value)));

            if (ExpectedValue != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("ExpectedValue", DiagnosticPayloadValue.FromString(ExpectedValue)));

            if (ActualValue != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("ActualValue", DiagnosticPayloadValue.FromString(ActualValue)));

            if (SuggestedFix != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("SuggestedFix", DiagnosticPayloadValue.FromString(SuggestedFix)));

            return new KernelDiagnostic(
                new DiagnosticCode(Code),
                MapSeverity(Severity),
                DiagnosticDomain.Boot,
                DiagnosticFailureBoundary.Kernel,
                Message,
                context,
                new DiagnosticPayload(payloadEntries));
        }

        public override string ToString()
        {
            return "BootValidationIssue(Code=" + Code + ", Severity=" + Severity + ", Gate=" + Gate + ", Message=" + Message + ")";
        }

        static DiagnosticSeverity MapSeverity(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Info => DiagnosticSeverity.Info,
                ValidationSeverity.Warning => DiagnosticSeverity.Warning,
                ValidationSeverity.Error => DiagnosticSeverity.Error,
                ValidationSeverity.Fatal => DiagnosticSeverity.Fatal,
                _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported validation severity."),
            };
        }
    }

    public sealed class BootValidationSummary
    {
        BootValidationSummary(int infoCount, int warningCount, int errorCount, int fatalCount)
        {
            InfoCount = infoCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            FatalCount = fatalCount;
        }

        public int InfoCount { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public int FatalCount { get; }

        public static BootValidationSummary FromIssues(IReadOnlyList<BootValidationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            int infoCount = 0;
            int warningCount = 0;
            int errorCount = 0;
            int fatalCount = 0;

            for (int index = 0; index < issues.Count; index++)
            {
                BootValidationIssue issue = issues[index] ?? throw new ArgumentException("Boot validation issue collections must not contain null items.", nameof(issues));
                switch (issue.Severity)
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
                        throw new ArgumentOutOfRangeException(nameof(issues), issue.Severity, "Boot validation issue collections must contain only defined severities.");
                }
            }

            return new BootValidationSummary(infoCount, warningCount, errorCount, fatalCount);
        }
    }

    public sealed class BootValidationReport
    {
        readonly ReadOnlyCollection<BootValidationIssue> issues;

        public BootValidationReport(KernelBootManifest? manifest, KernelProfile? selectedProfile, IReadOnlyList<BootValidationIssue>? issues)
        {
            Manifest = manifest;
            SelectedProfile = selectedProfile;

            BootValidationIssue[] snapshot = issues == null || issues.Count == 0
                ? Array.Empty<BootValidationIssue>()
                : CloneIssues(issues);

            this.issues = Array.AsReadOnly(snapshot);
            Summary = BootValidationSummary.FromIssues(snapshot);
            Status = DeriveStatus(Summary);
        }

        public KernelBootManifest? Manifest { get; }

        public KernelProfile? SelectedProfile { get; }

        public ValidationResultStatus Status { get; }

        public IReadOnlyList<BootValidationIssue> Issues => issues;

        public BootValidationSummary Summary { get; }

        public bool HasBlockingIssues => Status == ValidationResultStatus.Failed || Status == ValidationResultStatus.Fatal;

        static BootValidationIssue[] CloneIssues(IReadOnlyList<BootValidationIssue> issues)
        {
            BootValidationIssue[] snapshot = new BootValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                snapshot[index] = issues[index] ?? throw new ArgumentException("Boot validation report issue collections must not contain null items.", nameof(issues));
            }

            return snapshot;
        }

        static ValidationResultStatus DeriveStatus(BootValidationSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (summary.FatalCount > 0)
                return ValidationResultStatus.Fatal;

            if (summary.ErrorCount > 0)
                return ValidationResultStatus.Failed;

            if (summary.WarningCount > 0)
                return ValidationResultStatus.PassedWithWarnings;

            return ValidationResultStatus.Passed;
        }
    }

    public sealed class BootArtifactValidationState
    {
        public BootArtifactValidationState(
            bool artifactSetComplete,
            bool artifactHeadersCompatible,
            bool artifactStale,
            bool debugMapRequired,
            string? kernelIRHash = null,
            string? registryHash = null,
            string? profileHash = null,
            string? debugMapHash = null)
        {
            if (kernelIRHash != null && string.IsNullOrWhiteSpace(kernelIRHash))
                throw new ArgumentException("Artifact validation hashes must be null or non-empty.", nameof(kernelIRHash));

            if (registryHash != null && string.IsNullOrWhiteSpace(registryHash))
                throw new ArgumentException("Artifact validation hashes must be null or non-empty.", nameof(registryHash));

            if (profileHash != null && string.IsNullOrWhiteSpace(profileHash))
                throw new ArgumentException("Artifact validation hashes must be null or non-empty.", nameof(profileHash));

            if (debugMapHash != null && string.IsNullOrWhiteSpace(debugMapHash))
                throw new ArgumentException("Artifact validation hashes must be null or non-empty.", nameof(debugMapHash));

            ArtifactSetComplete = artifactSetComplete;
            ArtifactHeadersCompatible = artifactHeadersCompatible;
            ArtifactStale = artifactStale;
            DebugMapRequired = debugMapRequired;
            KernelIRHash = kernelIRHash;
            RegistryHash = registryHash;
            ProfileHash = profileHash;
            DebugMapHash = debugMapHash;
        }

        public bool ArtifactSetComplete { get; }

        public bool ArtifactHeadersCompatible { get; }

        public bool ArtifactStale { get; }

        public bool DebugMapRequired { get; }

        public string? KernelIRHash { get; }

        public string? RegistryHash { get; }

        public string? ProfileHash { get; }

        public string? DebugMapHash { get; }
    }

    public sealed class BootRootValidationState
    {
        readonly RuntimeIdentityRef[] requiredRootServices;
        readonly RuntimeIdentityRef[] availableRootServices;
        readonly RuntimeIdentityRef[] requiredRootScopes;
        readonly RuntimeIdentityRef[] availableRootScopes;

        public BootRootValidationState(
            RuntimeIdentityRef[]? requiredRootServices,
            RuntimeIdentityRef[]? availableRootServices,
            RuntimeIdentityRef[]? requiredRootScopes,
            RuntimeIdentityRef[]? availableRootScopes)
        {
            this.requiredRootServices = CloneIdentities(requiredRootServices, RuntimeIdentityKind.Service, nameof(requiredRootServices));
            this.availableRootServices = CloneIdentities(availableRootServices, RuntimeIdentityKind.Service, nameof(availableRootServices));
            this.requiredRootScopes = CloneIdentities(requiredRootScopes, RuntimeIdentityKind.ScopePlan, nameof(requiredRootScopes));
            this.availableRootScopes = CloneIdentities(availableRootScopes, RuntimeIdentityKind.ScopePlan, nameof(availableRootScopes));

            ValidateUniqueIdentities(this.requiredRootServices, nameof(requiredRootServices));
            ValidateUniqueIdentities(this.availableRootServices, nameof(availableRootServices));
            ValidateUniqueIdentities(this.requiredRootScopes, nameof(requiredRootScopes));
            ValidateUniqueIdentities(this.availableRootScopes, nameof(availableRootScopes));
        }

        public ReadOnlySpan<RuntimeIdentityRef> RequiredRootServices => requiredRootServices;

        public ReadOnlySpan<RuntimeIdentityRef> AvailableRootServices => availableRootServices;

        public ReadOnlySpan<RuntimeIdentityRef> RequiredRootScopes => requiredRootScopes;

        public ReadOnlySpan<RuntimeIdentityRef> AvailableRootScopes => availableRootScopes;

        static RuntimeIdentityRef[] CloneIdentities(RuntimeIdentityRef[]? source, RuntimeIdentityKind requiredKind, string paramName)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] clone = new RuntimeIdentityRef[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                RuntimeIdentityRef identity = source[index];
                if (identity.Kind != requiredKind)
                    throw new ArgumentException("Boot root validation identities must use the expected runtime identity kind.", paramName);

                if (identity.Kind == RuntimeIdentityKind.None || identity.Value == 0)
                    throw new ArgumentException("Boot root validation identities must be fully specified.", paramName);

                clone[index] = identity;
            }

            return clone;
        }

        static void ValidateUniqueIdentities(ReadOnlySpan<RuntimeIdentityRef> identities, string paramName)
        {
            HashSet<RuntimeIdentityRef> seen = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < identities.Length; index++)
            {
                if (!seen.Add(identities[index]))
                    throw new ArgumentException("Boot root validation identities must not contain duplicates.", paramName);
            }
        }
    }

    public sealed class BootFallbackValidationState
    {
        public BootFallbackValidationState(
            bool legacyFallbackAttempted,
            bool runtimeDiscoveryAttempted,
            bool resourcesFallbackAttempted,
            bool defaultRootCreationAttempted,
            bool duplicateRootCleanupAttempted,
            bool nonDeterministicTestPolicy)
        {
            LegacyFallbackAttempted = legacyFallbackAttempted;
            RuntimeDiscoveryAttempted = runtimeDiscoveryAttempted;
            ResourcesFallbackAttempted = resourcesFallbackAttempted;
            DefaultRootCreationAttempted = defaultRootCreationAttempted;
            DuplicateRootCleanupAttempted = duplicateRootCleanupAttempted;
            NonDeterministicTestPolicy = nonDeterministicTestPolicy;
        }

        public bool LegacyFallbackAttempted { get; }

        public bool RuntimeDiscoveryAttempted { get; }

        public bool ResourcesFallbackAttempted { get; }

        public bool DefaultRootCreationAttempted { get; }

        public bool DuplicateRootCleanupAttempted { get; }

        public bool NonDeterministicTestPolicy { get; }
    }

    public sealed class BootValidationInput
    {
        public BootValidationInput(
            KernelBootManifest? manifest,
            KernelProfile? selectedProfile,
            bool artifactSetReferencePresent,
            ValidationResultStatus dependencyValidationStatus,
            BootArtifactValidationState artifactState,
            BootRootValidationState rootState,
            BootFallbackValidationState fallbackState)
        {
            ArtifactState = artifactState ?? throw new ArgumentNullException(nameof(artifactState));
            RootState = rootState ?? throw new ArgumentNullException(nameof(rootState));
            FallbackState = fallbackState ?? throw new ArgumentNullException(nameof(fallbackState));

            Manifest = manifest;
            SelectedProfile = selectedProfile;
            ArtifactSetReferencePresent = artifactSetReferencePresent;
            DependencyValidationStatus = dependencyValidationStatus;
        }

        public KernelBootManifest? Manifest { get; }

        public KernelProfile? SelectedProfile { get; }

        public bool ArtifactSetReferencePresent { get; }

        public ValidationResultStatus DependencyValidationStatus { get; }

        public BootArtifactValidationState ArtifactState { get; }

        public BootRootValidationState RootState { get; }

        public BootFallbackValidationState FallbackState { get; }
    }

    public static class BootValidator
    {
        public static BootValidationReport Validate(BootValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            List<BootValidationIssue> issues = new List<BootValidationIssue>();

            KernelBootManifest? manifest = input.Manifest;
            KernelProfile? selectedProfile = input.SelectedProfile;

            if (manifest == null)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ManifestMissing,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ManifestMissing,
                    "Boot manifest reference is missing.",
                    "Provide a verified boot manifest before booting."));
            }

            if (!input.ArtifactSetReferencePresent)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ArtifactSetMissing,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ArtifactSetMissing,
                    "Verified artifact set reference is missing.",
                    "Load the verified artifact set reference from the manifest before booting."));
            }

            if (selectedProfile == null)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.SelectedProfileMissing,
                    ValidationSeverity.Error,
                    BootValidationGateKind.SelectedProfileMissing,
                    "Selected kernel profile is missing.",
                    "Select a verified kernel profile before booting."));
            }

            if (manifest != null && selectedProfile != null && manifest.ProfileId != selectedProfile.Id)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ProfileMismatch,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ProfileMismatch,
                    "Boot manifest profile does not match the selected profile.",
                    "Regenerate the boot manifest for the selected profile or select the matching profile.",
                    new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value),
                    expectedValue: manifest.ProfileId.Value.ToString(),
                    actualValue: selectedProfile.Id.Value.ToString()));
            }

            if (input.ArtifactState.ArtifactStale)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ArtifactStale,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ArtifactStale,
                    "Verified artifact set is stale.",
                    "Regenerate the artifact set with the current generator version.",
                    manifest == null ? null : new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value)));
            }

            if (!input.ArtifactState.ArtifactSetComplete)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ArtifactSetIncomplete,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ArtifactSetIncomplete,
                    "Verified artifact set is incomplete.",
                    "Regenerate the missing projections before booting.",
                    manifest == null ? null : new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value)));
            }

            if (!input.ArtifactState.ArtifactHeadersCompatible)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ArtifactHeadersIncompatible,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ArtifactHeadersIncompatible,
                    "Artifact headers are incompatible with boot.",
                    "Regenerate the verified artifact set with compatible headers.",
                    manifest == null ? null : new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value)));
            }

            bool debugMapRequired = input.ArtifactState.DebugMapRequired
                || selectedProfile == null
                || selectedProfile.Kind == KernelProfileKind.Development
                || selectedProfile.Kind == KernelProfileKind.Test;

            if (manifest != null)
            {
                ValidateKernelIRHash(input, manifest, selectedProfile, issues);
                ValidateRegistryHash(input, manifest, selectedProfile, issues);

                if (selectedProfile != null)
                    ValidateProfileHash(input, manifest, selectedProfile, issues);

                if (debugMapRequired)
                {
                    if (string.IsNullOrWhiteSpace(input.ArtifactState.DebugMapHash))
                    {
                        issues.Add(new BootValidationIssue(
                            BootValidationCodes.DebugMapMissing,
                            GetBlockingSeverity(selectedProfile),
                            BootValidationGateKind.DebugMapMissing,
                            "Debug map is required for boot but is missing.",
                            "Regenerate the verified artifact set with DebugMap coverage.",
                            new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value)));
                    }
                    else
                    {
                        ValidateDebugMapHash(input, manifest, selectedProfile, issues);
                    }
                }
            }

            if (input.DependencyValidationStatus != ValidationResultStatus.Passed
                && input.DependencyValidationStatus != ValidationResultStatus.PassedWithWarnings)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.DependencyValidationFailed,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.DependencyValidationFailed,
                    "Dependency validation failed before boot.",
                    "Fix dependency validation errors before booting."));
            }

            ValidateRequiredRootServices(input, selectedProfile, issues);
            ValidateRequiredRootScopes(input, selectedProfile, issues);

            if (input.FallbackState.LegacyFallbackAttempted)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.LegacyFallbackForbidden,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.LegacyFallbackForbidden,
                    "Legacy fallback is forbidden for boot.",
                    "Replace legacy fallback with a verified LegacyCompat boot bridge or remove the fallback path."));
            }

            if (input.FallbackState.NonDeterministicTestPolicy && selectedProfile != null && selectedProfile.Kind == KernelProfileKind.Test)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.TestNonDeterministicPolicy,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.TestNonDeterministicPolicy,
                    "Test profile boot policy is non-deterministic.",
                    "Make the test boot path deterministic before booting."));
            }

            if (input.FallbackState.RuntimeDiscoveryAttempted)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.RuntimeDiscoveryForbidden,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.RuntimeDiscoveryForbidden,
                    "Runtime discovery is forbidden during boot.",
                    "Remove scene-wide discovery and pass verified boot inputs explicitly."));
            }

            if (input.FallbackState.ResourcesFallbackAttempted)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ResourcesFallbackForbidden,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ResourcesFallbackForbidden,
                    "Resources fallback is forbidden for required boot inputs.",
                    "Provide the required boot input through verified manifest references instead of Resources.Load."));
            }

            if (input.FallbackState.DefaultRootCreationAttempted)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.DefaultRootCreationForbidden,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.DefaultRootCreationForbidden,
                    "Default root creation is forbidden during boot.",
                    "Provide the required root scope through verified boot inputs instead of creating a default root."));
            }

            if (input.FallbackState.DuplicateRootCleanupAttempted)
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.DuplicateRootCleanupForbidden,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.DuplicateRootCleanupForbidden,
                    "Duplicate root cleanup is forbidden during boot.",
                    "Remove duplicate root sources before booting instead of destroying runtime objects."));
            }

            return new BootValidationReport(manifest, selectedProfile, issues);
        }

        static void ValidateKernelIRHash(BootValidationInput input, KernelBootManifest manifest, KernelProfile? selectedProfile, List<BootValidationIssue> issues)
        {
            string expected = manifest.ArtifactSet.KernelIRHash;
            string? actual = input.ArtifactState.KernelIRHash;

            if (!StringComparer.OrdinalIgnoreCase.Equals(expected, actual))
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.KernelIRHashMismatch,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.KernelIRHashMismatch,
                    "Kernel IR hash does not match the verified artifact set.",
                    "Regenerate the artifact set from the current Kernel IR.",
                    new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value),
                    expected,
                    actual));
            }
        }

        static void ValidateRegistryHash(BootValidationInput input, KernelBootManifest manifest, KernelProfile? selectedProfile, List<BootValidationIssue> issues)
        {
            string? expected = manifest.ArtifactSet.RegistryHash;
            if (expected == null)
                return;

            string? actual = input.ArtifactState.RegistryHash;
            if (!StringComparer.OrdinalIgnoreCase.Equals(expected, actual))
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.RegistryHashMismatch,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.RegistryHashMismatch,
                    "Registry hash does not match the verified artifact set.",
                    "Regenerate the artifact set from the current registry projection.",
                    new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value),
                    expected,
                    actual));
            }
        }

        static void ValidateProfileHash(BootValidationInput input, KernelBootManifest manifest, KernelProfile selectedProfile, List<BootValidationIssue> issues)
        {
            string expected = manifest.ArtifactSet.ProfileHash;
            string? actual = input.ArtifactState.ProfileHash;

            if (!StringComparer.OrdinalIgnoreCase.Equals(expected, actual))
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.ProfileHashMismatch,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.ProfileHashMismatch,
                    "Profile hash does not match the verified artifact set.",
                    "Regenerate the artifact set for the selected profile.",
                    new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value),
                    expected,
                    actual));
            }
        }

        static void ValidateDebugMapHash(BootValidationInput input, KernelBootManifest manifest, KernelProfile? selectedProfile, List<BootValidationIssue> issues)
        {
            string? expected = manifest.ArtifactSet.DebugMapHash;
            string? actual = input.ArtifactState.DebugMapHash;

            if (expected == null || !StringComparer.OrdinalIgnoreCase.Equals(expected, actual))
            {
                issues.Add(new BootValidationIssue(
                    BootValidationCodes.DebugMapHashMismatch,
                    GetBlockingSeverity(selectedProfile),
                    BootValidationGateKind.DebugMapHashMismatch,
                    "Debug map hash does not match the verified artifact set.",
                    "Regenerate the debug map or update the boot manifest reference.",
                    new RuntimeIdentityRef(RuntimeIdentityKind.ArtifactSet, manifest.ArtifactSet.ArtifactSetId.Value),
                    expected,
                    actual));
            }
        }

        static void ValidateRequiredRootServices(BootValidationInput input, KernelProfile? selectedProfile, List<BootValidationIssue> issues)
        {
            ReadOnlySpan<RuntimeIdentityRef> required = input.RootState.RequiredRootServices;
            if (required.Length == 0)
                return;

            ReadOnlySpan<RuntimeIdentityRef> available = input.RootState.AvailableRootServices;

            for (int index = 0; index < required.Length; index++)
            {
                RuntimeIdentityRef requiredIdentity = required[index];
                if (!ContainsIdentity(available, requiredIdentity))
                {
                    issues.Add(new BootValidationIssue(
                        BootValidationCodes.RequiredRootServiceMissing,
                        GetBlockingSeverity(selectedProfile),
                        BootValidationGateKind.RequiredRootServiceMissing,
                        "Required root service is missing from the validated service projection.",
                        "Add the required service to the verified boot input set.",
                        requiredIdentity));
                }
            }
        }

        static void ValidateRequiredRootScopes(BootValidationInput input, KernelProfile? selectedProfile, List<BootValidationIssue> issues)
        {
            ReadOnlySpan<RuntimeIdentityRef> required = input.RootState.RequiredRootScopes;
            if (required.Length == 0)
                return;

            ReadOnlySpan<RuntimeIdentityRef> available = input.RootState.AvailableRootScopes;

            for (int index = 0; index < required.Length; index++)
            {
                RuntimeIdentityRef requiredIdentity = required[index];
                if (!ContainsIdentity(available, requiredIdentity))
                {
                    issues.Add(new BootValidationIssue(
                        BootValidationCodes.RequiredRootScopeMissing,
                        GetBlockingSeverity(selectedProfile),
                        BootValidationGateKind.RequiredRootScopeMissing,
                        "Required root scope is missing from the validated scope projection.",
                        "Add the required scope to the verified boot input set.",
                        requiredIdentity));
                }
            }
        }

        static bool ContainsIdentity(ReadOnlySpan<RuntimeIdentityRef> values, RuntimeIdentityRef target)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == target)
                    return true;
            }

            return false;
        }

        static ValidationSeverity GetBlockingSeverity(KernelProfile? profile)
        {
            if (profile == null)
                return ValidationSeverity.Error;

            return profile.Kind == KernelProfileKind.Development
                ? ValidationSeverity.Error
                : ValidationSeverity.Fatal;
        }
    }
}