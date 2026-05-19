#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;

namespace Game.Kernel.Generation
{
    public sealed class KernelProjectionGenerationResult
    {
        public KernelProjectionGenerationResult(
            KernelProjectionSet projections,
            GeneratedKernelPlan generatedPlan,
            KernelPlanVerificationResult planVerification,
            ProjectionValidationReport projectionValidationReport)
        {
            Projections = projections ?? throw new ArgumentNullException(nameof(projections));
            GeneratedPlan = generatedPlan ?? throw new ArgumentNullException(nameof(generatedPlan));
            PlanVerification = planVerification ?? throw new ArgumentNullException(nameof(planVerification));
            ProjectionValidationReport = projectionValidationReport ?? throw new ArgumentNullException(nameof(projectionValidationReport));
        }

        public KernelProjectionSet Projections { get; }

        public GeneratedKernelPlan GeneratedPlan { get; }

        public KernelPlanVerificationResult PlanVerification { get; }

        public ProjectionValidationReport ProjectionValidationReport { get; }

        public bool IsVerified => PlanVerification.IsVerified && ProjectionValidationReport.Status == ValidationResultStatus.Passed;
    }

    public sealed class KernelProjectionSet
    {
        public KernelProjectionSet(
            ServiceGraphPlan serviceGraph,
            ScopeGraphPlan scopeGraph,
            LifecyclePlan lifecyclePlan,
            CommandCatalogPlan commandCatalog,
            ValueSchemaPlan valueSchema,
            RuntimeQueryPlan runtimeQuery,
            KernelDebugMap debugMap,
            GenerationReport generationReport,
            ValidationReport validationReport)
        {
            ServiceGraph = serviceGraph ?? throw new ArgumentNullException(nameof(serviceGraph));
            ScopeGraph = scopeGraph ?? throw new ArgumentNullException(nameof(scopeGraph));
            LifecyclePlan = lifecyclePlan ?? throw new ArgumentNullException(nameof(lifecyclePlan));
            CommandCatalog = commandCatalog ?? throw new ArgumentNullException(nameof(commandCatalog));
            ValueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
            RuntimeQuery = runtimeQuery ?? throw new ArgumentNullException(nameof(runtimeQuery));
            DebugMap = debugMap ?? throw new ArgumentNullException(nameof(debugMap));
            GenerationReport = generationReport ?? throw new ArgumentNullException(nameof(generationReport));
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));
        }

        public ServiceGraphPlan ServiceGraph { get; }

        public ScopeGraphPlan ScopeGraph { get; }

        public LifecyclePlan LifecyclePlan { get; }

        public CommandCatalogPlan CommandCatalog { get; }

        public ValueSchemaPlan ValueSchema { get; }

        public RuntimeQueryPlan RuntimeQuery { get; }

        public KernelDebugMap DebugMap { get; }

        public GenerationReport GenerationReport { get; }

        public ValidationReport ValidationReport { get; }
    }

    public sealed class ServiceGraphPlan
    {
        readonly ServiceIR[] services;

        public ServiceGraphPlan(VerifiedArtifactHeader header, ReadOnlySpan<ServiceIR> services)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ServiceGraph);
            this.services = KernelProjectionArrayHelpers.CloneAndSort(services, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            ContentHash = KernelProjectionHashing.ComputeServiceGraphHash(this.services);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ServiceIR> Services => services;

        public Hash128 ContentHash { get; }
    }

    public sealed class ScopeGraphPlan
    {
        readonly ScopeIR[] scopes;

        public ScopeGraphPlan(VerifiedArtifactHeader header, ReadOnlySpan<ScopeIR> scopes)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ScopeGraph);
            this.scopes = KernelProjectionArrayHelpers.CloneAndSort(scopes, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            ContentHash = KernelProjectionHashing.ComputeScopeGraphHash(this.scopes);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ScopeIR> Scopes => scopes;

        public Hash128 ContentHash { get; }
    }

    public sealed class LifecyclePlan
    {
        readonly LifecycleIR[] lifecycles;

        public LifecyclePlan(VerifiedArtifactHeader header, ReadOnlySpan<LifecycleIR> lifecycles)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.LifecyclePlan);
            this.lifecycles = KernelProjectionArrayHelpers.CloneAndSort(lifecycles, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            ContentHash = KernelProjectionHashing.ComputeLifecyclePlanHash(this.lifecycles);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<LifecycleIR> Lifecycles => lifecycles;

        public Hash128 ContentHash { get; }
    }

    public sealed class CommandCatalogPlan
    {
        readonly CommandIR[] commands;

        public CommandCatalogPlan(VerifiedArtifactHeader header, ReadOnlySpan<CommandIR> commands)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.CommandCatalog);
            this.commands = KernelProjectionArrayHelpers.CloneAndSort(commands, static (left, right) => left.TypeId.Value.CompareTo(right.TypeId.Value));
            ContentHash = KernelProjectionHashing.ComputeCommandCatalogHash(this.commands);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<CommandIR> Commands => commands;

        public Hash128 ContentHash { get; }
    }

    public sealed class ValueSchemaPlan
    {
        readonly ValueKeyIR[] valueKeys;

        public ValueSchemaPlan(VerifiedArtifactHeader header, ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ValueSchema);
            this.valueKeys = KernelProjectionArrayHelpers.CloneAndSort(valueKeys, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            ContentHash = KernelProjectionHashing.ComputeValueSchemaHash(this.valueKeys);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ValueKeyIR> ValueKeys => valueKeys;

        public Hash128 ContentHash { get; }
    }

    public sealed class RuntimeQueryPlan
    {
        readonly RuntimeQueryIR[] runtimeQueries;

        public RuntimeQueryPlan(VerifiedArtifactHeader header, ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.RuntimeQuery);
            this.runtimeQueries = KernelProjectionArrayHelpers.CloneAndSort(runtimeQueries, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            ContentHash = KernelProjectionHashing.ComputeRuntimeQueryHash(this.runtimeQueries);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<RuntimeQueryIR> RuntimeQueries => runtimeQueries;

        public Hash128 ContentHash { get; }
    }

    public sealed class KernelDebugMap
    {
        readonly KernelDebugMapEntry[] entries;

        public KernelDebugMap(VerifiedArtifactHeader header, ReadOnlySpan<KernelDebugMapEntry> entries)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.KernelDebugMap);
            this.entries = KernelProjectionArrayHelpers.CloneAndSort(entries, KernelDebugMapEntryComparer.Instance);
            ContentHash = KernelProjectionHashing.ComputeDebugMapHash(this.entries);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<KernelDebugMapEntry> Entries => entries;

        public Hash128 ContentHash { get; }

        public bool TryGetSourceLocation(RuntimeIdentityRef identity, out SourceLocationRef sourceLocation)
        {
            if (identity.IsEmpty)
                throw new ArgumentException("Debug map lookups require a fully specified identity.", nameof(identity));

            for (int index = 0; index < entries.Length; index++)
            {
                KernelDebugMapEntry entry = entries[index];
                if (entry.Identity != identity)
                    continue;

                sourceLocation = new SourceLocationRef(entry.Source.Value);
                return true;
            }

            sourceLocation = default;
            return false;
        }
    }

    public sealed class GenerationReport
    {
        public GenerationReport(
            VerifiedArtifactHeader header,
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            int artifactCount,
            int mappingCount,
            int debugMapEntryCount,
            ValidationResultStatus validationStatus,
            Hash128 contentHash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.GenerationReport);
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Generation reports must provide a selected profile.", nameof(selectedProfile));

            SelectedProfile = selectedProfile;
            SelectedProfileMask = selectedProfileMask;
            ArtifactCount = artifactCount;
            MappingCount = mappingCount;
            DebugMapEntryCount = debugMapEntryCount;
            ValidationStatus = validationStatus;
            ContentHash = contentHash;
            KernelProjectionHashing.ValidateHeaderHash(header, contentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public string SelectedProfile { get; }

        public KernelProfileMask SelectedProfileMask { get; }

        public int ArtifactCount { get; }

        public int MappingCount { get; }

        public int DebugMapEntryCount { get; }

        public ValidationResultStatus ValidationStatus { get; }

        public Hash128 ContentHash { get; }
    }

    public sealed class ValidationReport
    {
        public ValidationReport(VerifiedArtifactHeader header, ProjectionValidationReport report, Hash128 contentHash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ValidationReport);
            ContentHash = contentHash;
            KernelProjectionHashing.ValidateHeaderHash(header, contentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ProjectionValidationReport Report { get; }

        public Hash128 ContentHash { get; }
    }

    public readonly struct KernelDebugMapEntry : IEquatable<KernelDebugMapEntry>
    {
        public KernelDebugMapEntry(
            RuntimeIdentityRef identity,
            string name,
            ModuleId ownerModule,
            SourceLocationId source,
            KernelProfileMask profileMask,
            Hash128 artifactHash,
            string? diagnosticSeedKey = null,
            string? legacyOrigin = null)
        {
            if (identity.IsEmpty)
                throw new ArgumentException("Debug map entries must provide an identity.", nameof(identity));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Debug map entries must provide a display name.", nameof(name));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Debug map entries must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Debug map entries must provide a non-zero source location identity.", nameof(source));

            if (!string.IsNullOrWhiteSpace(legacyOrigin) && legacyOrigin.Trim().Length == 0)
                throw new ArgumentException("Debug map entry legacy origin values must be null or non-empty.", nameof(legacyOrigin));

            if (diagnosticSeedKey != null && diagnosticSeedKey.Trim().Length == 0)
                throw new ArgumentException("Debug map entry diagnostic seed keys must be null or non-empty.", nameof(diagnosticSeedKey));

            if (identity.Kind == RuntimeIdentityKind.DiagnosticSeed && string.IsNullOrWhiteSpace(diagnosticSeedKey))
                throw new ArgumentException("Diagnostic seed debug map entries must provide a diagnostic seed key.", nameof(diagnosticSeedKey));

            if (identity.Kind != RuntimeIdentityKind.DiagnosticSeed && diagnosticSeedKey != null)
                throw new ArgumentException("Only diagnostic seed debug map entries may provide a diagnostic seed key.", nameof(diagnosticSeedKey));

            if (identity.Kind == RuntimeIdentityKind.DiagnosticSeed && legacyOrigin != null)
                throw new ArgumentException("Diagnostic seed debug map entries must not provide a legacy origin.", nameof(legacyOrigin));

            Identity = identity;
            Name = name;
            OwnerModule = ownerModule;
            Source = source;
            ProfileMask = profileMask;
            ArtifactHash = artifactHash;
            DiagnosticSeedKey = diagnosticSeedKey;
            LegacyOrigin = legacyOrigin;
        }

        public RuntimeIdentityRef Identity { get; }

        public string Name { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public KernelProfileMask ProfileMask { get; }

        public Hash128 ArtifactHash { get; }

        public string? DiagnosticSeedKey { get; }

        public string? LegacyOrigin { get; }

        public bool Equals(KernelDebugMapEntry other)
        {
            return Identity == other.Identity
                && StringComparer.Ordinal.Equals(Name, other.Name)
                && OwnerModule == other.OwnerModule
                && Source == other.Source
                && ProfileMask == other.ProfileMask
                && ArtifactHash == other.ArtifactHash
                && StringComparer.Ordinal.Equals(DiagnosticSeedKey, other.DiagnosticSeedKey)
                && StringComparer.Ordinal.Equals(LegacyOrigin, other.LegacyOrigin);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelDebugMapEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Identity.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 397) ^ OwnerModule.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ (int)ProfileMask;
                hash = (hash * 397) ^ ArtifactHash.GetHashCode();
                hash = (hash * 397) ^ (DiagnosticSeedKey != null ? StringComparer.Ordinal.GetHashCode(DiagnosticSeedKey) : 0);
                hash = (hash * 397) ^ (LegacyOrigin != null ? StringComparer.Ordinal.GetHashCode(LegacyOrigin) : 0);
                return hash;
            }
        }

        public static bool operator ==(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            return !left.Equals(right);
        }
    }

    static class KernelDebugMapEntryComparer
    {
        public static readonly IComparer<KernelDebugMapEntry> Instance = Comparer<KernelDebugMapEntry>.Create(Compare);

        static int Compare(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            int comparison = left.Identity.Kind.CompareTo(right.Identity.Kind);
            if (comparison != 0)
                return comparison;

            comparison = left.Identity.Value.CompareTo(right.Identity.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.Identity.Generation.CompareTo(right.Identity.Generation);
            if (comparison != 0)
                return comparison;

            comparison = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }
    }

    static class KernelProjectionArrayHelpers
    {
        public static T[] CloneAndSort<T>(ReadOnlySpan<T> source, Comparison<T> comparison) where T : class
        {
            if (source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                clone[index] = source[index] ?? throw new ArgumentException("Projection arrays must not contain null items.", nameof(source));

            Array.Sort(clone, comparison);
            return clone;
        }

        public static T[] CloneAndSort<T>(ReadOnlySpan<T> source, IComparer<T> comparer)
        {
            if (source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                clone[index] = source[index];

            Array.Sort(clone, comparer);
            return clone;
        }
    }

    static class KernelProjectionHashing
    {
        public static Hash128 ComputeServiceGraphHash(ReadOnlySpan<ServiceIR> services)
        {
            List<string> tokens = new List<string>(services.Length * 4);
            for (int index = 0; index < services.Length; index++)
                AddServiceTokens(tokens, services[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeScopeGraphHash(ReadOnlySpan<ScopeIR> scopes)
        {
            List<string> tokens = new List<string>(scopes.Length * 4);
            for (int index = 0; index < scopes.Length; index++)
                AddScopeTokens(tokens, scopes[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeLifecyclePlanHash(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            List<string> tokens = new List<string>(lifecycles.Length * 4);
            for (int index = 0; index < lifecycles.Length; index++)
                AddLifecycleTokens(tokens, lifecycles[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeCommandCatalogHash(ReadOnlySpan<CommandIR> commands)
        {
            List<string> tokens = new List<string>(commands.Length * 4);
            for (int index = 0; index < commands.Length; index++)
                AddCommandTokens(tokens, commands[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeValueSchemaHash(ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            List<string> tokens = new List<string>(valueKeys.Length * 4);
            for (int index = 0; index < valueKeys.Length; index++)
                AddValueKeyTokens(tokens, valueKeys[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeRuntimeQueryHash(ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            List<string> tokens = new List<string>(runtimeQueries.Length * 4);
            for (int index = 0; index < runtimeQueries.Length; index++)
                AddRuntimeQueryTokens(tokens, runtimeQueries[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeDebugMapHash(ReadOnlySpan<KernelDebugMapEntry> entries)
        {
            List<string> tokens = new List<string>(entries.Length * 4);
            for (int index = 0; index < entries.Length; index++)
            {
                KernelDebugMapEntry entry = entries[index];
                tokens.Add("DEBUG|" + entry.Identity + "|" + entry.Name + "|" + entry.OwnerModule.Value + "|" + entry.Source.Value + "|" + entry.ProfileMask + "|" + entry.ArtifactHash + "|" + (entry.DiagnosticSeedKey ?? string.Empty) + "|" + (entry.LegacyOrigin ?? string.Empty));
            }

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeGenerationReportHash(
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            int artifactCount,
            int mappingCount,
            int debugMapEntryCount,
            ValidationResultStatus validationStatus,
            ReadOnlySpan<Hash128> artifactHashes)
        {
            List<string> tokens = new List<string>(artifactHashes.Length + 8)
            {
                selectedProfile,
                selectedProfileMask.ToString(),
                artifactCount.ToString(),
                mappingCount.ToString(),
                debugMapEntryCount.ToString(),
                validationStatus.ToString(),
            };

            for (int index = 0; index < artifactHashes.Length; index++)
                tokens.Add(artifactHashes[index].ToString());

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeValidationReportHash(ProjectionValidationReport report)
        {
            List<string> tokens = new List<string>(report.Issues.Count * 10 + 8)
            {
                report.SelectedProfile,
                report.Status.ToString(),
                report.Summary.InfoCount.ToString(),
                report.Summary.WarningCount.ToString(),
                report.Summary.ErrorCount.ToString(),
                report.Summary.FatalCount.ToString(),
            };

            for (int index = 0; index < report.Issues.Count; index++)
            {
                DependencyValidationIssue issue = report.Issues[index];
                tokens.Add(issue.Code);
                tokens.Add(issue.Severity.ToString());
                tokens.Add(issue.Category.ToString());
                tokens.Add(issue.From.ToString());
                tokens.Add(issue.To.HasValue ? issue.To.Value.ToString() : string.Empty);
                tokens.Add(issue.OwnerModule.Value.ToString());
                tokens.Add(issue.Source.Value.ToString());
                tokens.Add(issue.Phase.ToString());
                tokens.Add(issue.Message);
                tokens.Add(issue.SuggestedFix ?? string.Empty);
            }

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeRegistryHash(KernelIR kernelIR)
        {
            List<string> tokens = new List<string>();

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
                AddModuleTokens(tokens, modules[index]);

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
                AddServiceTokens(tokens, services[index]);

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
                AddScopeTokens(tokens, scopes[index]);

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
                AddLifecycleTokens(tokens, lifecycles[index]);

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
                AddCommandTokens(tokens, commands[index]);

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
                AddValueKeyTokens(tokens, valueKeys[index]);

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
                AddRuntimeQueryTokens(tokens, runtimeQueries[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeProfileHash(KernelIR kernelIR, string selectedProfile, KernelProfileMask selectedProfileMask)
        {
            List<string> tokens = new List<string>(kernelIR.Modules.Length * 8 + 8)
            {
                selectedProfile,
                selectedProfileMask.ToString(),
                kernelIR.Profile.Id,
                kernelIR.Profile.Mask.ToString(),
                kernelIR.Profile.Availability.Profiles.ToString(),
                kernelIR.Profile.Availability.EnabledByDefault.ToString(),
                kernelIR.Profile.Availability.Condition ?? string.Empty,
            };

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
                AddModuleTokens(tokens, modules[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeSourceHash(KernelIR kernelIR)
        {
            return VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR);
        }

        public static void ValidateHeaderHash(VerifiedArtifactHeader header, Hash128 contentHash)
        {
            if (header.GeneratedHash != contentHash)
                throw new ArgumentException("Projection artifact headers must match their generated content hash.", nameof(header));
        }

        static void AddModuleTokens(List<string> tokens, ModuleIR module)
        {
            AvailabilityIR availability = module.Availability.Value;
            tokens.Add("MODULE|" + module.Id.Value + "|" + module.Name + "|" + module.Kind + "|" + module.Version.Value + "|" + availability.Profiles + "|" + availability.EnabledByDefault + "|" + (availability.Condition ?? string.Empty));

            ModuleDependencyIR[] requiredModules = module.RequiredModules.ToArray();
            Array.Sort(requiredModules, static (left, right) => CompareModuleDependency(left, right));
            for (int index = 0; index < requiredModules.Length; index++)
            {
                ModuleDependencyIR dependency = requiredModules[index];
                tokens.Add("MODULE_REQUIRED|" + dependency.ModuleId.Value + "|" + dependency.AbsenceBehavior + "|" + (dependency.DisabledContribution ?? string.Empty) + "|" + dependency.AlternativeModuleId.Value + "|" + dependency.ProfileSpecificErrorProfiles);
            }

            ModuleDependencyIR[] optionalModules = module.OptionalModules.ToArray();
            Array.Sort(optionalModules, static (left, right) => CompareModuleDependency(left, right));
            for (int index = 0; index < optionalModules.Length; index++)
            {
                ModuleDependencyIR dependency = optionalModules[index];
                tokens.Add("MODULE_OPTIONAL|" + dependency.ModuleId.Value + "|" + dependency.AbsenceBehavior + "|" + (dependency.DisabledContribution ?? string.Empty) + "|" + dependency.AlternativeModuleId.Value + "|" + dependency.ProfileSpecificErrorProfiles);
            }

            if (module.LegacyCompat != null)
            {
                LegacyCompatDescriptorIR legacyCompat = module.LegacyCompat;
                tokens.Add("MODULE_LEGACY|" + legacyCompat.Kind + "|" + legacyCompat.LegacySystemName + "|" + legacyCompat.TargetSubsystem + "|" + legacyCompat.Profiles + "|" + legacyCompat.RemovalStatus + "|" + (legacyCompat.DiagnosticsCode ?? string.Empty) + "|" + (legacyCompat.RemovalCondition ?? string.Empty));
            }
        }

        static int CompareModuleDependency(ModuleDependencyIR left, ModuleDependencyIR right)
        {
            int comparison = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.AbsenceBehavior.HasValue.CompareTo(right.AbsenceBehavior.HasValue);
            if (comparison != 0)
                return comparison;

            comparison = left.AbsenceBehavior.GetValueOrDefault().CompareTo(right.AbsenceBehavior.GetValueOrDefault());
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.DisabledContribution, right.DisabledContribution);
            if (comparison != 0)
                return comparison;

            comparison = left.AlternativeModuleId.Value.CompareTo(right.AlternativeModuleId.Value);
            if (comparison != 0)
                return comparison;

            return left.ProfileSpecificErrorProfiles.CompareTo(right.ProfileSpecificErrorProfiles);
        }

        static void AddServiceTokens(List<string> tokens, ServiceIR service)
        {
            tokens.Add("SERVICE|" + service.Id.Value + "|" + service.Name + "|" + service.Lifetime + "|" + service.OwnerModule.Value + "|" + service.FactoryKind);

            ServiceContractIR[] contracts = service.Contracts.ToArray();
            Array.Sort(contracts, static (left, right) => StringComparer.Ordinal.Compare(left.ContractName, right.ContractName));
            for (int index = 0; index < contracts.Length; index++)
                tokens.Add("CONTRACT|" + contracts[index].ContractName);

            ServiceDependencyIR[] dependencies = service.Dependencies.ToArray();
            Array.Sort(dependencies, static (left, right) => StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString()));
            for (int index = 0; index < dependencies.Length; index++)
            {
                ServiceDependencyIR dependency = dependencies[index];
                tokens.Add("DEPENDENCY|" + dependency.Target + "|" + dependency.Strength);
            }
        }

        static void AddScopeTokens(List<string> tokens, ScopeIR scope)
        {
            tokens.Add("SCOPE|" + scope.AuthoringId.Value + "|" + scope.PlanId.Value + "|" + scope.Name + "|" + scope.Kind + "|" + scope.OwnerModule.Value + "|" + scope.ParentAuthoringId.Value + "|" + scope.Lifecycle.PlanId.Value);

            ScopeServiceRequirementIR[] requiredServices = scope.RequiredServices.ToArray();
            Array.Sort(requiredServices, static (left, right) => left.ServiceId.Value.CompareTo(right.ServiceId.Value));
            for (int index = 0; index < requiredServices.Length; index++)
            {
                ScopeServiceRequirementIR requirement = requiredServices[index];
                tokens.Add("SCOPE_SERVICE|" + requirement.ServiceId.Value + "|" + requirement.Strength);
            }

            ScopeValueInitRefIR[] valueInits = scope.ValueInitPlans.ToArray();
            Array.Sort(valueInits, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            for (int index = 0; index < valueInits.Length; index++)
            {
                ScopeValueInitRefIR valueInit = valueInits[index];
                tokens.Add("SCOPE_VALUE|" + valueInit.PlanId.Value);
            }
        }

        static void AddLifecycleTokens(List<string> tokens, LifecycleIR lifecycle)
        {
            tokens.Add("LIFECYCLE|" + lifecycle.PlanId.Value + "|" + lifecycle.Name + "|" + lifecycle.OwnerModule.Value);

            LifecycleStepIR[] steps = lifecycle.Steps.ToArray();
            Array.Sort(steps, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            for (int index = 0; index < steps.Length; index++)
            {
                LifecycleStepIR step = steps[index];
                tokens.Add("STEP|" + step.Id.Value + "|" + step.Phase + "|" + step.Order + "|" + step.Target.Kind + "|" + step.Target.TargetService.Value + "|" + step.Target.TargetScope.Value + "|" + step.Target.TargetRuntimeQuery.Value + "|" + (step.Target.TargetLocalRef ?? string.Empty) + "|" + step.Action);

                DependencyEdgeId[] dependencies = step.Dependencies.ToArray();
                Array.Sort(dependencies, static (left, right) => left.Value.CompareTo(right.Value));
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                    tokens.Add("STEP_DEP|" + step.Id.Value + "|" + dependencies[dependencyIndex].Value);
            }
        }

        static void AddCommandTokens(List<string> tokens, CommandIR command)
        {
            tokens.Add("COMMAND|" + command.TypeId.Value + "|" + command.RuntimeName + "|" + command.AuthoringKey + "|" + command.CategoryId.Value + "|" + command.OwnerModule.Value + "|" + command.PayloadSchema.Id.Value + "|" + command.Executor.Id.Value);

            CommandDependencyIR[] dependencies = command.Dependencies.ToArray();
            Array.Sort(dependencies, static (left, right) => StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString()));
            for (int index = 0; index < dependencies.Length; index++)
            {
                CommandDependencyIR dependency = dependencies[index];
                tokens.Add("COMMAND_DEP|" + dependency.Target + "|" + dependency.Strength);
            }
        }

        static void AddValueKeyTokens(List<string> tokens, ValueKeyIR valueKey)
        {
            tokens.Add("VALUE|" + valueKey.Id.Value + "|" + valueKey.StableKey + "|" + valueKey.DisplayName + "|" + valueKey.Kind + "|" + valueKey.OwnerModule.Value + "|" + valueKey.Schema.Id.Value + "|" + valueKey.SavePolicy.Persists + "|" + valueKey.SavePolicy.SaveAcrossProfiles + "|" + (valueKey.SavePolicy.Channel ?? string.Empty));
        }

        static void AddRuntimeQueryTokens(List<string> tokens, RuntimeQueryIR runtimeQuery)
        {
            tokens.Add("QUERY|" + runtimeQuery.Id.Value + "|" + runtimeQuery.Name + "|" + runtimeQuery.TargetKind + "|" + runtimeQuery.OwnerModule.Value + "|" + runtimeQuery.Policy.RequiresUniqueResult + "|" + runtimeQuery.Policy.AllowMissing + "|" + runtimeQuery.Policy.UpdatePhase);

            RuntimeIdentityFieldIR[] indexedFields = runtimeQuery.IndexedFields.ToArray();
            Array.Sort(indexedFields, static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            for (int index = 0; index < indexedFields.Length; index++)
            {
                RuntimeIdentityFieldIR field = indexedFields[index];
                tokens.Add("QUERY_FIELD|" + field.Name + "|" + field.ValueType + "|" + field.IsRequired);
            }
        }
    }

    static class KernelProjectionArtifactKindValidator
    {
        public static void ValidateArtifactKind(VerifiedArtifactHeader header, ArtifactKind expectedKind)
        {
            if (header.ArtifactKind != expectedKind)
                throw new ArgumentException("Projection artifacts must be created with the matching artifact kind.", nameof(header));
        }
    }
}