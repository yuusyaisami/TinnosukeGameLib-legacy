#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;

namespace Game.Kernel.Validation
{
    public enum ProjectionArtifactKind
    {
        Unknown = 0,
        ServiceGraph = 10,
        ScopeGraph = 20,
        LifecyclePlan = 30,
        CommandCatalog = 40,
        ValueSchema = 50,
        RuntimeQuery = 60,
        KernelDebugMap = 70,
        GenerationReport = 80,
        ValidationReport = 90,
    }

    public sealed class ProjectionValidationInput
    {
        readonly ProjectionMappingIR[] mappings;
        readonly RuntimeIdentityRef[] debugMapCoverage;
        readonly ProjectionArtifactKind[] selectedArtifacts;

        public ProjectionValidationInput(
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            KernelIR sourceKernelIR,
            ProjectionArtifactKind[]? selectedArtifacts,
            ProjectionMappingIR[]? mappings,
            RuntimeIdentityRef[]? debugMapCoverage)
        {
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Projection validation inputs must provide a selected profile.", nameof(selectedProfile));

            if (selectedProfileMask == KernelProfileMask.None)
                throw new ArgumentException("Projection validation inputs must provide a non-empty selected profile mask.", nameof(selectedProfileMask));

            SourceKernelIR = sourceKernelIR ?? throw new ArgumentNullException(nameof(sourceKernelIR));
            SelectedProfile = selectedProfile;
            SelectedProfileMask = selectedProfileMask;
            this.selectedArtifacts = CloneArtifacts(selectedArtifacts);
            this.mappings = CloneMappings(mappings);
            this.debugMapCoverage = CloneCoverage(debugMapCoverage);
        }

        public string SelectedProfile { get; }

        public KernelProfileMask SelectedProfileMask { get; }

        public KernelIR SourceKernelIR { get; }

        public ReadOnlySpan<ProjectionArtifactKind> SelectedArtifacts => selectedArtifacts;

        public ReadOnlySpan<ProjectionMappingIR> Mappings => mappings;

        public ReadOnlySpan<RuntimeIdentityRef> DebugMapCoverage => debugMapCoverage;

        static ProjectionMappingIR[] CloneMappings(ProjectionMappingIR[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ProjectionMappingIR>();

            ProjectionMappingIR[] clone = new ProjectionMappingIR[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] == null)
                    throw new ArgumentException("Projection validation mappings must not contain null entries.", nameof(source));

                clone[index] = source[index];
            }

            return clone;
        }

        static ProjectionArtifactKind[] CloneArtifacts(ProjectionArtifactKind[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ProjectionArtifactKind>();

            ProjectionArtifactKind[] clone = new ProjectionArtifactKind[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] == ProjectionArtifactKind.Unknown)
                    throw new ArgumentException("Projection validation artifact selections must not include unknown kinds.", nameof(source));

                clone[index] = source[index];
            }

            return clone;
        }

        static RuntimeIdentityRef[] CloneCoverage(RuntimeIdentityRef[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] clone = new RuntimeIdentityRef[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clone[index] = source[index];
            }

            return clone;
        }
    }

    public sealed class ProjectionMappingIR
    {
        public ProjectionMappingIR(
            RuntimeIdentityRef sourceIdentity,
            RuntimeIdentityRef projectedIdentity,
            ModuleId ownerModule,
            SourceLocationId source,
            bool hasProvenance = true)
        {
            if (sourceIdentity.Kind == RuntimeIdentityKind.None || sourceIdentity.Value == 0)
                throw new ArgumentException("Projection mappings must provide a source identity.", nameof(sourceIdentity));

            if (projectedIdentity.Kind == RuntimeIdentityKind.None || projectedIdentity.Value == 0)
                throw new ArgumentException("Projection mappings must provide a projected identity.", nameof(projectedIdentity));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Projection mappings must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Projection mappings must provide a non-zero source location identity.", nameof(source));

            SourceIdentity = sourceIdentity;
            ProjectedIdentity = projectedIdentity;
            OwnerModule = ownerModule;
            Source = source;
            HasProvenance = hasProvenance;
        }

        public RuntimeIdentityRef SourceIdentity { get; }

        public RuntimeIdentityRef ProjectedIdentity { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public bool HasProvenance { get; }
    }

    public sealed class ProjectionValidationSummary
    {
        ProjectionValidationSummary(int infoCount, int warningCount, int errorCount, int fatalCount)
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

        public static ProjectionValidationSummary FromIssues(IReadOnlyList<DependencyValidationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            int infoCount = 0;
            int warningCount = 0;
            int errorCount = 0;
            int fatalCount = 0;

            for (int index = 0; index < issues.Count; index++)
            {
                DependencyValidationIssue issue = issues[index] ?? throw new ArgumentException("Projection validation issue collections must not contain null items.", nameof(issues));
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
                        throw new ArgumentOutOfRangeException(nameof(issues), issue.Severity, "Projection validation issue collections must contain only defined severities.");
                }
            }

            return new ProjectionValidationSummary(infoCount, warningCount, errorCount, fatalCount);
        }
    }

    public sealed class ProjectionValidationReport
    {
        readonly ReadOnlyCollection<DependencyValidationIssue> issues;

        public ProjectionValidationReport(string selectedProfile, IReadOnlyList<DependencyValidationIssue>? issues)
        {
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Projection validation reports must provide a selected profile.", nameof(selectedProfile));

            DependencyValidationIssue[] snapshot = issues == null || issues.Count == 0
                ? Array.Empty<DependencyValidationIssue>()
                : CloneIssues(selectedProfile, issues);

            SelectedProfile = selectedProfile;
            this.issues = Array.AsReadOnly(snapshot);
            Summary = ProjectionValidationSummary.FromIssues(snapshot);
            Status = DeriveStatus(Summary);
        }

        public ValidationResultStatus Status { get; }

        public string SelectedProfile { get; }

        public IReadOnlyList<DependencyValidationIssue> Issues => issues;

        public ProjectionValidationSummary Summary { get; }

        static DependencyValidationIssue[] CloneIssues(string selectedProfile, IReadOnlyList<DependencyValidationIssue> issues)
        {
            DependencyValidationIssue[] snapshot = new DependencyValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                DependencyValidationIssue issue = issues[index] ?? throw new ArgumentException("Projection validation report issue collections must not contain null items.", nameof(issues));
                if (!StringComparer.Ordinal.Equals(issue.Profile, selectedProfile))
                    throw new ArgumentException("Projection validation report issues must match the report selected profile.", nameof(issues));

                snapshot[index] = issue;
            }

            return snapshot;
        }

        static ValidationResultStatus DeriveStatus(ProjectionValidationSummary summary)
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
}