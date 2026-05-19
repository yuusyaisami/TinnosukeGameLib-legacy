#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;

namespace Game.Kernel.Validation
{
    public static class ProjectionValidator
    {
        static readonly IProjectionValidationRule[] Rules =
        {
            new UnknownProjectedIdRule(),
            new DroppedMappingRule(),
            new DebugMapCoverageRule(),
        };

        public static ProjectionValidationReport Validate(ProjectionValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            ProjectionValidationContext context = new ProjectionValidationContext(input);
            List<DependencyValidationIssue> issues = new List<DependencyValidationIssue>();
            for (int ruleIndex = 0; ruleIndex < Rules.Length; ruleIndex++)
                Rules[ruleIndex].CollectIssues(context, issues);

            issues.Sort(CompareIssues);
            return new ProjectionValidationReport(input.SelectedProfile, issues);
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

    interface IProjectionValidationRule
    {
        void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues);
    }

    sealed class ProjectionValidationContext
    {
        readonly HashSet<RuntimeIdentityRef> sourceIdentities;
        readonly HashSet<RuntimeIdentityRef> projectedIdentities;
        readonly HashSet<RuntimeIdentityRef> coverageIdentities;

        public ProjectionValidationContext(ProjectionValidationInput input)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            sourceIdentities = BuildSourceIdentities(input.SourceKernelIR);
            projectedIdentities = BuildProjectedIdentities(input.Mappings);
            coverageIdentities = BuildCoverageIdentities(input.DebugMapCoverage);
        }

        public ProjectionValidationInput Input { get; }

        public bool HasSourceIdentity(RuntimeIdentityRef identity)
        {
            return sourceIdentities.Contains(identity);
        }

        public bool HasProjectedIdentity(RuntimeIdentityRef identity)
        {
            return projectedIdentities.Contains(identity);
        }

        public bool HasCoverage(RuntimeIdentityRef identity)
        {
            return coverageIdentities.Contains(identity);
        }

        public IEnumerable<RuntimeIdentityRef> EnumerateRequiredSourceIdentities()
        {
            ReadOnlySpan<ProjectionArtifactKind> selectedArtifacts = Input.SelectedArtifacts;
            HashSet<RuntimeIdentityRef> required = new HashSet<RuntimeIdentityRef>();

            for (int artifactIndex = 0; artifactIndex < selectedArtifacts.Length; artifactIndex++)
            {
                ProjectionArtifactKind artifactKind = selectedArtifacts[artifactIndex];
                switch (artifactKind)
                {
                    case ProjectionArtifactKind.ServiceGraph:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Services, service => new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value));
                        break;
                    case ProjectionArtifactKind.CommandCatalog:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, command.Executor.Id.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, command.PayloadSchema.Id.Value));
                        break;
                    case ProjectionArtifactKind.ValueSchema:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.ValueKeys, valueKey => new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKey.Id.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.ValueKeys, valueKey => new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKey.Schema.Id.Value));
                        break;
                    case ProjectionArtifactKind.RuntimeQuery:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.RuntimeQueries, runtimeQuery => new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, runtimeQuery.Id.Value));
                        break;
                    case ProjectionArtifactKind.ScopeGraph:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Scopes, scope => new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scope.AuthoringId.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Scopes, scope => new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value));
                        break;
                    case ProjectionArtifactKind.LifecyclePlan:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Lifecycles, lifecycle => new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycle.PlanId.Value));
                        for (int lifecycleIndex = 0; lifecycleIndex < Input.SourceKernelIR.Lifecycles.Length; lifecycleIndex++)
                        {
                            ReadOnlySpan<LifecycleStepIR> steps = Input.SourceKernelIR.Lifecycles[lifecycleIndex].Steps;
                            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                                required.Add(new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, steps[stepIndex].Id.Value));
                        }
                        break;
                }
            }

            List<RuntimeIdentityRef> ordered = new List<RuntimeIdentityRef>(required);
            ordered.Sort(CompareRuntimeIdentityRef);

            foreach (RuntimeIdentityRef identity in ordered)
                yield return identity;
        }

        static int CompareRuntimeIdentityRef(RuntimeIdentityRef left, RuntimeIdentityRef right)
        {
            int result = left.Kind.CompareTo(right.Kind);
            if (result != 0)
                return result;

            result = left.Value.CompareTo(right.Value);
            if (result != 0)
                return result;

            return left.Generation.CompareTo(right.Generation);
        }

        static HashSet<RuntimeIdentityRef> BuildSourceIdentities(KernelIR kernelIR)
        {
            HashSet<RuntimeIdentityRef> identities = new HashSet<RuntimeIdentityRef>();

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, modules[index].Id.Value));

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Service, services[index].Id.Value));

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scopes[index].AuthoringId.Value));
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scopes[index].PlanId.Value));
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, commands[index].TypeId.Value));
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, commands[index].Executor.Id.Value));
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, commands[index].PayloadSchema.Id.Value));
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKeys[index].Id.Value));
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKeys[index].Schema.Id.Value));
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycles[index].PlanId.Value));
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                    identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, steps[stepIndex].Id.Value));
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, runtimeQueries[index].Id.Value));

            return identities;
        }

        static HashSet<RuntimeIdentityRef> BuildProjectedIdentities(ReadOnlySpan<ProjectionMappingIR> mappings)
        {
            HashSet<RuntimeIdentityRef> identities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
                identities.Add(mappings[index].ProjectedIdentity);

            return identities;
        }

        static HashSet<RuntimeIdentityRef> BuildCoverageIdentities(ReadOnlySpan<RuntimeIdentityRef> coverage)
        {
            HashSet<RuntimeIdentityRef> identities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < coverage.Length; index++)
                identities.Add(coverage[index]);

            return identities;
        }

        static void AddRuntimeIdentities<T>(HashSet<RuntimeIdentityRef> target, ReadOnlySpan<T> items, Func<T, RuntimeIdentityRef> selector)
        {
            for (int index = 0; index < items.Length; index++)
                target.Add(selector(items[index]));
        }
    }

    sealed class UnknownProjectedIdRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ProjectionMappingIR> mappings = context.Input.Mappings;
            HashSet<RuntimeIdentityRef> reportedUnknownIdentities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                if (context.HasSourceIdentity(mapping.ProjectedIdentity))
                    continue;

                if (!reportedUnknownIdentities.Add(mapping.ProjectedIdentity))
                    continue;

                issues.Add(ProjectionValidationIssueFactoryBridge.CreateIssue(
                    context.Input.SelectedProfile,
                    mapping,
                    mapping.ProjectedIdentity,
                    ProjectionValidationRuleHelpers.GetUnknownProjectedCode(mapping.ProjectedIdentity.Kind),
                    "Generated projection introduced an identity that does not exist in the normalized source kernel IR.",
                    "Ensure the projection is derived from declared source identities before promotion."));
            }
        }
    }

    sealed class DroppedMappingRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ProjectionMappingIR> mappings = context.Input.Mappings;
            HashSet<RuntimeIdentityRef> mappedSources = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                mappedSources.Add(mapping.SourceIdentity);

                if (!mapping.HasProvenance)
                {
                    issues.Add(ProjectionValidationIssueFactoryBridge.CreateIssue(
                        context.Input.SelectedProfile,
                        mapping,
                        mapping.SourceIdentity,
                        "DEP_PROJECTION_PROVENANCE_MISSING",
                        "Generated projection dropped provenance required to explain the dependency through diagnostics.",
                        "Preserve the source location or explicit mapping provenance for every projected dependency."));
                }
            }

            AddMissingMappingIssues(context, issues, mappedSources);
        }

        static void AddMissingMappingIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues, HashSet<RuntimeIdentityRef> mappedSources)
        {
            foreach (RuntimeIdentityRef sourceIdentity in context.EnumerateRequiredSourceIdentities())
            {
                if (mappedSources.Contains(sourceIdentity))
                    continue;

                issues.Add(ProjectionValidationHelpersBridge.CreateMissingProjectionIssue(
                    context,
                    sourceIdentity,
                    context.Input.SelectedProfile));
            }
        }
    }

    sealed class DebugMapCoverageRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ProjectionMappingIR> mappings = context.Input.Mappings;
            HashSet<RuntimeIdentityRef> reportedMissingCoverage = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                if (!context.HasSourceIdentity(mapping.ProjectedIdentity))
                    continue;

                if (!context.HasCoverage(mapping.ProjectedIdentity))
                {
                    if (!reportedMissingCoverage.Add(mapping.ProjectedIdentity))
                        goto CheckOwnerModuleCoverage;

                    issues.Add(ProjectionValidationIssueFactoryBridge.CreateIssue(
                        context.Input.SelectedProfile,
                        mapping,
                        mapping.ProjectedIdentity,
                        "DEP_DEBUGMAP_COVERAGE_MISSING",
                        "Generated projection is missing DebugMap coverage for a runtime-facing identity.",
                        "Add the identity to the DebugMap coverage set for the selected profile."));
                }

            CheckOwnerModuleCoverage:
                RuntimeIdentityRef ownerModuleCoverage = new RuntimeIdentityRef(RuntimeIdentityKind.Module, mapping.OwnerModule.Value);
                if (!context.HasCoverage(ownerModuleCoverage))
                {
                    if (!reportedMissingCoverage.Add(ownerModuleCoverage))
                        continue;

                    issues.Add(ProjectionValidationIssueFactoryBridge.CreateIssue(
                        context.Input.SelectedProfile,
                        mapping,
                        ownerModuleCoverage,
                        "DEP_DEBUGMAP_COVERAGE_MISSING",
                        "Generated projection is missing DebugMap coverage for its owner module.",
                        "Add the owner module identity to the DebugMap coverage set for the selected profile."));
                }
            }
        }
    }

    static class ProjectionValidationIssueFactory
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            DependencyNodeIR node = CreateRepresentativeNode(mapping, identity);
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>
            {
                new DiagnosticPayloadEntry("RuntimeIdentityKind", DiagnosticPayloadValue.FromString(identity.Kind.ToString())),
                new DiagnosticPayloadEntry("RuntimeIdentityValue", DiagnosticPayloadValue.FromInt32(identity.Value)),
            };

            if (identity.Generation != 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("RuntimeIdentityGeneration", DiagnosticPayloadValue.FromInt32(identity.Generation)));

            payloadEntries.Add(new DiagnosticPayloadEntry("ProjectedIdentity", DiagnosticPayloadValue.FromString(identity.ToString())));
            payloadEntries.Add(new DiagnosticPayloadEntry("HasProvenance", DiagnosticPayloadValue.FromBoolean(mapping.HasProvenance)));

            return new DependencyValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.Projection,
                node,
                null,
                ValidationPhase.Generate,
                mapping.OwnerModule,
                mapping.Source,
                selectedProfile,
                message,
                suggestedFix,
                payloadEntries.ToArray());
        }

        public static DependencyValidationIssue CreateSourceLocationMissingIssue(
            string selectedProfile,
            RuntimeIdentityRef identity,
            ModuleId ownerModule)
        {
            DependencyNodeIR node = CreateRepresentativeNode(identity, ownerModule);
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>
            {
                new DiagnosticPayloadEntry("RuntimeIdentityKind", DiagnosticPayloadValue.FromString(identity.Kind.ToString())),
                new DiagnosticPayloadEntry("RuntimeIdentityValue", DiagnosticPayloadValue.FromInt32(identity.Value)),
                new DiagnosticPayloadEntry("HasSourceLocationProvenance", DiagnosticPayloadValue.FromBoolean(false)),
            };

            if (identity.Generation != 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("RuntimeIdentityGeneration", DiagnosticPayloadValue.FromInt32(identity.Generation)));

            return new DependencyValidationIssue(
                "DEP_DIAGNOSTICS_SOURCE_LOCATION_MISSING",
                ValidationSeverity.Error,
                ValidationIssueCategory.Projection,
                node,
                null,
                ValidationPhase.Generate,
                ownerModule,
                default,
                selectedProfile,
                "Generated projection could not preserve source location provenance for a validation issue.",
                "Attach a stable source location to the source identity before projecting it.",
                payloadEntries.ToArray(),
                allowMissingSourceLocation: true);
        }

        static DependencyNodeIR CreateRepresentativeNode(ProjectionMappingIR mapping, RuntimeIdentityRef identity)
        {
            return CreateRepresentativeNode(identity, mapping.OwnerModule);
        }

        static DependencyNodeIR CreateRepresentativeNode(RuntimeIdentityRef identity, ModuleId ownerModule)
        {
            switch (identity.Kind)
            {
                case RuntimeIdentityKind.Module:
                    return new DependencyNodeIR(new ModuleId(identity.Value));
                case RuntimeIdentityKind.Service:
                    return new DependencyNodeIR(new ServiceId(identity.Value));
                case RuntimeIdentityKind.ScopePlan:
                    return new DependencyNodeIR(new ScopePlanId(identity.Value));
                case RuntimeIdentityKind.CommandType:
                    return new DependencyNodeIR(new CommandTypeId(identity.Value));
                case RuntimeIdentityKind.ValueKey:
                    return new DependencyNodeIR(new ValueKeyId(identity.Value));
                case RuntimeIdentityKind.LifecycleStep:
                    return new DependencyNodeIR(new LifecycleStepId(identity.Value));
                case RuntimeIdentityKind.RuntimeQuery:
                    return new DependencyNodeIR(new RuntimeQueryId(identity.Value));
                default:
                    return new DependencyNodeIR(ownerModule);
            }
        }
    }

    static class ProjectionValidationRuleHelpers
    {
        public static string GetUnknownProjectedCode(RuntimeIdentityKind kind)
        {
            switch (kind)
            {
                case RuntimeIdentityKind.Module:
                    return "DEP_PROJECTION_UNKNOWN_MODULE_ID";
                case RuntimeIdentityKind.Service:
                    return "DEP_PROJECTION_UNKNOWN_SERVICE_ID";
                case RuntimeIdentityKind.ScopeAuthoring:
                    return "DEP_PROJECTION_UNKNOWN_SCOPE_AUTHORING_ID";
                case RuntimeIdentityKind.ScopePlan:
                    return "DEP_PROJECTION_UNKNOWN_SCOPE_PLAN_ID";
                case RuntimeIdentityKind.LifecyclePlan:
                    return "DEP_PROJECTION_UNKNOWN_LIFECYCLE_PLAN_ID";
                case RuntimeIdentityKind.LifecycleStep:
                    return "DEP_PROJECTION_UNKNOWN_LIFECYCLE_STEP_ID";
                case RuntimeIdentityKind.CommandType:
                    return "DEP_PROJECTION_UNKNOWN_COMMAND_TYPE_ID";
                case RuntimeIdentityKind.CommandExecutor:
                    return "DEP_PROJECTION_UNKNOWN_COMMAND_EXECUTOR_ID";
                case RuntimeIdentityKind.CommandPayloadSchema:
                    return "DEP_PROJECTION_UNKNOWN_COMMAND_PAYLOAD_SCHEMA_ID";
                case RuntimeIdentityKind.ValueKey:
                    return "DEP_PROJECTION_UNKNOWN_VALUE_KEY_ID";
                case RuntimeIdentityKind.ValueSchema:
                    return "DEP_PROJECTION_UNKNOWN_VALUE_SCHEMA_ID";
                case RuntimeIdentityKind.RuntimeQuery:
                    return "DEP_PROJECTION_UNKNOWN_RUNTIME_QUERY_ID";
                default:
                    return "DEP_PROJECTION_UNKNOWN_ID";
            }
        }

        public static string GetMissingProjectionCode(RuntimeIdentityKind kind)
        {
            switch (kind)
            {
                case RuntimeIdentityKind.ValueKey:
                    return "DEP_PROJECTION_VALUE_SCHEMA_MISSING";
                case RuntimeIdentityKind.Service:
                    return "DEP_PROJECTION_SERVICE_MISSING";
                case RuntimeIdentityKind.ScopeAuthoring:
                    return "DEP_PROJECTION_SCOPE_AUTHORING_MISSING";
                case RuntimeIdentityKind.ScopePlan:
                    return "DEP_PROJECTION_SCOPE_PLAN_MISSING";
                case RuntimeIdentityKind.LifecyclePlan:
                    return "DEP_PROJECTION_LIFECYCLE_PLAN_MISSING";
                case RuntimeIdentityKind.LifecycleStep:
                    return "DEP_PROJECTION_LIFECYCLE_STEP_MISSING";
                case RuntimeIdentityKind.CommandType:
                    return "DEP_PROJECTION_COMMAND_CATALOG_MISSING";
                case RuntimeIdentityKind.CommandExecutor:
                    return "DEP_PROJECTION_COMMAND_EXECUTOR_MISSING";
                case RuntimeIdentityKind.CommandPayloadSchema:
                    return "DEP_PROJECTION_COMMAND_PAYLOAD_SCHEMA_MISSING";
                case RuntimeIdentityKind.ValueSchema:
                    return "DEP_PROJECTION_VALUE_SCHEMA_MISSING";
                case RuntimeIdentityKind.RuntimeQuery:
                    return "DEP_PROJECTION_RUNTIME_QUERY_MISSING";
                default:
                    return "DEP_PROJECTION_MAPPING_MISSING";
            }
        }

        public static bool RequiresProjection(RuntimeIdentityKind kind)
        {
            return kind == RuntimeIdentityKind.Service
                || kind == RuntimeIdentityKind.ScopeAuthoring
                || kind == RuntimeIdentityKind.ScopePlan
                || kind == RuntimeIdentityKind.LifecyclePlan
                || kind == RuntimeIdentityKind.LifecycleStep
                || kind == RuntimeIdentityKind.CommandType
                || kind == RuntimeIdentityKind.CommandExecutor
                || kind == RuntimeIdentityKind.CommandPayloadSchema
                || kind == RuntimeIdentityKind.ValueKey
                || kind == RuntimeIdentityKind.ValueSchema
                || kind == RuntimeIdentityKind.RuntimeQuery;
        }
    }

    static class ProjectionValidationContextExtensions
    {
        public static IEnumerable<RuntimeIdentityRef> EnumerateRequiredSourceIdentities(this ProjectionValidationContext context)
        {
            HashSet<RuntimeIdentityRef> required = new HashSet<RuntimeIdentityRef>();
            ReadOnlySpan<ProjectionArtifactKind> selectedArtifacts = context.Input.SelectedArtifacts;
            for (int artifactIndex = 0; artifactIndex < selectedArtifacts.Length; artifactIndex++)
            {
                switch (selectedArtifacts[artifactIndex])
                {
                    case ProjectionArtifactKind.ServiceGraph:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.Services, service => new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value));
                        break;
                    case ProjectionArtifactKind.CommandCatalog:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value));
                        break;
                    case ProjectionArtifactKind.ValueSchema:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.ValueKeys, valueKey => new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKey.Id.Value));
                        break;
                    case ProjectionArtifactKind.RuntimeQuery:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.RuntimeQueries, runtimeQuery => new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, runtimeQuery.Id.Value));
                        break;
                    case ProjectionArtifactKind.ScopeGraph:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.Scopes, scope => new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value));
                        break;
                    case ProjectionArtifactKind.LifecyclePlan:
                        AddRuntimeIdentities(required, context.Input.SourceKernelIR.Lifecycles, lifecycle => new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycle.PlanId.Value));
                        break;
                }
            }

            return required;

            static void AddRuntimeIdentities<T>(HashSet<RuntimeIdentityRef> target, ReadOnlySpan<T> items, Func<T, RuntimeIdentityRef> selector)
            {
                for (int index = 0; index < items.Length; index++)
                    target.Add(selector(items[index]));
            }
        }
    }

    static class ProjectionValidationIssueFactoryProxy
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationHelpers
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }

        public static DependencyValidationIssue CreateMissingProjectionIssue(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity, string selectedProfile)
        {
            ModuleId ownerModule = ResolveOwnerModule(context, sourceIdentity);
            SourceLocationId sourceLocation = ResolveSourceLocation(context, sourceIdentity);
            DependencyNodeIR representativeNode = CreateRepresentativeNode(context, sourceIdentity, ownerModule);

            return new DependencyValidationIssue(
                ProjectionValidationRuleHelpers.GetMissingProjectionCode(sourceIdentity.Kind),
                ValidationSeverity.Error,
                ValidationIssueCategory.Projection,
                representativeNode,
                null,
                ValidationPhase.Generate,
                ownerModule,
                sourceLocation,
                selectedProfile,
                "Generated projection omitted a required source identity.",
                "Emit the projection mapping for this source identity or mark the source as intentionally excluded.");
        }

        static DependencyNodeIR CreateRepresentativeNode(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity, ModuleId ownerModule)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            switch (sourceIdentity.Kind)
            {
                case RuntimeIdentityKind.Module:
                    return new DependencyNodeIR(new ModuleId(sourceIdentity.Value));
                case RuntimeIdentityKind.Service:
                    return new DependencyNodeIR(new ServiceId(sourceIdentity.Value));
                case RuntimeIdentityKind.ScopeAuthoring:
                    return ResolveScopeRepresentativeNode(kernelIR.Scopes, sourceIdentity.Value, ownerModule);
                case RuntimeIdentityKind.ScopePlan:
                    return new DependencyNodeIR(new ScopePlanId(sourceIdentity.Value));
                case RuntimeIdentityKind.CommandType:
                    return new DependencyNodeIR(new CommandTypeId(sourceIdentity.Value));
                case RuntimeIdentityKind.CommandExecutor:
                case RuntimeIdentityKind.CommandPayloadSchema:
                    return ResolveCommandRepresentativeNode(kernelIR.Commands, sourceIdentity.Value, ownerModule);
                case RuntimeIdentityKind.ValueKey:
                    return new DependencyNodeIR(new ValueKeyId(sourceIdentity.Value));
                case RuntimeIdentityKind.ValueSchema:
                    return ResolveValueSchemaRepresentativeNode(kernelIR.ValueKeys, sourceIdentity.Value, ownerModule);
                case RuntimeIdentityKind.LifecyclePlan:
                    return new DependencyNodeIR(ownerModule);
                case RuntimeIdentityKind.LifecycleStep:
                    return new DependencyNodeIR(new LifecycleStepId(sourceIdentity.Value));
                case RuntimeIdentityKind.RuntimeQuery:
                    return new DependencyNodeIR(new RuntimeQueryId(sourceIdentity.Value));
                default:
                    return new DependencyNodeIR(ownerModule);
            }
        }

        static DependencyNodeIR ResolveScopeRepresentativeNode(ReadOnlySpan<ScopeIR> scopes, int authoringId, ModuleId ownerModule)
        {
            for (int index = 0; index < scopes.Length; index++)
            {
                if (scopes[index].AuthoringId.Value == authoringId)
                    return new DependencyNodeIR(scopes[index].PlanId);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static DependencyNodeIR ResolveCommandRepresentativeNode(ReadOnlySpan<CommandIR> commands, int identityValue, ModuleId ownerModule)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                if (commands[index].Executor.Id.Value == identityValue || commands[index].PayloadSchema.Id.Value == identityValue)
                    return new DependencyNodeIR(commands[index].TypeId);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static DependencyNodeIR ResolveValueSchemaRepresentativeNode(ReadOnlySpan<ValueKeyIR> valueKeys, int schemaId, ModuleId ownerModule)
        {
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (valueKeys[index].Schema.Id.Value == schemaId)
                    return new DependencyNodeIR(valueKeys[index].Id);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static ModuleId ResolveOwnerModule(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Module && modules[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(modules[index].Id.Value);
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(services[index].OwnerModule.Value);
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return new ModuleId(scopes[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return new ModuleId(scopes[index].OwnerModule.Value);
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && commands[index].TypeId.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && commands[index].Executor.Id.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && commands[index].PayloadSchema.Id.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(valueKeys[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return new ModuleId(valueKeys[index].OwnerModule.Value);
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return new ModuleId(lifecycles[index].OwnerModule.Value);

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return new ModuleId(lifecycles[index].OwnerModule.Value);
                }
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.RuntimeQuery && runtimeQueries[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(runtimeQueries[index].OwnerModule.Value);
            }

            throw new InvalidOperationException("Projection validation could not resolve an owner module for a required source identity.");
        }

        static SourceLocationId ResolveSourceLocation(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Module && modules[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(modules[index].Source.Value);
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(services[index].Source.Value);
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return new SourceLocationId(scopes[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return new SourceLocationId(scopes[index].Source.Value);
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && commands[index].TypeId.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && commands[index].Executor.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].Executor.Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && commands[index].PayloadSchema.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].PayloadSchema.Source.Value);
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(valueKeys[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(valueKeys[index].Schema.Source.Value);
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return new SourceLocationId(lifecycles[index].Source.Value);

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return new SourceLocationId(steps[stepIndex].Source.Value);
                }
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.RuntimeQuery && runtimeQueries[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(runtimeQueries[index].Source.Value);
            }

            throw new InvalidOperationException("Projection validation could not resolve a source location for a required source identity.");
        }
    }

    static class ProjectionValidationIssueFactoryAdapter
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryExtensions
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryPublic
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryLink
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryAlias
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryFacade
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryBridge
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryHelper
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationIssueFactoryEntry
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }
    }

    static class ProjectionValidationHelpersBridge
    {
        public static DependencyValidationIssue CreateIssue(
            string selectedProfile,
            ProjectionMappingIR mapping,
            RuntimeIdentityRef identity,
            string code,
            string message,
            string suggestedFix)
        {
            return ProjectionValidationIssueFactory.CreateIssue(selectedProfile, mapping, identity, code, message, suggestedFix);
        }

        public static DependencyValidationIssue CreateSourceLocationMissingIssue(
            string selectedProfile,
            RuntimeIdentityRef identity,
            ModuleId ownerModule)
        {
            return ProjectionValidationIssueFactory.CreateSourceLocationMissingIssue(selectedProfile, identity, ownerModule);
        }

        public static DependencyValidationIssue CreateMissingProjectionIssue(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity, string selectedProfile)
        {
            ModuleId ownerModule = ResolveOwnerModule(context, sourceIdentity);
            SourceLocationId sourceLocation = ResolveSourceLocation(context, sourceIdentity);
            DependencyNodeIR representativeNode = CreateRepresentativeNode(context, sourceIdentity, ownerModule);

            return new DependencyValidationIssue(
                ProjectionValidationRuleHelpers.GetMissingProjectionCode(sourceIdentity.Kind),
                ValidationSeverity.Error,
                ValidationIssueCategory.Projection,
                representativeNode,
                null,
                ValidationPhase.Generate,
                ownerModule,
                sourceLocation,
                selectedProfile,
                "Generated projection omitted a required source identity.",
                "Emit the projection mapping for this source identity or mark the source as intentionally excluded.");
        }

        static DependencyNodeIR CreateRepresentativeNode(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity, ModuleId ownerModule)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            switch (sourceIdentity.Kind)
            {
                case RuntimeIdentityKind.Module:
                    return new DependencyNodeIR(new ModuleId(sourceIdentity.Value));

                case RuntimeIdentityKind.Service:
                    return new DependencyNodeIR(new ServiceId(sourceIdentity.Value));

                case RuntimeIdentityKind.ScopeAuthoring:
                    return ResolveScopeRepresentativeNode(kernelIR.Scopes, sourceIdentity.Value, ownerModule);

                case RuntimeIdentityKind.ScopePlan:
                    return new DependencyNodeIR(new ScopePlanId(sourceIdentity.Value));

                case RuntimeIdentityKind.CommandType:
                    return new DependencyNodeIR(new CommandTypeId(sourceIdentity.Value));

                case RuntimeIdentityKind.CommandExecutor:
                case RuntimeIdentityKind.CommandPayloadSchema:
                    return ResolveCommandRepresentativeNode(kernelIR.Commands, sourceIdentity.Value, ownerModule);

                case RuntimeIdentityKind.ValueKey:
                    return new DependencyNodeIR(new ValueKeyId(sourceIdentity.Value));

                case RuntimeIdentityKind.ValueSchema:
                    return ResolveValueSchemaRepresentativeNode(kernelIR.ValueKeys, sourceIdentity.Value, ownerModule);

                case RuntimeIdentityKind.LifecyclePlan:
                    return new DependencyNodeIR(ownerModule);

                case RuntimeIdentityKind.LifecycleStep:
                    return new DependencyNodeIR(new LifecycleStepId(sourceIdentity.Value));

                case RuntimeIdentityKind.RuntimeQuery:
                    return new DependencyNodeIR(new RuntimeQueryId(sourceIdentity.Value));

                default:
                    return new DependencyNodeIR(ownerModule);
            }
        }

        static DependencyNodeIR ResolveScopeRepresentativeNode(ReadOnlySpan<ScopeIR> scopes, int authoringId, ModuleId ownerModule)
        {
            for (int index = 0; index < scopes.Length; index++)
            {
                if (scopes[index].AuthoringId.Value == authoringId)
                    return new DependencyNodeIR(scopes[index].PlanId);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static DependencyNodeIR ResolveCommandRepresentativeNode(ReadOnlySpan<CommandIR> commands, int identityValue, ModuleId ownerModule)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                if (commands[index].Executor.Id.Value == identityValue || commands[index].PayloadSchema.Id.Value == identityValue)
                    return new DependencyNodeIR(commands[index].TypeId);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static DependencyNodeIR ResolveValueSchemaRepresentativeNode(ReadOnlySpan<ValueKeyIR> valueKeys, int schemaId, ModuleId ownerModule)
        {
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (valueKeys[index].Schema.Id.Value == schemaId)
                    return new DependencyNodeIR(valueKeys[index].Id);
            }

            return new DependencyNodeIR(ownerModule);
        }

        static ModuleId ResolveOwnerModule(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Module && modules[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(modules[index].Id.Value);
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(services[index].OwnerModule.Value);
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return new ModuleId(scopes[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return new ModuleId(scopes[index].OwnerModule.Value);
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && commands[index].TypeId.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && commands[index].Executor.Id.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && commands[index].PayloadSchema.Id.Value == sourceIdentity.Value)
                    return new ModuleId(commands[index].OwnerModule.Value);
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(valueKeys[index].OwnerModule.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return new ModuleId(valueKeys[index].OwnerModule.Value);
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return new ModuleId(lifecycles[index].OwnerModule.Value);

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return new ModuleId(lifecycles[index].OwnerModule.Value);
                }
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.RuntimeQuery && runtimeQueries[index].Id.Value == sourceIdentity.Value)
                    return new ModuleId(runtimeQueries[index].OwnerModule.Value);
            }

            throw new InvalidOperationException("Projection validation could not resolve an owner module for a required source identity.");
        }

        static SourceLocationId ResolveSourceLocation(ProjectionValidationContext context, RuntimeIdentityRef sourceIdentity)
        {
            KernelIR kernelIR = context.Input.SourceKernelIR;

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Module && modules[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(modules[index].Source.Value);
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(services[index].Source.Value);
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return new SourceLocationId(scopes[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return new SourceLocationId(scopes[index].Source.Value);
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && commands[index].TypeId.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && commands[index].Executor.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].Executor.Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && commands[index].PayloadSchema.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(commands[index].PayloadSchema.Source.Value);
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(valueKeys[index].Source.Value);

                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return new SourceLocationId(valueKeys[index].Schema.Source.Value);
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return new SourceLocationId(lifecycles[index].Source.Value);

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return new SourceLocationId(steps[stepIndex].Source.Value);
                }
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.RuntimeQuery && runtimeQueries[index].Id.Value == sourceIdentity.Value)
                    return runtimeQueries[index].Source;
            }

            throw new InvalidOperationException("Projection validation could not resolve a source location for a required source identity.");
        }
    }
}