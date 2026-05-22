#nullable enable
using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;

namespace Game.Kernel.Boot
{
    public sealed class KernelBootPublishedArtifactBundle
    {
        readonly RuntimeIdentityRef[] requiredRootServices;
        readonly RuntimeIdentityRef[] availableRootServices;
        readonly RuntimeIdentityRef[] requiredRootScopes;
        readonly RuntimeIdentityRef[] availableRootScopes;

        public KernelBootPublishedArtifactBundle(
            KernelBootManifest manifest,
            KernelProfile selectedProfile,
            ServiceGraphPlan serviceGraphPlan,
            ScopeGraphPlan scopeGraphPlan,
            LifecyclePlan? lifecyclePlan,
            KernelDebugMap debugMap,
            RuntimeIdentityRef[]? requiredRootServices = null,
            RuntimeIdentityRef[]? availableRootServices = null,
            RuntimeIdentityRef[]? requiredRootScopes = null,
            RuntimeIdentityRef[]? availableRootScopes = null,
            CommandCatalogPlan? commandCatalogPlan = null,
            ValueSchemaPlan? valueSchemaPlan = null,
            RuntimeQueryPlan? runtimeQueryPlan = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            SelectedProfile = selectedProfile ?? throw new ArgumentNullException(nameof(selectedProfile));
            ServiceGraphPlan = serviceGraphPlan ?? throw new ArgumentNullException(nameof(serviceGraphPlan));
            ScopeGraphPlan = scopeGraphPlan ?? throw new ArgumentNullException(nameof(scopeGraphPlan));
            LifecyclePlan = lifecyclePlan;
            DebugMap = debugMap ?? throw new ArgumentNullException(nameof(debugMap));
            CommandCatalogPlan = commandCatalogPlan;
            ValueSchemaPlan = valueSchemaPlan;
            RuntimeQueryPlan = runtimeQueryPlan;

            this.requiredRootServices = CloneIdentities(requiredRootServices);
            this.availableRootServices = CloneIdentities(availableRootServices);
            this.requiredRootScopes = CloneIdentities(requiredRootScopes);
            this.availableRootScopes = CloneIdentities(availableRootScopes);
        }

        public KernelBootManifest Manifest { get; }

        public KernelProfile SelectedProfile { get; }

        public ServiceGraphPlan ServiceGraphPlan { get; }

        public ScopeGraphPlan ScopeGraphPlan { get; }

        public LifecyclePlan? LifecyclePlan { get; }

        public KernelDebugMap DebugMap { get; }

        public CommandCatalogPlan? CommandCatalogPlan { get; }

        public ValueSchemaPlan? ValueSchemaPlan { get; }

        public RuntimeQueryPlan? RuntimeQueryPlan { get; }

        public BootArtifactValidationState CreateArtifactState()
        {
            return new BootArtifactValidationState(
                artifactSetComplete: true,
                artifactHeadersCompatible: true,
                artifactStale: false,
                debugMapRequired: true,
                kernelIRHash: Manifest.ArtifactSet.KernelIRHash,
                registryHash: Manifest.ArtifactSet.RegistryHash,
                profileHash: Manifest.ArtifactSet.ProfileHash,
                debugMapHash: Manifest.ArtifactSet.DebugMapHash);
        }

        public BootRootValidationState CreateRootState()
        {
            return new BootRootValidationState(
                requiredRootServices,
                availableRootServices,
                requiredRootScopes,
                availableRootScopes);
        }

        public BootValidationInput CreateValidationInput(
            BootFallbackValidationState fallbackState,
            ValidationResultStatus dependencyValidationStatus = ValidationResultStatus.Passed)
        {
            return new BootValidationInput(
                Manifest,
                SelectedProfile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus,
                CreateArtifactState(),
                CreateRootState(),
                fallbackState ?? throw new ArgumentNullException(nameof(fallbackState)),
                ScopeGraphPlan,
                ServiceGraphPlan,
                LifecyclePlan,
                DebugMap,
                CommandCatalogPlan,
                ValueSchemaPlan,
                RuntimeQueryPlan,
                BootRequiredProjectionKind.All);
        }

        static RuntimeIdentityRef[] CloneIdentities(RuntimeIdentityRef[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] clone = new RuntimeIdentityRef[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clone[index] = source[index];
            }

            return clone;
        }
    }

    public static class KernelBootPublishedArtifactBundleFactory
    {
        public static KernelBootPublishedArtifactBundle CreateMinimal(
            KernelProfile selectedProfile,
            ManifestId manifestId,
            BootPolicyId bootPolicyId,
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            RuntimeIdentityRef[]? requiredRootServices = null,
            RuntimeIdentityRef[]? availableRootServices = null,
            RuntimeIdentityRef[]? requiredRootScopes = null,
            RuntimeIdentityRef[]? availableRootScopes = null)
        {
            if (selectedProfile == null)
                throw new ArgumentNullException(nameof(selectedProfile));

            if (selectedProfile.Kind != KernelProfileKind.Development)
                throw new InvalidOperationException("Synthetic live-boot published bundles are development-only. Use explicit published artifacts for release or test profiles.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Published boot bundles must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Published boot bundles must provide a generator version.", nameof(generatorVersion));

            Hash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelLiveBoot/Minimal/Source",
                "PlanId:" + planId.Value,
                "ArtifactSetId:" + artifactSetId.Value,
            });

            Hash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelLiveBoot/Minimal/Registry",
                "FormatVersion:" + formatVersion,
            });

            Hash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelLiveBoot/Minimal/Profile",
                "ProfileId:" + selectedProfile.Id.Value,
                "ProfileKind:" + selectedProfile.Kind,
            });

            ServiceIR[] services = Array.Empty<ServiceIR>();
            ScopeIR[] scopes = Array.Empty<ScopeIR>();
            CommandIR[] commands = Array.Empty<CommandIR>();
            ValueKeyIR[] valueKeys = Array.Empty<ValueKeyIR>();
            RuntimeQueryIR[] runtimeQueries = Array.Empty<RuntimeQueryIR>();
            KernelDebugMapEntry[] debugMapEntries = Array.Empty<KernelDebugMapEntry>();

            Hash128 serviceGraphHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            Hash128 scopeGraphHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);
            Hash128 commandCatalogHash = KernelProjectionHashing.ComputeCommandCatalogHash(commands);
            Hash128 valueSchemaHash = KernelProjectionHashing.ComputeValueSchemaHash(valueKeys);
            Hash128 runtimeQueryHash = KernelProjectionHashing.ComputeRuntimeQueryHash(runtimeQueries);
            Hash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugMapEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(1),
                    ArtifactKind.ServiceGraph,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    serviceGraphHash,
                    generatorVersion),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(2),
                    ArtifactKind.ScopeGraph,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    scopeGraphHash,
                    generatorVersion),
                scopes);

            CommandCatalogPlan commandCatalogPlan = new CommandCatalogPlan(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(4),
                    ArtifactKind.CommandCatalog,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    commandCatalogHash,
                    generatorVersion),
                commands);

            ValueSchemaPlan valueSchemaPlan = new ValueSchemaPlan(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(5),
                    ArtifactKind.ValueSchema,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    valueSchemaHash,
                    generatorVersion),
                valueKeys);

            RuntimeQueryPlan runtimeQueryPlan = new RuntimeQueryPlan(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(6),
                    ArtifactKind.RuntimeQuery,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    runtimeQueryHash,
                    generatorVersion),
                runtimeQueries);

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(
                    planId,
                    artifactSetId,
                    new ArtifactId(7),
                    ArtifactKind.KernelDebugMap,
                    formatVersion,
                    sourceHash,
                    registryHash,
                    profileHash,
                    debugMapHash,
                    debugMapHash,
                    generatorVersion),
                debugMapEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                artifactSetId,
                planId,
                sourceHash.ToString(),
                profileHash.ToString(),
                formatVersion,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                manifestId,
                selectedProfile.Id,
                artifactSet,
                bootPolicyId,
                BootDiagnosticsPolicy.ForKind(selectedProfile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                selectedProfile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap,
                requiredRootServices,
                availableRootServices,
                requiredRootScopes,
                availableRootScopes,
                commandCatalogPlan,
                valueSchemaPlan,
                runtimeQueryPlan);
        }

        static VerifiedArtifactHeader CreateHeader(
            PlanId planId,
            ArtifactSetId artifactSetId,
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            int formatVersion,
            Hash128 sourceHash,
            Hash128 registryHash,
            Hash128 profileHash,
            Hash128 debugMapHash,
            Hash128 generatedHash,
            string generatorVersion)
        {
            return new VerifiedArtifactHeader(
                planId,
                artifactSetId,
                artifactId,
                artifactKind,
                formatVersion,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                generatedHash,
                generatorVersion);
        }
    }
}