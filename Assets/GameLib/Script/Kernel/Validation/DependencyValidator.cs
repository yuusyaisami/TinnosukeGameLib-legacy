#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;

namespace Game.Kernel.Validation
{
    public sealed class DependencyValidationInput
    {
        readonly ModuleIR[] modules;
        readonly ScopeIR[] scopes;
        readonly ServiceIR[] services;
        readonly CommandIR[] commands;
        readonly ValueKeyIR[] valueKeys;
        readonly ValueInitPlanIR[] valueInitPlans;
        readonly LifecycleIR[] lifecycles;
        readonly RuntimeQueryIR[] runtimeQueries;
        readonly DependencyEdgeIR[] dependencies;
        readonly CommandExecutorId[]? commandExecutors;
        readonly CommandPayloadSchemaId[]? commandPayloadSchemas;
        readonly SourceLocationTable sources;

        public DependencyValidationInput(
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            ModuleIR[]? modules,
            ScopeIR[]? scopes,
            ServiceIR[]? services,
            CommandIR[]? commands,
            ValueKeyIR[]? valueKeys,
            LifecycleIR[]? lifecycles,
            RuntimeQueryIR[]? runtimeQueries,
            DependencyEdgeIR[]? dependencies,
            CommandExecutorId[]? commandExecutors = null,
            CommandPayloadSchemaId[]? commandPayloadSchemas = null,
            SourceLocationTable? sources = null,
            ValueInitPlanIR[]? valueInitPlans = null)
        {
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Validation inputs must provide a selected profile.", nameof(selectedProfile));

            if (selectedProfileMask == KernelProfileMask.None)
                throw new ArgumentException("Validation inputs must provide a non-empty selected profile mask.", nameof(selectedProfileMask));

            if (sources == null)
                throw new ArgumentNullException(nameof(sources), "Validation inputs must provide a source location table for provenance-aware validation.");

            SelectedProfile = selectedProfile;
            SelectedProfileMask = selectedProfileMask;
            this.modules = CloneArray(modules, nameof(modules))!;
            this.scopes = CloneArray(scopes, nameof(scopes))!;
            this.services = CloneArray(services, nameof(services))!;
            this.commands = CloneArray(commands, nameof(commands))!;
            this.valueKeys = CloneArray(valueKeys, nameof(valueKeys))!;
            this.valueInitPlans = CloneArray(valueInitPlans, nameof(valueInitPlans))!;
            this.lifecycles = CloneArray(lifecycles, nameof(lifecycles))!;
            this.runtimeQueries = CloneArray(runtimeQueries, nameof(runtimeQueries))!;
            this.dependencies = CloneArray(dependencies, nameof(dependencies))!;
            this.commandExecutors = CloneArray(commandExecutors, nameof(commandExecutors), allowNull: true);
            this.commandPayloadSchemas = CloneArray(commandPayloadSchemas, nameof(commandPayloadSchemas), allowNull: true);
            this.sources = sources;
        }

        public string SelectedProfile { get; }

        public KernelProfileMask SelectedProfileMask { get; }

        public ReadOnlySpan<ModuleIR> Modules => modules;

        public ReadOnlySpan<ScopeIR> Scopes => scopes;

        public ReadOnlySpan<ServiceIR> Services => services;

        public ReadOnlySpan<CommandIR> Commands => commands;

        public ReadOnlySpan<ValueKeyIR> ValueKeys => valueKeys;

        public ReadOnlySpan<ValueInitPlanIR> ValueInitPlans => valueInitPlans;

        public ReadOnlySpan<LifecycleIR> Lifecycles => lifecycles;

        public ReadOnlySpan<RuntimeQueryIR> RuntimeQueries => runtimeQueries;

        public ReadOnlySpan<DependencyEdgeIR> Dependencies => dependencies;

        public bool HasCommandExecutorRegistry => commandExecutors != null;

        public ReadOnlySpan<CommandExecutorId> CommandExecutors => commandExecutors ?? Array.Empty<CommandExecutorId>();

        public bool HasCommandPayloadSchemaRegistry => commandPayloadSchemas != null;

        public ReadOnlySpan<CommandPayloadSchemaId> CommandPayloadSchemas => commandPayloadSchemas ?? Array.Empty<CommandPayloadSchemaId>();

        public SourceLocationTable Sources => sources;

        public static DependencyValidationInput FromKernelIR(KernelIR kernelIR)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            return new DependencyValidationInput(
                kernelIR.Profile.Id,
                kernelIR.Profile.Mask,
                CopySpan(kernelIR.Modules),
                CopySpan(kernelIR.Scopes),
                CopySpan(kernelIR.Services),
                CopySpan(kernelIR.Commands),
                CopySpan(kernelIR.ValueKeys),
                CopySpan(kernelIR.Lifecycles),
                CopySpan(kernelIR.RuntimeQueries),
                CopySpan(kernelIR.Dependencies),
                null,
                null,
                kernelIR.Sources,
                CopySpan(kernelIR.ValueInitPlans));
        }

        static T[]? CloneArray<T>(T[]? source, string parameterName, bool allowNull = false)
        {
            if (allowNull && source == null)
                return null;

            if (source == null || source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] is null)
                    throw new ArgumentException("Validation input arrays must not contain null items.", parameterName);

                clone[index] = source[index];
            }

            return clone;
        }

        static T[] CopySpan<T>(ReadOnlySpan<T> source)
        {
            if (source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clone[index] = source[index];
            }

            return clone;
        }
    }

    public static class DependencyValidator
    {
        static readonly IDependencyValidationRule[] Rules =
        {
            new DuplicateIdentityValidationRule(),
            new MissingRequiredDependencyValidationRule(),
            new OptionalAbsenceBehaviorValidationRule(),
            new InvalidDependencyKindValidationRule(),
            new WrongDomainDependencyValidationRule(),
            new PhaseAwareCycleDetectionValidationRule(),
            new LegacyLeakageValidationRule(),
        };

        public static DependencyValidationReport Validate(DependencyValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            List<DependencyValidationIssue> issues = new List<DependencyValidationIssue>();
            for (int ruleIndex = 0; ruleIndex < Rules.Length; ruleIndex++)
            {
                Rules[ruleIndex].CollectIssues(input, issues);
            }

            issues.Sort(CompareIssues);
            return new DependencyValidationReport(input.SelectedProfile, issues.ToArray());
        }

        public static DependencyValidationReport Validate(KernelIR kernelIR)
        {
            return Validate(DependencyValidationInput.FromKernelIR(kernelIR));
        }

        static int CompareIssues(DependencyValidationIssue left, DependencyValidationIssue right)
        {
            int result = ((int)right.Severity).CompareTo((int)left.Severity);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (result != 0)
                return result;

            result = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (result != 0)
                return result;

            result = CompareDependencyNode(left.From, right.From);
            if (result != 0)
                return result;

            if (left.To.HasValue && right.To.HasValue)
            {
                result = CompareDependencyNode(left.To.Value, right.To.Value);
                if (result != 0)
                    return result;
            }
            else if (left.To.HasValue)
            {
                return 1;
            }
            else if (right.To.HasValue)
            {
                return -1;
            }

            result = left.Source.Value.CompareTo(right.Source.Value);
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(left.Message, right.Message);
        }

        static int CompareDependencyNode(DependencyNodeIR left, DependencyNodeIR right)
        {
            int result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0)
                return result;

            switch (left.Kind)
            {
                case DependencyNodeKind.Module:
                    return left.ModuleId.Value.CompareTo(right.ModuleId.Value);
                case DependencyNodeKind.Service:
                    return left.ServiceId.Value.CompareTo(right.ServiceId.Value);
                case DependencyNodeKind.Scope:
                    return left.ScopePlanId.Value.CompareTo(right.ScopePlanId.Value);
                case DependencyNodeKind.Command:
                    return left.CommandTypeId.Value.CompareTo(right.CommandTypeId.Value);
                case DependencyNodeKind.ValueKey:
                    return left.ValueKeyId.Value.CompareTo(right.ValueKeyId.Value);
                case DependencyNodeKind.LifecycleStep:
                    return left.LifecycleStepId.Value.CompareTo(right.LifecycleStepId.Value);
                case DependencyNodeKind.RuntimeQuery:
                    return left.RuntimeQueryId.Value.CompareTo(right.RuntimeQueryId.Value);
                default:
                    return 0;
            }
        }
    }

    interface IDependencyValidationRule
    {
        void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues);
    }

    sealed class ValidationLookupContext
    {
        readonly Dictionary<int, ModuleIR> modulesById;
        readonly Dictionary<int, ScopeIR> scopesByPlanId;
        readonly Dictionary<int, ScopeIR> scopesByAuthoringId;
        readonly Dictionary<int, ServiceIR> servicesById;
        readonly Dictionary<int, CommandIR> commandsById;
        readonly Dictionary<int, ValueKeyIR> valueKeysById;
        readonly Dictionary<int, ValueInitPlanIR> valueInitPlansById;
        readonly Dictionary<int, RuntimeQueryIR> runtimeQueriesById;
        readonly Dictionary<int, LifecycleStepRecord> lifecycleStepsById;
        readonly HashSet<int>? commandExecutors;
        readonly HashSet<int>? commandPayloadSchemas;
        readonly KernelProfileMask selectedProfileMask;

        public ValidationLookupContext(DependencyValidationInput input)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            selectedProfileMask = input.SelectedProfileMask;
            modulesById = IndexModules(input.Modules);
            scopesByPlanId = IndexScopesByPlanId(input.Scopes);
            scopesByAuthoringId = IndexScopesByAuthoringId(input.Scopes);
            servicesById = IndexServices(input.Services);
            commandsById = IndexCommands(input.Commands);
            valueKeysById = IndexValueKeys(input.ValueKeys);
            valueInitPlansById = IndexValueInitPlans(input.ValueInitPlans);
            runtimeQueriesById = IndexRuntimeQueries(input.RuntimeQueries);
            lifecycleStepsById = IndexLifecycleSteps(input.Lifecycles);
            commandExecutors = input.HasCommandExecutorRegistry ? IndexValues(input.CommandExecutors) : null;
            commandPayloadSchemas = input.HasCommandPayloadSchemaRegistry ? IndexValues(input.CommandPayloadSchemas) : null;
        }

        public DependencyValidationInput Input { get; }

        public bool HasCommandExecutorRegistry => commandExecutors != null;

        public bool HasCommandPayloadSchemaRegistry => commandPayloadSchemas != null;

        public bool TryGetModule(ModuleId moduleId, out ModuleIR module)
        {
            return modulesById.TryGetValue(moduleId.Value, out module!);
        }

        public bool TryGetScopeByPlanId(ScopePlanId planId, out ScopeIR scope)
        {
            return scopesByPlanId.TryGetValue(planId.Value, out scope!);
        }

        public bool TryGetScopeByAuthoringId(ScopeAuthoringId authoringId, out ScopeIR scope)
        {
            return scopesByAuthoringId.TryGetValue(authoringId.Value, out scope!);
        }

        public bool TryGetService(ServiceId serviceId, out ServiceIR service)
        {
            return servicesById.TryGetValue(serviceId.Value, out service!);
        }

        public bool TryGetCommand(CommandTypeId commandTypeId, out CommandIR command)
        {
            return commandsById.TryGetValue(commandTypeId.Value, out command!);
        }

        public bool TryGetValueKey(ValueKeyId valueKeyId, out ValueKeyIR valueKey)
        {
            return valueKeysById.TryGetValue(valueKeyId.Value, out valueKey!);
        }

        public bool TryGetValueInitPlan(ValueInitPlanId valueInitPlanId, out ValueInitPlanIR valueInitPlan)
        {
            return valueInitPlansById.TryGetValue(valueInitPlanId.Value, out valueInitPlan!);
        }

        public bool TryGetRuntimeQuery(RuntimeQueryId runtimeQueryId, out RuntimeQueryIR runtimeQuery)
        {
            return runtimeQueriesById.TryGetValue(runtimeQueryId.Value, out runtimeQuery!);
        }

        public bool TryGetLifecycleStep(LifecycleStepId lifecycleStepId, out LifecycleStepRecord step)
        {
            return lifecycleStepsById.TryGetValue(lifecycleStepId.Value, out step);
        }

        public bool ContainsCommandExecutor(CommandExecutorId commandExecutorId)
        {
            return commandExecutors != null && commandExecutors.Contains(commandExecutorId.Value);
        }

        public bool ContainsCommandPayloadSchema(CommandPayloadSchemaId commandPayloadSchemaId)
        {
            return commandPayloadSchemas != null && commandPayloadSchemas.Contains(commandPayloadSchemaId.Value);
        }

        public bool IsModuleAvailable(ModuleId moduleId)
        {
            return TryGetModule(moduleId, out ModuleIR module) && IsModuleAvailable(module);
        }

        public bool IsModuleAvailable(ModuleIR module)
        {
            AvailabilityIR availability = module.Availability.Value;
            if (!availability.EnabledByDefault)
                return false;

            if (selectedProfileMask == KernelProfileMask.None)
                return true;

            return (availability.Profiles & selectedProfileMask) != 0;
        }

        public bool IsScopeAvailable(ScopeIR scope)
        {
            return IsModuleAvailable(scope.OwnerModule);
        }

        public bool IsServiceAvailable(ServiceIR service)
        {
            return IsModuleAvailable(service.OwnerModule);
        }

        public bool IsValueKeyAvailable(ValueKeyIR valueKey)
        {
            return IsModuleAvailable(valueKey.OwnerModule);
        }

        public bool IsValueInitPlanAvailable(ValueInitPlanIR valueInitPlan)
        {
            return IsModuleAvailable(valueInitPlan.OwnerModule) && IsAvailabilityEnabled(valueInitPlan.Availability);
        }

        public bool IsRuntimeQueryAvailable(RuntimeQueryIR runtimeQuery)
        {
            return IsModuleAvailable(runtimeQuery.OwnerModule);
        }

        public bool IsCommandAvailable(CommandIR command)
        {
            return IsModuleAvailable(command.OwnerModule);
        }

        public bool IsLifecycleStepAvailable(LifecycleStepRecord step)
        {
            return IsModuleAvailable(step.OwnerModule);
        }

        public bool HasAvailableValueStoreTarget(string? targetStoreRef, LifecyclePhase phase)
        {
            if (string.IsNullOrWhiteSpace(targetStoreRef))
                return false;

            ReadOnlySpan<ValueInitPlanIR> valueInitPlans = Input.ValueInitPlans;
            for (int index = 0; index < valueInitPlans.Length; index++)
            {
                ValueInitPlanIR valueInitPlan = valueInitPlans[index];
                if (!StringComparer.Ordinal.Equals(valueInitPlan.TargetStoreRef, targetStoreRef))
                    continue;

                if (valueInitPlan.ExecutionPhase != phase)
                    continue;

                if (!IsValueInitPlanAvailable(valueInitPlan))
                    continue;

                if (!TryGetScopeByPlanId(valueInitPlan.TargetScopePlanId, out ScopeIR scope) || !IsScopeAvailable(scope))
                    continue;

                return true;
            }

            return false;
        }

        public bool IsNodeAvailable(DependencyNodeIR node)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Module:
                    return IsModuleAvailable(node.ModuleId);
                case DependencyNodeKind.Service:
                    return TryGetService(node.ServiceId, out ServiceIR service) && IsServiceAvailable(service);
                case DependencyNodeKind.Scope:
                    return TryGetScopeByPlanId(node.ScopePlanId, out ScopeIR scope) && IsScopeAvailable(scope);
                case DependencyNodeKind.Command:
                    return TryGetCommand(node.CommandTypeId, out CommandIR command) && IsCommandAvailable(command);
                case DependencyNodeKind.ValueKey:
                    return TryGetValueKey(node.ValueKeyId, out ValueKeyIR valueKey) && IsValueKeyAvailable(valueKey);
                case DependencyNodeKind.LifecycleStep:
                    return TryGetLifecycleStep(node.LifecycleStepId, out LifecycleStepRecord step) && IsLifecycleStepAvailable(step);
                case DependencyNodeKind.RuntimeQuery:
                    return TryGetRuntimeQuery(node.RuntimeQueryId, out RuntimeQueryIR runtimeQuery) && IsRuntimeQueryAvailable(runtimeQuery);
                default:
                    return false;
            }
        }

        bool IsAvailabilityEnabled(AvailabilityIR availability)
        {
            if (!availability.EnabledByDefault)
                return false;

            if (selectedProfileMask == KernelProfileMask.None)
                return true;

            return (availability.Profiles & selectedProfileMask) != 0;
        }

        public bool TryResolveOwnerModule(DependencyNodeIR node, out ModuleId ownerModule)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Module:
                    ownerModule = node.ModuleId;
                    return true;
                case DependencyNodeKind.Service:
                    if (TryGetService(node.ServiceId, out ServiceIR service))
                    {
                        ownerModule = service.OwnerModule;
                        return true;
                    }

                    break;
                case DependencyNodeKind.Scope:
                    if (TryGetScopeByPlanId(node.ScopePlanId, out ScopeIR scope))
                    {
                        ownerModule = scope.OwnerModule;
                        return true;
                    }

                    break;
                case DependencyNodeKind.Command:
                    if (TryGetCommand(node.CommandTypeId, out CommandIR command))
                    {
                        ownerModule = command.OwnerModule;
                        return true;
                    }

                    break;
                case DependencyNodeKind.ValueKey:
                    if (TryGetValueKey(node.ValueKeyId, out ValueKeyIR valueKey))
                    {
                        ownerModule = valueKey.OwnerModule;
                        return true;
                    }

                    break;
                case DependencyNodeKind.LifecycleStep:
                    if (TryGetLifecycleStep(node.LifecycleStepId, out LifecycleStepRecord lifecycleStep))
                    {
                        ownerModule = lifecycleStep.OwnerModule;
                        return true;
                    }

                    break;
                case DependencyNodeKind.RuntimeQuery:
                    if (TryGetRuntimeQuery(node.RuntimeQueryId, out RuntimeQueryIR runtimeQuery))
                    {
                        ownerModule = runtimeQuery.OwnerModule;
                        return true;
                    }

                    break;
            }

            ownerModule = default;
            return false;
        }

        public bool TryGetSource(SourceLocationId sourceId, out SourceLocationIR source)
        {
            if (Input.Sources == null)
            {
                source = default;
                return false;
            }

            return Input.Sources.TryGetSource(sourceId, out source);
        }

        public bool IsLegacySource(SourceLocationId sourceId)
        {
            return TryGetSource(sourceId, out SourceLocationIR source) && source.Kind == SourceLocationKind.Legacy;
        }

        static Dictionary<int, ModuleIR> IndexModules(ReadOnlySpan<ModuleIR> modules)
        {
            Dictionary<int, ModuleIR> result = new Dictionary<int, ModuleIR>();
            for (int index = 0; index < modules.Length; index++)
            {
                result[modules[index].Id.Value] = modules[index];
            }

            return result;
        }

        static Dictionary<int, ScopeIR> IndexScopesByPlanId(ReadOnlySpan<ScopeIR> scopes)
        {
            Dictionary<int, ScopeIR> result = new Dictionary<int, ScopeIR>();
            for (int index = 0; index < scopes.Length; index++)
            {
                result[scopes[index].PlanId.Value] = scopes[index];
            }

            return result;
        }

        static Dictionary<int, ScopeIR> IndexScopesByAuthoringId(ReadOnlySpan<ScopeIR> scopes)
        {
            Dictionary<int, ScopeIR> result = new Dictionary<int, ScopeIR>();
            for (int index = 0; index < scopes.Length; index++)
            {
                result[scopes[index].AuthoringId.Value] = scopes[index];
            }

            return result;
        }

        static Dictionary<int, ServiceIR> IndexServices(ReadOnlySpan<ServiceIR> services)
        {
            Dictionary<int, ServiceIR> result = new Dictionary<int, ServiceIR>();
            for (int index = 0; index < services.Length; index++)
            {
                result[services[index].Id.Value] = services[index];
            }

            return result;
        }

        static Dictionary<int, CommandIR> IndexCommands(ReadOnlySpan<CommandIR> commands)
        {
            Dictionary<int, CommandIR> result = new Dictionary<int, CommandIR>();
            for (int index = 0; index < commands.Length; index++)
            {
                result[commands[index].TypeId.Value] = commands[index];
            }

            return result;
        }

        static Dictionary<int, ValueKeyIR> IndexValueKeys(ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            Dictionary<int, ValueKeyIR> result = new Dictionary<int, ValueKeyIR>();
            for (int index = 0; index < valueKeys.Length; index++)
            {
                result[valueKeys[index].Id.Value] = valueKeys[index];
            }

            return result;
        }

        static Dictionary<int, ValueInitPlanIR> IndexValueInitPlans(ReadOnlySpan<ValueInitPlanIR> valueInitPlans)
        {
            Dictionary<int, ValueInitPlanIR> result = new Dictionary<int, ValueInitPlanIR>();
            for (int index = 0; index < valueInitPlans.Length; index++)
            {
                result[valueInitPlans[index].PlanId.Value] = valueInitPlans[index];
            }

            return result;
        }

        static Dictionary<int, RuntimeQueryIR> IndexRuntimeQueries(ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            Dictionary<int, RuntimeQueryIR> result = new Dictionary<int, RuntimeQueryIR>();
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                result[runtimeQueries[index].Id.Value] = runtimeQueries[index];
            }

            return result;
        }

        static Dictionary<int, LifecycleStepRecord> IndexLifecycleSteps(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            Dictionary<int, LifecycleStepRecord> result = new Dictionary<int, LifecycleStepRecord>();
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    result[steps[stepIndex].Id.Value] = new LifecycleStepRecord(lifecycle.OwnerModule, steps[stepIndex]);
                }
            }

            return result;
        }

        static HashSet<int> IndexValues<T>(ReadOnlySpan<T> values) where T : struct
        {
            HashSet<int> result = new HashSet<int>();
            for (int index = 0; index < values.Length; index++)
            {
                result.Add(GetValue(values[index]));
            }

            return result;
        }

        static int GetValue<T>(T value) where T : struct
        {
            object boxed = value;
            switch (boxed)
            {
                case CommandExecutorId commandExecutorId:
                    return commandExecutorId.Value;
                case CommandPayloadSchemaId commandPayloadSchemaId:
                    return commandPayloadSchemaId.Value;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), typeof(T).Name, "Unsupported validation registry value type.");
            }
        }

        public readonly struct LifecycleStepRecord
        {
            public LifecycleStepRecord(ModuleId ownerModule, LifecycleStepIR step)
            {
                OwnerModule = ownerModule;
                Step = step;
            }

            public ModuleId OwnerModule { get; }

            public LifecycleStepIR Step { get; }
        }
    }

    sealed class MissingRequiredDependencyValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidationLookupContext context = new ValidationLookupContext(input);
            ValidateModules(context, issues);
            ValidateScopes(context, issues);
            ValidateValueInitPlans(context, issues);
            ValidateServices(context, issues);
            ValidateLifecycles(context, issues);
            ValidateCommands(context, issues);
        }

        static void ValidateModules(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ModuleIR> modules = context.Input.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                ModuleIR module = modules[index];
                ReadOnlySpan<ModuleDependencyIR> requiredModules = module.RequiredModules;
                for (int dependencyIndex = 0; dependencyIndex < requiredModules.Length; dependencyIndex++)
                {
                    ModuleDependencyIR dependency = requiredModules[dependencyIndex];
                    if (context.IsModuleAvailable(dependency.ModuleId))
                        continue;

                    issues.Add(new DependencyValidationIssue(
                        "DEP_MODULE_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.CrossNode,
                        new DependencyNodeIR(module.Id),
                        new DependencyNodeIR(dependency.ModuleId),
                        ValidationPhase.Build,
                        module.Id,
                        dependency.Source,
                        context.Input.SelectedProfile,
                        "Required module dependency is missing or unavailable for the selected profile.",
                        "Add the required module contribution or enable it for the selected profile."));
                }
            }
        }

        static void ValidateScopes(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ScopeIR> scopes = context.Input.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                ValidateScopeParent(context, scope, issues);
                ValidateScopeServiceBoundary(context, scope, issues);

                ReadOnlySpan<ScopeServiceRequirementIR> requiredServices = scope.RequiredServices;
                for (int serviceIndex = 0; serviceIndex < requiredServices.Length; serviceIndex++)
                {
                    ScopeServiceRequirementIR requirement = requiredServices[serviceIndex];
                    if (requirement.Strength != DependencyStrength.Required)
                        continue;

                    if (context.TryGetService(requirement.ServiceId, out ServiceIR service) && context.IsServiceAvailable(service))
                        continue;

                    issues.Add(new DependencyValidationIssue(
                        "DEP_SERVICE_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.CrossNode,
                        new DependencyNodeIR(scope.PlanId),
                        new DependencyNodeIR(requirement.ServiceId),
                        ValidationPhase.Build,
                        scope.OwnerModule,
                        requirement.Source,
                        context.Input.SelectedProfile,
                        "Scope requires a service that is missing or unavailable for the selected profile.",
                        "Add the required service or enable its owner module for the selected profile."));
                }
            }
        }

        static void ValidateScopeServiceBoundary(ValidationLookupContext context, ScopeIR scope, List<DependencyValidationIssue> issues)
        {
            if (scope.RequiredServices.Length > 0 && !scope.ServiceBoundary.OwnsLocalServiceGraph)
            {
                issues.Add(new DependencyValidationIssue(
                    "DEP_SCOPE_SERVICE_BOUNDARY_INVALID",
                    ValidationSeverity.Error,
                    ValidationIssueCategory.CrossNode,
                    new DependencyNodeIR(scope.PlanId),
                    null,
                    ValidationPhase.Build,
                    scope.OwnerModule,
                    scope.ServiceBoundary.Source,
                    context.Input.SelectedProfile,
                    "Scopes with required services must own a local service boundary.",
                    "Change the scope boundary to own the local service graph or remove the scope-local service requirements."));
                return;
            }

            if (scope.ServiceBoundary.ReferencesParentServiceGraph && (scope.Kind == ScopeKind.Root || scope.Kind == ScopeKind.Detached))
            {
                issues.Add(new DependencyValidationIssue(
                    "DEP_SCOPE_SERVICE_BOUNDARY_INVALID",
                    ValidationSeverity.Error,
                    ValidationIssueCategory.CrossNode,
                    new DependencyNodeIR(scope.PlanId),
                    null,
                    ValidationPhase.Build,
                    scope.OwnerModule,
                    scope.ServiceBoundary.Source,
                    context.Input.SelectedProfile,
                    "Root and detached scopes cannot reference a parent service boundary.",
                    "Change the scope boundary to own the local service graph or re-author the scope as a child scope."));
            }
        }

        static void ValidateScopeParent(ValidationLookupContext context, ScopeIR scope, List<DependencyValidationIssue> issues)
        {
            if (scope.ParentAuthoringId.Value == 0)
            {
                if (scope.Kind == ScopeKind.Root || scope.Kind == ScopeKind.Detached)
                    return;

                issues.Add(new DependencyValidationIssue(
                    "DEP_SCOPE_PARENT_MISSING",
                    ValidationSeverity.Error,
                    ValidationIssueCategory.CrossNode,
                    new DependencyNodeIR(scope.PlanId),
                    null,
                    ValidationPhase.Build,
                    scope.OwnerModule,
                    scope.Source,
                    context.Input.SelectedProfile,
                    "Non-root scopes must declare an explicit parent scope.",
                    "Assign an explicit parent scope or mark the scope as a verified root."));
                return;
            }

            if (!context.TryGetScopeByAuthoringId(scope.ParentAuthoringId, out ScopeIR parent) || !context.IsScopeAvailable(parent))
            {
                issues.Add(new DependencyValidationIssue(
                    "DEP_SCOPE_PARENT_MISSING",
                    ValidationSeverity.Error,
                    ValidationIssueCategory.CrossNode,
                    new DependencyNodeIR(scope.PlanId),
                    null,
                    ValidationPhase.Build,
                    scope.OwnerModule,
                    scope.Source,
                    context.Input.SelectedProfile,
                    "Scope parent reference is missing or unavailable for the selected profile.",
                    "Add the referenced parent scope or enable its owner module for the selected profile."));
                return;
            }

            if (IsLegalParentScopeKind(parent.Kind, scope.Kind))
                return;

            issues.Add(new DependencyValidationIssue(
                "DEP_SCOPE_PARENT_KIND_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                new DependencyNodeIR(scope.PlanId),
                new DependencyNodeIR(parent.PlanId),
                ValidationPhase.Build,
                scope.OwnerModule,
                scope.Source,
                context.Input.SelectedProfile,
                "Scope parent kind is illegal for the child scope kind.",
                "Use a parent scope kind that is valid for the child scope kind."));
        }

        static void ValidateValueInitPlans(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ScopeIR> scopes = context.Input.Scopes;
            for (int scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
            {
                ScopeIR scope = scopes[scopeIndex];
                ReadOnlySpan<ScopeValueInitRefIR> scopeValueInitPlans = scope.ValueInitPlans;
                for (int valueInitIndex = 0; valueInitIndex < scopeValueInitPlans.Length; valueInitIndex++)
                {
                    ScopeValueInitRefIR valueInitRef = scopeValueInitPlans[valueInitIndex];
                    if (!context.TryGetValueInitPlan(valueInitRef.PlanId, out ValueInitPlanIR valueInitPlan) || !context.IsValueInitPlanAvailable(valueInitPlan))
                    {
                        issues.Add(new DependencyValidationIssue(
                            "DEP_VALUE_INIT_PLAN_MISSING",
                            ValidationSeverity.Error,
                            ValidationIssueCategory.LocalNode,
                            new DependencyNodeIR(scope.PlanId),
                            null,
                            ValidationPhase.Build,
                            scope.OwnerModule,
                            valueInitRef.Source,
                            context.Input.SelectedProfile,
                            "Scope references a value init plan that is missing or unavailable for the selected profile.",
                            "Add the referenced value init plan or enable its owner module for the selected profile."));
                        continue;
                    }

                    if (valueInitPlan.TargetScopePlanId != scope.PlanId)
                    {
                        issues.Add(new DependencyValidationIssue(
                            "DEP_VALUE_INIT_SCOPE_MISMATCH",
                            ValidationSeverity.Error,
                            ValidationIssueCategory.CrossNode,
                            new DependencyNodeIR(scope.PlanId),
                            new DependencyNodeIR(valueInitPlan.TargetScopePlanId),
                            ValidationPhase.Build,
                            scope.OwnerModule,
                            valueInitRef.Source,
                            context.Input.SelectedProfile,
                            "Scope references a value init plan whose target scope does not match the referencing scope.",
                            "Reference only value init plans whose TargetScopePlanId matches the owning scope."));
                    }
                }
            }

            ReadOnlySpan<ValueInitPlanIR> valueInitPlans = context.Input.ValueInitPlans;
            for (int planIndex = 0; planIndex < valueInitPlans.Length; planIndex++)
            {
                ValueInitPlanIR valueInitPlan = valueInitPlans[planIndex];
                if (!context.TryGetScopeByPlanId(valueInitPlan.TargetScopePlanId, out ScopeIR targetScope) || !context.IsScopeAvailable(targetScope))
                {
                    issues.Add(new DependencyValidationIssue(
                        "DEP_VALUE_INIT_TARGET_SCOPE_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.CrossNode,
                        new DependencyNodeIR(valueInitPlan.TargetScopePlanId),
                        null,
                        ValidationPhase.Build,
                        valueInitPlan.OwnerModule,
                        valueInitPlan.Source,
                        context.Input.SelectedProfile,
                        "Value init plan targets a scope that is missing or unavailable for the selected profile.",
                        "Add the target scope or enable its owner module for the selected profile."));
                    continue;
                }

                if (!context.IsValueInitPlanAvailable(valueInitPlan))
                    continue;

                ReadOnlySpan<ValueInitEntryIR> entries = valueInitPlan.Entries;
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ValueInitEntryIR entry = entries[entryIndex];
                    if (context.TryGetValueKey(entry.KeyId, out ValueKeyIR valueKey) && context.IsValueKeyAvailable(valueKey))
                        continue;

                    issues.Add(new DependencyValidationIssue(
                        "DEP_VALUE_INIT_KEY_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.CrossNode,
                        new DependencyNodeIR(valueInitPlan.TargetScopePlanId),
                        new DependencyNodeIR(entry.KeyId),
                        ValidationPhase.Build,
                        valueInitPlan.OwnerModule,
                        entry.Source,
                        context.Input.SelectedProfile,
                        "Value init plan entry targets a value key that is missing or unavailable for the selected profile.",
                        "Add the referenced value key or enable its owner module for the selected profile."));
                }
            }
        }

        static bool IsLegalParentScopeKind(ScopeKind parentKind, ScopeKind childKind)
        {
            switch (childKind)
            {
                case ScopeKind.Child:
                    return parentKind == ScopeKind.Root || parentKind == ScopeKind.Child;
                case ScopeKind.Dynamic:
                    return parentKind == ScopeKind.Root || parentKind == ScopeKind.Child || parentKind == ScopeKind.Dynamic;
                default:
                    return false;
            }
        }

        static void ValidateServices(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ServiceIR> services = context.Input.Services;
            for (int serviceIndex = 0; serviceIndex < services.Length; serviceIndex++)
            {
                ServiceIR service = services[serviceIndex];
                ReadOnlySpan<ServiceDependencyIR> dependencies = service.Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    ServiceDependencyIR dependency = dependencies[dependencyIndex];
                    if (TryCreateDeclaredTargetKindIssue(
                        service.OwnerModule,
                        context.Input.SelectedProfile,
                        new DependencyNodeIR(service.Id),
                        dependency.Target,
                        dependency.Source,
                        AllowedDeclaredServiceTargetKinds,
                        "Service dependencies must target ServiceId, ValueKeyId, or RuntimeQueryId identities.",
                        out DependencyValidationIssue? targetKindIssue))
                    {
                        issues.Add(targetKindIssue!);
                        continue;
                    }

                    if (dependency.Strength != DependencyStrength.Required)
                        continue;

                    if (TryCreateMissingNodeIssue(context, new DependencyNodeIR(service.Id), service.OwnerModule, dependency.Target, dependency.Source, out DependencyValidationIssue? issue))
                    {
                        issues.Add(issue!);
                    }
                }
            }
        }

        static void ValidateLifecycles(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<LifecycleIR> lifecycles = context.Input.Lifecycles;
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    LifecycleStepIR step = steps[stepIndex];
                    switch (step.Target.Kind)
                    {
                        case LifecycleTargetKind.Service:
                            if (context.TryGetService(step.Target.TargetService, out ServiceIR service) && context.IsServiceAvailable(service))
                                continue;

                            issues.Add(new DependencyValidationIssue(
                                "DEP_LIFECYCLE_TARGET_SERVICE_MISSING",
                                ValidationSeverity.Error,
                                ValidationIssueCategory.CrossNode,
                                new DependencyNodeIR(step.Id),
                                new DependencyNodeIR(step.Target.TargetService),
                                ConvertPhase(step.Phase),
                                lifecycle.OwnerModule,
                                step.Source,
                                context.Input.SelectedProfile,
                                "Lifecycle step targets a service that is missing or unavailable for the selected profile.",
                                "Add the referenced service or enable its owner module for the selected profile."));
                            break;

                        case LifecycleTargetKind.Scope:
                            if (context.TryGetScopeByPlanId(step.Target.TargetScope, out ScopeIR scope) && context.IsScopeAvailable(scope))
                                continue;

                            issues.Add(CreateInvalidLifecycleTargetIssue(lifecycle, step, context));
                            break;

                        case LifecycleTargetKind.RuntimeQuery:
                            if (context.TryGetRuntimeQuery(step.Target.TargetRuntimeQuery, out RuntimeQueryIR runtimeQuery) && context.IsRuntimeQueryAvailable(runtimeQuery))
                                continue;

                            issues.Add(CreateInvalidLifecycleTargetIssue(lifecycle, step, context));
                            break;

                        case LifecycleTargetKind.RuntimeObjectOwner:
                        case LifecycleTargetKind.LegacyAdapter:
                        case LifecycleTargetKind.ValueStore:
                            issues.Add(CreateUnsupportedLifecycleLocalTargetIssue(lifecycle, step, context));
                            break;

                        default:
                            issues.Add(CreateInvalidLifecycleTargetKindIssue(lifecycle, step, context));
                            break;
                    }
                }
            }
        }

        static DependencyValidationIssue CreateInvalidLifecycleTargetIssue(LifecycleIR lifecycle, LifecycleStepIR step, ValidationLookupContext context)
        {
            DependencyNodeIR? to = null;
            switch (step.Target.Kind)
            {
                case LifecycleTargetKind.Scope:
                    to = new DependencyNodeIR(step.Target.TargetScope);
                    break;
                case LifecycleTargetKind.RuntimeQuery:
                    to = new DependencyNodeIR(step.Target.TargetRuntimeQuery);
                    break;
            }

            return new DependencyValidationIssue(
                "DEP_LIFECYCLE_TARGET_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                new DependencyNodeIR(step.Id),
                to,
                ConvertPhase(step.Phase),
                lifecycle.OwnerModule,
                step.Source,
                context.Input.SelectedProfile,
                "Lifecycle step target is missing or invalid for the declared target kind.",
                "Add a target that matches the declared lifecycle target kind.");
        }

        static DependencyValidationIssue CreateInvalidLifecycleTargetKindIssue(LifecycleIR lifecycle, LifecycleStepIR step, ValidationLookupContext context)
        {
            return new DependencyValidationIssue(
                "DEP_LIFECYCLE_TARGET_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                new DependencyNodeIR(step.Id),
                null,
                ConvertPhase(step.Phase),
                lifecycle.OwnerModule,
                step.Source,
                context.Input.SelectedProfile,
                "Lifecycle step target kind is not valid for dependency validation.",
                "Use a lifecycle target kind that is supported by the verified lifecycle model.");
        }

        static DependencyValidationIssue CreateUnsupportedLifecycleLocalTargetIssue(LifecycleIR lifecycle, LifecycleStepIR step, ValidationLookupContext context)
        {
            return new DependencyValidationIssue(
            "DEP_LIFECYCLE_TARGET_LOCAL_REF_UNSUPPORTED",
            ValidationSeverity.Error,
            ValidationIssueCategory.LocalNode,
            new DependencyNodeIR(step.Id),
            null,
            ConvertPhase(step.Phase),
            lifecycle.OwnerModule,
            step.Source,
            context.Input.SelectedProfile,
            "Lifecycle local-owner targets require a lower-spec verified runtime boundary that is not implemented in the current kernel runtime.",
            "Replace the step target with a verified service, scope, or runtime query target, or defer it until the lower-spec runtime boundary exists.");
        }

        static void ValidateCommands(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<CommandIR> commands = context.Input.Commands;
            for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                CommandIR command = commands[commandIndex];

                if (context.HasCommandExecutorRegistry && !context.ContainsCommandExecutor(command.Executor.Id))
                {
                    issues.Add(new DependencyValidationIssue(
                        "DEP_COMMAND_EXECUTOR_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        new DependencyNodeIR(command.TypeId),
                        null,
                        ValidationPhase.Build,
                        command.OwnerModule,
                        command.Executor.Source,
                        context.Input.SelectedProfile,
                        "Command executor reference is missing from the validated executor registry.",
                        "Add the referenced command executor to the validated input registry."));
                }

                if (context.HasCommandPayloadSchemaRegistry && !context.ContainsCommandPayloadSchema(command.PayloadSchema.Id))
                {
                    issues.Add(new DependencyValidationIssue(
                        "DEP_COMMAND_PAYLOAD_SCHEMA_MISSING",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        new DependencyNodeIR(command.TypeId),
                        null,
                        ValidationPhase.Build,
                        command.OwnerModule,
                        command.PayloadSchema.Source,
                        context.Input.SelectedProfile,
                        "Command payload schema reference is missing from the validated schema registry.",
                        "Add the referenced payload schema to the validated input registry."));
                }

                ReadOnlySpan<CommandDependencyIR> dependencies = command.Dependencies;
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    CommandDependencyIR dependency = dependencies[dependencyIndex];
                    if (TryCreateDeclaredTargetKindIssue(
                        command.OwnerModule,
                        context.Input.SelectedProfile,
                        new DependencyNodeIR(command.TypeId),
                        dependency.Target,
                        dependency.Source,
                        AllowedDeclaredCommandTargetKinds,
                        "Command dependencies must target ServiceId, ValueKeyId, or RuntimeQueryId identities.",
                        out DependencyValidationIssue? targetKindIssue))
                    {
                        issues.Add(targetKindIssue!);
                        continue;
                    }

                    if (dependency.Strength != DependencyStrength.Required)
                        continue;

                    if (TryCreateMissingNodeIssue(context, new DependencyNodeIR(command.TypeId), command.OwnerModule, dependency.Target, dependency.Source, out DependencyValidationIssue? issue))
                    {
                        issues.Add(issue!);
                    }
                }
            }
        }

        static bool TryCreateMissingNodeIssue(
            ValidationLookupContext context,
            DependencyNodeIR from,
            ModuleId ownerModule,
            DependencyNodeIR target,
            SourceLocationId source,
            out DependencyValidationIssue? issue)
        {
            switch (target.Kind)
            {
                case DependencyNodeKind.Module:
                    if (context.IsModuleAvailable(target.ModuleId))
                        break;

                    issue = CreateCrossNodeIssue(
                        "DEP_MODULE_MISSING",
                        from,
                        target,
                        ownerModule,
                        source,
                        context.Input.SelectedProfile,
                        "Required module dependency is missing or unavailable for the selected profile.",
                        "Add the required module contribution or enable it for the selected profile.");
                    return true;

                case DependencyNodeKind.Service:
                    if (context.TryGetService(target.ServiceId, out ServiceIR service) && context.IsServiceAvailable(service))
                        break;

                    issue = CreateCrossNodeIssue(
                        "DEP_SERVICE_MISSING",
                        from,
                        target,
                        ownerModule,
                        source,
                        context.Input.SelectedProfile,
                        "Required service dependency is missing or unavailable for the selected profile.",
                        "Add the required service or enable its owner module for the selected profile.");
                    return true;

                case DependencyNodeKind.ValueKey:
                    if (context.TryGetValueKey(target.ValueKeyId, out ValueKeyIR valueKey) && context.IsValueKeyAvailable(valueKey))
                        break;

                    issue = CreateCrossNodeIssue(
                        "DEP_VALUE_KEY_MISSING",
                        from,
                        target,
                        ownerModule,
                        source,
                        context.Input.SelectedProfile,
                        "Required value key dependency is missing or unavailable for the selected profile.",
                        "Add the referenced value key or enable its owner module for the selected profile.");
                    return true;

                case DependencyNodeKind.RuntimeQuery:
                    if (context.TryGetRuntimeQuery(target.RuntimeQueryId, out RuntimeQueryIR runtimeQuery) && context.IsRuntimeQueryAvailable(runtimeQuery))
                        break;

                    issue = CreateCrossNodeIssue(
                        "DEP_RUNTIME_QUERY_MISSING",
                        from,
                        target,
                        ownerModule,
                        source,
                        context.Input.SelectedProfile,
                        "Required runtime query dependency is missing or unavailable for the selected profile.",
                        "Add the referenced runtime query or enable its owner module for the selected profile.");
                    return true;
            }

            issue = null;
            return false;
        }

        static bool TryCreateDeclaredTargetKindIssue(
            ModuleId ownerModule,
            string profile,
            DependencyNodeIR from,
            DependencyNodeIR target,
            SourceLocationId source,
            DependencyNodeKind[] allowedKinds,
            string message,
            out DependencyValidationIssue? issue)
        {
            for (int index = 0; index < allowedKinds.Length; index++)
            {
                if (target.Kind == allowedKinds[index])
                {
                    issue = null;
                    return false;
                }
            }

            issue = new DependencyValidationIssue(
                "DEP_IDENTITY_DOMAIN_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalEdge,
                from,
                target,
                ValidationPhase.Build,
                ownerModule,
                source,
                profile,
                message,
                "Use a dependency target in the allowed identity domain for this declaration.");
            return true;
        }

        static DependencyValidationIssue CreateCrossNodeIssue(
            string code,
            DependencyNodeIR from,
            DependencyNodeIR to,
            ModuleId ownerModule,
            SourceLocationId source,
            string profile,
            string message,
            string suggestedFix)
        {
            return new DependencyValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                from,
                to,
                ValidationPhase.Build,
                ownerModule,
                source,
                profile,
                message,
                suggestedFix);
        }

        static ValidationPhase ConvertPhase(LifecyclePhase phase)
        {
            switch (phase)
            {
                case LifecyclePhase.Boot:
                    return ValidationPhase.Boot;
                case LifecyclePhase.Acquire:
                    return ValidationPhase.Acquire;
                case LifecyclePhase.Tick:
                case LifecyclePhase.FixedTick:
                case LifecyclePhase.LateTick:
                    return ValidationPhase.Runtime;
                case LifecyclePhase.PreRelease:
                case LifecyclePhase.Release:
                case LifecyclePhase.Reset:
                case LifecyclePhase.Destroy:
                case LifecyclePhase.Dispose:
                    return ValidationPhase.Runtime;
                default:
                    return ValidationPhase.Build;
            }
        }

        static readonly DependencyNodeKind[] AllowedDeclaredServiceTargetKinds =
        {
            DependencyNodeKind.Service,
            DependencyNodeKind.ValueKey,
            DependencyNodeKind.RuntimeQuery,
        };

        static readonly DependencyNodeKind[] AllowedDeclaredCommandTargetKinds =
        {
            DependencyNodeKind.Service,
            DependencyNodeKind.ValueKey,
            DependencyNodeKind.RuntimeQuery,
        };
    }

    sealed class OptionalAbsenceBehaviorValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidationLookupContext context = new ValidationLookupContext(input);
            ReadOnlySpan<ModuleIR> modules = input.Modules;
            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                ModuleIR module = modules[moduleIndex];
                ReadOnlySpan<ModuleDependencyIR> optionalModules = module.OptionalModules;
                for (int dependencyIndex = 0; dependencyIndex < optionalModules.Length; dependencyIndex++)
                {
                    ModuleDependencyIR dependency = optionalModules[dependencyIndex];
                    if (context.IsModuleAvailable(dependency.ModuleId))
                        continue;

                    if (!dependency.AbsenceBehavior.HasValue)
                    {
                        issues.Add(CreateIssue(
                            "DEP_OPTIONAL_ABSENCE_BEHAVIOR_MISSING",
                            module,
                            dependency,
                            input.SelectedProfile,
                            "Optional module dependency is absent but no explicit absence behavior is declared.",
                            "Declare an explicit absence behavior for the optional module dependency."));
                        continue;
                    }

                    switch (dependency.AbsenceBehavior.Value)
                    {
                        case OptionalDependencyAbsenceBehavior.DisableContribution:
                        case OptionalDependencyAbsenceBehavior.EmitWarning:
                            break;

                        case OptionalDependencyAbsenceBehavior.UseExplicitAlternative:
                            if (context.IsModuleAvailable(dependency.AlternativeModuleId))
                                break;

                            issues.Add(CreateIssue(
                                "DEP_OPTIONAL_ALTERNATIVE_INVALID",
                                module,
                                dependency,
                                input.SelectedProfile,
                                "Optional module dependency declares an explicit alternative that is missing or unavailable for the selected profile.",
                                "Point the optional dependency to an alternative module that exists and is enabled for the selected profile."));
                            break;

                        case OptionalDependencyAbsenceBehavior.ProfileSpecificError:
                            if (!IsSelectedProfileInBoundary(input.SelectedProfile, dependency.ProfileSpecificErrorProfiles))
                                break;

                            issues.Add(CreateIssue(
                                "DEP_OPTIONAL_PROFILE_ERROR",
                                module,
                                dependency,
                                input.SelectedProfile,
                                "Optional module dependency is absent and the declared profile boundary upgrades the absence to an error.",
                                "Provide the optional module in this profile or choose a non-error absence behavior."));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(dependency), dependency.AbsenceBehavior.Value, "Unsupported optional absence behavior.");
                    }
                }
            }
        }

        static bool IsSelectedProfileInBoundary(string selectedProfile, KernelProfileMask boundary)
        {
            if (!Enum.TryParse(selectedProfile, true, out KernelProfileMask selectedProfileMask))
                return false;

            return (selectedProfileMask & boundary) != 0;
        }

        static DependencyValidationIssue CreateIssue(
            string code,
            ModuleIR module,
            ModuleDependencyIR dependency,
            string profile,
            string message,
            string suggestedFix)
        {
            return new DependencyValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                new DependencyNodeIR(module.Id),
                new DependencyNodeIR(dependency.ModuleId),
                ValidationPhase.Build,
                module.Id,
                dependency.Source,
                profile,
                message,
                suggestedFix);
        }
    }

    sealed class InvalidDependencyKindValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidationLookupContext context = new ValidationLookupContext(input);
            ReadOnlySpan<DependencyEdgeIR> dependencies = input.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                DependencyEdgeIR edge = dependencies[index];
                if (IsWrongDomainOwnedPair(edge.From.Kind, edge.To.Kind))
                    continue;

                if (IsAllowedRuntimeMediatedKind(edge))
                    continue;

                if (!TryGetExpectedKind(edge.From.Kind, edge.To.Kind, out DependencyKind expectedKind))
                {
                    ModuleId ownerModuleForUnknown = context.TryResolveOwnerModule(edge.From, out ModuleId resolvedOwnerForUnknown)
                        ? resolvedOwnerForUnknown
                        : default;

                    issues.Add(new DependencyValidationIssue(
                        "DEP_DEPENDENCY_KIND_INVALID",
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalEdge,
                        edge.From,
                        edge.To,
                        edge.Phase,
                        ownerModuleForUnknown,
                        edge.Source,
                        input.SelectedProfile,
                        "Declared dependency edge uses an unsupported node-kind pair for M3.3 validation.",
                        "Use a verified node-kind pair with an explicit dependency-kind rule."));
                    continue;
                }

                if (edge.Kind == expectedKind)
                    continue;

                ModuleId ownerModule = context.TryResolveOwnerModule(edge.From, out ModuleId resolvedOwnerModule)
                    ? resolvedOwnerModule
                    : default;

                issues.Add(new DependencyValidationIssue(
                    "DEP_DEPENDENCY_KIND_INVALID",
                    ValidationSeverity.Error,
                    ValidationIssueCategory.LocalEdge,
                    edge.From,
                    edge.To,
                    edge.Phase,
                    ownerModule,
                    edge.Source,
                    input.SelectedProfile,
                    "Declared dependency kind is invalid for the participating node kinds.",
                    "Change the dependency kind to " + expectedKind + "."));
            }
        }

        static bool TryGetExpectedKind(DependencyNodeKind fromKind, DependencyNodeKind toKind, out DependencyKind expectedKind)
        {
            if (fromKind == DependencyNodeKind.Module && toKind == DependencyNodeKind.Module)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Scope && toKind == DependencyNodeKind.Scope)
            {
                expectedKind = DependencyKind.Owns;
                return true;
            }

            if (fromKind == DependencyNodeKind.Scope && toKind == DependencyNodeKind.Service)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Service && toKind == DependencyNodeKind.Service)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Service && toKind == DependencyNodeKind.ValueKey)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Service && toKind == DependencyNodeKind.RuntimeQuery)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Command && toKind == DependencyNodeKind.Service)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Command && toKind == DependencyNodeKind.ValueKey)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            if (fromKind == DependencyNodeKind.Command && toKind == DependencyNodeKind.RuntimeQuery)
            {
                expectedKind = DependencyKind.Requires;
                return true;
            }

            expectedKind = DependencyKind.Unknown;
            return false;
        }

        static bool IsWrongDomainOwnedPair(DependencyNodeKind fromKind, DependencyNodeKind toKind)
        {
            return (fromKind == DependencyNodeKind.Command && toKind == DependencyNodeKind.Service)
                || (fromKind == DependencyNodeKind.RuntimeQuery && toKind == DependencyNodeKind.ValueKey)
                || (fromKind == DependencyNodeKind.LifecycleStep && toKind == DependencyNodeKind.RuntimeQuery);
        }

        static bool IsAllowedRuntimeMediatedKind(DependencyEdgeIR edge)
        {
            if (edge.Phase != DependencyPhase.Runtime)
                return false;

            switch (edge.RuntimeCycleMediation)
            {
                case RuntimeCycleMediationKind.LazyHandle:
                    return edge.From.Kind == DependencyNodeKind.Service
                        && edge.To.Kind == DependencyNodeKind.Service
                        && edge.Kind == DependencyKind.References;
                case RuntimeCycleMediationKind.EventChannel:
                    return edge.From.Kind == DependencyNodeKind.Service
                        && edge.To.Kind == DependencyNodeKind.Service
                        && edge.Kind == DependencyKind.Triggers;
                case RuntimeCycleMediationKind.RuntimeQuery:
                    return edge.Kind == DependencyKind.Requires
                        && (edge.From.Kind == DependencyNodeKind.RuntimeQuery || edge.To.Kind == DependencyNodeKind.RuntimeQuery);
                default:
                    return false;
            }
        }
    }

    sealed class PhaseAwareCycleDetectionValidationRule : IDependencyValidationRule
    {
        static readonly DependencyPhase[] OrderedPhases =
        {
            DependencyPhase.Build,
            DependencyPhase.Generate,
            DependencyPhase.Boot,
            DependencyPhase.Acquire,
            DependencyPhase.Runtime,
            DependencyPhase.Save,
            DependencyPhase.EditorOnly,
        };

        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidationLookupContext context = new ValidationLookupContext(input);
            for (int phaseIndex = 0; phaseIndex < OrderedPhases.Length; phaseIndex++)
            {
                DependencyPhase phase = OrderedPhases[phaseIndex];
                List<DependencyEdgeIR> phaseEdges = CollectPhaseEdges(context, phase);
                if (phaseEdges.Count == 0)
                    continue;

                List<CycleComponent> components = FindCycleComponents(phaseEdges);
                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    if (!TryCreateCycleIssue(context, phase, components[componentIndex], out DependencyValidationIssue? issue))
                        continue;

                    issues.Add(issue!);
                }
            }
        }

        static List<DependencyEdgeIR> CollectPhaseEdges(ValidationLookupContext context, DependencyPhase phase)
        {
            ReadOnlySpan<DependencyEdgeIR> dependencies = context.Input.Dependencies;
            List<DependencyEdgeIR> result = new List<DependencyEdgeIR>();
            for (int index = 0; index < dependencies.Length; index++)
            {
                DependencyEdgeIR edge = dependencies[index];
                if (edge.Phase != phase)
                    continue;

                if (!context.IsNodeAvailable(edge.From) || !context.IsNodeAvailable(edge.To))
                    continue;

                result.Add(edge);
            }

            result.Sort(CompareDependencyEdge);
            return result;
        }

        static List<CycleComponent> FindCycleComponents(List<DependencyEdgeIR> edges)
        {
            List<DependencyNodeIR> nodes = CollectNodes(edges);
            Dictionary<DependencyNodeIR, int> nodeIndices = new Dictionary<DependencyNodeIR, int>(nodes.Count);
            for (int index = 0; index < nodes.Count; index++)
            {
                nodeIndices.Add(nodes[index], index);
            }

            List<DependencyEdgeIR>[] adjacency = new List<DependencyEdgeIR>[nodes.Count];
            for (int index = 0; index < adjacency.Length; index++)
            {
                adjacency[index] = new List<DependencyEdgeIR>();
            }

            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                DependencyEdgeIR edge = edges[edgeIndex];
                adjacency[nodeIndices[edge.From]].Add(edge);
            }

            for (int index = 0; index < adjacency.Length; index++)
            {
                adjacency[index].Sort(CompareDependencyEdge);
            }

            int[] discovery = new int[nodes.Count];
            int[] lowLink = new int[nodes.Count];
            bool[] onStack = new bool[nodes.Count];
            Stack<int> stack = new Stack<int>();
            int nextIndex = 1;
            List<CycleComponent> components = new List<CycleComponent>();

            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                if (discovery[nodeIndex] != 0)
                    continue;

                VisitNode(nodeIndex, nodes, nodeIndices, adjacency, discovery, lowLink, onStack, stack, ref nextIndex, components);
            }

            return components;
        }

        static void VisitNode(
            int nodeIndex,
            List<DependencyNodeIR> nodes,
            Dictionary<DependencyNodeIR, int> nodeIndices,
            List<DependencyEdgeIR>[] adjacency,
            int[] discovery,
            int[] lowLink,
            bool[] onStack,
            Stack<int> stack,
            ref int nextIndex,
            List<CycleComponent> components)
        {
            discovery[nodeIndex] = nextIndex;
            lowLink[nodeIndex] = nextIndex;
            nextIndex++;
            stack.Push(nodeIndex);
            onStack[nodeIndex] = true;

            List<DependencyEdgeIR> outgoingEdges = adjacency[nodeIndex];
            for (int edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex++)
            {
                DependencyEdgeIR edge = outgoingEdges[edgeIndex];
                int targetIndex = nodeIndices[edge.To];
                if (discovery[targetIndex] == 0)
                {
                    VisitNode(targetIndex, nodes, nodeIndices, adjacency, discovery, lowLink, onStack, stack, ref nextIndex, components);
                    if (lowLink[targetIndex] < lowLink[nodeIndex])
                        lowLink[nodeIndex] = lowLink[targetIndex];
                }
                else if (onStack[targetIndex] && discovery[targetIndex] < lowLink[nodeIndex])
                {
                    lowLink[nodeIndex] = discovery[targetIndex];
                }
            }

            if (lowLink[nodeIndex] != discovery[nodeIndex])
                return;

            List<DependencyNodeIR> componentNodes = new List<DependencyNodeIR>();
            while (stack.Count > 0)
            {
                int currentIndex = stack.Pop();
                onStack[currentIndex] = false;
                componentNodes.Add(nodes[currentIndex]);
                if (currentIndex == nodeIndex)
                    break;
            }

            componentNodes.Sort(CompareDependencyNode);
            HashSet<DependencyNodeIR> componentNodeSet = new HashSet<DependencyNodeIR>(componentNodes);
            List<DependencyEdgeIR> componentEdges = new List<DependencyEdgeIR>();
            for (int sourceIndex = 0; sourceIndex < componentNodes.Count; sourceIndex++)
            {
                List<DependencyEdgeIR> candidateEdges = adjacency[nodeIndices[componentNodes[sourceIndex]]];
                for (int edgeIndex = 0; edgeIndex < candidateEdges.Count; edgeIndex++)
                {
                    if (componentNodeSet.Contains(candidateEdges[edgeIndex].To))
                        componentEdges.Add(candidateEdges[edgeIndex]);
                }
            }

            componentEdges.Sort(CompareDependencyEdge);
            if (!IsCycleComponent(componentNodes, componentEdges))
                return;

            components.Add(new CycleComponent(componentNodes.ToArray(), componentEdges.ToArray()));
        }

        static bool TryCreateCycleIssue(ValidationLookupContext context, DependencyPhase phase, CycleComponent component, out DependencyValidationIssue? issue)
        {
            switch (phase)
            {
                case DependencyPhase.Runtime:
                    if (IsVerifiedRuntimeCycle(component))
                    {
                        issue = null;
                        return false;
                    }

                    if (!HasUnverifiedRequiredEdge(component))
                    {
                        issue = null;
                        return false;
                    }

                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_RUNTIME_REQUIRED",
                        component,
                        "Runtime dependency cycle includes an unverified direct dependency edge.",
                        "Break the direct Runtime dependency cycle or declare explicit verified Runtime mediation on every participating cycle edge.");
                    return true;

                case DependencyPhase.Build:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_BUILD",
                        component,
                        "Build dependency graph contains a cycle.",
                        "Break the Build-phase dependency cycle so initialization remains acyclic.");
                    return true;

                case DependencyPhase.Generate:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_GENERATE",
                        component,
                        "Generate dependency graph contains a cycle.",
                        "Break the Generate-phase dependency cycle so artifact generation remains acyclic.");
                    return true;

                case DependencyPhase.Boot:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_BOOT",
                        component,
                        "Boot dependency graph contains a cycle.",
                        "Break the Boot-phase dependency cycle so boot order remains explicit.");
                    return true;

                case DependencyPhase.Acquire:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_ACQUIRE",
                        component,
                        "Acquire dependency graph contains a cycle.",
                        "Break the Acquire-phase dependency cycle so acquisition order remains explicit.");
                    return true;

                case DependencyPhase.Save:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_SAVE",
                        component,
                        "Save dependency graph contains a cycle.",
                        "Break the Save-phase dependency cycle unless a lower spec defines a verified bounded exception.");
                    return true;

                case DependencyPhase.EditorOnly:
                    issue = CreateCycleIssue(
                        context,
                        "DEP_CYCLE_EDITORONLY",
                        component,
                        "EditorOnly dependency graph contains a cycle.",
                        "Break the EditorOnly dependency cycle so editor validation remains deterministic.");
                    return true;

                default:
                    issue = null;
                    return false;
            }
        }

        static bool IsVerifiedRuntimeCycle(CycleComponent component)
        {
            for (int index = 0; index < component.Edges.Length; index++)
            {
                if (!IsVerifiedRuntimeCycleEdge(component, component.Edges[index]))
                    return false;
            }

            return component.Edges.Length > 0;
        }

        static bool HasUnverifiedRequiredEdge(CycleComponent component)
        {
            for (int index = 0; index < component.Edges.Length; index++)
            {
                if (component.Edges[index].Strength != DependencyStrength.Required)
                    continue;

                if (!IsVerifiedRuntimeCycleEdge(component, component.Edges[index]))
                    return true;
            }

            return false;
        }

        static bool IsVerifiedRuntimeCycleEdge(CycleComponent component, DependencyEdgeIR edge)
        {
            switch (edge.RuntimeCycleMediation)
            {
                case RuntimeCycleMediationKind.LazyHandle:
                    return edge.Kind == DependencyKind.References
                        && edge.From.Kind == DependencyNodeKind.Service
                        && edge.To.Kind == DependencyNodeKind.Service;

                case RuntimeCycleMediationKind.EventChannel:
                    return edge.Kind == DependencyKind.Triggers
                        && edge.From.Kind == DependencyNodeKind.Service
                        && edge.To.Kind == DependencyNodeKind.Service;

                case RuntimeCycleMediationKind.RuntimeQuery:
                    return edge.Kind == DependencyKind.Requires
                        && (edge.From.Kind == DependencyNodeKind.RuntimeQuery
                            || edge.To.Kind == DependencyNodeKind.RuntimeQuery
                            || ComponentContainsNodeKind(component, DependencyNodeKind.RuntimeQuery));

                default:
                    return false;
            }
        }

        static bool ComponentContainsNodeKind(CycleComponent component, DependencyNodeKind kind)
        {
            for (int index = 0; index < component.Nodes.Length; index++)
            {
                if (component.Nodes[index].Kind == kind)
                    return true;
            }

            return false;
        }

        static DependencyValidationIssue CreateCycleIssue(
            ValidationLookupContext context,
            string code,
            CycleComponent component,
            string message,
            string suggestedFix)
        {
            DependencyEdgeIR representative = component.Edges[0];
            ModuleId ownerModule = context.TryResolveOwnerModule(representative.From, out ModuleId resolvedOwnerModule)
                ? resolvedOwnerModule
                : (context.TryResolveOwnerModule(representative.To, out resolvedOwnerModule) ? resolvedOwnerModule : default);

            return new DependencyValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                representative.From,
                representative.To,
                representative.Phase,
                ownerModule,
                representative.Source,
                context.Input.SelectedProfile,
                message,
                suggestedFix);
        }

        static bool IsCycleComponent(List<DependencyNodeIR> componentNodes, List<DependencyEdgeIR> componentEdges)
        {
            if (componentNodes.Count > 1)
                return true;

            if (componentNodes.Count == 0)
                return false;

            for (int index = 0; index < componentEdges.Count; index++)
            {
                if (componentEdges[index].From == componentNodes[0] && componentEdges[index].To == componentNodes[0])
                    return true;
            }

            return false;
        }

        static List<DependencyNodeIR> CollectNodes(List<DependencyEdgeIR> edges)
        {
            List<DependencyNodeIR> nodes = new List<DependencyNodeIR>();
            HashSet<DependencyNodeIR> seen = new HashSet<DependencyNodeIR>();
            for (int index = 0; index < edges.Count; index++)
            {
                if (seen.Add(edges[index].From))
                    nodes.Add(edges[index].From);

                if (seen.Add(edges[index].To))
                    nodes.Add(edges[index].To);
            }

            nodes.Sort(CompareDependencyNode);
            return nodes;
        }

        static int CompareDependencyNode(DependencyNodeIR left, DependencyNodeIR right)
        {
            int result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0)
                return result;

            result = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (result != 0)
                return result;

            result = left.ServiceId.Value.CompareTo(right.ServiceId.Value);
            if (result != 0)
                return result;

            result = left.ScopePlanId.Value.CompareTo(right.ScopePlanId.Value);
            if (result != 0)
                return result;

            result = left.CommandTypeId.Value.CompareTo(right.CommandTypeId.Value);
            if (result != 0)
                return result;

            result = left.ValueKeyId.Value.CompareTo(right.ValueKeyId.Value);
            if (result != 0)
                return result;

            result = left.LifecycleStepId.Value.CompareTo(right.LifecycleStepId.Value);
            if (result != 0)
                return result;

            return left.RuntimeQueryId.Value.CompareTo(right.RuntimeQueryId.Value);
        }

        static int CompareDependencyEdge(DependencyEdgeIR left, DependencyEdgeIR right)
        {
            int result = CompareDependencyNode(left.From, right.From);
            if (result != 0)
                return result;

            result = CompareDependencyNode(left.To, right.To);
            if (result != 0)
                return result;

            result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0)
                return result;

            result = ((int)left.Phase).CompareTo((int)right.Phase);
            if (result != 0)
                return result;

            result = ((int)left.Strength).CompareTo((int)right.Strength);
            if (result != 0)
                return result;

            result = ((int)left.RuntimeCycleMediation).CompareTo((int)right.RuntimeCycleMediation);
            if (result != 0)
                return result;

            result = left.Source.Value.CompareTo(right.Source.Value);
            if (result != 0)
                return result;

            return left.Id.Value.CompareTo(right.Id.Value);
        }

        readonly struct CycleComponent
        {
            public CycleComponent(DependencyNodeIR[] nodes, DependencyEdgeIR[] edges)
            {
                Nodes = nodes;
                Edges = edges;
            }

            public DependencyNodeIR[] Nodes { get; }

            public DependencyEdgeIR[] Edges { get; }
        }
    }

    sealed class DuplicateIdentityValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidateDuplicateModules(input, issues);
            ValidateDuplicateServices(input, issues);
            ValidateDuplicateCommands(input, issues);
            ValidateDuplicateValueKeys(input, issues);
            ValidateDuplicateStableKeys(input, issues);
            ValidateDuplicateRuntimeQueries(input, issues);
            ValidateDuplicateLifecycleSteps(input, issues);
        }

        static void ValidateDuplicateModules(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, ModuleIR> seen = new Dictionary<int, ModuleIR>();
            ReadOnlySpan<ModuleIR> modules = input.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                ModuleIR module = modules[index];
                if (!seen.TryAdd(module.Id.Value, module))
                {
                    ModuleIR first = seen[module.Id.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_MODULE_DUPLICATE_ID",
                        new DependencyNodeIR(module.Id),
                        new DependencyNodeIR(first.Id),
                        module.Id,
                        module.Source,
                        input.SelectedProfile,
                        "Duplicate ModuleId detected. Module identities must be unique before runtime.",
                        "Assign a unique ModuleId to each module contribution."));
                }
            }
        }

        static void ValidateDuplicateServices(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, ServiceIR> seen = new Dictionary<int, ServiceIR>();
            ReadOnlySpan<ServiceIR> services = input.Services;
            for (int index = 0; index < services.Length; index++)
            {
                ServiceIR service = services[index];
                if (!seen.TryAdd(service.Id.Value, service))
                {
                    ServiceIR first = seen[service.Id.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_SERVICE_DUPLICATE_ID",
                        new DependencyNodeIR(service.Id),
                        new DependencyNodeIR(first.Id),
                        service.OwnerModule,
                        service.Source,
                        input.SelectedProfile,
                        "Duplicate ServiceId detected. Service identities must be unique before runtime.",
                        "Assign a unique ServiceId to each service contribution."));
                }
            }
        }

        static void ValidateDuplicateCommands(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, CommandIR> seen = new Dictionary<int, CommandIR>();
            Dictionary<int, CommandIR> seenAuthoringKeyIds = new Dictionary<int, CommandIR>();
            Dictionary<string, CommandIR> seenAuthoringKeys = new Dictionary<string, CommandIR>(StringComparer.Ordinal);
            ReadOnlySpan<CommandIR> commands = input.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                if (!seen.TryAdd(command.TypeId.Value, command))
                {
                    CommandIR first = seen[command.TypeId.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_COMMAND_DUPLICATE_ID",
                        new DependencyNodeIR(command.TypeId),
                        new DependencyNodeIR(first.TypeId),
                        command.OwnerModule,
                        command.Source,
                        input.SelectedProfile,
                        "Duplicate CommandTypeId detected. Command identities must be unique before runtime.",
                        "Assign a unique CommandTypeId to each command contribution."));
                }

                if (!seenAuthoringKeyIds.TryAdd(command.AuthoringKey.Id.Value, command))
                {
                    CommandIR first = seenAuthoringKeyIds[command.AuthoringKey.Id.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_COMMAND_AUTHORING_KEY_ID_DUPLICATE",
                        new DependencyNodeIR(command.TypeId),
                        new DependencyNodeIR(first.TypeId),
                        command.OwnerModule,
                        command.AuthoringKey.Source,
                        input.SelectedProfile,
                        "Duplicate CommandAuthoringKeyId detected. Preserved command authoring identities must be unique before runtime.",
                        "Assign a unique CommandAuthoringKeyId to each command contribution."));
                }

                if (!seenAuthoringKeys.TryAdd(command.AuthoringKey.Value, command))
                {
                    CommandIR first = seenAuthoringKeys[command.AuthoringKey.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_COMMAND_AUTHORING_KEY_DUPLICATE",
                        new DependencyNodeIR(command.TypeId),
                        new DependencyNodeIR(first.TypeId),
                        command.OwnerModule,
                        command.AuthoringKey.Source,
                        input.SelectedProfile,
                        "Duplicate normalized command authoring key detected. Preserved authoring keys must remain unique where command identity provenance is required.",
                        "Assign a unique normalized authoring key to each command contribution."));
                }
            }
        }

        static void ValidateDuplicateValueKeys(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, ValueKeyIR> seen = new Dictionary<int, ValueKeyIR>();
            ReadOnlySpan<ValueKeyIR> valueKeys = input.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                ValueKeyIR valueKey = valueKeys[index];
                if (!seen.TryAdd(valueKey.Id.Value, valueKey))
                {
                    ValueKeyIR first = seen[valueKey.Id.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_VALUE_KEY_DUPLICATE_ID",
                        new DependencyNodeIR(valueKey.Id),
                        new DependencyNodeIR(first.Id),
                        valueKey.OwnerModule,
                        valueKey.Source,
                        input.SelectedProfile,
                        "Duplicate ValueKeyId detected. Value identities must be unique before runtime.",
                        "Assign a unique ValueKeyId to each value contribution."));
                }
            }
        }

        static void ValidateDuplicateStableKeys(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<string, ValueKeyIR> seen = new Dictionary<string, ValueKeyIR>(StringComparer.Ordinal);
            ReadOnlySpan<ValueKeyIR> valueKeys = input.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                ValueKeyIR valueKey = valueKeys[index];
                if (!seen.TryAdd(valueKey.StableKey, valueKey))
                {
                    ValueKeyIR first = seen[valueKey.StableKey];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_VALUE_STABLE_KEY_DUPLICATE",
                        new DependencyNodeIR(valueKey.Id),
                        new DependencyNodeIR(first.Id),
                        valueKey.OwnerModule,
                        valueKey.Source,
                        input.SelectedProfile,
                        "Duplicate stable value key detected. Stable keys must be unique where runtime value identity is required.",
                        "Assign a unique StableKey to each value contribution."));
                }
            }
        }

        static void ValidateDuplicateRuntimeQueries(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, RuntimeQueryIR> seen = new Dictionary<int, RuntimeQueryIR>();
            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = input.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                RuntimeQueryIR runtimeQuery = runtimeQueries[index];
                if (!seen.TryAdd(runtimeQuery.Id.Value, runtimeQuery))
                {
                    RuntimeQueryIR first = seen[runtimeQuery.Id.Value];
                    issues.Add(CreateDuplicateIssue(
                        "DEP_RUNTIME_QUERY_DUPLICATE_ID",
                        new DependencyNodeIR(runtimeQuery.Id),
                        new DependencyNodeIR(first.Id),
                        runtimeQuery.OwnerModule,
                        runtimeQuery.Source,
                        input.SelectedProfile,
                        "Duplicate RuntimeQueryId detected. Runtime query identities must be unique before runtime.",
                        "Assign a unique RuntimeQueryId to each runtime query contribution."));
                }
            }
        }

        static void ValidateDuplicateLifecycleSteps(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            Dictionary<int, LifecycleStepRecord> seen = new Dictionary<int, LifecycleStepRecord>();
            ReadOnlySpan<LifecycleIR> lifecycles = input.Lifecycles;
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    LifecycleStepIR step = steps[stepIndex];
                    LifecycleStepRecord current = new LifecycleStepRecord(lifecycle.OwnerModule, step);
                    if (!seen.TryAdd(step.Id.Value, current))
                    {
                        LifecycleStepRecord first = seen[step.Id.Value];
                        issues.Add(CreateDuplicateIssue(
                            "DEP_LIFECYCLE_STEP_DUPLICATE_ID",
                            new DependencyNodeIR(step.Id),
                            new DependencyNodeIR(first.Step.Id),
                            lifecycle.OwnerModule,
                            step.Source,
                            input.SelectedProfile,
                            "Duplicate LifecycleStepId detected. Lifecycle step identities must be unique before runtime.",
                            "Assign a unique LifecycleStepId to each lifecycle step."));
                    }
                }
            }
        }

        static DependencyValidationIssue CreateDuplicateIssue(
            string code,
            DependencyNodeIR from,
            DependencyNodeIR to,
            ModuleId ownerModule,
            SourceLocationId source,
            string profile,
            string message,
            string suggestedFix)
        {
            return new DependencyValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalNode,
                from,
                to,
                ValidationPhase.Build,
                ownerModule,
                source,
                profile,
                message,
                suggestedFix);
        }

        readonly struct LifecycleStepRecord
        {
            public LifecycleStepRecord(ModuleId ownerModule, LifecycleStepIR step)
            {
                OwnerModule = ownerModule;
                Step = step;
            }

            public ModuleId OwnerModule { get; }

            public LifecycleStepIR Step { get; }
        }
    }

    sealed class LegacyLeakageValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ValidationLookupContext context = new ValidationLookupContext(input);
            ValidateModules(context, issues);
            ValidateLegacyOwnership(context, issues);
            ValidateModuleDependencyDirection(context, issues);
            ValidateEdgeDirection(context, issues);
        }

        static void ValidateModules(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            LegacyMigrationReport legacyReport = LegacyMigrationReport.Validate(context.Input);
            issues.AddRange(legacyReport.Issues);
        }

        static void ValidateLegacyOwnership(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ModuleIR> modules = context.Input.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                ValidateModuleSource(context, modules[index], issues);
                ValidateModuleDependencySources(context, modules[index], issues);
            }

            ReadOnlySpan<ScopeIR> scopes = context.Input.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ValidateOwnedSource(context, scopes[index].OwnerModule, new DependencyNodeIR(scopes[index].PlanId), scopes[index].Source, issues, "Scopes with legacy provenance must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<ServiceIR> services = context.Input.Services;
            for (int index = 0; index < services.Length; index++)
            {
                ValidateOwnedSource(context, services[index].OwnerModule, new DependencyNodeIR(services[index].Id), services[index].Source, issues, "Services with legacy provenance must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<CommandIR> commands = context.Input.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                ValidateOwnedSource(context, commands[index].OwnerModule, new DependencyNodeIR(commands[index].TypeId), commands[index].Source, issues, "Commands with legacy provenance must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = context.Input.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                ValidateOwnedSource(context, valueKeys[index].OwnerModule, new DependencyNodeIR(valueKeys[index].Id), valueKeys[index].Source, issues, "Value keys with legacy provenance must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = context.Input.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                ValidateOwnedSource(context, runtimeQueries[index].OwnerModule, new DependencyNodeIR(runtimeQueries[index].Id), runtimeQueries[index].Source, issues, "Runtime queries with legacy provenance must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<LifecycleIR> lifecycles = context.Input.Lifecycles;
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                ValidateOwnedSource(context, lifecycle.OwnerModule, new DependencyNodeIR(lifecycle.OwnerModule), lifecycle.Source, issues, "Lifecycles with legacy provenance must be owned by an explicit legacy bridge module.");
                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    ValidateOwnedSource(context, lifecycle.OwnerModule, new DependencyNodeIR(steps[stepIndex].Id), steps[stepIndex].Source, issues, "Lifecycle steps with legacy provenance must be owned by an explicit legacy bridge module.");
                }
            }

            ReadOnlySpan<DependencyEdgeIR> dependencies = context.Input.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                if (!context.TryResolveOwnerModule(dependencies[index].From, out ModuleId ownerModule))
                    continue;

                ValidateOwnedSource(context, ownerModule, dependencies[index].From, dependencies[index].Source, issues, "Dependency edges with legacy provenance must be owned by an explicit legacy bridge module.");
            }
        }

        static void ValidateModuleSource(ValidationLookupContext context, ModuleIR module, List<DependencyValidationIssue> issues)
        {
            if (!context.IsLegacySource(module.Source))
                return;

            if (module.LegacyCompat != null)
                return;

            issues.Add(CreateIssue(
                "LEGACY_MIGRATION_REQUIRED",
                ValidationSeverity.Error,
                new DependencyNodeIR(module.Id),
                null,
                module.Id,
                module.Source,
                context.Input.SelectedProfile,
                "Legacy-origin modules must be classified through an explicit legacy bridge descriptor.",
                "Declare legacy compatibility metadata for the module or complete migration into generated v2 input."));
        }

        static void ValidateModuleDependencySources(ValidationLookupContext context, ModuleIR module, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ModuleDependencyIR> requiredModules = module.RequiredModules;
            for (int index = 0; index < requiredModules.Length; index++)
            {
                ValidateOwnedSource(context, module.Id, new DependencyNodeIR(module.Id), requiredModules[index].Source, issues, "Legacy-origin module dependency declarations must be owned by an explicit legacy bridge module.");
            }

            ReadOnlySpan<ModuleDependencyIR> optionalModules = module.OptionalModules;
            for (int index = 0; index < optionalModules.Length; index++)
            {
                ValidateOwnedSource(context, module.Id, new DependencyNodeIR(module.Id), optionalModules[index].Source, issues, "Legacy-origin module dependency declarations must be owned by an explicit legacy bridge module.");
            }
        }

        static void ValidateOwnedSource(ValidationLookupContext context, ModuleId ownerModule, DependencyNodeIR from, SourceLocationId source, List<DependencyValidationIssue> issues, string message)
        {
            if (!context.IsLegacySource(source))
                return;

            if (context.TryGetModule(ownerModule, out ModuleIR module) && module.LegacyCompat != null)
                return;

            issues.Add(CreateLegacyIssue(
                context,
                from,
                ownerModule,
                source,
                GetOwnershipCode(from.Kind),
                message,
                "Move the legacy-origin data behind an explicit adapter module or complete migration into v2-owned normalized data."));
        }

        static void ValidateModuleDependencyDirection(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ModuleIR> modules = context.Input.Modules;
            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                ModuleIR module = modules[moduleIndex];
                if (IsLegacyBridgeModule(module))
                    continue;

                ReadOnlySpan<ModuleDependencyIR> requiredModules = module.RequiredModules;
                for (int dependencyIndex = 0; dependencyIndex < requiredModules.Length; dependencyIndex++)
                {
                    if (!TryCreateLegacyDirectionIssue(context, module.Id, new DependencyNodeIR(module.Id), requiredModules[dependencyIndex].ModuleId, requiredModules[dependencyIndex].Source, out DependencyValidationIssue? issue))
                        continue;

                    issues.Add(issue!);
                }

                ReadOnlySpan<ModuleDependencyIR> optionalModules = module.OptionalModules;
                for (int dependencyIndex = 0; dependencyIndex < optionalModules.Length; dependencyIndex++)
                {
                    if (!TryCreateLegacyDirectionIssue(context, module.Id, new DependencyNodeIR(module.Id), optionalModules[dependencyIndex].ModuleId, optionalModules[dependencyIndex].Source, out DependencyValidationIssue? issue))
                        continue;

                    issues.Add(issue!);
                }
            }
        }

        static void ValidateEdgeDirection(ValidationLookupContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<DependencyEdgeIR> dependencies = context.Input.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                DependencyEdgeIR edge = dependencies[index];
                if (!context.TryResolveOwnerModule(edge.From, out ModuleId fromOwnerModule))
                    continue;

                if (context.TryGetModule(fromOwnerModule, out ModuleIR fromModule) && IsLegacyBridgeModule(fromModule))
                    continue;

                if (!context.TryResolveOwnerModule(edge.To, out ModuleId toOwnerModule))
                    continue;

                if (!TryCreateLegacyDirectionIssue(context, fromOwnerModule, edge.From, toOwnerModule, edge.Source, out DependencyValidationIssue? issue))
                    continue;

                issues.Add(issue!);
            }
        }

        static bool TryCreateLegacyDirectionIssue(ValidationLookupContext context, ModuleId fromOwnerModule, DependencyNodeIR from, ModuleId targetModuleId, SourceLocationId source, out DependencyValidationIssue? issue)
        {
            if (!context.TryGetModule(targetModuleId, out ModuleIR targetModule) || !IsLegacyBridgeModule(targetModule))
            {
                issue = null;
                return false;
            }

            issue = CreateLegacyIssue(
                context,
                from,
                fromOwnerModule,
                source,
                GetDirectionCode(from.Kind),
                "New-kernel dependencies must not depend on legacy compatibility bridges as a truth or fallback source.",
                "Reverse the dependency so legacy adapts into v2 or complete migration so the v2 module depends only on v2-owned data and runtime.",
                ValidationSeverity.Error,
                new DependencyNodeIR(targetModuleId));
            return true;
        }

        static bool IsLegacyBridgeModule(ModuleIR module)
        {
            return module.LegacyCompat != null;
        }

        static bool IsRuntimeCapable(LegacyCompatKind kind)
        {
            return kind == LegacyCompatKind.RuntimeAdapter
                || kind == LegacyCompatKind.TemporaryBridge
                || kind == LegacyCompatKind.ForbiddenFallback;
        }

        static bool RequiresRemovalCondition(LegacyCompatKind kind)
        {
            return kind == LegacyCompatKind.RuntimeAdapter
                || kind == LegacyCompatKind.TemporaryBridge
                || kind == LegacyCompatKind.TestAdapter;
        }

        static bool IsProfileForbidden(LegacyCompatDescriptorIR legacyCompat, KernelProfileMask selectedProfileMask)
        {
            return legacyCompat.Kind == LegacyCompatKind.ForbiddenFallback
                || (legacyCompat.Profiles & selectedProfileMask) == KernelProfileMask.None;
        }

        static DependencyValidationIssue CreateIssue(
            string code,
            ValidationSeverity severity,
            DependencyNodeIR from,
            DependencyNodeIR? to,
            ModuleId ownerModule,
            SourceLocationId source,
            string profile,
            string message,
            string suggestedFix,
            DiagnosticPayloadEntry[]? additionalPayloadEntries = null)
        {
            return new DependencyValidationIssue(
                code,
                severity,
                ValidationIssueCategory.LegacyBoundary,
                from,
                to,
                ValidationPhase.Build,
                ownerModule,
                source,
                profile,
                message,
                suggestedFix,
                additionalPayloadEntries);
        }

        static DependencyValidationIssue CreateLegacyIssue(
            ValidationLookupContext context,
            DependencyNodeIR from,
            ModuleIR module,
            SourceLocationId source,
            string code,
            string message,
            string suggestedFix,
            ValidationSeverity severity = ValidationSeverity.Error,
            DependencyNodeIR? to = null)
        {
            return CreateLegacyIssue(context, from, module.Id, source, code, message, suggestedFix, severity, to);
        }

        static DependencyValidationIssue CreateLegacyIssue(
            ValidationLookupContext context,
            DependencyNodeIR from,
            ModuleId ownerModule,
            SourceLocationId source,
            string code,
            string message,
            string suggestedFix,
            ValidationSeverity severity = ValidationSeverity.Error,
            DependencyNodeIR? to = null)
        {
            if (!context.TryGetModule(ownerModule, out ModuleIR module) || module.LegacyCompat == null)
                return CreateIssue(code, severity, from, to, ownerModule, source, context.Input.SelectedProfile, message, suggestedFix);

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(7)
            {
                new DiagnosticPayloadEntry("LegacySystemName", DiagnosticPayloadValue.FromString(module.LegacyCompat.LegacySystemName)),
                new DiagnosticPayloadEntry("BridgeKind", DiagnosticPayloadValue.FromString(module.LegacyCompat.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetSubsystem", DiagnosticPayloadValue.FromString(module.LegacyCompat.TargetSubsystem)),
                new DiagnosticPayloadEntry("Profiles", DiagnosticPayloadValue.FromString(module.LegacyCompat.Profiles.ToString())),
                new DiagnosticPayloadEntry("RemovalStatus", DiagnosticPayloadValue.FromString(module.LegacyCompat.RemovalStatus.ToString())),
            };

            if (!string.IsNullOrWhiteSpace(module.LegacyCompat.DiagnosticsCode))
                payloadEntries.Add(new DiagnosticPayloadEntry("LegacyDiagnosticsCode", DiagnosticPayloadValue.FromString(module.LegacyCompat.DiagnosticsCode)));

            if (!string.IsNullOrWhiteSpace(module.LegacyCompat.RemovalCondition))
                payloadEntries.Add(new DiagnosticPayloadEntry("RemovalCondition", DiagnosticPayloadValue.FromString(module.LegacyCompat.RemovalCondition)));

            return CreateIssue(code, severity, from, to, ownerModule, source, context.Input.SelectedProfile, message, suggestedFix, payloadEntries.ToArray());
        }

        static string GetOwnershipCode(DependencyNodeKind kind)
        {
            switch (kind)
            {
                case DependencyNodeKind.Scope:
                    return "LEGACY_INSTALLER_DISCOVERY_FORBIDDEN";
                case DependencyNodeKind.Command:
                    return "LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN";
                case DependencyNodeKind.ValueKey:
                    return "LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN";
                case DependencyNodeKind.LifecycleStep:
                    return "LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN";
                case DependencyNodeKind.RuntimeQuery:
                    return "LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN";
                case DependencyNodeKind.Service:
                    return "LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN";
                default:
                    return "LEGACY_MIGRATION_REQUIRED";
            }
        }

        static string GetDirectionCode(DependencyNodeKind kind)
        {
            switch (kind)
            {
                case DependencyNodeKind.Command:
                    return "LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN";
                case DependencyNodeKind.ValueKey:
                    return "LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN";
                case DependencyNodeKind.LifecycleStep:
                    return "LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN";
                case DependencyNodeKind.RuntimeQuery:
                    return "LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN";
                case DependencyNodeKind.Scope:
                    return "LEGACY_INSTALLER_DISCOVERY_FORBIDDEN";
                case DependencyNodeKind.Service:
                    return "LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN";
                default:
                    return "LEGACY_CORE_DEPENDENCY_FORBIDDEN";
            }
        }
    }

    sealed class WrongDomainDependencyValidationRule : IDependencyValidationRule
    {
        public void CollectIssues(DependencyValidationInput input, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<DependencyEdgeIR> dependencies = input.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                DependencyEdgeIR edge = dependencies[index];
                if (!TryCreateIssue(input, edge, out DependencyValidationIssue? issue))
                    continue;

                issues.Add(issue!);
            }
        }

        static bool TryCreateIssue(DependencyValidationInput input, DependencyEdgeIR edge, out DependencyValidationIssue? issue)
        {
            ModuleId ownerModule = ResolveOwnerModule(input, edge.From);
            string? message = null;

            if (edge.From.Kind == DependencyNodeKind.Command && edge.To.Kind == DependencyNodeKind.Service)
            {
                message = "ServiceId cannot satisfy a CommandTypeId dependency target.";
            }
            else if (edge.From.Kind == DependencyNodeKind.RuntimeQuery && edge.To.Kind == DependencyNodeKind.ValueKey)
            {
                message = "ValueKeyId cannot satisfy a RuntimeQueryId dependency target.";
            }
            else if (edge.From.Kind == DependencyNodeKind.LifecycleStep && edge.To.Kind == DependencyNodeKind.RuntimeQuery)
            {
                message = "RuntimeQueryId cannot satisfy a LifecycleStepId dependency target.";
            }

            if (message == null)
            {
                issue = null;
                return false;
            }

            issue = new DependencyValidationIssue(
                "DEP_IDENTITY_DOMAIN_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalEdge,
                edge.From,
                edge.To,
                edge.Phase,
                ownerModule,
                edge.Source,
                input.SelectedProfile,
                message,
                "Use the correct identity domain for the dependency target.");
            return true;
        }

        static ModuleId ResolveOwnerModule(DependencyValidationInput input, DependencyNodeIR node)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Module:
                    return node.ModuleId;
                case DependencyNodeKind.Service:
                    return FindServiceOwner(input.Services, node.ServiceId);
                case DependencyNodeKind.Scope:
                    return FindScopeOwner(input.Scopes, node.ScopePlanId);
                case DependencyNodeKind.Command:
                    return FindCommandOwner(input.Commands, node.CommandTypeId);
                case DependencyNodeKind.ValueKey:
                    return FindValueKeyOwner(input.ValueKeys, node.ValueKeyId);
                case DependencyNodeKind.LifecycleStep:
                    return FindLifecycleOwner(input.Lifecycles, node.LifecycleStepId);
                case DependencyNodeKind.RuntimeQuery:
                    return FindRuntimeQueryOwner(input.RuntimeQueries, node.RuntimeQueryId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(node), node.Kind, "Validation edges must use defined node kinds.");
            }
        }

        static ModuleId FindServiceOwner(ReadOnlySpan<ServiceIR> services, ServiceId serviceId)
        {
            for (int index = 0; index < services.Length; index++)
            {
                if (services[index].Id == serviceId)
                    return services[index].OwnerModule;
            }

            throw new ArgumentException("Wrong-domain validation requires service nodes to resolve to an owning module.", nameof(serviceId));
        }

        static ModuleId FindScopeOwner(ReadOnlySpan<ScopeIR> scopes, ScopePlanId scopePlanId)
        {
            for (int index = 0; index < scopes.Length; index++)
            {
                if (scopes[index].PlanId == scopePlanId)
                    return scopes[index].OwnerModule;
            }

            throw new ArgumentException("Wrong-domain validation requires scope nodes to resolve to an owning module.", nameof(scopePlanId));
        }

        static ModuleId FindCommandOwner(ReadOnlySpan<CommandIR> commands, CommandTypeId commandTypeId)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                if (commands[index].TypeId == commandTypeId)
                    return commands[index].OwnerModule;
            }

            throw new ArgumentException("Wrong-domain validation requires command nodes to resolve to an owning module.", nameof(commandTypeId));
        }

        static ModuleId FindValueKeyOwner(ReadOnlySpan<ValueKeyIR> valueKeys, ValueKeyId valueKeyId)
        {
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (valueKeys[index].Id == valueKeyId)
                    return valueKeys[index].OwnerModule;
            }

            throw new ArgumentException("Wrong-domain validation requires value key nodes to resolve to an owning module.", nameof(valueKeyId));
        }

        static ModuleId FindLifecycleOwner(ReadOnlySpan<LifecycleIR> lifecycles, LifecycleStepId lifecycleStepId)
        {
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (steps[stepIndex].Id == lifecycleStepId)
                        return lifecycles[lifecycleIndex].OwnerModule;
                }
            }

            throw new ArgumentException("Wrong-domain validation requires lifecycle step nodes to resolve to an owning module.", nameof(lifecycleStepId));
        }

        static ModuleId FindRuntimeQueryOwner(ReadOnlySpan<RuntimeQueryIR> runtimeQueries, RuntimeQueryId runtimeQueryId)
        {
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (runtimeQueries[index].Id == runtimeQueryId)
                    return runtimeQueries[index].OwnerModule;
            }

            throw new ArgumentException("Wrong-domain validation requires runtime query nodes to resolve to an owning module.", nameof(runtimeQueryId));
        }
    }
}