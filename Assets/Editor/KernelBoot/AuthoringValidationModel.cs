#nullable enable

using System;
using System.Collections.Generic;
using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public sealed class AuthoringValidationIssue
    {
        readonly RuntimeIdentityRef[] runtimeIdentities;
        readonly DiagnosticPayloadEntry[] additionalPayloadEntries;

        public AuthoringValidationIssue(
            string code,
            ValidationSeverity severity,
            ValidationIssueCategory category,
            ModuleId ownerModule,
            string message,
            AuthoringUnitySourceLocation? sourceLocation = null,
            AuthoringUnitySourceLocation? secondarySourceLocation = null,
            AuthoringUnitySourceLocation? baseSourceLocation = null,
            RuntimeIdentityRef[]? runtimeIdentities = null,
            string? suggestedFix = null,
            string? subjectName = null,
            DiagnosticPayloadEntry[]? additionalPayloadEntries = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Authoring validation issues must provide a stable code.", nameof(code));

            if (severity == default)
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Authoring validation issues must provide a defined severity.");

            if (!IsDefinedCategory(category))
                throw new ArgumentOutOfRangeException(nameof(category), category, "Authoring validation issues must provide a defined category.");

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Authoring validation issues must provide a message.", nameof(message));

            if (suggestedFix != null && string.IsNullOrWhiteSpace(suggestedFix))
                throw new ArgumentException("Authoring validation issue suggested fixes must be null or non-empty.", nameof(suggestedFix));

            if (subjectName != null && string.IsNullOrWhiteSpace(subjectName))
                throw new ArgumentException("Authoring validation issue subject names must be null or non-empty.", nameof(subjectName));

            Code = code;
            Severity = severity;
            Category = category;
            OwnerModule = ownerModule;
            Message = message;
            SourceLocation = sourceLocation;
            SecondarySourceLocation = secondarySourceLocation;
            BaseSourceLocation = baseSourceLocation;
            this.runtimeIdentities = CloneRuntimeIdentities(runtimeIdentities);
            SuggestedFix = suggestedFix;
            SubjectName = subjectName;
            this.additionalPayloadEntries = ClonePayloadEntries(additionalPayloadEntries);
        }

        public string Code { get; }

        public ValidationSeverity Severity { get; }

        public ValidationIssueCategory Category { get; }

        public ModuleId OwnerModule { get; }

        public string Message { get; }

        public AuthoringUnitySourceLocation? SourceLocation { get; }

        public AuthoringUnitySourceLocation? SecondarySourceLocation { get; }

        public AuthoringUnitySourceLocation? BaseSourceLocation { get; }

        public ReadOnlySpan<RuntimeIdentityRef> RuntimeIdentities => runtimeIdentities;

        public string? SuggestedFix { get; }

        public string? SubjectName { get; }

        public ReadOnlySpan<DiagnosticPayloadEntry> AdditionalPayloadEntries => additionalPayloadEntries;

        public KernelDiagnostic ToKernelDiagnostic(
            DiagnosticDomain domain = DiagnosticDomain.Validation,
            DiagnosticFailureBoundary failureBoundary = DiagnosticFailureBoundary.Build,
            string phase = "AuthoringValidation")
        {
            List<RuntimeIdentityRef> runtimeIdentityList = new List<RuntimeIdentityRef>(runtimeIdentities.Length + 1);
            if (OwnerModule.Value != 0)
                runtimeIdentityList.Add(new RuntimeIdentityRef(RuntimeIdentityKind.Module, OwnerModule.Value));

            for (int index = 0; index < runtimeIdentities.Length; index++)
                runtimeIdentityList.Add(runtimeIdentities[index]);

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(8)
            {
                new DiagnosticPayloadEntry("AuthoringCode", DiagnosticPayloadValue.FromString(Code)),
                new DiagnosticPayloadEntry("AuthoringCategory", DiagnosticPayloadValue.FromString(Category.ToString())),
            };

            if (OwnerModule.Value != 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("AuthoringOwnerModuleId", DiagnosticPayloadValue.FromInt32(OwnerModule.Value)));

            if (SubjectName != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("AuthoringSubjectName", DiagnosticPayloadValue.FromString(SubjectName)));

            if (SourceLocation.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("AuthoringSourceLocation", DiagnosticPayloadValue.FromString(SourceLocation.Value.ToString())));

            if (SecondarySourceLocation.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("AuthoringSecondarySourceLocation", DiagnosticPayloadValue.FromString(SecondarySourceLocation.Value.ToString())));

            if (BaseSourceLocation.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("AuthoringBaseSourceLocation", DiagnosticPayloadValue.FromString(BaseSourceLocation.Value.ToString())));

            if (SuggestedFix != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("SuggestedFix", DiagnosticPayloadValue.FromString(SuggestedFix)));

            for (int index = 0; index < additionalPayloadEntries.Length; index++)
                payloadEntries.Add(additionalPayloadEntries[index]);

            DiagnosticContext context = new DiagnosticContext(
                runtimeIdentityList.Count == 0 ? null : runtimeIdentityList.ToArray(),
                OwnerModule.Value == 0 ? default : new ModuleIdentityRef(OwnerModule.Value),
                phase: phase);

            return new KernelDiagnostic(
                new DiagnosticCode(Code),
                ToDiagnosticSeverity(Severity),
                domain,
                failureBoundary,
                Message,
                context,
                new DiagnosticPayload(payloadEntries));
        }

        public override string ToString()
        {
            return Code + ": " + Message;
        }

        static bool IsDefinedCategory(ValidationIssueCategory category)
        {
            return category == ValidationIssueCategory.LocalNode
                || category == ValidationIssueCategory.LocalEdge
                || category == ValidationIssueCategory.CrossNode
                || category == ValidationIssueCategory.CrossModule
                || category == ValidationIssueCategory.ProfileAware
                || category == ValidationIssueCategory.Projection
                || category == ValidationIssueCategory.LegacyBoundary;
        }

        static DiagnosticSeverity ToDiagnosticSeverity(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Info:
                    return DiagnosticSeverity.Info;
                case ValidationSeverity.Warning:
                    return DiagnosticSeverity.Warning;
                case ValidationSeverity.Error:
                    return DiagnosticSeverity.Error;
                case ValidationSeverity.Fatal:
                    return DiagnosticSeverity.Fatal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, "Authoring validation issues must use a defined severity.");
            }
        }

        static RuntimeIdentityRef[] CloneRuntimeIdentities(RuntimeIdentityRef[]? runtimeIdentities)
        {
            if (runtimeIdentities == null || runtimeIdentities.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] snapshot = new RuntimeIdentityRef[runtimeIdentities.Length];
            Array.Copy(runtimeIdentities, snapshot, runtimeIdentities.Length);
            return snapshot;
        }

        static DiagnosticPayloadEntry[] ClonePayloadEntries(DiagnosticPayloadEntry[]? payloadEntries)
        {
            if (payloadEntries == null || payloadEntries.Length == 0)
                return Array.Empty<DiagnosticPayloadEntry>();

            DiagnosticPayloadEntry[] snapshot = new DiagnosticPayloadEntry[payloadEntries.Length];
            Array.Copy(payloadEntries, snapshot, payloadEntries.Length);
            return snapshot;
        }
    }

    public sealed class AuthoringValidationReport
    {
        readonly AuthoringValidationIssue[] issues;

        public AuthoringValidationReport(AuthoringValidationIssue[] issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            for (int index = 0; index < issues.Length; index++)
            {
                if (issues[index] == null)
                    throw new ArgumentException("Authoring validation reports must not contain null issues.", nameof(issues));
            }

            this.issues = issues;
            Array.Sort(this.issues, CompareIssues);
        }

        public IReadOnlyList<AuthoringValidationIssue> Issues => issues;

        public bool IsValid => issues.Length == 0;

        static int CompareIssues(AuthoringValidationIssue left, AuthoringValidationIssue right)
        {
            int result = StringComparer.Ordinal.Compare(left.Code, right.Code);
            if (result != 0)
                return result;

            result = left.Severity.CompareTo(right.Severity);
            if (result != 0)
                return result;

            result = left.Category.CompareTo(right.Category);
            if (result != 0)
                return result;

            result = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (result != 0)
                return result;

            result = CompareRuntimeIdentities(left.RuntimeIdentities, right.RuntimeIdentities);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.SubjectName ?? string.Empty, right.SubjectName ?? string.Empty);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.Message, right.Message);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(FormatSourceLocation(left.SourceLocation), FormatSourceLocation(right.SourceLocation));
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(FormatSourceLocation(left.SecondarySourceLocation), FormatSourceLocation(right.SecondarySourceLocation));
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(FormatSourceLocation(left.BaseSourceLocation), FormatSourceLocation(right.BaseSourceLocation));
        }

        static int CompareRuntimeIdentities(ReadOnlySpan<RuntimeIdentityRef> left, ReadOnlySpan<RuntimeIdentityRef> right)
        {
            int count = Math.Min(left.Length, right.Length);
            for (int index = 0; index < count; index++)
            {
                int result = left[index].Kind.CompareTo(right[index].Kind);
                if (result != 0)
                    return result;

                result = left[index].Value.CompareTo(right[index].Value);
                if (result != 0)
                    return result;

                result = left[index].Generation.CompareTo(right[index].Generation);
                if (result != 0)
                    return result;
            }

            return left.Length.CompareTo(right.Length);
        }

        static string FormatSourceLocation(AuthoringUnitySourceLocation? sourceLocation)
        {
            return sourceLocation.HasValue ? sourceLocation.Value.ToString() : string.Empty;
        }
    }

    public static class AuthoringValidationDiagnostics
    {
        public static KernelDiagnostic[] ToKernelDiagnostics(AuthoringValidationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            return ToKernelDiagnostics(report.Issues);
        }

        public static KernelDiagnostic[] ToKernelDiagnostics(IReadOnlyList<AuthoringValidationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            KernelDiagnostic[] diagnostics = new KernelDiagnostic[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                AuthoringValidationIssue issue = issues[index] ?? throw new ArgumentException("Authoring validation issues must not contain null items.", nameof(issues));
                diagnostics[index] = issue.ToKernelDiagnostic();
            }

            return diagnostics;
        }

        public static void Emit(KernelDiagnosticService service, AuthoringValidationReport report)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            service.ReportBatch(ToKernelDiagnostics(report));
        }

        public static void Emit(KernelDiagnosticService service, IReadOnlyList<AuthoringValidationIssue> issues)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            service.ReportBatch(ToKernelDiagnostics(issues));
        }
    }
}