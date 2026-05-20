#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using UnityEngine;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class ScopeAuthoringValidationCodes
    {
        public const string RootNull = "UNITY_SCOPE_AUTHORING_ROOT_NULL";
        public const string RootInvalid = "UNITY_SCOPE_AUTHORING_ROOT_INVALID";
        public const string NestedRoot = "UNITY_SCOPE_AUTHORING_NESTED_ROOT";
        public const string DuplicateModuleId = "UNITY_SCOPE_AUTHORING_DUPLICATE_MODULE_ID";
        public const string RootEmpty = "UNITY_SCOPE_AUTHORING_ROOT_EMPTY";
        public const string ContributionInvalid = "UNITY_SCOPE_AUTHORING_CONTRIBUTION_INVALID";
        public const string BaseTraceUnsupported = "UNITY_SCOPE_AUTHORING_BASE_TRACE_UNSUPPORTED";
        public const string LinkSourceKind = "UNITY_SCOPE_AUTHORING_LINK_SOURCE_KIND";
        public const string LinkInvalid = "UNITY_SCOPE_AUTHORING_INVALID";
        public const string LinkNull = "UNITY_SCOPE_AUTHORING_LINK_NULL";
        public const string DuplicateScopeAuthoringId = "UNITY_SCOPE_AUTHORING_DUPLICATE_ID";
    }

    public sealed class ScopeAuthoringValidationInput
    {
        readonly ScopeAuthoringLink[] links;

        public ScopeAuthoringValidationInput(ScopeAuthoringRoot? root, IReadOnlyList<ScopeAuthoringLink>? links, bool hasNestedRoot = false)
        {
            Root = root;
            HasNestedRoot = hasNestedRoot;
            this.links = CloneLinks(links);
        }

        public ScopeAuthoringRoot? Root { get; }

        public bool HasNestedRoot { get; }

        public IReadOnlyList<ScopeAuthoringLink> Links => links;

        static ScopeAuthoringLink[] CloneLinks(IReadOnlyList<ScopeAuthoringLink>? links)
        {
            if (links == null || links.Count == 0)
                return Array.Empty<ScopeAuthoringLink>();

            ScopeAuthoringLink[] snapshot = new ScopeAuthoringLink[links.Count];
            for (int index = 0; index < links.Count; index++)
                snapshot[index] = links[index];

            return snapshot;
        }
    }

    public static class ScopeAuthoringValidationService
    {
        public static AuthoringValidationReport Validate(ScopeAuthoringRoot root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            ScopeAuthoringLink[] links = root.GetComponentsInChildren<ScopeAuthoringLink>(true);
            bool hasNestedRoot = root.GetComponentsInChildren<ScopeAuthoringRoot>(true).Length > 1;
            return Validate(new[] { new ScopeAuthoringValidationInput(root, links, hasNestedRoot) });
        }

        public static AuthoringValidationReport Validate(IReadOnlyList<ScopeAuthoringRoot> roots)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            List<ScopeAuthoringValidationInput> inputs = new List<ScopeAuthoringValidationInput>(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                ScopeAuthoringRoot root = roots[index];
                if (root == null)
                {
                    inputs.Add(new ScopeAuthoringValidationInput(null, Array.Empty<ScopeAuthoringLink>()));
                    continue;
                }

                ScopeAuthoringLink[] links = root.GetComponentsInChildren<ScopeAuthoringLink>(true);
                bool hasNestedRoot = root.GetComponentsInChildren<ScopeAuthoringRoot>(true).Length > 1;
                inputs.Add(new ScopeAuthoringValidationInput(root, links, hasNestedRoot));
            }

            return Validate(inputs);
        }

        public static AuthoringValidationReport Validate(IReadOnlyList<ScopeAuthoringValidationInput> inputs)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            List<ScopeAuthoringValidationInput> orderedInputs = new List<ScopeAuthoringValidationInput>(inputs.Count);
            for (int index = 0; index < inputs.Count; index++)
                orderedInputs.Add(inputs[index]);

            orderedInputs.Sort(CompareInputs);

            List<AuthoringValidationIssue> issues = new List<AuthoringValidationIssue>();
            HashSet<int> seenModuleIds = new HashSet<int>();

            for (int index = 0; index < orderedInputs.Count; index++)
            {
                ScopeAuthoringValidationInput input = orderedInputs[index];
                ScopeAuthoringRoot? root = input.Root;

                if (root == null)
                {
                    issues.Add(CreateRootNullIssue());
                    continue;
                }

                if (!root.TryValidate(out string failureReason))
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.RootInvalid,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        failureReason,
                        root.HasSourceLocation ? root.CreateSourceLocation() : null,
                        subjectName: root.name,
                        runtimeIdentities: root.HasModuleMetadata
                            ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) }
                            : Array.Empty<RuntimeIdentityRef>()));
                }

                if (input.HasNestedRoot)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.NestedRoot,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        "Explicit authoring roots must not contain nested ScopeAuthoringRoot components.",
                        root.HasSourceLocation ? root.CreateSourceLocation() : null,
                        subjectName: root.name,
                        runtimeIdentities: root.HasModuleMetadata
                            ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) }
                            : Array.Empty<RuntimeIdentityRef>()));
                }

                if (root.HasModuleMetadata)
                {
                    int moduleId = root.ModuleId.Value;
                    if (!seenModuleIds.Add(moduleId))
                    {
                        issues.Add(new AuthoringValidationIssue(
                            ScopeAuthoringValidationCodes.DuplicateModuleId,
                            ValidationSeverity.Error,
                            ValidationIssueCategory.CrossModule,
                            root.ModuleId,
                            "Duplicate module identity detected across explicit authoring roots.",
                            root.HasSourceLocation ? root.CreateSourceLocation() : null,
                            subjectName: root.name,
                            runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, moduleId) }));
                    }
                }

                IReadOnlyList<ScopeAuthoringLink> links = input.Links;
                if (links.Count == 0)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.RootEmpty,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        "Explicit authoring roots must contain at least one authored scope link.",
                        root.HasSourceLocation ? root.CreateSourceLocation() : null,
                        subjectName: root.name,
                        runtimeIdentities: root.HasModuleMetadata
                            ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) }
                            : Array.Empty<RuntimeIdentityRef>()));
                    continue;
                }

                AppendLinkIssues(issues, root, links);
            }

            return new AuthoringValidationReport(issues.ToArray());
        }

        static void AppendLinkIssues(List<AuthoringValidationIssue> issues, ScopeAuthoringRoot root, IReadOnlyList<ScopeAuthoringLink> links)
        {
            ScopeAuthoringLinkValidationReport report = ScopeAuthoringLinkValidationUtility.Validate(links);
            for (int index = 0; index < report.Issues.Count; index++)
            {
                ScopeAuthoringLinkValidationIssue issue = report.Issues[index];
                if (issue.Code == ScopeAuthoringValidationCodes.DuplicateScopeAuthoringId)
                {
                    issues.Add(CreateDuplicateScopeAuthoringIdIssue(root, issue));
                    continue;
                }

                issues.Add(CreateLinkIssue(root, issue));
            }

            for (int index = 0; index < links.Count; index++)
            {
                ScopeAuthoringLink link = links[index];
                if (link == null)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.LinkNull,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalEdge,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        "Explicit authoring input must not contain null links.",
                        root.HasSourceLocation ? root.CreateSourceLocation() : null,
                        subjectName: root.name,
                        runtimeIdentities: root.HasModuleMetadata
                            ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value) }
                            : Array.Empty<RuntimeIdentityRef>()));
                    continue;
                }

                bool supportedSourceKind = link.SourceKind == UnityAuthoringSourceKind.SceneObject
                    || link.SourceKind == UnityAuthoringSourceKind.PrefabAsset
                    || link.SourceKind == UnityAuthoringSourceKind.PrefabInstance
                    || link.SourceKind == UnityAuthoringSourceKind.PrefabVariant
                    || link.SourceKind == UnityAuthoringSourceKind.ScriptableObjectAsset
                    || link.SourceKind == UnityAuthoringSourceKind.GeneratedAsset;

                if (!supportedSourceKind)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.LinkSourceKind,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalEdge,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        "Unsupported Unity authoring source kind on authored scope link.",
                        link.HasSourceLocation ? link.CreateSourceLocation() : null,
                        subjectName: link.name,
                        runtimeIdentities: BuildRuntimeIdentities(root, link),
                        additionalPayloadEntries: new[]
                        {
                            new DiagnosticPayloadEntry("ScopeAuthoringSourceKind", DiagnosticPayloadValue.FromString(link.SourceKind.ToString())),
                        }));
                    continue;
                }

                if (link.HasBaseSourceLocation)
                {
                    issues.Add(new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.BaseTraceUnsupported,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalEdge,
                        root.HasModuleMetadata ? root.ModuleId : default,
                        "Scope authoring links with preserved base source trace are not supported by the current extraction model.",
                        link.HasSourceLocation ? link.CreateSourceLocation() : null,
                        baseSourceLocation: link.TryGetBaseSourceLocation(out UnitySourceLocation baseSourceLocation) ? baseSourceLocation : null,
                        subjectName: link.name,
                        runtimeIdentities: BuildRuntimeIdentities(root, link)));
                }
            }
        }

        static AuthoringValidationIssue CreateRootNullIssue()
        {
            return new AuthoringValidationIssue(
                ScopeAuthoringValidationCodes.RootNull,
                ValidationSeverity.Error,
                ValidationIssueCategory.LocalNode,
                default,
                "Explicit authoring roots must not contain null entries.");
        }

        static AuthoringValidationIssue CreateLinkIssue(ScopeAuthoringRoot root, ScopeAuthoringLinkValidationIssue issue)
        {
            ValidationIssueCategory category = issue.Code == ScopeAuthoringValidationCodes.LinkInvalid
                ? ValidationIssueCategory.LocalEdge
                : ValidationIssueCategory.CrossNode;

            return new AuthoringValidationIssue(
                issue.Code,
                ValidationSeverity.Error,
                category,
                root.HasModuleMetadata ? root.ModuleId : default,
                issue.Message,
                issue.Primary != null && issue.Primary.HasSourceLocation ? issue.Primary.CreateSourceLocation() : null,
                issue.HasSecondarySourceLocation ? issue.SecondarySourceLocation : null,
                issue.HasBaseSourceLocation ? issue.BaseSourceLocation : null,
                BuildRuntimeIdentities(root, issue.Primary, issue.Secondary),
                subjectName: issue.Primary != null ? issue.Primary.name : null,
                additionalPayloadEntries: BuildLinkPayloadEntries(issue));
        }

        static AuthoringValidationIssue CreateDuplicateScopeAuthoringIdIssue(ScopeAuthoringRoot root, ScopeAuthoringLinkValidationIssue issue)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(3);
            if (root.HasModuleMetadata)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value));

            if (issue.Primary.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, issue.Primary.ScopeAuthoringId.Value));

            if (issue.Secondary.HasValue && issue.Secondary.Value.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, issue.Secondary.Value.ScopeAuthoringId.Value));

            return new AuthoringValidationIssue(
                issue.Code,
                ValidationSeverity.Error,
                ValidationIssueCategory.CrossNode,
                root.HasModuleMetadata ? root.ModuleId : default,
                issue.Message,
                issue.Primary != null && issue.Primary.HasSourceLocation ? issue.Primary.CreateSourceLocation() : null,
                issue.HasSecondarySourceLocation ? issue.SecondarySourceLocation : null,
                issue.HasBaseSourceLocation ? issue.BaseSourceLocation : null,
                runtimeIdentities.ToArray(),
                subjectName: issue.Primary != null ? issue.Primary.name : null,
                additionalPayloadEntries: BuildDuplicatePayloadEntries(issue));
        }

        static DiagnosticPayloadEntry[] BuildLinkPayloadEntries(ScopeAuthoringLinkValidationIssue issue)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(4)
            {
                new DiagnosticPayloadEntry("ScopeAuthoringCode", DiagnosticPayloadValue.FromString(issue.Code)),
                new DiagnosticPayloadEntry("ScopeAuthoringId", DiagnosticPayloadValue.FromInt32(issue.AuthoringId.Value)),
            };

            if (issue.HasSecondarySourceLocation)
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeAuthoringSecondarySourceLocation", DiagnosticPayloadValue.FromString(issue.SecondarySourceLocation.ToString())));

            if (issue.HasBaseSourceLocation)
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeAuthoringBaseSourceLocation", DiagnosticPayloadValue.FromString(issue.BaseSourceLocation.ToString())));

            return payloadEntries.ToArray();
        }

        static DiagnosticPayloadEntry[] BuildDuplicatePayloadEntries(ScopeAuthoringLinkValidationIssue issue)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(5)
            {
                new DiagnosticPayloadEntry("ScopeAuthoringCode", DiagnosticPayloadValue.FromString(issue.Code)),
                new DiagnosticPayloadEntry("ScopeAuthoringId", DiagnosticPayloadValue.FromInt32(issue.AuthoringId.Value)),
            };

            if (issue.Secondary.HasValue && issue.Secondary.Value.HasScopeAuthoringId)
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeAuthoringSecondaryId", DiagnosticPayloadValue.FromInt32(issue.Secondary.Value.ScopeAuthoringId.Value)));

            if (issue.HasSecondarySourceLocation)
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeAuthoringSecondarySourceLocation", DiagnosticPayloadValue.FromString(issue.SecondarySourceLocation.ToString())));

            if (issue.HasBaseSourceLocation)
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeAuthoringBaseSourceLocation", DiagnosticPayloadValue.FromString(issue.BaseSourceLocation.ToString())));

            return payloadEntries.ToArray();
        }

        static RuntimeIdentityRef[] BuildRuntimeIdentities(ScopeAuthoringRoot root, ScopeAuthoringLinkValidationIssue issue)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(3);
            if (root.HasModuleMetadata)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value));

            if (issue.Primary.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, issue.Primary.ScopeAuthoringId.Value));

            if (issue.Secondary.HasValue && issue.Secondary.Value.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, issue.Secondary.Value.ScopeAuthoringId.Value));

            return runtimeIdentities.ToArray();
        }

        static RuntimeIdentityRef[] BuildRuntimeIdentities(ScopeAuthoringRoot root, ScopeAuthoringLink primary, ScopeAuthoringLink? secondary)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(3);
            if (root.HasModuleMetadata)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value));

            if (primary.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, primary.ScopeAuthoringId.Value));

            if (secondary.HasValue && secondary.Value.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, secondary.Value.ScopeAuthoringId.Value));

            return runtimeIdentities.ToArray();
        }

        static RuntimeIdentityRef[] BuildRuntimeIdentities(ScopeAuthoringRoot root, ScopeAuthoringLink link)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(2);
            if (root.HasModuleMetadata)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, root.ModuleId.Value));

            if (link.HasScopeAuthoringId)
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, link.ScopeAuthoringId.Value));

            return runtimeIdentities.ToArray();
        }

        static int CompareInputs(ScopeAuthoringValidationInput left, ScopeAuthoringValidationInput right)
        {
            ScopeAuthoringRoot? leftRoot = left.Root;
            ScopeAuthoringRoot? rightRoot = right.Root;

            if (leftRoot == null)
                return rightRoot == null ? 0 : -1;

            if (rightRoot == null)
                return 1;

            int result = leftRoot.HasModuleMetadata.CompareTo(rightRoot.HasModuleMetadata);
            if (result != 0)
                return -result;

            if (leftRoot.HasModuleMetadata && rightRoot.HasModuleMetadata)
            {
                result = leftRoot.ModuleId.Value.CompareTo(rightRoot.ModuleId.Value);
                if (result != 0)
                    return result;
            }

            result = StringComparer.Ordinal.Compare(leftRoot.ModuleName, rightRoot.ModuleName);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(leftRoot.name, rightRoot.name);
            if (result != 0)
                return result;

            return left.HasNestedRoot.CompareTo(right.HasNestedRoot);
        }
    }
}