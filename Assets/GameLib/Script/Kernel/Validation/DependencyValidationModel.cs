#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;

namespace Game.Kernel.Validation
{
    public enum ValidationResultStatus
    {
        Passed = 10,
        PassedWithWarnings = 20,
        Failed = 30,
        Fatal = 40,
    }

    public enum ValidationSeverity
    {
        Info = 10,
        Warning = 20,
        Error = 30,
        Fatal = 40,
    }

    public enum ValidationPhase
    {
        Build = 10,
        Generate = 20,
        Boot = 30,
        Acquire = 40,
        Runtime = 50,
        Save = 60,
        EditorOnly = 70,
    }

    public enum ValidationIssueCategory
    {
        LocalNode = 10,
        LocalEdge = 20,
        CrossNode = 30,
        CrossModule = 40,
        ProfileAware = 50,
        Projection = 60,
        LegacyBoundary = 70,
    }

    public sealed class DependencyValidationIssue
    {
        readonly DiagnosticPayloadEntry[] additionalPayloadEntries;

        public DependencyValidationIssue(
            string code,
            ValidationSeverity severity,
            ValidationIssueCategory category,
            DependencyNodeIR from,
            DependencyNodeIR? to,
            ValidationPhase phase,
            ModuleId ownerModule,
            SourceLocationId source,
            string profile,
            string message,
            string? suggestedFix = null,
            DiagnosticPayloadEntry[]? additionalPayloadEntries = null,
            bool allowMissingSourceLocation = false)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Validation issue codes must not be blank.", nameof(code));

            if (!IsDefinedSeverity(severity))
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Validation issue severity must be a defined non-default value.");

            if (!IsDefinedCategory(category))
                throw new ArgumentOutOfRangeException(nameof(category), category, "Validation issue category must be a defined non-default value.");

            if (!IsSpecifiedNode(from))
                throw new ArgumentException("Validation issues must provide a valid source node.", nameof(from));

            if (!IsDefinedPhase(phase))
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Validation issue phase must be a defined non-default value.");

            if (ownerModule.Value == 0)
                throw new ArgumentException("Validation issues must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0 && !allowMissingSourceLocation)
                throw new ArgumentException("Validation issues must provide a non-zero source location identity.", nameof(source));

            if (string.IsNullOrWhiteSpace(profile))
                throw new ArgumentException("Validation issues must provide a selected profile.", nameof(profile));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Validation issues must provide a non-empty message.", nameof(message));

            if (suggestedFix != null && string.IsNullOrWhiteSpace(suggestedFix))
                throw new ArgumentException("Validation issue suggested fixes must be null or non-empty.", nameof(suggestedFix));

            if (to.HasValue && !IsSpecifiedNode(to.Value))
                throw new ArgumentException("Validation issue target nodes must be fully specified when present.", nameof(to));

            this.additionalPayloadEntries = ClonePayloadEntries(additionalPayloadEntries);

            Code = code;
            Severity = severity;
            Category = category;
            From = from;
            To = to;
            Phase = phase;
            OwnerModule = ownerModule;
            Source = source;
            Profile = profile;
            Message = message;
            SuggestedFix = suggestedFix;
        }

        public DependencyValidationIssue(
            string code,
            ValidationSeverity severity,
            ValidationIssueCategory category,
            DependencyNodeIR from,
            DependencyNodeIR? to,
            DependencyPhase phase,
            ModuleId ownerModule,
            SourceLocationId source,
            string profile,
            string message,
            string? suggestedFix = null,
            DiagnosticPayloadEntry[]? additionalPayloadEntries = null)
            : this(code, severity, category, from, to, ValidationPhaseConversion.FromDependencyPhase(phase), ownerModule, source, profile, message, suggestedFix, additionalPayloadEntries)
        {
        }

        public string Code { get; }

        public ValidationSeverity Severity { get; }

        public ValidationIssueCategory Category { get; }

        public DependencyNodeIR From { get; }

        public DependencyNodeIR? To { get; }

        public ValidationPhase Phase { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public string Profile { get; }

        public string Message { get; }

        public string? SuggestedFix { get; }

        public ReadOnlySpan<DiagnosticPayloadEntry> AdditionalPayloadEntries => additionalPayloadEntries;

        public KernelDiagnostic ToKernelDiagnostic(DiagnosticFailureBoundary failureBoundary = DiagnosticFailureBoundary.Build)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(To.HasValue ? 2 : 1)
            {
                ToRuntimeIdentityRef(From),
            };

            if (To.HasValue)
                runtimeIdentities.Add(ToRuntimeIdentityRef(To.Value));

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>
            {
                new DiagnosticPayloadEntry("Profile", DiagnosticPayloadValue.FromString(Profile)),
                new DiagnosticPayloadEntry("ValidationPhase", DiagnosticPayloadValue.FromString(Phase.ToString())),
                new DiagnosticPayloadEntry("ValidationCategory", DiagnosticPayloadValue.FromString(Category.ToString())),
                new DiagnosticPayloadEntry("FromNode", DiagnosticPayloadValue.FromString(FormatDependencyNode(From))),
            };

            if (To.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("ToNode", DiagnosticPayloadValue.FromString(FormatDependencyNode(To.Value))));

            if (SuggestedFix != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("SuggestedFix", DiagnosticPayloadValue.FromString(SuggestedFix)));

            for (int index = 0; index < additionalPayloadEntries.Length; index++)
                payloadEntries.Add(additionalPayloadEntries[index]);

            DiagnosticContext context = new DiagnosticContext(
                runtimeIdentities.ToArray(),
                ownerModule: new ModuleIdentityRef(OwnerModule.Value),
                source: new SourceLocationRef(Source.Value),
                phase: Phase.ToString());

            return new KernelDiagnostic(
                code: new DiagnosticCode(Code),
                severity: ToDiagnosticSeverity(Severity),
                domain: Category == ValidationIssueCategory.LegacyBoundary ? DiagnosticDomain.LegacyCompat : DiagnosticDomain.Validation,
                failureBoundary: failureBoundary,
                message: Message,
                context: context,
                payload: new DiagnosticPayload(payloadEntries));
        }

        static DiagnosticPayloadEntry[] ClonePayloadEntries(DiagnosticPayloadEntry[]? entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<DiagnosticPayloadEntry>();

            DiagnosticPayloadEntry[] clone = new DiagnosticPayloadEntry[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(entries[index].Key))
                    throw new ArgumentException("Validation issue additional payload entries must provide a stable key.", nameof(entries));

                clone[index] = entries[index];
            }

            return clone;
        }

        static bool IsSpecifiedNode(DependencyNodeIR node)
        {
            return node.Kind != DependencyNodeKind.Unknown;
        }

        static bool IsDefinedSeverity(ValidationSeverity severity)
        {
            return severity == ValidationSeverity.Info
                || severity == ValidationSeverity.Warning
                || severity == ValidationSeverity.Error
                || severity == ValidationSeverity.Fatal;
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

        static bool IsDefinedPhase(ValidationPhase phase)
        {
            return phase == ValidationPhase.Build
                || phase == ValidationPhase.Generate
                || phase == ValidationPhase.Boot
                || phase == ValidationPhase.Acquire
                || phase == ValidationPhase.Runtime
                || phase == ValidationPhase.Save
                || phase == ValidationPhase.EditorOnly;
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
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, "Validation issue severity must be defined before diagnostic conversion.");
            }
        }

        static RuntimeIdentityRef ToRuntimeIdentityRef(DependencyNodeIR node)
        {
            switch (node.Kind)
            {
                case DependencyNodeKind.Module:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.Module, node.ModuleId.Value);
                case DependencyNodeKind.Service:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.Service, node.ServiceId.Value);
                case DependencyNodeKind.Scope:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, node.ScopePlanId.Value);
                case DependencyNodeKind.Command:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, node.CommandTypeId.Value);
                case DependencyNodeKind.ValueKey:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, node.ValueKeyId.Value);
                case DependencyNodeKind.LifecycleStep:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, node.LifecycleStepId.Value);
                case DependencyNodeKind.RuntimeQuery:
                    return new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, node.RuntimeQueryId.Value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(node), node.Kind, "Validation issue nodes must be defined before diagnostic conversion.");
            }
        }

        static string FormatDependencyNode(DependencyNodeIR node)
        {
            return node.ToString();
        }
    }

    public sealed class DependencyValidationSummary
    {
        DependencyValidationSummary(int infoCount, int warningCount, int errorCount, int fatalCount)
        {
            InfoCount = infoCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            FatalCount = fatalCount;
        }

        public int InfoCount { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public int FatalCount { get; }

        public static DependencyValidationSummary FromIssues(IReadOnlyList<DependencyValidationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            int infoCount = 0;
            int warningCount = 0;
            int errorCount = 0;
            int fatalCount = 0;

            for (int index = 0; index < issues.Count; index++)
            {
                DependencyValidationIssue issue = issues[index] ?? throw new ArgumentException("Validation issue collections must not contain null items.", nameof(issues));
                switch (issue.Severity)
                {
                    case ValidationSeverity.Info:
                        infoCount++;
                        break;
                    case ValidationSeverity.Warning:
                        warningCount++;
                        break;
                    case ValidationSeverity.Error:
                        errorCount++;
                        break;
                    case ValidationSeverity.Fatal:
                        fatalCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(issues), issue.Severity, "Validation issue collections must contain only defined severities.");
                }
            }

            return new DependencyValidationSummary(infoCount, warningCount, errorCount, fatalCount);
        }
    }

    public sealed class DependencyValidationReport
    {
        readonly ReadOnlyCollection<DependencyValidationIssue> issues;

        public DependencyValidationReport(string selectedProfile, IReadOnlyList<DependencyValidationIssue>? issues)
        {
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Validation reports must provide a selected profile.", nameof(selectedProfile));

            DependencyValidationIssue[] snapshot = issues == null || issues.Count == 0
                ? Array.Empty<DependencyValidationIssue>()
                : CloneIssues(selectedProfile, issues);

            SelectedProfile = selectedProfile;
            this.issues = Array.AsReadOnly(snapshot);
            Summary = DependencyValidationSummary.FromIssues(snapshot);
            Status = DeriveStatus(Summary);
        }

        public ValidationResultStatus Status { get; }

        public string SelectedProfile { get; }

        public IReadOnlyList<DependencyValidationIssue> Issues => issues;

        public DependencyValidationSummary Summary { get; }

        static DependencyValidationIssue[] CloneIssues(string selectedProfile, IReadOnlyList<DependencyValidationIssue> issues)
        {
            DependencyValidationIssue[] snapshot = new DependencyValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                DependencyValidationIssue issue = issues[index] ?? throw new ArgumentException("Validation report issue collections must not contain null items.", nameof(issues));
                if (!StringComparer.Ordinal.Equals(issue.Profile, selectedProfile))
                    throw new ArgumentException("Validation report issues must match the report selected profile.", nameof(issues));

                snapshot[index] = issue;
            }

            return snapshot;
        }

        static ValidationResultStatus DeriveStatus(DependencyValidationSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (summary.FatalCount > 0)
                return ValidationResultStatus.Fatal;

            if (summary.ErrorCount > 0)
                return ValidationResultStatus.Failed;

            if (summary.WarningCount > 0)
                return ValidationResultStatus.PassedWithWarnings;

            return ValidationResultStatus.Passed;
        }
    }

    public static class ValidationPhaseConversion
    {
        public static ValidationPhase FromDependencyPhase(DependencyPhase phase)
        {
            switch (phase)
            {
                case DependencyPhase.Build:
                    return ValidationPhase.Build;
                case DependencyPhase.Generate:
                    return ValidationPhase.Generate;
                case DependencyPhase.Boot:
                    return ValidationPhase.Boot;
                case DependencyPhase.Acquire:
                    return ValidationPhase.Acquire;
                case DependencyPhase.Runtime:
                    return ValidationPhase.Runtime;
                case DependencyPhase.Save:
                    return ValidationPhase.Save;
                case DependencyPhase.EditorOnly:
                    return ValidationPhase.EditorOnly;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Dependency validation phases must be defined non-default values.");
            }
        }

        public static DependencyPhase ToDependencyPhase(ValidationPhase phase)
        {
            switch (phase)
            {
                case ValidationPhase.Build:
                    return DependencyPhase.Build;
                case ValidationPhase.Generate:
                    return DependencyPhase.Generate;
                case ValidationPhase.Boot:
                    return DependencyPhase.Boot;
                case ValidationPhase.Acquire:
                    return DependencyPhase.Acquire;
                case ValidationPhase.Runtime:
                    return DependencyPhase.Runtime;
                case ValidationPhase.Save:
                    return DependencyPhase.Save;
                case ValidationPhase.EditorOnly:
                    return DependencyPhase.EditorOnly;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Validation phases must be defined non-default values.");
            }
        }
    }
}