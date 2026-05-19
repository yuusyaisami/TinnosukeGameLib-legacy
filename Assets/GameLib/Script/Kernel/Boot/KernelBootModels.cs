#nullable enable
using System;
using Game.Kernel.Abstractions;

namespace Game.Kernel.Boot
{
    public enum KernelProfileKind
    {
        Development = 10,
        Release = 20,
        Test = 30,
    }

    public enum KernelProfileStaleArtifactDisposition
    {
        ErrorAndBootBlock = 10,
        FatalAndBootBlock = 20,
    }

    public enum KernelProfileMissingDebugMapDisposition
    {
        Error = 10,
        ErrorIfFatalDiagnosticsCannotBeProduced = 20,
        Fatal = 30,
    }

    public enum KernelProfileLegacyBridgeDisposition
    {
        WarningIfExplicitlyAllowed = 10,
        ForbiddenUnlessLegacyCompatSpecAllows = 20,
        ErrorOrFatal = 30,
    }

    public enum KernelProfileDiagnosticsDetail
    {
        Full = 10,
        MinimalRequired = 20,
        FullCaptured = 30,
    }

    public enum KernelProfileRuntimeAssertionsMode
    {
        Enabled = 10,
        Minimal = 20,
    }

    public enum KernelProfileValidationStrictness
    {
        Strict = 10,
        MaximumPractical = 20,
    }

    public enum KernelProfileGeneratedMismatchDisposition
    {
        BootBlock = 10,
    }

    public enum KernelProfileFallbackDisposition
    {
        Forbidden = 10,
        DevOnlyBridgeAllowed = 20,
    }

    public enum BootDiagnosticsFailureBoundaryBehavior
    {
        ReportAndBlock = 10,
        BlockWithoutDiagnostics = 20,
    }

    public enum BootDiagnosticsInspectionMode
    {
        Disabled = 10,
        Enabled = 20,
    }

    public enum BootDiagnosticsDeterminismMode
    {
        Disabled = 10,
        Enabled = 20,
    }

    public sealed class KernelProfilePolicy : IEquatable<KernelProfilePolicy>
    {
        private KernelProfilePolicy(
            KernelProfileKind kind,
            KernelProfileStaleArtifactDisposition staleArtifactDisposition,
            KernelProfileMissingDebugMapDisposition missingDebugMapDisposition,
            KernelProfileLegacyBridgeDisposition legacyBridgeDisposition,
            KernelProfileDiagnosticsDetail diagnosticsDetail,
            KernelProfileRuntimeAssertionsMode runtimeAssertionsMode,
            KernelProfileValidationStrictness validationStrictness,
            KernelProfileGeneratedMismatchDisposition generatedMismatchDisposition,
            KernelProfileFallbackDisposition fallbackDisposition)
        {
            if (kind == default)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "KernelProfilePolicy must target a valid kernel profile kind.");

            Kind = kind;
            StaleArtifactDisposition = staleArtifactDisposition;
            MissingDebugMapDisposition = missingDebugMapDisposition;
            LegacyBridgeDisposition = legacyBridgeDisposition;
            DiagnosticsDetail = diagnosticsDetail;
            RuntimeAssertionsMode = runtimeAssertionsMode;
            ValidationStrictness = validationStrictness;
            GeneratedMismatchDisposition = generatedMismatchDisposition;
            FallbackDisposition = fallbackDisposition;
        }

        public KernelProfileKind Kind { get; }

        public KernelProfileStaleArtifactDisposition StaleArtifactDisposition { get; }

        public KernelProfileMissingDebugMapDisposition MissingDebugMapDisposition { get; }

        public KernelProfileLegacyBridgeDisposition LegacyBridgeDisposition { get; }

        public KernelProfileDiagnosticsDetail DiagnosticsDetail { get; }

        public KernelProfileRuntimeAssertionsMode RuntimeAssertionsMode { get; }

        public KernelProfileValidationStrictness ValidationStrictness { get; }

        public KernelProfileGeneratedMismatchDisposition GeneratedMismatchDisposition { get; }

        public KernelProfileFallbackDisposition FallbackDisposition { get; }

        public static KernelProfilePolicy ForKind(KernelProfileKind kind)
        {
            return kind switch
            {
                KernelProfileKind.Development => new KernelProfilePolicy(
                    kind,
                    KernelProfileStaleArtifactDisposition.ErrorAndBootBlock,
                    KernelProfileMissingDebugMapDisposition.Error,
                    KernelProfileLegacyBridgeDisposition.WarningIfExplicitlyAllowed,
                    KernelProfileDiagnosticsDetail.Full,
                    KernelProfileRuntimeAssertionsMode.Enabled,
                    KernelProfileValidationStrictness.Strict,
                    KernelProfileGeneratedMismatchDisposition.BootBlock,
                    KernelProfileFallbackDisposition.DevOnlyBridgeAllowed),
                KernelProfileKind.Release => new KernelProfilePolicy(
                    kind,
                    KernelProfileStaleArtifactDisposition.FatalAndBootBlock,
                    KernelProfileMissingDebugMapDisposition.ErrorIfFatalDiagnosticsCannotBeProduced,
                    KernelProfileLegacyBridgeDisposition.ForbiddenUnlessLegacyCompatSpecAllows,
                    KernelProfileDiagnosticsDetail.MinimalRequired,
                    KernelProfileRuntimeAssertionsMode.Minimal,
                    KernelProfileValidationStrictness.Strict,
                    KernelProfileGeneratedMismatchDisposition.BootBlock,
                    KernelProfileFallbackDisposition.Forbidden),
                KernelProfileKind.Test => new KernelProfilePolicy(
                    kind,
                    KernelProfileStaleArtifactDisposition.FatalAndBootBlock,
                    KernelProfileMissingDebugMapDisposition.Fatal,
                    KernelProfileLegacyBridgeDisposition.ErrorOrFatal,
                    KernelProfileDiagnosticsDetail.FullCaptured,
                    KernelProfileRuntimeAssertionsMode.Enabled,
                    KernelProfileValidationStrictness.MaximumPractical,
                    KernelProfileGeneratedMismatchDisposition.BootBlock,
                    KernelProfileFallbackDisposition.Forbidden),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported kernel profile kind."),
            };
        }

        public bool Equals(KernelProfilePolicy? other)
        {
            return other != null
                && Kind == other.Kind
                && StaleArtifactDisposition == other.StaleArtifactDisposition
                && MissingDebugMapDisposition == other.MissingDebugMapDisposition
                && LegacyBridgeDisposition == other.LegacyBridgeDisposition
                && DiagnosticsDetail == other.DiagnosticsDetail
                && RuntimeAssertionsMode == other.RuntimeAssertionsMode
                && ValidationStrictness == other.ValidationStrictness
                && GeneratedMismatchDisposition == other.GeneratedMismatchDisposition
                && FallbackDisposition == other.FallbackDisposition;
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelProfilePolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (int)StaleArtifactDisposition;
                hash = (hash * 397) ^ (int)MissingDebugMapDisposition;
                hash = (hash * 397) ^ (int)LegacyBridgeDisposition;
                hash = (hash * 397) ^ (int)DiagnosticsDetail;
                hash = (hash * 397) ^ (int)RuntimeAssertionsMode;
                hash = (hash * 397) ^ (int)ValidationStrictness;
                hash = (hash * 397) ^ (int)GeneratedMismatchDisposition;
                hash = (hash * 397) ^ (int)FallbackDisposition;
                return hash;
            }
        }

        public override string ToString()
        {
            return "KernelProfilePolicy(Kind=" + Kind + ", StaleArtifactDisposition=" + StaleArtifactDisposition + ", MissingDebugMapDisposition=" + MissingDebugMapDisposition + ", LegacyBridgeDisposition=" + LegacyBridgeDisposition + ", DiagnosticsDetail=" + DiagnosticsDetail + ", RuntimeAssertionsMode=" + RuntimeAssertionsMode + ", ValidationStrictness=" + ValidationStrictness + ", GeneratedMismatchDisposition=" + GeneratedMismatchDisposition + ", FallbackDisposition=" + FallbackDisposition + ")";
        }

        public static bool operator ==(KernelProfilePolicy? left, KernelProfilePolicy? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KernelProfilePolicy? left, KernelProfilePolicy? right)
        {
            return !Equals(left, right);
        }
    }

    public sealed class BootDiagnosticsPolicy : IEquatable<BootDiagnosticsPolicy>
    {
        private BootDiagnosticsPolicy(
            KernelProfileKind kind,
            BootDiagnosticsFailureBoundaryBehavior failureBoundaryBehavior,
            KernelProfileDiagnosticsDetail diagnosticsDetail,
            BootDiagnosticsInspectionMode editorInspectionMode,
            BootDiagnosticsDeterminismMode testDeterminismMode)
        {
            if (kind == default)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "BootDiagnosticsPolicy must target a valid kernel profile kind.");

            Kind = kind;
            FailureBoundaryBehavior = failureBoundaryBehavior;
            DiagnosticsDetail = diagnosticsDetail;
            EditorInspectionMode = editorInspectionMode;
            TestDeterminismMode = testDeterminismMode;
        }

        public KernelProfileKind Kind { get; }

        public BootDiagnosticsFailureBoundaryBehavior FailureBoundaryBehavior { get; }

        public KernelProfileDiagnosticsDetail DiagnosticsDetail { get; }

        public BootDiagnosticsInspectionMode EditorInspectionMode { get; }

        public BootDiagnosticsDeterminismMode TestDeterminismMode { get; }

        public static BootDiagnosticsPolicy ForKind(KernelProfileKind kind)
        {
            return kind switch
            {
                KernelProfileKind.Development => new BootDiagnosticsPolicy(
                    kind,
                    BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                    KernelProfileDiagnosticsDetail.Full,
                    BootDiagnosticsInspectionMode.Enabled,
                    BootDiagnosticsDeterminismMode.Disabled),
                KernelProfileKind.Release => new BootDiagnosticsPolicy(
                    kind,
                    BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                    KernelProfileDiagnosticsDetail.MinimalRequired,
                    BootDiagnosticsInspectionMode.Disabled,
                    BootDiagnosticsDeterminismMode.Disabled),
                KernelProfileKind.Test => new BootDiagnosticsPolicy(
                    kind,
                    BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                    KernelProfileDiagnosticsDetail.FullCaptured,
                    BootDiagnosticsInspectionMode.Enabled,
                    BootDiagnosticsDeterminismMode.Enabled),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported kernel profile kind."),
            };
        }

        public bool Equals(BootDiagnosticsPolicy? other)
        {
            return other != null
                && Kind == other.Kind
                && FailureBoundaryBehavior == other.FailureBoundaryBehavior
                && DiagnosticsDetail == other.DiagnosticsDetail
                && EditorInspectionMode == other.EditorInspectionMode
                && TestDeterminismMode == other.TestDeterminismMode;
        }

        public override bool Equals(object? obj)
        {
            return obj is BootDiagnosticsPolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (int)FailureBoundaryBehavior;
                hash = (hash * 397) ^ (int)DiagnosticsDetail;
                hash = (hash * 397) ^ (int)EditorInspectionMode;
                hash = (hash * 397) ^ (int)TestDeterminismMode;
                return hash;
            }
        }

        public override string ToString()
        {
            return "BootDiagnosticsPolicy(Kind=" + Kind + ", FailureBoundaryBehavior=" + FailureBoundaryBehavior + ", DiagnosticsDetail=" + DiagnosticsDetail + ", EditorInspectionMode=" + EditorInspectionMode + ", TestDeterminismMode=" + TestDeterminismMode + ")";
        }

        public static bool operator ==(BootDiagnosticsPolicy? left, BootDiagnosticsPolicy? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BootDiagnosticsPolicy? left, BootDiagnosticsPolicy? right)
        {
            return !Equals(left, right);
        }
    }

    public sealed class KernelProfile : IEquatable<KernelProfile>
    {
        public KernelProfile(KernelProfileId id, KernelProfileKind kind)
            : this(id, kind, KernelProfilePolicy.ForKind(kind))
        {
        }

        public KernelProfile(KernelProfileId id, KernelProfileKind kind, KernelProfilePolicy policy)
        {
            if (id.Value == 0)
                throw new ArgumentOutOfRangeException(nameof(id), id.Value, "KernelProfile must provide a non-zero profile identity.");

            Policy = policy ?? throw new ArgumentNullException(nameof(policy));

            if (Policy.Kind != kind)
                throw new ArgumentException("KernelProfilePolicy kind must match the profile kind.", nameof(policy));

            Id = id;
            Kind = kind;
        }

        public KernelProfileId Id { get; }

        public KernelProfileKind Kind { get; }

        public KernelProfilePolicy Policy { get; }

        public bool Equals(KernelProfile? other)
        {
            return other != null
                && Id == other.Id
                && Kind == other.Kind
                && Policy.Equals(other.Policy);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Policy.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "KernelProfile(Id=" + Id + ", Kind=" + Kind + ", Policy=" + Policy + ")";
        }

        public static bool operator ==(KernelProfile? left, KernelProfile? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KernelProfile? left, KernelProfile? right)
        {
            return !Equals(left, right);
        }
    }
}