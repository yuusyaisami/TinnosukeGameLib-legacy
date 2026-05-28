#nullable enable

using System;
using System.Collections.Generic;
using Game;
using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using UnityEngine;

using AuthoringUnitySourceKind = Game.Kernel.Authoring.UnityAuthoringSourceKind;
using ContributionItemModel = Game.Kernel.Contributions.ContributionItem;
using ContributionKindModel = Game.Kernel.Contributions.ContributionKind;
using ContributionSourceModel = Game.Kernel.Contributions.ContributionSource;
using ModuleContributionDataModel = Game.Kernel.Contributions.ModuleContributionData;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public sealed class ScopeAuthoringExtractionReport
    {
        readonly ModuleContributionDataModel[] contributions;
        readonly EntityAuthoringInput[] entityInputs;
        readonly EntityDeclarationPlanInput[] declarationInputs;
        readonly EntityServiceDeclarationInput[] serviceDeclarations;
        readonly CommandDeclarationInput[] commandDeclarations;
        readonly AuthoringValidationIssue[] issues;

        public ScopeAuthoringExtractionReport(ModuleContributionDataModel[] contributions, EntityAuthoringInput[] entityInputs, EntityDeclarationPlanInput[] declarationInputs, EntityServiceDeclarationInput[] serviceDeclarations, CommandDeclarationInput[] commandDeclarations, AuthoringValidationIssue[] issues)
        {
            this.contributions = contributions ?? Array.Empty<ModuleContributionDataModel>();
            this.entityInputs = entityInputs ?? Array.Empty<EntityAuthoringInput>();
            this.declarationInputs = declarationInputs ?? Array.Empty<EntityDeclarationPlanInput>();
            this.serviceDeclarations = serviceDeclarations ?? Array.Empty<EntityServiceDeclarationInput>();
            this.commandDeclarations = commandDeclarations ?? Array.Empty<CommandDeclarationInput>();
            this.issues = issues ?? Array.Empty<AuthoringValidationIssue>();
        }

        public IReadOnlyList<ModuleContributionDataModel> Contributions => contributions;

        public IReadOnlyList<EntityAuthoringInput> EntityInputs => entityInputs;

        public IReadOnlyList<EntityDeclarationPlanInput> DeclarationInputs => declarationInputs;

        public IReadOnlyList<EntityServiceDeclarationInput> ServiceDeclarations => serviceDeclarations;

        public IReadOnlyList<CommandDeclarationInput> CommandDeclarations => commandDeclarations;

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
                return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionDataModel>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), Array.Empty<CommandDeclarationInput>(), CopyIssues(validationReport.Issues));

            List<ScopeAuthoringRoot> orderedRoots = new List<ScopeAuthoringRoot>(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                ScopeAuthoringRoot root = roots[index];
                if (root != null)
                    orderedRoots.Add(root);
            }

            orderedRoots.Sort(CompareRoots);

            List<ModuleContributionDataModel> contributions = new List<ModuleContributionDataModel>(orderedRoots.Count);
            List<EntityAuthoringInput> entityInputs = new List<EntityAuthoringInput>();
            List<EntityDeclarationPlanInput> declarationInputs = new List<EntityDeclarationPlanInput>();
            List<EntityServiceDeclarationInput> serviceDeclarations = new List<EntityServiceDeclarationInput>();
            List<CommandDeclarationInput> commandDeclarations = new List<CommandDeclarationInput>();
            List<AuthoringValidationIssue> entityIssues = new List<AuthoringValidationIssue>();
            HashSet<string> seenEntityRefs = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> seenDeclaredServiceIds = new HashSet<int>();
            HashSet<int> seenDeclaredCommandTypeIds = new HashSet<int>();
            HashSet<string> seenDeclaredCommandStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < orderedRoots.Count; index++)
            {
                ScopeAuthoringRoot root = orderedRoots[index];
                List<EntityServiceDeclarationInput> rootServiceDeclarations = new List<EntityServiceDeclarationInput>();
                List<CommandDeclarationInput> rootCommandDeclarations = new List<CommandDeclarationInput>();
                ExtractEntityAuthoring(root, entityInputs, declarationInputs, rootServiceDeclarations, entityIssues, seenEntityRefs, seenDeclaredServiceIds);
                ExtractCommandDeclarations(root, rootCommandDeclarations, entityIssues, seenDeclaredCommandTypeIds, seenDeclaredCommandStableIds);

                ScopeAuthoringLink[] links = root.GetComponentsInChildren<ScopeAuthoringLink>(true);
                Array.Sort(links, CompareLinks);

                ContributionItemModel[] items = BuildContributionItems(root, links, rootServiceDeclarations, rootCommandDeclarations);
                if (items.Length == 0)
                    return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionDataModel>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), Array.Empty<CommandDeclarationInput>(), new[]
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
                    bool hasLifecycleContributions = HasLifecycleContributions(rootServiceDeclarations);
                    ContributionKindModel[] ownedContributionKinds = BuildOwnedContributionKinds(rootServiceDeclarations.Count, rootCommandDeclarations.Count, hasLifecycleContributions);

                    ModuleContributionDataModel contribution = new ModuleContributionDataModel(
                        root.ModuleId,
                        root.ModuleName,
                        root.ModuleKind,
                        root.ModuleVersion,
                        root.Availability,
                        UnityAuthoringBridge.ToKernelSourceLocation(root.CreateSourceLocation()),
                        ownedContributionKinds,
                        Array.Empty<ModuleId>(),
                        Array.Empty<ModuleId>(),
                        items);

                    contributions.Add(contribution);
                    serviceDeclarations.AddRange(rootServiceDeclarations);
                    commandDeclarations.AddRange(rootCommandDeclarations);
                }
                catch (ArgumentException exception)
                {
                    return new ScopeAuthoringExtractionReport(
                        Array.Empty<ModuleContributionDataModel>(),
                        Array.Empty<EntityAuthoringInput>(),
                        Array.Empty<EntityDeclarationPlanInput>(),
                        Array.Empty<EntityServiceDeclarationInput>(),
                        Array.Empty<CommandDeclarationInput>(),
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
                return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionDataModel>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), Array.Empty<CommandDeclarationInput>(), entityIssues.ToArray());

            return new ScopeAuthoringExtractionReport(contributions.ToArray(), entityInputs.ToArray(), declarationInputs.ToArray(), serviceDeclarations.ToArray(), commandDeclarations.ToArray(), Array.Empty<AuthoringValidationIssue>());
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

        static void ExtractCommandDeclarations(
            ScopeAuthoringRoot root,
            List<CommandDeclarationInput> commandDeclarations,
            List<AuthoringValidationIssue> issues,
            HashSet<int> seenDeclaredCommandTypeIds,
            HashSet<string> seenDeclaredCommandStableIds)
        {
            MonoBehaviour[] components = root.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component == null || component is not ICommandDeclarationAuthoring commandDeclarationAuthoring)
                    continue;

                if (!commandDeclarationAuthoring.TryCreateCommandDeclarations(root.ModuleId, out CommandDeclarationInput[] declarations, out string failureReason))
                {
                    issues.Add(CreateComponentIssue(root, component, ScopeAuthoringValidationCodes.CommandDeclarationInvalid, failureReason));
                    continue;
                }

                for (int declarationIndex = 0; declarationIndex < declarations.Length; declarationIndex++)
                {
                    CommandDeclarationInput declaration = declarations[declarationIndex];
                    if (!seenDeclaredCommandTypeIds.Add(declaration.TypeId.Value))
                    {
                        issues.Add(CreateComponentIssue(root, component, ScopeAuthoringValidationCodes.DuplicateCommandDeclaration, "Duplicate command declaration id detected across explicit authoring roots."));
                        continue;
                    }

                    if (!seenDeclaredCommandStableIds.Add(declaration.StableId))
                    {
                        issues.Add(CreateComponentIssue(root, component, ScopeAuthoringValidationCodes.DuplicateCommandDeclaration, "Duplicate command declaration stable key detected across explicit authoring roots."));
                        continue;
                    }

                    commandDeclarations.Add(declaration);
                }
            }
        }

        static ContributionItemModel[] BuildContributionItems(ScopeAuthoringRoot root, ScopeAuthoringLink[] links, IReadOnlyList<EntityServiceDeclarationInput> serviceDeclarations, IReadOnlyList<CommandDeclarationInput> commandDeclarations)
        {
            int linkCount = links.Length;
            int serviceCount = serviceDeclarations == null ? 0 : serviceDeclarations.Count;
            int commandCount = commandDeclarations == null ? 0 : commandDeclarations.Count;
            int lifecycleCount = CountLifecycleContributions(serviceDeclarations);
            ContributionItemModel[] items = new ContributionItemModel[linkCount + serviceCount + commandCount + lifecycleCount];
            for (int index = 0; index < linkCount; index++)
            {
                ScopeAuthoringLink link = links[index];
                if (!TryMapContributionSource(link.SourceKind, out ContributionSourceModel source))
                    return Array.Empty<ContributionItemModel>();

                string stableId = CreateStableId(link.ScopeAuthoringId);
                string? debugName = string.IsNullOrWhiteSpace(link.name) ? null : link.name;

                try
                {
                    items[index] = new ContributionItemModel(
                        ContributionKindModel.ScopeContribution,
                        root.ModuleId,
                        source,
                        UnityAuthoringBridge.ToKernelSourceLocation(link.CreateSourceLocation()),
                        stableId,
                        root.Availability,
                        null,
                        ContributionConflictPolicy.ValidationError,
                        debugName);
                }
                catch (ArgumentException)
                {
                    return Array.Empty<ContributionItemModel>();
                }
            }

            for (int index = 0; index < serviceCount; index++)
            {
                EntityServiceDeclarationInput serviceDeclaration = serviceDeclarations[index];
                if (!TryMapContributionSource(serviceDeclaration.SourceKind, out ContributionSourceModel source))
                    return Array.Empty<ContributionItemModel>();

                try
                {
                    items[linkCount + index] = new ContributionItemModel(
                        ContributionKindModel.ServiceContribution,
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
                    return Array.Empty<ContributionItemModel>();
                }
            }

            for (int index = 0; index < commandCount; index++)
            {
                CommandDeclarationInput commandDeclaration = commandDeclarations[index];
                if (!TryMapContributionSource(commandDeclaration.Source, out ContributionSourceModel source))
                    return Array.Empty<ContributionItemModel>();

                try
                {
                    items[linkCount + serviceCount + index] = new ContributionItemModel(
                        ContributionKindModel.CommandContribution,
                        root.ModuleId,
                        source,
                        commandDeclaration.Source,
                        commandDeclaration.StableId,
                        root.Availability,
                        null,
                        ContributionConflictPolicy.ValidationError,
                        commandDeclaration.RuntimeName);
                }
                catch (ArgumentException)
                {
                    return Array.Empty<ContributionItemModel>();
                }
            }

            int lifecycleItemIndex = linkCount + serviceCount + commandCount;
            for (int serviceIndex = 0; serviceIndex < serviceCount; serviceIndex++)
            {
                EntityServiceDeclarationInput serviceDeclaration = serviceDeclarations[serviceIndex];
                ReadOnlySpan<ServiceLifecycleContributionInput> lifecycleContributions = serviceDeclaration.LifecycleContributions;
                for (int contributionIndex = 0; contributionIndex < lifecycleContributions.Length; contributionIndex++)
                {
                    ServiceLifecycleContributionInput lifecycleContribution = lifecycleContributions[contributionIndex];
                    if (!TryMapContributionSource(lifecycleContribution.Source, out ContributionSourceModel source))
                        return Array.Empty<ContributionItemModel>();

                    try
                    {
                        items[lifecycleItemIndex++] = new ContributionItemModel(
                            ContributionKindModel.LifecycleContribution,
                            root.ModuleId,
                            source,
                            lifecycleContribution.Source,
                            lifecycleContribution.StableId,
                            root.Availability,
                            null,
                            ContributionConflictPolicy.ValidationError,
                            lifecycleContribution.DebugName);
                    }
                    catch (ArgumentException)
                    {
                        return Array.Empty<ContributionItemModel>();
                    }
                }
            }

            return items;
        }

        static bool HasLifecycleContributions(IReadOnlyList<EntityServiceDeclarationInput> serviceDeclarations)
        {
            if (serviceDeclarations == null)
                return false;

            for (int index = 0; index < serviceDeclarations.Count; index++)
            {
                if (serviceDeclarations[index].LifecycleContributions.Length > 0)
                    return true;
            }

            return false;
        }

        static int CountLifecycleContributions(IReadOnlyList<EntityServiceDeclarationInput> serviceDeclarations)
        {
            if (serviceDeclarations == null || serviceDeclarations.Count == 0)
                return 0;

            int count = 0;
            for (int index = 0; index < serviceDeclarations.Count; index++)
                count += serviceDeclarations[index].LifecycleContributions.Length;

            return count;
        }

        static ContributionKindModel[] BuildOwnedContributionKinds(int serviceDeclarationCount, int commandDeclarationCount, bool hasLifecycleContributions)
        {
            if (serviceDeclarationCount == 0 && commandDeclarationCount == 0)
                return new[] { ContributionKindModel.ScopeContribution };

            if (serviceDeclarationCount == 0)
                return new[] { ContributionKindModel.ScopeContribution, ContributionKindModel.CommandContribution };

            if (commandDeclarationCount == 0)
                return hasLifecycleContributions
                    ? new[] { ContributionKindModel.ScopeContribution, ContributionKindModel.ServiceContribution, ContributionKindModel.LifecycleContribution }
                    : new[] { ContributionKindModel.ScopeContribution, ContributionKindModel.ServiceContribution };

            if (hasLifecycleContributions)
                return new[] { ContributionKindModel.ScopeContribution, ContributionKindModel.ServiceContribution, ContributionKindModel.CommandContribution, ContributionKindModel.LifecycleContribution };

            return new[] { ContributionKindModel.ScopeContribution, ContributionKindModel.ServiceContribution, ContributionKindModel.CommandContribution };
        }

        static bool TryMapContributionSource(SourceLocationIR sourceLocation, out ContributionSourceModel source)
        {
            if (sourceLocation.Kind == SourceLocationKind.Unity && sourceLocation.UnitySource.HasValue)
            {
                Game.Kernel.IR.UnitySourceLocation unitySource = sourceLocation.UnitySource.Value;
                if (!string.IsNullOrEmpty(unitySource.ScenePath))
                {
                    source = !string.IsNullOrEmpty(unitySource.AssetPath)
                        && unitySource.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? ContributionSourceModel.PrefabInstance
                        : ContributionSourceModel.SceneObject;
                    return true;
                }

                if (!string.IsNullOrEmpty(unitySource.AssetPath))
                {
                    source = unitySource.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                        ? ContributionSourceModel.PrefabAsset
                        : ContributionSourceModel.ScriptableObjectAsset;
                    return true;
                }
            }

            if (sourceLocation.Kind == SourceLocationKind.Generated)
            {
                source = ContributionSourceModel.GeneratedAsset;
                return true;
            }

            source = ContributionSourceModel.Unknown;
            return false;
        }

        static bool TryMapContributionSource(AuthoringUnitySourceKind kind, out ContributionSourceModel source)
        {
            switch (kind)
            {
                case AuthoringUnitySourceKind.SceneObject:
                    source = ContributionSourceModel.SceneObject;
                    return true;
                case AuthoringUnitySourceKind.PrefabAsset:
                    source = ContributionSourceModel.PrefabAsset;
                    return true;
                case AuthoringUnitySourceKind.PrefabInstance:
                    source = ContributionSourceModel.PrefabInstance;
                    return true;
                case AuthoringUnitySourceKind.PrefabVariant:
                    source = ContributionSourceModel.PrefabVariant;
                    return true;
                case AuthoringUnitySourceKind.ScriptableObjectAsset:
                    source = ContributionSourceModel.ScriptableObjectAsset;
                    return true;
                case AuthoringUnitySourceKind.GeneratedAsset:
                    source = ContributionSourceModel.GeneratedAsset;
                    return true;
                default:
                    source = ContributionSourceModel.Unknown;
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

        static AuthoringValidationIssue CreateComponentIssue(ScopeAuthoringRoot root, Component component, string code, string message)
        {
            return new AuthoringValidationIssue(
            code,
            ValidationSeverity.Error,
            ValidationIssueCategory.LocalNode,
            root.ModuleId,
            message,
            root.HasSourceLocation ? root.CreateSourceLocation() : null,
            subjectName: component == null ? root.name : component.name,
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
