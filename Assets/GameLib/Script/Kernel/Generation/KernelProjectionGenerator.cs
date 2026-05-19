#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;

namespace Game.Kernel.Generation
{
    public static class KernelProjectionGenerator
    {
        static readonly ArtifactKind[] ArtifactKinds =
        {
            ArtifactKind.ServiceGraph,
            ArtifactKind.ScopeGraph,
            ArtifactKind.LifecyclePlan,
            ArtifactKind.CommandCatalog,
            ArtifactKind.ValueSchema,
            ArtifactKind.RuntimeQuery,
            ArtifactKind.KernelDebugMap,
            ArtifactKind.GenerationReport,
            ArtifactKind.ValidationReport,
        };

        static readonly ProjectionArtifactKind[] SelectedProjectionKinds =
        {
            ProjectionArtifactKind.ServiceGraph,
            ProjectionArtifactKind.ScopeGraph,
            ProjectionArtifactKind.LifecyclePlan,
            ProjectionArtifactKind.CommandCatalog,
            ProjectionArtifactKind.ValueSchema,
            ProjectionArtifactKind.RuntimeQuery,
        };

        public static KernelProjectionGenerationResult Generate(
            KernelIR kernelIR,
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            string selectedProfile,
            KernelProfileMask selectedProfileMask)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            Hash128 sourceHash = KernelProjectionHashing.ComputeSourceHash(kernelIR);
            Hash128 registryHash = KernelProjectionHashing.ComputeRegistryHash(kernelIR);
            Hash128 profileHash = KernelProjectionHashing.ComputeProfileHash(kernelIR, selectedProfile, selectedProfileMask);

            Hash128 serviceGraphHash = KernelProjectionHashing.ComputeServiceGraphHash(kernelIR.Services);
            Hash128 scopeGraphHash = KernelProjectionHashing.ComputeScopeGraphHash(kernelIR.Scopes);
            Hash128 lifecyclePlanHash = KernelProjectionHashing.ComputeLifecyclePlanHash(kernelIR.Lifecycles);
            Hash128 commandCatalogHash = KernelProjectionHashing.ComputeCommandCatalogHash(kernelIR.Commands);
            Hash128 valueSchemaHash = KernelProjectionHashing.ComputeValueSchemaHash(kernelIR.ValueKeys);
            Hash128 runtimeQueryHash = KernelProjectionHashing.ComputeRuntimeQueryHash(kernelIR.RuntimeQueries);

            KernelDebugMapEntry[] debugMapEntries = BuildDebugMapEntries(kernelIR, selectedProfileMask, serviceGraphHash, scopeGraphHash, lifecyclePlanHash, commandCatalogHash, valueSchemaHash, runtimeQueryHash, sourceHash);
            Array.Sort(debugMapEntries, KernelDebugMapEntryComparer.Instance);
            Hash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugMapEntries);

            ServiceGraphPlan serviceGraph = CreateServiceGraphPlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, serviceGraphHash);
            ScopeGraphPlan scopeGraph = CreateScopeGraphPlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, scopeGraphHash);
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, lifecyclePlanHash);
            CommandCatalogPlan commandCatalog = CreateCommandCatalogPlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, commandCatalogHash);
            ValueSchemaPlan valueSchema = CreateValueSchemaPlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, valueSchemaHash);
            RuntimeQueryPlan runtimeQuery = CreateRuntimeQueryPlan(planId, artifactSetId, formatVersion, generatorVersion, kernelIR, sourceHash, registryHash, profileHash, debugMapHash, runtimeQueryHash);
            KernelDebugMap debugMap = CreateDebugMap(planId, artifactSetId, formatVersion, generatorVersion, debugMapEntries, sourceHash, registryHash, profileHash, debugMapHash);

            ProjectionValidationReport projectionValidationReport = ValidateProjection(
                selectedProfile,
                selectedProfileMask,
                kernelIR,
                serviceGraph,
                scopeGraph,
                lifecyclePlan,
                commandCatalog,
                valueSchema,
                runtimeQuery,
                debugMap);

            Hash128 validationReportHash = KernelProjectionHashing.ComputeValidationReportHash(projectionValidationReport);
            ValidationReport validationReportArtifact = CreateValidationReport(planId, artifactSetId, formatVersion, generatorVersion, sourceHash, registryHash, profileHash, debugMapHash, validationReportHash, projectionValidationReport);

            Hash128 generationReportHash = KernelProjectionHashing.ComputeGenerationReportHash(
                selectedProfile,
                selectedProfileMask,
                ArtifactKinds.Length,
                projectionValidationReport.Issues.Count,
                debugMapEntries.Length,
                projectionValidationReport.Status,
                new[]
                {
                    serviceGraph.ContentHash,
                    scopeGraph.ContentHash,
                    lifecyclePlan.ContentHash,
                    commandCatalog.ContentHash,
                    valueSchema.ContentHash,
                    runtimeQuery.ContentHash,
                    debugMap.ContentHash,
                    validationReportArtifact.ContentHash,
                });

            GenerationReport generationReportArtifact = CreateGenerationReport(
                planId,
                artifactSetId,
                formatVersion,
                generatorVersion,
                selectedProfile,
                selectedProfileMask,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                generationReportHash,
                ArtifactKinds.Length,
                projectionValidationReport.Issues.Count,
                debugMapEntries.Length,
                projectionValidationReport.Status);

            KernelProjectionSet projections = new KernelProjectionSet(
                serviceGraph,
                scopeGraph,
                lifecyclePlan,
                commandCatalog,
                valueSchema,
                runtimeQuery,
                debugMap,
                generationReportArtifact,
                validationReportArtifact);

            VerifiedArtifactHeader[] artifactHeaders =
            {
                serviceGraph.Header,
                scopeGraph.Header,
                lifecyclePlan.Header,
                commandCatalog.Header,
                valueSchema.Header,
                runtimeQuery.Header,
                debugMap.Header,
                generationReportArtifact.Header,
                validationReportArtifact.Header,
            };

            KernelPlanHeader provisionalHeader = new KernelPlanHeader(
                planId,
                artifactSetId,
                formatVersion,
                generatorVersion,
                ArtifactKinds,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                default);

            Hash128 consistencyHash = ArtifactSetManifest.ComputeConsistencyHash(provisionalHeader, artifactHeaders);
            KernelPlanHeader finalHeader = new KernelPlanHeader(
                planId,
                artifactSetId,
                formatVersion,
                generatorVersion,
                ArtifactKinds,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                consistencyHash);

            GeneratedKernelPlan generatedPlan = new GeneratedKernelPlan(finalHeader, artifactHeaders);
            KernelPlanVerificationResult planVerification = KernelPlanVerification.Verify(generatedPlan);

            return new KernelProjectionGenerationResult(projections, generatedPlan, planVerification, projectionValidationReport);
        }

        static ServiceGraphPlan CreateServiceGraphPlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 1, ArtifactKind.ServiceGraph, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new ServiceGraphPlan(header, kernelIR.Services);
        }

        static ScopeGraphPlan CreateScopeGraphPlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 2, ArtifactKind.ScopeGraph, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new ScopeGraphPlan(header, kernelIR.Scopes);
        }

        static LifecyclePlan CreateLifecyclePlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 3, ArtifactKind.LifecyclePlan, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new LifecyclePlan(header, kernelIR.Lifecycles);
        }

        static CommandCatalogPlan CreateCommandCatalogPlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 4, ArtifactKind.CommandCatalog, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new CommandCatalogPlan(header, kernelIR.Commands);
        }

        static ValueSchemaPlan CreateValueSchemaPlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 5, ArtifactKind.ValueSchema, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new ValueSchemaPlan(header, kernelIR.ValueKeys);
        }

        static RuntimeQueryPlan CreateRuntimeQueryPlan(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, KernelIR kernelIR, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, Hash128 generatedHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 6, ArtifactKind.RuntimeQuery, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new RuntimeQueryPlan(header, kernelIR.RuntimeQueries);
        }

        static KernelDebugMap CreateDebugMap(PlanId planId, ArtifactSetId artifactSetId, int formatVersion, string generatorVersion, ReadOnlySpan<KernelDebugMapEntry> entries, Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 7, ArtifactKind.KernelDebugMap, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash, generatorVersion);
            return new KernelDebugMap(header, entries);
        }

        static GenerationReport CreateGenerationReport(
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            Hash128 sourceHash,
            Hash128 registryHash,
            Hash128 profileHash,
            Hash128 debugMapHash,
            Hash128 generatedHash,
            int artifactCount,
            int mappingCount,
            int debugMapEntryCount,
            ValidationResultStatus validationStatus)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 8, ArtifactKind.GenerationReport, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new GenerationReport(header, selectedProfile, selectedProfileMask, artifactCount, mappingCount, debugMapEntryCount, validationStatus, generatedHash);
        }

        static ValidationReport CreateValidationReport(
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            Hash128 sourceHash,
            Hash128 registryHash,
            Hash128 profileHash,
            Hash128 debugMapHash,
            Hash128 generatedHash,
            ProjectionValidationReport report)
        {
            VerifiedArtifactHeader header = CreateArtifactHeader(planId, artifactSetId, 9, ArtifactKind.ValidationReport, formatVersion, sourceHash, registryHash, profileHash, debugMapHash, generatedHash, generatorVersion);
            return new ValidationReport(header, report, generatedHash);
        }

        static VerifiedArtifactHeader CreateArtifactHeader(
            PlanId planId,
            ArtifactSetId artifactSetId,
            int artifactId,
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
                new ArtifactId(artifactId),
                artifactKind,
                formatVersion,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                generatedHash,
                generatorVersion);
        }

        static ProjectionValidationReport ValidateProjection(
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            KernelIR kernelIR,
            ServiceGraphPlan serviceGraph,
            ScopeGraphPlan scopeGraph,
            LifecyclePlan lifecyclePlan,
            CommandCatalogPlan commandCatalog,
            ValueSchemaPlan valueSchema,
            RuntimeQueryPlan runtimeQuery,
            KernelDebugMap debugMap)
        {
            ProjectionMappingIR[] mappings = BuildMappings(kernelIR, serviceGraph, scopeGraph, lifecyclePlan, commandCatalog, valueSchema, runtimeQuery);
            RuntimeIdentityRef[] coverage = BuildCoverage(kernelIR, serviceGraph, scopeGraph, lifecyclePlan, commandCatalog, valueSchema, runtimeQuery, debugMap);

            ProjectionValidationInput input = new ProjectionValidationInput(
                selectedProfile,
                selectedProfileMask,
                kernelIR,
                SelectedProjectionKinds,
                mappings,
                coverage);

            return ProjectionValidator.Validate(input);
        }

        static ProjectionMappingIR[] BuildMappings(
            KernelIR kernelIR,
            ServiceGraphPlan serviceGraph,
            ScopeGraphPlan scopeGraph,
            LifecyclePlan lifecyclePlan,
            CommandCatalogPlan commandCatalog,
            ValueSchemaPlan valueSchema,
            RuntimeQueryPlan runtimeQuery)
        {
            List<ProjectionMappingIR> mappings = new List<ProjectionMappingIR>();

            ReadOnlySpan<ServiceIR> services = serviceGraph.Services;
            for (int index = 0; index < services.Length; index++)
            {
                ServiceIR service = services[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value), service.OwnerModule, service.Source);
            }

            ReadOnlySpan<ScopeIR> scopes = scopeGraph.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scope.AuthoringId.Value), new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scope.AuthoringId.Value), scope.OwnerModule, scope.Source);
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value), new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value), scope.OwnerModule, scope.Source);
            }

            ReadOnlySpan<LifecycleIR> lifecycles = lifecyclePlan.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                LifecycleIR lifecycle = lifecycles[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycle.PlanId.Value), new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycle.PlanId.Value), lifecycle.OwnerModule, lifecycle.Source);

                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    LifecycleStepIR step = steps[stepIndex];
                    AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, step.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, step.Id.Value), lifecycle.OwnerModule, step.Source);
                }
            }

            ReadOnlySpan<CommandIR> commands = commandCatalog.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value), command.OwnerModule, command.Source);
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, command.Executor.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, command.Executor.Id.Value), command.OwnerModule, command.Executor.Source);
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, command.PayloadSchema.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, command.PayloadSchema.Id.Value), command.OwnerModule, command.PayloadSchema.Source);
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = valueSchema.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                ValueKeyIR valueKey = valueKeys[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKey.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKey.Schema.Id.Value), valueKey.OwnerModule, valueKey.Source);
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKey.Schema.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKey.Schema.Id.Value), valueKey.OwnerModule, valueKey.Schema.Source);
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = runtimeQuery.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                RuntimeQueryIR query = runtimeQueries[index];
                AddMapping(mappings, new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, query.Id.Value), new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, query.Id.Value), query.OwnerModule, query.Source);
            }

            ProjectionMappingIR[] snapshot = mappings.ToArray();
            Array.Sort(snapshot, static (left, right) => CompareMapping(left, right));
            return snapshot;
        }

        static RuntimeIdentityRef[] BuildCoverage(
            KernelIR kernelIR,
            ServiceGraphPlan serviceGraph,
            ScopeGraphPlan scopeGraph,
            LifecyclePlan lifecyclePlan,
            CommandCatalogPlan commandCatalog,
            ValueSchemaPlan valueSchema,
            RuntimeQueryPlan runtimeQuery,
            KernelDebugMap debugMap)
        {
            List<RuntimeIdentityRef> coverage = new List<RuntimeIdentityRef>();

            AddCoverage(coverage, kernelIR.Modules);

            ReadOnlySpan<ServiceIR> services = serviceGraph.Services;
            for (int index = 0; index < services.Length; index++)
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.Service, services[index].Id.Value));

            ReadOnlySpan<ScopeIR> scopes = scopeGraph.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scopes[index].AuthoringId.Value));
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scopes[index].PlanId.Value));
            }

            ReadOnlySpan<LifecycleIR> lifecycles = lifecyclePlan.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycles[index].PlanId.Value));
                ReadOnlySpan<LifecycleStepIR> steps = lifecycles[index].Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                    AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, steps[stepIndex].Id.Value));
            }

            ReadOnlySpan<CommandIR> commands = commandCatalog.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, commands[index].TypeId.Value));
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, commands[index].Executor.Id.Value));
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, commands[index].PayloadSchema.Id.Value));
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = valueSchema.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKeys[index].Id.Value));
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKeys[index].Schema.Id.Value));
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = runtimeQuery.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, runtimeQueries[index].Id.Value));

            ReadOnlySpan<KernelDebugMapEntry> entries = debugMap.Entries;
            for (int index = 0; index < entries.Length; index++)
                AddCoverage(coverage, entries[index].Identity);

            RuntimeIdentityRef[] snapshot = coverage.ToArray();
            Array.Sort(snapshot, RuntimeIdentityRefComparer.Instance);
            return snapshot;
        }

        static void AddMapping(List<ProjectionMappingIR> mappings, RuntimeIdentityRef sourceIdentity, RuntimeIdentityRef projectedIdentity, ModuleId ownerModule, SourceLocationId source)
        {
            mappings.Add(new ProjectionMappingIR(sourceIdentity, projectedIdentity, ownerModule, source));
        }

        static void AddCoverage(List<RuntimeIdentityRef> coverage, ReadOnlySpan<ModuleIR> modules)
        {
            for (int index = 0; index < modules.Length; index++)
                AddCoverage(coverage, new RuntimeIdentityRef(RuntimeIdentityKind.Module, modules[index].Id.Value));
        }

        static void AddCoverage(List<RuntimeIdentityRef> coverage, RuntimeIdentityRef identity)
        {
            for (int index = 0; index < coverage.Count; index++)
            {
                if (coverage[index] == identity)
                    return;
            }

            coverage.Add(identity);
        }

        static int CompareMapping(ProjectionMappingIR left, ProjectionMappingIR right)
        {
            int comparison = RuntimeIdentityRefComparer.Compare(left.SourceIdentity, right.SourceIdentity);
            if (comparison != 0)
                return comparison;

            comparison = RuntimeIdentityRefComparer.Compare(left.ProjectedIdentity, right.ProjectedIdentity);
            if (comparison != 0)
                return comparison;

            comparison = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }

        static KernelDebugMapEntry[] BuildDebugMapEntries(
            KernelIR kernelIR,
            KernelProfileMask selectedProfileMask,
            Hash128 serviceGraphHash,
            Hash128 scopeGraphHash,
            Hash128 lifecyclePlanHash,
            Hash128 commandCatalogHash,
            Hash128 valueSchemaHash,
            Hash128 runtimeQueryHash,
            Hash128 sourceHash)
        {
            List<KernelDebugMapEntry> entries = new List<KernelDebugMapEntry>();

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
            {
                ModuleIR module = modules[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.Module, module.Id.Value), module.Name, module.Id, module.Source, selectedProfileMask, sourceHash));
            }

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
            {
                ServiceIR service = services[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.Service, service.Id.Value), service.Name, service.OwnerModule, service.Source, selectedProfileMask, serviceGraphHash));
            }

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, scope.AuthoringId.Value), scope.Name, scope.OwnerModule, scope.Source, selectedProfileMask, scopeGraphHash));
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value), scope.Name, scope.OwnerModule, scope.Source, selectedProfileMask, scopeGraphHash));
            }

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
            {
                LifecycleIR lifecycle = lifecycles[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecycle.PlanId.Value), lifecycle.Name, lifecycle.OwnerModule, lifecycle.Source, selectedProfileMask, lifecyclePlanHash));

                ReadOnlySpan<LifecycleStepIR> steps = lifecycle.Steps;
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    LifecycleStepIR step = steps[stepIndex];
                    entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, step.Id.Value), lifecycle.Name + "/Step" + step.Id.Value, lifecycle.OwnerModule, step.Source, selectedProfileMask, lifecyclePlanHash));
                }
            }

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
            {
                CommandIR command = commands[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, command.TypeId.Value), command.RuntimeName, command.OwnerModule, command.Source, selectedProfileMask, commandCatalogHash));
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.CommandExecutor, command.Executor.Id.Value), command.RuntimeName + "/Executor", command.OwnerModule, command.Executor.Source, selectedProfileMask, commandCatalogHash));
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.CommandPayloadSchema, command.PayloadSchema.Id.Value), command.RuntimeName + "/Payload", command.OwnerModule, command.PayloadSchema.Source, selectedProfileMask, commandCatalogHash));
            }

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
            {
                ValueKeyIR valueKey = valueKeys[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, valueKey.Id.Value), valueKey.DisplayName, valueKey.OwnerModule, valueKey.Source, selectedProfileMask, valueSchemaHash));
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, valueKey.Schema.Id.Value), valueKey.DisplayName + "/Schema", valueKey.OwnerModule, valueKey.Schema.Source, selectedProfileMask, valueSchemaHash));
            }

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
            {
                RuntimeQueryIR runtimeQuery = runtimeQueries[index];
                entries.Add(new KernelDebugMapEntry(new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, runtimeQuery.Id.Value), runtimeQuery.Name, runtimeQuery.OwnerModule, runtimeQuery.Source, selectedProfileMask, runtimeQueryHash));
            }

            ReadOnlySpan<DiagnosticSeedIR> diagnosticSeeds = kernelIR.DiagnosticSeeds;
            if (diagnosticSeeds.Length > 0)
            {
                DiagnosticSeedIR[] sortedSeeds = diagnosticSeeds.ToArray();
                Array.Sort(sortedSeeds, static (left, right) => CompareDiagnosticSeed(left, right));

                for (int index = 0; index < sortedSeeds.Length; index++)
                {
                    DiagnosticSeedIR seed = sortedSeeds[index];
                    entries.Add(new KernelDebugMapEntry(
                        new RuntimeIdentityRef(RuntimeIdentityKind.DiagnosticSeed, ComputeDiagnosticSeedIdentityValue(seed.SeedKey)),
                        seed.DebugName,
                        seed.OwnerModule,
                        seed.Source,
                        selectedProfileMask,
                        VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
                        {
                            seed.SeedKey,
                            seed.DebugName,
                            seed.OwnerModule.Value.ToString(),
                            seed.Source.Value.ToString(),
                        }),
                        seed.SeedKey));
                }
            }

            return entries.ToArray();
        }

        static int CompareDiagnosticSeed(DiagnosticSeedIR left, DiagnosticSeedIR right)
        {
            int comparison = StringComparer.Ordinal.Compare(left.SeedKey, right.SeedKey);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.DebugName, right.DebugName);
            if (comparison != 0)
                return comparison;

            comparison = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }

        static int ComputeDiagnosticSeedIdentityValue(string seedKey)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;

                ulong hash = offsetBasis;
                for (int index = 0; index < seedKey.Length; index++)
                {
                    hash ^= seedKey[index];
                    hash *= prime;
                }

                int value = (int)(hash ^ (hash >> 32));
                value &= int.MaxValue;
                return value == 0 ? 1 : value;
            }
        }
    }

    static class RuntimeIdentityRefComparer
    {
        public static readonly IComparer<RuntimeIdentityRef> Instance = Comparer<RuntimeIdentityRef>.Create(Compare);

        public static int Compare(RuntimeIdentityRef left, RuntimeIdentityRef right)
        {
            int comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
                return comparison;

            comparison = left.Value.CompareTo(right.Value);
            if (comparison != 0)
                return comparison;

            return left.Generation.CompareTo(right.Generation);
        }
    }
}