#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Kernel.IR
{
    public sealed class SourceLocationTable
    {
        readonly SourceLocationIR[] sources;

        public SourceLocationTable(SourceLocationIR[] sources)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("Source location tables must contain at least one source location.", nameof(sources));

            this.sources = new SourceLocationIR[sources.Length];
            HashSet<SourceLocationIR> seenSources = new HashSet<SourceLocationIR>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].IsSpecified)
                    throw new ArgumentException("Source location tables must contain only specified source locations.", nameof(sources));

                if (!seenSources.Add(sources[i]))
                    throw new ArgumentException("Source location tables must not contain duplicate source locations.", nameof(sources));

                this.sources[i] = sources[i];
            }
        }

        public int Count => sources.Length;

        public ReadOnlySpan<SourceLocationIR> Sources => sources;

        public bool TryGetSource(SourceLocationId id, out SourceLocationIR source)
        {
            if (id.Value <= 0 || id.Value > sources.Length)
            {
                source = default;
                return false;
            }

            source = sources[id.Value - 1];
            return true;
        }

        public SourceLocationIR GetSource(SourceLocationId id)
        {
            if (!TryGetSource(id, out SourceLocationIR source))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Source location id is outside the source location table.");

            return source;
        }

        public bool Contains(SourceLocationId id)
        {
            return id.Value > 0 && id.Value <= sources.Length;
        }
    }

    public sealed class KernelIRHeader
    {
        public KernelIRHeader(string documentId, int formatVersion, string projectName, string profileId, string generatorVersion, Hash128 sourceHash, Hash128 normalizedHash)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Kernel IR headers must provide a document identifier.", nameof(documentId));

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Kernel IR headers must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("Kernel IR headers must provide a project name.", nameof(projectName));

            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("Kernel IR headers must provide a profile identifier.", nameof(profileId));

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Kernel IR headers must provide a generator version.", nameof(generatorVersion));

            DocumentId = documentId;
            FormatVersion = formatVersion;
            ProjectName = projectName;
            ProfileId = profileId;
            GeneratorVersion = generatorVersion;
            SourceHash = sourceHash;
            NormalizedHash = normalizedHash;
        }

        public string DocumentId { get; }

        public int FormatVersion { get; }

        public string ProjectName { get; }

        public string ProfileId { get; }

        public string GeneratorVersion { get; }

        public Hash128 SourceHash { get; }

        public Hash128 NormalizedHash { get; }
    }

    public sealed class KernelIR
    {
        readonly ModuleIR[] modules;
        readonly ScopeIR[] scopes;
        readonly ServiceIR[] services;
        readonly CommandIR[] commands;
        readonly ValueKeyIR[] valueKeys;
        readonly LifecycleIR[] lifecycles;
        readonly RuntimeQueryIR[] runtimeQueries;
        readonly DependencyEdgeIR[] dependencies;
        readonly DiagnosticSeedIR[] diagnosticSeeds;

        public KernelIR(KernelIRHeader header, KernelProfileIR profile, ModuleIR[] modules, ScopeIR[] scopes, ServiceIR[] services, CommandIR[] commands, ValueKeyIR[] valueKeys, LifecycleIR[] lifecycles, RuntimeQueryIR[] runtimeQueries, DependencyEdgeIR[] dependencies, SourceLocationTable sources, DiagnosticSeedIR[]? diagnosticSeeds = null)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
            this.modules = CloneAndSortModules(modules);
            this.scopes = CloneAndSortScopes(scopes);
            this.services = CloneAndSortServices(services);
            this.commands = CloneAndSortCommands(commands);
            this.valueKeys = CloneAndSortValueKeys(valueKeys);
            this.lifecycles = CloneAndSortLifecycles(lifecycles);
            this.runtimeQueries = CloneAndSortRuntimeQueries(runtimeQueries);
            this.dependencies = CloneAndSortDependencies(dependencies);
            this.diagnosticSeeds = CloneDiagnosticSeeds(diagnosticSeeds);

            ValidateUniqueIds();
            ValidateSourceCoverage();
            ValidateDiagnosticSeeds();
            ValidateOwnership();
            ValidateDependencyCoverage();
            ValidateReferences();
            ValidateLifecycleDependencyCoverage();
        }

        public KernelIRHeader Header { get; }

        public KernelProfileIR Profile { get; }

        public ReadOnlySpan<ModuleIR> Modules => modules;

        public ReadOnlySpan<ScopeIR> Scopes => scopes;

        public ReadOnlySpan<ServiceIR> Services => services;

        public ReadOnlySpan<CommandIR> Commands => commands;

        public ReadOnlySpan<ValueKeyIR> ValueKeys => valueKeys;

        public ReadOnlySpan<LifecycleIR> Lifecycles => lifecycles;

        public ReadOnlySpan<RuntimeQueryIR> RuntimeQueries => runtimeQueries;

        public ReadOnlySpan<DependencyEdgeIR> Dependencies => dependencies;

        public SourceLocationTable Sources { get; }

        public ReadOnlySpan<DiagnosticSeedIR> DiagnosticSeeds => diagnosticSeeds;

        void ValidateUniqueIds()
        {
            EnsureUniqueIds(modules, nameof(Modules), module => module.Id.Value, "KernelIR modules must use unique module identities.");
            EnsureUniqueIds(scopes, nameof(Scopes), scope => scope.PlanId.Value, "KernelIR scopes must use unique scope plan identities.");
            EnsureUniqueIds(services, nameof(Services), service => service.Id.Value, "KernelIR services must use unique service identities.");
            EnsureUniqueIds(commands, nameof(Commands), command => command.TypeId.Value, "KernelIR commands must use unique command identities.");
            EnsureUniqueIds(valueKeys, nameof(ValueKeys), valueKey => valueKey.Id.Value, "KernelIR value keys must use unique value key identities.");
            EnsureUniqueIds(lifecycles, nameof(Lifecycles), lifecycle => lifecycle.PlanId.Value, "KernelIR lifecycles must use unique lifecycle plan identities.");
            EnsureUniqueIds(runtimeQueries, nameof(RuntimeQueries), runtimeQuery => runtimeQuery.Id.Value, "KernelIR runtime queries must use unique runtime query identities.");

            HashSet<int> lifecycleStepIds = new HashSet<int>();
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (!lifecycleStepIds.Add(steps[stepIndex].Id.Value))
                        throw new ArgumentException("KernelIR lifecycle steps must use unique lifecycle step identities.", nameof(Lifecycles));
                }
            }
        }

        void ValidateSourceCoverage()
        {
            for (int i = 0; i < modules.Length; i++)
            {
                EnsureValidSource(modules[i].Source, nameof(ModuleIR));

                ReadOnlySpan<ModuleDependencyIR> requiredModules = modules[i].RequiredModules;
                for (int dependencyIndex = 0; dependencyIndex < requiredModules.Length; dependencyIndex++)
                {
                    EnsureValidSource(requiredModules[dependencyIndex].Source, nameof(ModuleDependencyIR));
                }

                ReadOnlySpan<ModuleDependencyIR> optionalModules = modules[i].OptionalModules;
                for (int dependencyIndex = 0; dependencyIndex < optionalModules.Length; dependencyIndex++)
                {
                    EnsureValidSource(optionalModules[dependencyIndex].Source, nameof(ModuleDependencyIR));
                }
            }

            for (int i = 0; i < scopes.Length; i++)
            {
                EnsureValidSource(scopes[i].Source, nameof(ScopeIR));

                ReadOnlySpan<ScopeServiceRequirementIR> requiredServices = scopes[i].RequiredServices;
                for (int requirementIndex = 0; requirementIndex < requiredServices.Length; requirementIndex++)
                {
                    EnsureValidSource(requiredServices[requirementIndex].Source, nameof(ScopeServiceRequirementIR));
                }

                ReadOnlySpan<ScopeValueInitRefIR> valueInitPlans = scopes[i].ValueInitPlans;
                for (int valueInitIndex = 0; valueInitIndex < valueInitPlans.Length; valueInitIndex++)
                {
                    EnsureValidSource(valueInitPlans[valueInitIndex].Source, nameof(ScopeValueInitRefIR));
                }

                EnsureValidSource(scopes[i].Lifecycle.Source, nameof(LifecyclePlanRefIR));
            }

            for (int i = 0; i < services.Length; i++)
            {
                EnsureValidSource(services[i].Source, nameof(ServiceIR));

                ReadOnlySpan<ServiceContractIR> contracts = services[i].Contracts;
                for (int contractIndex = 0; contractIndex < contracts.Length; contractIndex++)
                {
                    EnsureValidSource(contracts[contractIndex].Source, nameof(ServiceContractIR));
                }

                ReadOnlySpan<ServiceDependencyIR> serviceDependencies = services[i].Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < serviceDependencies.Length; dependencyIndex++)
                {
                    EnsureValidSource(serviceDependencies[dependencyIndex].Source, nameof(ServiceDependencyIR));
                }
            }

            for (int i = 0; i < commands.Length; i++)
            {
                EnsureValidSource(commands[i].Source, nameof(CommandIR));
                EnsureValidSource(commands[i].PayloadSchema.Source, nameof(CommandPayloadSchemaRefIR));
                EnsureValidSource(commands[i].Executor.Source, nameof(CommandExecutorRefIR));

                ReadOnlySpan<CommandDependencyIR> commandDependencies = commands[i].Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < commandDependencies.Length; dependencyIndex++)
                {
                    EnsureValidSource(commandDependencies[dependencyIndex].Source, nameof(CommandDependencyIR));
                }
            }

            for (int i = 0; i < valueKeys.Length; i++)
            {
                EnsureValidSource(valueKeys[i].Source, nameof(ValueKeyIR));
                EnsureValidSource(valueKeys[i].Schema.Source, nameof(ValueSchemaRefIR));
            }

            for (int i = 0; i < lifecycles.Length; i++)
            {
                EnsureValidSource(lifecycles[i].Source, nameof(LifecycleIR));
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[i].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    EnsureValidSource(steps[stepIndex].Source, nameof(LifecycleStepIR));
                }
            }

            for (int i = 0; i < runtimeQueries.Length; i++)
            {
                EnsureValidSource(runtimeQueries[i].Source, nameof(RuntimeQueryIR));
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                EnsureValidSource(dependencies[i].Source, nameof(DependencyEdgeIR));
            }

            for (int i = 0; i < diagnosticSeeds.Length; i++)
            {
                EnsureValidSource(diagnosticSeeds[i].Source, nameof(DiagnosticSeedIR));
            }
        }

        void ValidateDiagnosticSeeds()
        {
            HashSet<string> seenSeedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < diagnosticSeeds.Length; i++)
            {
                if (!seenSeedKeys.Add(diagnosticSeeds[i].SeedKey))
                    throw new ArgumentException("KernelIR diagnostic seed keys must be unique.", nameof(DiagnosticSeeds));
            }
        }

        void ValidateOwnership()
        {
            HashSet<int> moduleIds = new HashSet<int>();
            for (int i = 0; i < modules.Length; i++)
            {
                moduleIds.Add(modules[i].Id.Value);
            }

            for (int i = 0; i < scopes.Length; i++)
            {
                EnsureModuleExists(moduleIds, scopes[i].OwnerModule, nameof(ScopeIR));
            }

            for (int i = 0; i < services.Length; i++)
            {
                EnsureModuleExists(moduleIds, services[i].OwnerModule, nameof(ServiceIR));
            }

            for (int i = 0; i < commands.Length; i++)
            {
                EnsureModuleExists(moduleIds, commands[i].OwnerModule, nameof(CommandIR));
            }

            for (int i = 0; i < valueKeys.Length; i++)
            {
                EnsureModuleExists(moduleIds, valueKeys[i].OwnerModule, nameof(ValueKeyIR));
            }

            for (int i = 0; i < lifecycles.Length; i++)
            {
                EnsureModuleExists(moduleIds, lifecycles[i].OwnerModule, nameof(LifecycleIR));
            }

            for (int i = 0; i < runtimeQueries.Length; i++)
            {
                EnsureModuleExists(moduleIds, runtimeQueries[i].OwnerModule, nameof(RuntimeQueryIR));
            }
        }

        void ValidateDependencyCoverage()
        {
            HashSet<int> moduleIds = new HashSet<int>();
            HashSet<int> serviceIds = new HashSet<int>();
            HashSet<int> scopeIds = new HashSet<int>();
            HashSet<int> commandIds = new HashSet<int>();
            HashSet<int> valueKeyIds = new HashSet<int>();
            HashSet<int> lifecycleStepIds = new HashSet<int>();
            HashSet<int> runtimeQueryIds = new HashSet<int>();

            for (int i = 0; i < modules.Length; i++) moduleIds.Add(modules[i].Id.Value);
            for (int i = 0; i < services.Length; i++) serviceIds.Add(services[i].Id.Value);
            for (int i = 0; i < scopes.Length; i++) scopeIds.Add(scopes[i].PlanId.Value);
            for (int i = 0; i < commands.Length; i++) commandIds.Add(commands[i].TypeId.Value);
            for (int i = 0; i < valueKeys.Length; i++) valueKeyIds.Add(valueKeys[i].Id.Value);
            for (int i = 0; i < runtimeQueries.Length; i++) runtimeQueryIds.Add(runtimeQueries[i].Id.Value);

            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    lifecycleStepIds.Add(steps[stepIndex].Id.Value);
                }
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                ValidateDependencyNodeExists(dependencies[i].From, moduleIds, serviceIds, scopeIds, commandIds, valueKeyIds, lifecycleStepIds, runtimeQueryIds);
                ValidateDependencyNodeExists(dependencies[i].To, moduleIds, serviceIds, scopeIds, commandIds, valueKeyIds, lifecycleStepIds, runtimeQueryIds);
            }
        }

        void ValidateReferences()
        {
            HashSet<int> moduleIds = BuildIdSet(modules, module => module.Id.Value);
            HashSet<int> serviceIds = BuildIdSet(services, service => service.Id.Value);
            HashSet<int> scopeIds = BuildIdSet(scopes, scope => scope.PlanId.Value);
            HashSet<int> lifecyclePlanIds = BuildIdSet(lifecycles, lifecycle => lifecycle.PlanId.Value);
            HashSet<int> runtimeQueryIds = BuildIdSet(runtimeQueries, runtimeQuery => runtimeQuery.Id.Value);
            HashSet<int> dependencyNodeModuleIds = moduleIds;
            HashSet<int> dependencyNodeServiceIds = serviceIds;
            HashSet<int> dependencyNodeScopeIds = scopeIds;
            HashSet<int> dependencyNodeCommandIds = BuildIdSet(commands, command => command.TypeId.Value);
            HashSet<int> dependencyNodeValueKeyIds = BuildIdSet(valueKeys, valueKey => valueKey.Id.Value);
            HashSet<int> dependencyNodeLifecycleStepIds = BuildLifecycleStepIdSet();

            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                ValidateModuleDependencies(modules[moduleIndex].RequiredModules, moduleIds, nameof(ModuleIR));
                ValidateModuleDependencies(modules[moduleIndex].OptionalModules, moduleIds, nameof(ModuleIR));
            }

            for (int scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
            {
                ReadOnlySpan<ScopeServiceRequirementIR> requiredServices = scopes[scopeIndex].RequiredServices;
                for (int requirementIndex = 0; requirementIndex < requiredServices.Length; requirementIndex++)
                {
                    if (!serviceIds.Contains(requiredServices[requirementIndex].ServiceId.Value))
                        throw new ArgumentException("ScopeIR required services must reference services that exist in KernelIR.Services.", nameof(Scopes));
                }

                if (!lifecyclePlanIds.Contains(scopes[scopeIndex].Lifecycle.PlanId.Value))
                    throw new ArgumentException("ScopeIR lifecycle refs must reference lifecycle plans that exist in KernelIR.Lifecycles.", nameof(Scopes));
            }

            for (int serviceIndex = 0; serviceIndex < services.Length; serviceIndex++)
            {
                ReadOnlySpan<ServiceDependencyIR> serviceDependencies = services[serviceIndex].Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < serviceDependencies.Length; dependencyIndex++)
                {
                    ValidateDependencyNodeExists(serviceDependencies[dependencyIndex].Target, dependencyNodeModuleIds, dependencyNodeServiceIds, dependencyNodeScopeIds, dependencyNodeCommandIds, dependencyNodeValueKeyIds, dependencyNodeLifecycleStepIds, runtimeQueryIds);
                }
            }

            for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                ReadOnlySpan<CommandDependencyIR> commandDependencies = commands[commandIndex].Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < commandDependencies.Length; dependencyIndex++)
                {
                    ValidateDependencyNodeExists(commandDependencies[dependencyIndex].Target, dependencyNodeModuleIds, dependencyNodeServiceIds, dependencyNodeScopeIds, dependencyNodeCommandIds, dependencyNodeValueKeyIds, dependencyNodeLifecycleStepIds, runtimeQueryIds);
                }
            }

            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    ValidateLifecycleTarget(steps[stepIndex].Target, serviceIds, scopeIds, runtimeQueryIds);
                }
            }
        }

        void ValidateLifecycleDependencyCoverage()
        {
            HashSet<int> dependencyIds = new HashSet<int>();
            for (int i = 0; i < dependencies.Length; i++)
            {
                dependencyIds.Add(dependencies[i].Id.Value);
            }

            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    ReadOnlySpan<DependencyEdgeId> stepDependencies = steps[stepIndex].Dependencies;
                    for (int dependencyIndex = 0; dependencyIndex < stepDependencies.Length; dependencyIndex++)
                    {
                        if (!dependencyIds.Contains(stepDependencies[dependencyIndex].Value))
                            throw new ArgumentException("Lifecycle steps must reference dependency edges that exist in KernelIR.", nameof(Lifecycles));
                    }
                }
            }
        }

        void EnsureValidSource(SourceLocationId source, string ownerName)
        {
            if (!Sources.Contains(source))
                throw new ArgumentException(ownerName + " must reference a source location that exists in SourceLocationTable.", ownerName);
        }

        static void EnsureModuleExists(HashSet<int> moduleIds, ModuleId ownerModule, string ownerName)
        {
            if (!moduleIds.Contains(ownerModule.Value))
                throw new ArgumentException(ownerName + " must reference an owner module that exists in KernelIR.Modules.", ownerName);
        }

        static void ValidateModuleDependencies(ReadOnlySpan<ModuleDependencyIR> dependencies, HashSet<int> moduleIds, string ownerName)
        {
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                if (!moduleIds.Contains(dependencies[dependencyIndex].ModuleId.Value))
                    throw new ArgumentException(ownerName + " module dependencies must reference modules that exist in KernelIR.Modules.", ownerName);
            }
        }

        static void ValidateLifecycleTarget(LifecycleTargetRefIR target, HashSet<int> serviceIds, HashSet<int> scopeIds, HashSet<int> runtimeQueryIds)
        {
            switch (target.Kind)
            {
                case LifecycleTargetKind.Service when serviceIds.Contains(target.TargetService.Value):
                case LifecycleTargetKind.Scope when scopeIds.Contains(target.TargetScope.Value):
                case LifecycleTargetKind.RuntimeQuery when runtimeQueryIds.Contains(target.TargetRuntimeQuery.Value):
                case LifecycleTargetKind.ValueStore when !string.IsNullOrWhiteSpace(target.TargetLocalRef):
                case LifecycleTargetKind.RuntimeObjectOwner when !string.IsNullOrWhiteSpace(target.TargetLocalRef):
                case LifecycleTargetKind.LegacyAdapter when !string.IsNullOrWhiteSpace(target.TargetLocalRef):
                    return;
                default:
                    throw new ArgumentException("Lifecycle steps must reference targets that exist in KernelIR.", nameof(Lifecycles));
            }
        }

        static void ValidateDependencyNodeExists(DependencyNodeIR node, HashSet<int> moduleIds, HashSet<int> serviceIds, HashSet<int> scopeIds, HashSet<int> commandIds, HashSet<int> valueKeyIds, HashSet<int> lifecycleStepIds, HashSet<int> runtimeQueryIds)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Module when moduleIds.Contains(node.ModuleId.Value):
                case DependencyNodeKind.Service when serviceIds.Contains(node.ServiceId.Value):
                case DependencyNodeKind.Scope when scopeIds.Contains(node.ScopePlanId.Value):
                case DependencyNodeKind.Command when commandIds.Contains(node.CommandTypeId.Value):
                case DependencyNodeKind.ValueKey when valueKeyIds.Contains(node.ValueKeyId.Value):
                case DependencyNodeKind.LifecycleStep when lifecycleStepIds.Contains(node.LifecycleStepId.Value):
                case DependencyNodeKind.RuntimeQuery when runtimeQueryIds.Contains(node.RuntimeQueryId.Value):
                    return;
                default:
                    throw new ArgumentException("Dependency edges must reference nodes that exist in KernelIR.", nameof(node));
            }
        }

        static ModuleIR[] CloneAndSortModules(ModuleIR[] source)
        {
            ModuleIR[] clone = CloneArray(source, "modules");
            Array.Sort(clone, (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return clone;
        }

        static ScopeIR[] CloneAndSortScopes(ScopeIR[] source)
        {
            ScopeIR[] clone = CloneArray(source, "scopes");
            Array.Sort(clone, (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            return clone;
        }

        static ServiceIR[] CloneAndSortServices(ServiceIR[] source)
        {
            ServiceIR[] clone = CloneArray(source, "services");
            Array.Sort(clone, (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return clone;
        }

        static CommandIR[] CloneAndSortCommands(CommandIR[] source)
        {
            CommandIR[] clone = CloneArray(source, "commands");
            Array.Sort(clone, (left, right) => left.TypeId.Value.CompareTo(right.TypeId.Value));
            return clone;
        }

        static ValueKeyIR[] CloneAndSortValueKeys(ValueKeyIR[] source)
        {
            ValueKeyIR[] clone = CloneArray(source, "valueKeys");
            Array.Sort(clone, (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return clone;
        }

        static LifecycleIR[] CloneAndSortLifecycles(LifecycleIR[] source)
        {
            LifecycleIR[] clone = CloneArray(source, "lifecycles");
            Array.Sort(clone, (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            return clone;
        }

        static RuntimeQueryIR[] CloneAndSortRuntimeQueries(RuntimeQueryIR[] source)
        {
            RuntimeQueryIR[] clone = CloneArray(source, "runtimeQueries");
            Array.Sort(clone, (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return clone;
        }

        static DependencyEdgeIR[] CloneAndSortDependencies(DependencyEdgeIR[] source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            DependencyEdgeIR[] clone = new DependencyEdgeIR[source.Length];
            HashSet<int> seenDependencyIds = new HashSet<int>();
            for (int i = 0; i < source.Length; i++)
            {
                if (!seenDependencyIds.Add(source[i].Id.Value))
                    throw new ArgumentException("KernelIR dependency edges must use unique dependency identities.", nameof(source));

                clone[i] = source[i];
            }

            Array.Sort(clone, (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return clone;
        }

        static DiagnosticSeedIR[] CloneDiagnosticSeeds(DiagnosticSeedIR[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DiagnosticSeedIR>();

            DiagnosticSeedIR[] clone = new DiagnosticSeedIR[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }

        static T[] CloneArray<T>(T[] source, string parameterName) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);

            T[] clone = new T[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i] ?? throw new ArgumentException("KernelIR arrays must not contain null items.", parameterName);
            }

            return clone;
        }

        static HashSet<int> BuildIdSet<T>(T[] items, Func<T, int> selector) where T : class
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < items.Length; i++)
            {
                ids.Add(selector(items[i]));
            }

            return ids;
        }

        HashSet<int> BuildLifecycleStepIdSet()
        {
            HashSet<int> ids = new HashSet<int>();
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    ids.Add(steps[stepIndex].Id.Value);
                }
            }

            return ids;
        }

        static void EnsureUniqueIds<T>(T[] items, string ownerName, Func<T, int> selector, string message) where T : class
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < items.Length; i++)
            {
                if (!ids.Add(selector(items[i])))
                    throw new ArgumentException(message, ownerName);
            }
        }
    }
}