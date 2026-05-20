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
        readonly AuthoringValidationIssue[] issues;

        public ScopeAuthoringExtractionReport(ModuleContributionData[] contributions, AuthoringValidationIssue[] issues)
        {
            this.contributions = contributions ?? Array.Empty<ModuleContributionData>();
            this.issues = issues ?? Array.Empty<AuthoringValidationIssue>();
        }

        public IReadOnlyList<ModuleContributionData> Contributions => contributions;

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
                return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), CopyIssues(validationReport.Issues));

            List<ScopeAuthoringRoot> orderedRoots = new List<ScopeAuthoringRoot>(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                ScopeAuthoringRoot root = roots[index];
                if (root != null)
                    orderedRoots.Add(root);
            }

            orderedRoots.Sort(CompareRoots);

            List<ModuleContributionData> contributions = new List<ModuleContributionData>(orderedRoots.Count);
            for (int index = 0; index < orderedRoots.Count; index++)
            {
                ScopeAuthoringRoot root = orderedRoots[index];
                ScopeAuthoringLink[] links = root.GetComponentsInChildren<ScopeAuthoringLink>(true);
                Array.Sort(links, CompareLinks);

                ContributionItem[] items = BuildContributionItems(root, links);
                if (items.Length == 0)
                    return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), new[]
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
                    ModuleContributionData contribution = new ModuleContributionData(
                        root.ModuleId,
                        root.ModuleName,
                        root.ModuleKind,
                        root.ModuleVersion,
                        root.Availability,
                        new SourceLocationIR(root.CreateSourceLocation()),
                        new[] { ContributionKind.ScopeContribution },
                        Array.Empty<ModuleId>(),
                        Array.Empty<ModuleId>(),
                        items);

                    contributions.Add(contribution);
                }
                catch (ArgumentException exception)
                {
                    return new ScopeAuthoringExtractionReport(
                        Array.Empty<ModuleContributionData>(),
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

            return new ScopeAuthoringExtractionReport(contributions.ToArray(), Array.Empty<AuthoringValidationIssue>());
        }

        static ContributionItem[] BuildContributionItems(ScopeAuthoringRoot root, ScopeAuthoringLink[] links)
        {
            ContributionItem[] items = new ContributionItem[links.Length];
            for (int index = 0; index < links.Length; index++)
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

            return items;
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

        static string CreateStableId(ScopeAuthoringId authoringId)
        {
            return "scope-authoring-" + authoringId.Value.ToString("D10");
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