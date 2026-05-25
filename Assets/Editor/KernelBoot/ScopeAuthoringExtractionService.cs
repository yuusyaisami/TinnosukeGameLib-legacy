#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using UnityEngine;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public sealed class ScopeAuthoringExtractionReport
    {
        readonly ModuleContributionData[] contributions;
        readonly EntityAuthoringInput[] entityInputs;
        readonly EntityDeclarationPlanInput[] declarationInputs;
        readonly EntityServiceDeclarationInput[] serviceDeclarations;
        readonly AuthoringValidationIssue[] issues;

        public ScopeAuthoringExtractionReport(ModuleContributionData[] contributions, EntityAuthoringInput[] entityInputs, EntityDeclarationPlanInput[] declarationInputs, EntityServiceDeclarationInput[] serviceDeclarations, AuthoringValidationIssue[] issues)
        {
            this.contributions = contributions ?? Array.Empty<ModuleContributionData>();
            this.entityInputs = entityInputs ?? Array.Empty<EntityAuthoringInput>();
            this.declarationInputs = declarationInputs ?? Array.Empty<EntityDeclarationPlanInput>();
            this.serviceDeclarations = serviceDeclarations ?? Array.Empty<EntityServiceDeclarationInput>();
            this.issues = issues ?? Array.Empty<AuthoringValidationIssue>();
        }

        public IReadOnlyList<ModuleContributionData> Contributions => contributions;

        public IReadOnlyList<EntityAuthoringInput> EntityInputs => entityInputs;

        public IReadOnlyList<EntityDeclarationPlanInput> DeclarationInputs => declarationInputs;

        public IReadOnlyList<EntityServiceDeclarationInput> ServiceDeclarations => serviceDeclarations;

        public IReadOnlyList<AuthoringValidationIssue> Issues => issues;

        public bool IsValid => issues.Length == 0;

        public KernelDiagnostic[] ToKernelDiagnostics()
        {
            return AuthoringValidationDiagnostics.ToKernelDiagnostics(issues);
        }

        public void EmitDiagnostics(KernelDiagnosticService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            AuthoringValidationDiagnostics.Emit(service, issues);
        }
    }

    public static class ScopeAuthoringExtractionService
    {
        public static ScopeAuthoringExtractionReport Extract(ScopeAuthoringRoot root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            return Extract(new[] { root });
        }

        public static ScopeAuthoringExtractionReport Extract(IReadOnlyList<ScopeAuthoringRoot> roots)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            AuthoringValidationReport validationReport = ScopeAuthoringValidationService.Validate(roots);
            if (!validationReport.IsValid)
                return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), CopyIssues(validationReport.Issues));

            List<ScopeAuthoringRoot> orderedRoots = new List<ScopeAuthoringRoot>(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                ScopeAuthoringRoot root = roots[index];
                if (root != null)
                    orderedRoots.Add(root);
            }

            orderedRoots.Sort(CompareRoots);

            List<ModuleContributionData> contributions = new List<ModuleContributionData>(orderedRoots.Count);
            List<EntityAuthoringInput> entityInputs = new List<EntityAuthoringInput>();
            List<EntityDeclarationPlanInput> declarationInputs = new List<EntityDeclarationPlanInput>();
            List<EntityServiceDeclarationInput> serviceDeclarations = new List<EntityServiceDeclarationInput>();
            List<AuthoringValidationIssue> entityIssues = new List<AuthoringValidationIssue>();
            HashSet<string> seenEntityRefs = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> seenDeclaredServiceIds = new HashSet<int>();
            for (int index = 0; index < orderedRoots.Count; index++)
            {
                ScopeAuthoringRoot root = orderedRoots[index];
                List<EntityServiceDeclarationInput> rootServiceDeclarations = new List<EntityServiceDeclarationInput>();
                ExtractEntityAuthoring(root, entityInputs, declarationInputs, rootServiceDeclarations, entityIssues, seenEntityRefs, seenDeclaredServiceIds);

                ScopeAuthoringLink[] links = root.GetComponentsInChildren<ScopeAuthoringLink>(true);
                Array.Sort(links, CompareLinks);

                ContributionItem[] items = BuildContributionItems(root, links, rootServiceDeclarations);
                if (items.Length == 0)
                    return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), new[]
                    {
                        new AuthoringValidationIssue(
                            ScopeAuthoringValidationCodes.ContributionInvalid,
                            ValidationSeverity.Error,
                            ValidationIssueCategory.LocalNode,
                            root.ModuleId,
                            "Contribution item construction failed after validation.",
                            root.HasSourceLocation ? root.CreateSourceLocation() : null,
                            subjectName: root.name,
                            runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) })
                    });

                try
                {
                    ContributionKind[] ownedContributionKinds = rootServiceDeclarations.Count == 0
                        ? new[] { ContributionKind.ScopeContribution }
                        : new[] { ContributionKind.ScopeContribution, ContributionKind.ServiceContribution };

                    ModuleContributionData contribution = new ModuleContributionData(
                        root.ModuleId,
                        root.ModuleName,
                        root.ModuleKind,
                        root.ModuleVersion,
                        root.Availability,
                        new SourceLocationIR(root.CreateSourceLocation()),
                        ownedContributionKinds,
                        Array.Empty<ModuleId>(),
                        Array.Empty<ModuleId>(),
                        items);

                    contributions.Add(contribution);
                    serviceDeclarations.AddRange(rootServiceDeclarations);
                }
                catch (ArgumentException exception)
                {
                    return new ScopeAuthoringExtractionReport(
                        Array.Empty<ModuleContributionData>(),
                        Array.Empty<EntityAuthoringInput>(),
                        Array.Empty<EntityDeclarationPlanInput>(),
                        Array.Empty<EntityServiceDeclarationInput>(),
                        new[]
                        {
                            new AuthoringValidationIssue(
                                ScopeAuthoringValidationCodes.ContributionInvalid,
                                ValidationSeverity.Error,
                                ValidationIssueCategory.LocalNode,
                                root.ModuleId,
                                exception.Message,
                                root.HasSourceLocation ? root.CreateSourceLocation() : null,
                                subjectName: root.name,
                                runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) })
                        });
                }
            }

            if (entityIssues.Count > 0)
                return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), entityIssues.ToArray());

            return new ScopeAuthoringExtractionReport(contributions.ToArray(), entityInputs.ToArray(), declarationInputs.ToArray(), serviceDeclarations.ToArray(), Array.Empty<AuthoringValidationIssue>());
        }

        static void ExtractEntityAuthoring(
            ScopeAuthoringRoot root,
            List<EntityAuthoringInput> entityInputs,
            List<EntityDeclarationPlanInput> declarationInputs,
            List<EntityServiceDeclarationInput> serviceDeclarations,
            List<AuthoringValidationIssue> issues,
            HashSet<string> seenEntityRefs,
            HashSet<int> seenDeclaredServiceIds)
        {
            EntityIdentityMB[] entities = root.GetComponentsInChildren<EntityIdentityMB>(true);
            Array.Sort(entities, CompareEntities);

            for (int index = 0; index < entities.Length; index++)
            {
                EntityIdentityMB entity = entities[index];
                if (entity == null)
                    continue;

                if (!entity.TryCreatePlanInput(root.ModuleId, out EntityAuthoringInput input, out string failureReason))
                {
                    issues.Add(CreateEntityIssue(root, entity, ScopeAuthoringValidationCodes.EntityInvalid, failureReason));
                    continue;
                }

                if (!seenEntityRefs.Add(input.EntityRef.Value))
                {
                    issues.Add(CreateEntityIssue(root, entity, ScopeAuthoringValidationCodes.DuplicateEntityRef, "Duplicate EntityRef detected across explicit authoring roots."));
                    continue;
                }

                entityInputs.Add(input);
            }

            EntityDeclarationMB[] declarations = root.GetComponentsInChildren<EntityDeclarationMB>(true);
            Array.Sort(declarations, CompareDeclarations);

            for (int index = 0; index < declarations.Length; index++)
            {
                EntityDeclarationMB declaration = declarations[index];
                if (declaration == null)
                    continue;

                if (!declaration.IsBoundToExplicitAncestorEntity())
                {
                    issues.Add(CreateDeclarationIssue(root, declaration, ScopeAuthoringValidationCodes.DeclarationOwnerMismatch, "Declaration MBs must be attached to the bound EntityIdentityMB or one of its children."));
                    continue;
                }

                if (!declaration.TryCreatePlanInput(root.ModuleId, out EntityDeclarationPlanInput input, out string failureReason))
                {
                    issues.Add(CreateDeclarationIssue(root, declaration, ScopeAuthoringValidationCodes.DeclarationInvalid, failureReason));
                    continue;
                }

                declarationInputs.Add(input);
                ExtractServiceDeclarations(root, declaration, input, serviceDeclarations, issues, seenDeclaredServiceIds);
            }
        }

        static void ExtractServiceDeclarations(
            ScopeAuthoringRoot root,
            EntityDeclarationMB declaration,
            EntityDeclarationPlanInput declarationInput,
            List<EntityServiceDeclarationInput> serviceDeclarations,
            List<AuthoringValidationIssue> issues,
            HashSet<int> seenDeclaredServiceIds)
        {
            if (declaration is not IEntityServiceDeclarationAuthoring serviceDeclarationAuthoring)
                return;

            if (!serviceDeclarationAuthoring.TryCreateServiceDeclarations(in declarationInput, out EntityServiceDeclarationInput[] declarations, out string failureReason))
            {
                issues.Add(CreateDeclarationIssue(root, declaration, ScopeAuthoringValidationCodes.ServiceDeclarationInvalid, failureReason));
                return;
            }

            for (int index = 0; index < declarations.Length; index++)
            {
                EntityServiceDeclarationInput serviceDeclaration = declarations[index];
                if (!seenDeclaredServiceIds.Add(serviceDeclaration.ServiceId.Value))
                {
                    issues.Add(CreateDeclarationIssue(root, declaration, ScopeAuthoringValidationCodes.DuplicateServiceDeclaration, "Duplicate service declaration id detected across explicit authoring roots."));
                    continue;
                }

                serviceDeclarations.Add(serviceDeclaration);
            }
        }

        static ContributionItem[] BuildContributionItems(ScopeAuthoringRoot root, ScopeAuthoringLink[] links, IReadOnlyList<EntityServiceDeclarationInput> serviceDeclarations)
        {
            int linkCount = links.Length;
            int serviceCount = serviceDeclarations == null ? 0 : serviceDeclarations.Count;
            ContributionItem[] items = new ContributionItem[linkCount + serviceCount];
            for (int index = 0; index < linkCount; index++)
            {
                ScopeAuthoringLink link = links[index];
                if (!TryMapContributionSource(link.SourceKind, out ContributionSource source))
                    return Array.Empty<ContributionItem>();

                string stableId = CreateStableId(link.ScopeAuthoringId);
                string? debugName = string.IsNullOrWhiteSpace(link.name) ? null : link.name;

                try
                {
                    items[index] = new ContributionItem(
                        ContributionKind.ScopeContribution,
                        root.ModuleId,
                        source,
                        new SourceLocationIR(link.CreateSourceLocation()),
                        stableId,
                        root.Availability,
                        null,
                        ContributionConflictPolicy.ValidationError,
                        debugName);
                }
                catch (ArgumentException)
                {
                    return Array.Empty<ContributionItem>();
                }
            }

            for (int index = 0; index < serviceCount; index++)
            {
                EntityServiceDeclarationInput serviceDeclaration = serviceDeclarations[index];
                if (!TryMapContributionSource(serviceDeclaration.SourceKind, out ContributionSource source))
                    return Array.Empty<ContributionItem>();

                try
                {
                    items[linkCount + index] = new ContributionItem(
                        ContributionKind.ServiceContribution,
                        root.ModuleId,
                        source,
                        serviceDeclaration.Source,
                        serviceDeclaration.StableId,
                        root.Availability,
                        null,
                        ContributionConflictPolicy.ValidationError,
                        serviceDeclaration.DebugName.Length == 0 ? serviceDeclaration.ServiceName : serviceDeclaration.DebugName);
                }
                catch (ArgumentException)
                {
                    return Array.Empty<ContributionItem>();
                }
            }

            return items;
        }

        static bool TryMapContributionSource(SourceLocationIR sourceLocation, out ContributionSource source)
        {
            if (sourceLocation.Kind == SourceLocationKind.Unity && sourceLocation.UnitySource.HasValue)
            {
                UnitySourceLocation unitySource = sourceLocation.UnitySource.Value;
                if (!string.IsNullOrEmpty(unitySource.ScenePath))
                {
                    source = !string.IsNullOrEmpty(unitySource.AssetPath)
                        && unitySource.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? ContributionSource.PrefabInstance
                        : ContributionSource.SceneObject;
                    return true;
                }

                if (!string.IsNullOrEmpty(unitySource.AssetPath))
                {
                    source = unitySource.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? ContributionSource.PrefabAsset
                        : ContributionSource.ScriptableObjectAsset;
                    return true;
                }
            }

            if (sourceLocation.Kind == SourceLocationKind.Generated)
            {
                source = ContributionSource.GeneratedAsset;
                return true;
            }

            source = ContributionSource.Unknown;
            return false;
        }

        static bool TryMapContributionSource(UnityAuthoringSourceKind kind, out ContributionSource source)
        {
            switch (kind)
            {
                case UnityAuthoringSourceKind.SceneObject:
                    source = ContributionSource.SceneObject;
                    return true;
                case UnityAuthoringSourceKind.PrefabAsset:
                    source = ContributionSource.PrefabAsset;
                    return true;
                case UnityAuthoringSourceKind.PrefabInstance:
                    source = ContributionSource.PrefabInstance;
                    return true;
                case UnityAuthoringSourceKind.PrefabVariant:
                    source = ContributionSource.PrefabVariant;
                    return true;
                case UnityAuthoringSourceKind.ScriptableObjectAsset:
                    source = ContributionSource.ScriptableObjectAsset;
                    return true;
                case UnityAuthoringSourceKind.GeneratedAsset:
                    source = ContributionSource.GeneratedAsset;
                    return true;
                default:
                    source = ContributionSource.Unknown;
                    return false;
            }
        }

        static int CompareRoots(ScopeAuthoringRoot left, ScopeAuthoringRoot right)
        {
            if (left == null)
                return right == null ? 0 : -1;

            if (right == null)
                return 1;

            int result = left.HasModuleMetadata.CompareTo(right.HasModuleMetadata);
            if (result != 0)
                return -result;

            if (left.HasModuleMetadata && right.HasModuleMetadata)
            {
                result = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
                if (result != 0)
                    return result;
            }

            result = StringComparer.Ordinal.Compare(left.ModuleName, right.ModuleName);
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static int CompareLinks(ScopeAuthoringLink left, ScopeAuthoringLink right)
        {
            int idComparison = left.ScopeAuthoringId.Value.CompareTo(right.ScopeAuthoringId.Value);
            if (idComparison != 0)
                return idComparison;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static int CompareEntities(EntityIdentityMB left, EntityIdentityMB right)
        {
            int entityRefComparison = StringComparer.Ordinal.Compare(left.id, right.id);
            if (entityRefComparison != 0)
                return entityRefComparison;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static int CompareDeclarations(EntityDeclarationMB left, EntityDeclarationMB right)
        {
            int serviceComparison = StringComparer.Ordinal.Compare(left.ServiceId, right.ServiceId);
            if (serviceComparison != 0)
                return serviceComparison;

            int declarationIdComparison = StringComparer.Ordinal.Compare(left.DeclarationId, right.DeclarationId);
            if (declarationIdComparison != 0)
                return declarationIdComparison;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static string CreateStableId(ScopeAuthoringId authoringId)
        {
            return "scope-authoring-" + authoringId.Value.ToString("D10");
        }

        static AuthoringValidationIssue CreateEntityIssue(ScopeAuthoringRoot root, EntityIdentityMB entity, string code, string message)
        {
            return new AuthoringValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalNode,
                root.ModuleId,
                message,
                entity.HasSourceLocation ? entity.CreateSourceLocation() : null,
                subjectName: entity.name,
                runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) });
        }

        static AuthoringValidationIssue CreateDeclarationIssue(ScopeAuthoringRoot root, EntityDeclarationMB declaration, string code, string message)
        {
            return new AuthoringValidationIssue(
                code,
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalNode,
                root.ModuleId,
                message,
                declaration.HasSourceLocation ? declaration.CreateSourceLocation() : null,
                subjectName: declaration.name,
                runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) });
        }

        static AuthoringValidationIssue[] CopyIssues(IReadOnlyList<AuthoringValidationIssue> issues)
        {
            AuthoringValidationIssue[] snapshot = new AuthoringValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
                snapshot[index] = issues[index];

            return snapshot;
        }
    }
}
