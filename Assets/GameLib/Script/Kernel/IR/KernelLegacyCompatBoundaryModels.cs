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

        public bool Allows(KernelProfileMask profileMask)
        {
            return (AllowedProfiles & profileMask) != KernelProfileMask.None;
        }
    }

    public sealed class LegacyAdapterDescriptor
    {
        public LegacyAdapterDescriptor(
            LegacyCompatKind kind,
            ModuleId ownerModule,
            string legacySystemName,
            string targetSubsystemName,
            KernelProfileMask profiles,
            SourceLocationId source,
            LegacyRemovalPolicy removalPolicy)
        {
            if (kind == LegacyCompatKind.None)
                throw new ArgumentException("Legacy adapter descriptors must provide a bridge kind.", nameof(kind));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Legacy adapter descriptors must provide a non-zero owner module identity.", nameof(ownerModule));

            if (string.IsNullOrWhiteSpace(legacySystemName))
                throw new ArgumentException("Legacy adapter descriptors must provide a legacy system name.", nameof(legacySystemName));

            if (string.IsNullOrWhiteSpace(targetSubsystemName))
                throw new ArgumentException("Legacy adapter descriptors must provide a target subsystem name.", nameof(targetSubsystemName));

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

            Kind = kind;
            OwnerModule = ownerModule;
            LegacySystemName = legacySystemName;
            TargetSubsystemName = targetSubsystemName;
            Source = source;
            RemovalPolicy = removalPolicy;
        }

        public LegacyCompatKind Kind { get; }

        public ModuleId OwnerModule { get; }

        public string LegacySystemName { get; }

        public string TargetSubsystemName { get; }

        public KernelProfileMask Profiles => RemovalPolicy.AllowedProfiles;

        public SourceLocationId Source { get; }

        public LegacyRemovalPolicy RemovalPolicy { get; }

        public LegacyRemovalStatus RemovalStatus => RemovalPolicy.Status;

        public string DiagnosticsCode => RemovalPolicy.DiagnosticsCode;

        public string RemovalCondition => RemovalPolicy.TrackingIssueOrBlockingCondition;

        public bool IsRuntimeCapable => Kind == LegacyCompatKind.RuntimeAdapter
            || Kind == LegacyCompatKind.TemporaryBridge
            || Kind == LegacyCompatKind.ForbiddenFallback;

        public bool IsAllowedFor(KernelProfileMask profileMask)
        {
            return RemovalPolicy.Allows(profileMask);
        }
    }
}