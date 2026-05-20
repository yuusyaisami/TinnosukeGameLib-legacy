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
            new CommandPayloadSchemaStructureRule(),
        };

        public static ProjectionValidationReport Validate(ProjectionValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var context = new ProjectionValidationContext(input);
            var issues = new List<DependencyValidationIssue>();
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

        public bool HasSourceIdentity(RuntimeIdentityRef identity) => sourceIdentities.Contains(identity);

        public bool HasProjectedIdentity(RuntimeIdentityRef identity) => projectedIdentities.Contains(identity);

        public bool HasCoverage(RuntimeIdentityRef identity) => coverageIdentities.Contains(identity);

        public bool IsArtifactSelected(ProjectionArtifactKind kind)
        {
            ReadOnlySpan<ProjectionArtifactKind> selectedArtifacts = Input.SelectedArtifacts;
            for (int index = 0; index < selectedArtifacts.Length; index++)
            {
                if (selectedArtifacts[index] == kind)
                    return true;
            }

            return false;
        }

        public IEnumerable<RuntimeIdentityRef> EnumerateRequiredSourceIdentities()
        {
            var required = new HashSet<RuntimeIdentityRef>();
            ReadOnlySpan<ProjectionArtifactKind> selectedArtifacts = Input.SelectedArtifacts;

            for (int artifactIndex = 0; artifactIndex < selectedArtifacts.Length; artifactIndex++)
            {
                switch (selectedArtifacts[artifactIndex])
                {
                    case ProjectionArtifactKind.ServiceGraph:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Services, service => new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value));
                        break;
                    case ProjectionArtifactKind.CommandCatalog:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandAuthoringKey, command.AuthoringKey.Id.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, command.Executor.Id.Value));
                        AddRuntimeIdentities(required, Input.SourceKernelIR.Commands, command => new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, command.PayloadSchema.Id.Value));
                        break;
                    case ProjectionArtifactKind.ValueSchema:
                        AddRuntimeIdentities(required, Input.SourceKernelIR.ValueKeys, valueKey => new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKey.Id.Value));
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
                        ReadOnlySpan<LifecycleIR> lifecycles = Input.SourceKernelIR.Lifecycles;
                        for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
                        {
                            ReadOnlySpan<LifecycleStepIR> steps = lifecycles[lifecycleIndex].Steps;
                            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                                required.Add(new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, steps[stepIndex].Id.Value));
                        }
                        break;
                }
            }

            var ordered = new List<RuntimeIdentityRef>(required);
            ordered.Sort(CompareRuntimeIdentityRef);
            return ordered;
        }

        static HashSet<RuntimeIdentityRef> BuildSourceIdentities(KernelIR kernelIR)
        {
            var identities = new HashSet<RuntimeIdentityRef>();

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
                identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.CommandAuthoringKey, commands[index].AuthoringKey.Id.Value));
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
            var identities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
                identities.Add(mappings[index].ProjectedIdentity);

            return identities;
        }

        static HashSet<RuntimeIdentityRef> BuildCoverageIdentities(ReadOnlySpan<RuntimeIdentityRef> coverage)
        {
            var identities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < coverage.Length; index++)
                identities.Add(coverage[index]);

            return identities;
        }

        static void AddRuntimeIdentities<T>(HashSet<RuntimeIdentityRef> target, ReadOnlySpan<T> items, Func<T, RuntimeIdentityRef> selector)
        {
            for (int index = 0; index < items.Length; index++)
                target.Add(selector(items[index]));
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
    }

    sealed class UnknownProjectedIdRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ProjectionMappingIR> mappings = context.Input.Mappings;
            var reportedUnknownIdentities = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                if (context.HasSourceIdentity(mapping.ProjectedIdentity))
                    continue;

                if (!reportedUnknownIdentities.Add(mapping.ProjectedIdentity))
                    continue;

                issues.Add(ProjectionValidationIssueFactory.CreateIssue(
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
            var mappedSources = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                mappedSources.Add(mapping.SourceIdentity);

                if (!mapping.HasProvenance)
                {
                    issues.Add(ProjectionValidationIssueFactory.CreateIssue(
                        context.Input.SelectedProfile,
                        mapping,
                        mapping.SourceIdentity,
                        "DEP_PROJECTION_PROVENANCE_MISSING",
                        "Generated projection dropped provenance required to explain the dependency through diagnostics.",
                        "Preserve the source location or explicit mapping provenance for every projected dependency."));
                }
            }

            foreach (RuntimeIdentityRef sourceIdentity in context.EnumerateRequiredSourceIdentities())
            {
                if (mappedSources.Contains(sourceIdentity))
                    continue;

                issues.Add(ProjectionValidationHelpers.CreateMissingProjectionIssue(context, sourceIdentity, context.Input.SelectedProfile));
            }
        }
    }

    sealed class DebugMapCoverageRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            ReadOnlySpan<ProjectionMappingIR> mappings = context.Input.Mappings;
            var reportedMissingCoverage = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < mappings.Length; index++)
            {
                ProjectionMappingIR mapping = mappings[index];
                if (!context.HasSourceIdentity(mapping.ProjectedIdentity))
                    continue;

                if (!context.HasCoverage(mapping.ProjectedIdentity) && reportedMissingCoverage.Add(mapping.ProjectedIdentity))
                {
                    issues.Add(ProjectionValidationIssueFactory.CreateIssue(
                        context.Input.SelectedProfile,
                        mapping,
                        mapping.ProjectedIdentity,
                        "DEP_DEBUGMAP_COVERAGE_MISSING",
                        "Generated projection is missing DebugMap coverage for a runtime-facing identity.",
                        "Add the identity to the DebugMap coverage set for the selected profile."));
                }

                var ownerModuleCoverage = new RuntimeIdentityRef(RuntimeIdentityKind.Module, mapping.OwnerModule.Value);
                if (!context.HasCoverage(ownerModuleCoverage) && reportedMissingCoverage.Add(ownerModuleCoverage))
                {
                    issues.Add(ProjectionValidationIssueFactory.CreateIssue(
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

    sealed class CommandPayloadSchemaStructureRule : IProjectionValidationRule
    {
        public void CollectIssues(ProjectionValidationContext context, List<DependencyValidationIssue> issues)
        {
            if (!context.IsArtifactSelected(ProjectionArtifactKind.CommandCatalog))
                return;

            ReadOnlySpan<CommandIR> commands = context.Input.SourceKernelIR.Commands;
            var schemaIds = new HashSet<int>();
            for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                CommandIR command = commands[commandIndex];
                if (!schemaIds.Add(command.PayloadSchema.Id.Value))
                {
                    AddSchemaIssue(context, issues, command, command.PayloadSchema.Source, "Duplicate command payload schema ids are not allowed within a command catalog projection.", "Assign a unique CommandPayloadSchemaId to each command payload schema.", "SchemaId", command.PayloadSchema.Id.Value.ToString());
                }

                if (!Enum.IsDefined(typeof(CommandPayloadUnknownFieldPolicyIR), command.PayloadSchema.UnknownFieldPolicy))
                {
                    AddSchemaIssue(context, issues, command, command.PayloadSchema.Source, "Command payload schema declares an unknown-field policy outside the supported closed set.", "Use Reject or Ignore explicitly for the payload schema unknown-field policy.", "UnknownFieldPolicy", command.PayloadSchema.UnknownFieldPolicy.ToString());
                }

                ReadOnlySpan<CommandPayloadFieldIR> fields = command.PayloadSchema.Fields;
                var fieldPaths = new HashSet<string>(StringComparer.Ordinal);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    CommandPayloadFieldIR field = fields[fieldIndex];
                    if (!fieldPaths.Add(field.FieldPath))
                    {
                        AddSchemaIssue(context, issues, command, field.Source, "Command payload schema declares the same field path more than once.", "Keep exactly one descriptor for each payload field path.", "FieldPath", field.FieldPath);
                    }

                    if (!Enum.IsDefined(typeof(CommandPayloadFieldKindIR), field.Kind) || field.Kind == CommandPayloadFieldKindIR.Unknown)
                    {
                        AddSchemaIssue(context, issues, command, field.Source, "Command payload field schema must declare a concrete field kind.", "Set the field kind to the exact runtime payload value kind expected by the command data.", "FieldPath", field.FieldPath);
                    }

                    if (!Enum.IsDefined(typeof(CommandPayloadFieldRequirementIR), field.Requirement))
                    {
                        AddSchemaIssue(context, issues, command, field.Source, "Command payload field schema declares an unsupported requirement policy.", "Use Optional or Required explicitly for each payload field.", "FieldPath", field.FieldPath);
                    }

                    if (!Enum.IsDefined(typeof(CommandPayloadReferenceKindIR), field.ReferenceKind))
                    {
                        AddSchemaIssue(context, issues, command, field.Source, "Command payload field schema declares an unsupported reference kind.", "Use one of the closed reference kinds supported by the command payload validator.", "FieldPath", field.FieldPath);
                        continue;
                    }

                    if (!ReferenceKindMatchesFieldKind(field.ReferenceKind, field.Kind))
                    {
                        AddSchemaIssue(context, issues, command, field.Source, "Command payload field reference kind does not match the declared field kind.", "Use ValueKeyId, RuntimeQueryId, or TargetReference only on matching field kinds.", "FieldPath", field.FieldPath);
                    }
                }
            }
        }

        static bool ReferenceKindMatchesFieldKind(CommandPayloadReferenceKindIR referenceKind, CommandPayloadFieldKindIR fieldKind)
        {
            switch (referenceKind)
            {
                case CommandPayloadReferenceKindIR.None:
                    return true;
                case CommandPayloadReferenceKindIR.ValueKeyId:
                    return fieldKind == CommandPayloadFieldKindIR.ValueKeyId;
                case CommandPayloadReferenceKindIR.RuntimeQueryId:
                    return fieldKind == CommandPayloadFieldKindIR.RuntimeQueryId;
                case CommandPayloadReferenceKindIR.TargetReference:
                    return fieldKind == CommandPayloadFieldKindIR.TargetReference;
                default:
                    return false;
            }
        }

        static void AddSchemaIssue(
            ProjectionValidationContext context,
            List<DependencyValidationIssue> issues,
            CommandIR command,
            SourceLocationId source,
            string message,
            string suggestedFix,
            string payloadKey,
            string payloadValue)
        {
            var payloadEntries = new[]
            {
                new DiagnosticPayloadEntry("CommandTypeId", DiagnosticPayloadValue.FromInt32(command.TypeId.Value)),
                new DiagnosticPayloadEntry("CommandPayloadSchemaId", DiagnosticPayloadValue.FromInt32(command.PayloadSchema.Id.Value)),
                new DiagnosticPayloadEntry(payloadKey, DiagnosticPayloadValue.FromString(payloadValue)),
            };

            issues.Add(new DependencyValidationIssue(
                "DEP_PROJECTION_COMMAND_PAYLOAD_SCHEMA_INVALID",
                ValidationSeverity.Error,
                ValidationIssueCategory.Projection,
                new DependencyNodeIR(command.TypeId),
                null,
                ValidationPhase.Generate,
                command.OwnerModule,
                source,
                context.Input.SelectedProfile,
                message,
                suggestedFix,
                payloadEntries));
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
            var payloadEntries = new List<DiagnosticPayloadEntry>
            {
                new DiagnosticPayloadEntry("RuntimeIdentityKind", DiagnosticPayloadValue.FromString(identity.Kind.ToString())),
                new DiagnosticPayloadEntry("RuntimeIdentityValue", DiagnosticPayloadValue.FromInt32(identity.Value)),
                new DiagnosticPayloadEntry("SourceIdentity", DiagnosticPayloadValue.FromString(mapping.SourceIdentity.ToString())),
                new DiagnosticPayloadEntry("ProjectedIdentity", DiagnosticPayloadValue.FromString(identity.ToString())),
                new DiagnosticPayloadEntry("HasProvenance", DiagnosticPayloadValue.FromBoolean(mapping.HasProvenance)),
            };

            if (identity.Generation != 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("RuntimeIdentityGeneration", DiagnosticPayloadValue.FromInt32(identity.Generation)));

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

        static DependencyNodeIR CreateRepresentativeNode(ProjectionMappingIR mapping, RuntimeIdentityRef identity)
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
                    return CreateRepresentativeNode(mapping.SourceIdentity, mapping.OwnerModule);
            }
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
                case RuntimeIdentityKind.CommandAuthoringKey:
                    return "DEP_PROJECTION_UNKNOWN_COMMAND_AUTHORING_KEY_ID";
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
                case RuntimeIdentityKind.CommandAuthoringKey:
                    return "DEP_PROJECTION_COMMAND_AUTHORING_KEY_MISSING";
                case RuntimeIdentityKind.ValueKey:
                case RuntimeIdentityKind.ValueSchema:
                    return "DEP_PROJECTION_VALUE_SCHEMA_MISSING";
                case RuntimeIdentityKind.RuntimeQuery:
                    return "DEP_PROJECTION_RUNTIME_QUERY_MISSING";
                default:
                    return "DEP_PROJECTION_MAPPING_MISSING";
            }
        }
    }

    static class ProjectionValidationHelpers
    {
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
                case RuntimeIdentityKind.CommandAuthoringKey:
                case RuntimeIdentityKind.CommandExecutor:
                case RuntimeIdentityKind.CommandPayloadSchema:
                    return ResolveCommandRepresentativeNode(kernelIR.Commands, sourceIdentity, ownerModule);
                case RuntimeIdentityKind.ValueKey:
                    return new DependencyNodeIR(new ValueKeyId(sourceIdentity.Value));
                case RuntimeIdentityKind.ValueSchema:
                    return ResolveValueSchemaRepresentativeNode(kernelIR.ValueKeys, sourceIdentity.Value, ownerModule);
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

        static DependencyNodeIR ResolveCommandRepresentativeNode(ReadOnlySpan<CommandIR> commands, RuntimeIdentityRef sourceIdentity, ModuleId ownerModule)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandAuthoringKey && command.AuthoringKey.Id.Value == sourceIdentity.Value)
                    return new DependencyNodeIR(command.TypeId);
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && command.Executor.Id.Value == sourceIdentity.Value)
                    return new DependencyNodeIR(command.TypeId);
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && command.PayloadSchema.Id.Value == sourceIdentity.Value)
                    return new DependencyNodeIR(command.TypeId);
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
                    return modules[index].Id;
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return services[index].OwnerModule;
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return scopes[index].OwnerModule;
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return scopes[index].OwnerModule;
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && command.TypeId.Value == sourceIdentity.Value)
                    return command.OwnerModule;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandAuthoringKey && command.AuthoringKey.Id.Value == sourceIdentity.Value)
                    return command.OwnerModule;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && command.Executor.Id.Value == sourceIdentity.Value)
                    return command.OwnerModule;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && command.PayloadSchema.Id.Value == sourceIdentity.Value)
                    return command.OwnerModule;
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return valueKeys[index].OwnerModule;
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return valueKeys[index].OwnerModule;
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return lifecycles[index].OwnerModule;

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return lifecycles[index].OwnerModule;
                }
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.RuntimeQuery && runtimeQueries[index].Id.Value == sourceIdentity.Value)
                    return runtimeQueries[index].OwnerModule;
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
                    return modules[index].Source;
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.Service && services[index].Id.Value == sourceIdentity.Value)
                    return services[index].Source;
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopeAuthoring && scopes[index].AuthoringId.Value == sourceIdentity.Value)
                    return scopes[index].Source;
                if (sourceIdentity.Kind == RuntimeIdentityKind.ScopePlan && scopes[index].PlanId.Value == sourceIdentity.Value)
                    return scopes[index].Source;
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandType && command.TypeId.Value == sourceIdentity.Value)
                    return command.Source;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandAuthoringKey && command.AuthoringKey.Id.Value == sourceIdentity.Value)
                    return command.AuthoringKey.Source;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandExecutor && command.Executor.Id.Value == sourceIdentity.Value)
                    return command.Executor.Source;
                if (sourceIdentity.Kind == RuntimeIdentityKind.CommandPayloadSchema && command.PayloadSchema.Id.Value == sourceIdentity.Value)
                    return command.PayloadSchema.Source;
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueKey && valueKeys[index].Id.Value == sourceIdentity.Value)
                    return valueKeys[index].Source;
                if (sourceIdentity.Kind == RuntimeIdentityKind.ValueSchema && valueKeys[index].Schema.Id.Value == sourceIdentity.Value)
                    return valueKeys[index].Schema.Source;
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                if (sourceIdentity.Kind == RuntimeIdentityKind.LifecyclePlan && lifecycles[index].PlanId.Value == sourceIdentity.Value)
                    return lifecycles[index].Source;

                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    if (sourceIdentity.Kind == RuntimeIdentityKind.LifecycleStep && steps[stepIndex].Id.Value == sourceIdentity.Value)
                        return steps[stepIndex].Source;
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