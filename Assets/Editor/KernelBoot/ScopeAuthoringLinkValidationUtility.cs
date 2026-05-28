#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.IR;
using UnityEngine;

using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public readonly struct ScopeAuthoringLinkValidationIssue
    {
        public ScopeAuthoringLinkValidationIssue(
            string code,
            ScopeAuthoringLink primary,
            ScopeAuthoringLink? secondary,
            ScopeAuthoringId authoringId,
            string message,
            AuthoringUnitySourceLocation sourceLocation,
            bool hasSecondarySourceLocation,
            AuthoringUnitySourceLocation secondarySourceLocation,
            bool hasBaseSourceLocation,
            AuthoringUnitySourceLocation baseSourceLocation)
        {
            Code = code;
            Primary = primary;
            Secondary = secondary;
            AuthoringId = authoringId;
            Message = message;
            SourceLocation = sourceLocation;
            HasSecondarySourceLocation = hasSecondarySourceLocation;
            SecondarySourceLocation = secondarySourceLocation;
            HasBaseSourceLocation = hasBaseSourceLocation;
            BaseSourceLocation = baseSourceLocation;
        }

        public string Code { get; }

        public ScopeAuthoringLink Primary { get; }

        public ScopeAuthoringLink? Secondary { get; }

        public ScopeAuthoringId AuthoringId { get; }

        public string Message { get; }

        public AuthoringUnitySourceLocation SourceLocation { get; }

        public bool HasSecondarySourceLocation { get; }

        public AuthoringUnitySourceLocation SecondarySourceLocation { get; }

        public bool HasBaseSourceLocation { get; }

        public AuthoringUnitySourceLocation BaseSourceLocation { get; }
    }

    public readonly struct ScopeAuthoringLinkValidationReport
    {
        readonly ScopeAuthoringLinkValidationIssue[] issues;

        public ScopeAuthoringLinkValidationReport(ScopeAuthoringLinkValidationIssue[] issues)
        {
            this.issues = issues ?? Array.Empty<ScopeAuthoringLinkValidationIssue>();
        }

        public IReadOnlyList<ScopeAuthoringLinkValidationIssue> Issues => issues;

        public bool IsValid => issues.Length == 0;
    }

    public static class ScopeAuthoringLinkValidationUtility
    {
        public static ScopeAuthoringLinkValidationReport Validate(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            return Validate(root.GetComponentsInChildren<ScopeAuthoringLink>(true));
        }

        public static ScopeAuthoringLinkValidationReport Validate(IReadOnlyList<ScopeAuthoringLink> links)
        {
            if (links == null)
                throw new ArgumentNullException(nameof(links));

            List<ScopeAuthoringLinkValidationIssue> issues = new List<ScopeAuthoringLinkValidationIssue>();
            Dictionary<int, ScopeAuthoringLink> firstById = new Dictionary<int, ScopeAuthoringLink>();

            for (int index = 0; index < links.Count; index++)
            {
                ScopeAuthoringLink link = links[index];
                if (link == null)
                    continue;

                if (!link.TryValidate(out string failureReason))
                {
                    issues.Add(new ScopeAuthoringLinkValidationIssue(
                        "UNITY_SCOPE_AUTHORING_INVALID",
                        link,
                        null,
                        link.HasScopeAuthoringId ? link.ScopeAuthoringId : default,
                        failureReason,
                        TryCreateSourceLocation(link),
                        false,
                        default,
                        link.HasBaseSourceLocation,
                        TryCreateBaseSourceLocation(link)));
                }

                if (!link.HasScopeAuthoringId)
                    continue;

                int authoringId = link.ScopeAuthoringId.Value;
                if (!firstById.TryGetValue(authoringId, out ScopeAuthoringLink first))
                {
                    firstById.Add(authoringId, link);
                    continue;
                }

                if (ReferenceEquals(first, link))
                    continue;

                issues.Add(new ScopeAuthoringLinkValidationIssue(
                    "UNITY_SCOPE_AUTHORING_DUPLICATE_ID",
                    first,
                    link,
                    link.ScopeAuthoringId,
                    "Duplicate ScopeAuthoringId detected across authored scope links.",
                    TryCreateSourceLocation(first),
                    true,
                    TryCreateSourceLocation(link),
                    first.HasBaseSourceLocation,
                    TryCreateBaseSourceLocation(first)));
            }

            issues.Sort(CompareIssues);
            return new ScopeAuthoringLinkValidationReport(issues.ToArray());
        }

        static int CompareIssues(ScopeAuthoringLinkValidationIssue left, ScopeAuthoringLinkValidationIssue right)
        {
            int result = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (result != 0)
                return result;

            result = left.AuthoringId.Value.CompareTo(right.AuthoringId.Value);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.Message, right.Message);
            if (result != 0)
                return result;

            result = CompareLinkNames(left.Primary, right.Primary);
            if (result != 0)
                return result;

            if (left.Secondary != null && right.Secondary != null)
                return CompareLinkNames(left.Secondary, right.Secondary);

            if (left.Secondary != null)
                return 1;

            if (right.Secondary != null)
                return -1;

            return 0;
        }

        static int CompareLinkNames(ScopeAuthoringLink left, ScopeAuthoringLink right)
        {
            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static AuthoringUnitySourceLocation TryCreateSourceLocation(ScopeAuthoringLink link)
        {
            return link.HasSourceLocation ? link.CreateSourceLocation() : default;
        }

        static AuthoringUnitySourceLocation TryCreateBaseSourceLocation(ScopeAuthoringLink link)
        {
            return link.TryGetBaseSourceLocation(out AuthoringUnitySourceLocation sourceLocation) ? sourceLocation : default;
        }
    }
}