#nullable enable

using System;

namespace Game.Kernel.IR
{
    public sealed class LegacyRemovalPolicy
    {
        public LegacyRemovalPolicy(
            ModuleId ownerModule,
            LegacyRemovalStatus status,
            KernelProfileMask allowedProfiles,
            string reason,
            string targetReplacement,
            string expirationCondition,
            string diagnosticsCode,
            string trackingIssueOrBlockingCondition)
        {
            if (ownerModule.Value == 0)
                throw new ArgumentException("Legacy removal policies must provide a non-zero owner module identity.", nameof(ownerModule));

            if (status == LegacyRemovalStatus.Unknown)
                throw new ArgumentOutOfRangeException(nameof(status), status, "Legacy removal policies must provide a defined removal status.");

            if (allowedProfiles == KernelProfileMask.None)
                throw new ArgumentException("Legacy removal policies must declare at least one allowed profile.", nameof(allowedProfiles));

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Legacy removal policies must provide a temporary-existence reason.", nameof(reason));

            if (string.IsNullOrWhiteSpace(targetReplacement))
                throw new ArgumentException("Legacy removal policies must provide a target replacement.", nameof(targetReplacement));

            if (string.IsNullOrWhiteSpace(expirationCondition))
                throw new ArgumentException("Legacy removal policies must provide an expiration condition.", nameof(expirationCondition));

            if (string.IsNullOrWhiteSpace(diagnosticsCode))
                throw new ArgumentException("Legacy removal policies must provide a diagnostics code.", nameof(diagnosticsCode));

            if (string.IsNullOrWhiteSpace(trackingIssueOrBlockingCondition))
                throw new ArgumentException("Legacy removal policies must provide a tracking issue or blocking condition.", nameof(trackingIssueOrBlockingCondition));

            OwnerModule = ownerModule;
            Status = status;
            AllowedProfiles = allowedProfiles;
            Reason = reason;
            TargetReplacement = targetReplacement;
            ExpirationCondition = expirationCondition;
            DiagnosticsCode = diagnosticsCode;
            TrackingIssueOrBlockingCondition = trackingIssueOrBlockingCondition;
        }

        public ModuleId OwnerModule { get; }

        public LegacyRemovalStatus Status { get; }

        public KernelProfileMask AllowedProfiles { get; }

        public string Reason { get; }

        public string TargetReplacement { get; }

        public string ExpirationCondition { get; }

        public string DiagnosticsCode { get; }

        public string TrackingIssueOrBlockingCondition { get; }

        public bool IsExpired => Status == LegacyRemovalStatus.Deprecated || Status == LegacyRemovalStatus.Forbidden;

        public bool Allows(KernelProfileMask profileMask)
        {
            return (AllowedProfiles & profileMask) != KernelProfileMask.None;
        }
    }

    public sealed class LegacyAdapterDescriptor
    {
        readonly DependencyNodeIR[] explicitTargets;

        public LegacyAdapterDescriptor(
            LegacyCompatKind kind,
            ModuleId ownerModule,
            string legacySystemName,
            string legacySourceType,
            string targetSubsystemName,
            LegacyAdapterSurface surface,
            KernelProfileMask profiles,
            SourceLocationId source,
            LegacyRemovalPolicy removalPolicy,
            DependencyNodeIR[]? explicitTargets = null)
        {
            if (kind == LegacyCompatKind.None)
                throw new ArgumentException("Legacy adapter descriptors must provide a bridge kind.", nameof(kind));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Legacy adapter descriptors must provide a non-zero owner module identity.", nameof(ownerModule));

            if (string.IsNullOrWhiteSpace(legacySystemName))
                throw new ArgumentException("Legacy adapter descriptors must provide a legacy system name.", nameof(legacySystemName));

            if (string.IsNullOrWhiteSpace(legacySourceType))
                throw new ArgumentException("Legacy adapter descriptors must provide a legacy source type.", nameof(legacySourceType));

            if (string.IsNullOrWhiteSpace(targetSubsystemName))
                throw new ArgumentException("Legacy adapter descriptors must provide a target subsystem name.", nameof(targetSubsystemName));

            if (surface == LegacyAdapterSurface.None)
                throw new ArgumentException("Legacy adapter descriptors must provide a compatibility surface.", nameof(surface));

            if (profiles == KernelProfileMask.None)
                throw new ArgumentException("Legacy adapter descriptors must declare at least one allowed profile.", nameof(profiles));

            if (source.Value == 0)
                throw new ArgumentException("Legacy adapter descriptors must provide a non-zero source location identity.", nameof(source));

            if (removalPolicy == null)
                throw new ArgumentNullException(nameof(removalPolicy));

            if (removalPolicy.OwnerModule != ownerModule)
                throw new ArgumentException("Legacy adapter descriptors must use a removal policy owned by the same module.", nameof(removalPolicy));

            if (removalPolicy.AllowedProfiles != profiles)
                throw new ArgumentException("Legacy adapter descriptors must keep profile availability aligned with the removal policy.", nameof(profiles));

            if (kind == LegacyCompatKind.ForbiddenFallback && removalPolicy.Status != LegacyRemovalStatus.Forbidden)
                throw new ArgumentException("Forbidden fallback adapters must use a forbidden removal policy.", nameof(removalPolicy));

            this.explicitTargets = CloneExplicitTargets(explicitTargets);

            if (this.explicitTargets.Length == 0)
                throw new ArgumentException("Legacy adapter descriptors must declare explicit target nodes.", nameof(explicitTargets));

            Kind = kind;
            OwnerModule = ownerModule;
            LegacySystemName = legacySystemName;
            LegacySourceType = legacySourceType;
            TargetSubsystemName = targetSubsystemName;
            Surface = surface;
            Source = source;
            RemovalPolicy = removalPolicy;
        }

        public LegacyCompatKind Kind { get; }

        public ModuleId OwnerModule { get; }

        public string LegacySystemName { get; }

        public string LegacySourceType { get; }

        public string TargetSubsystemName { get; }

        public LegacyAdapterSurface Surface { get; }

        public KernelProfileMask Profiles => RemovalPolicy.AllowedProfiles;

        public SourceLocationId Source { get; }

        public LegacyRemovalPolicy RemovalPolicy { get; }

        public LegacyRemovalStatus RemovalStatus => RemovalPolicy.Status;

        public string DiagnosticsCode => RemovalPolicy.DiagnosticsCode;

        public string RemovalCondition => RemovalPolicy.ExpirationCondition;

        public string TrackingIssueOrBlockingCondition => RemovalPolicy.TrackingIssueOrBlockingCondition;

        public ReadOnlySpan<DependencyNodeIR> ExplicitTargets => explicitTargets;

        public bool IsRuntimeCapable => Kind == LegacyCompatKind.RuntimeAdapter
            || Kind == LegacyCompatKind.TemporaryBridge
            || Kind == LegacyCompatKind.ForbiddenFallback;

        public bool IsAllowedFor(KernelProfileMask profileMask)
        {
            return RemovalPolicy.Allows(profileMask);
        }

        static DependencyNodeIR[] CloneExplicitTargets(DependencyNodeIR[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DependencyNodeIR>();

            DependencyNodeIR[] clone = new DependencyNodeIR[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Kind == DependencyNodeKind.Unknown)
                    throw new ArgumentException("Legacy adapter explicit targets must not contain unknown dependency nodes.", nameof(source));

                clone[index] = source[index];
            }

            return clone;
        }
    }
}
