#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.Kernel.Abstractions;
using Game.Kernel.Generation;

namespace Game.Kernel.Boot
{
    public readonly struct VerifiedArtifactSetRef : IEquatable<VerifiedArtifactSetRef>
    {
        public VerifiedArtifactSetRef(
            ArtifactSetId artifactSetId,
            PlanId planId,
            string kernelIRHash,
            string profileHash,
            int formatVersion,
            string? registryHash = null,
            string? debugMapHash = null)
        {
            if (artifactSetId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactSetId), artifactSetId.Value, "Verified artifact set references must provide a positive artifact set identity.");

            if (planId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(planId), planId.Value, "Verified artifact set references must provide a positive plan identity.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Verified artifact set references must provide a positive format version.");

            if (!IsValidHashString(kernelIRHash))
                throw new ArgumentException("Verified artifact set references must provide a 32-character hexadecimal kernel IR hash.", nameof(kernelIRHash));

            if (IsZeroHashString(kernelIRHash))
                throw new ArgumentException("Verified artifact set references must not use a zero kernel IR hash.", nameof(kernelIRHash));

            if (!IsValidHashString(profileHash))
                throw new ArgumentException("Verified artifact set references must provide a 32-character hexadecimal profile hash.", nameof(profileHash));

            if (IsZeroHashString(profileHash))
                throw new ArgumentException("Verified artifact set references must not use a zero profile hash.", nameof(profileHash));

            if (registryHash != null && !IsValidHashString(registryHash))
                throw new ArgumentException("Verified artifact set references must provide a 32-character hexadecimal registry hash when one is present.", nameof(registryHash));

            if (registryHash != null && IsZeroHashString(registryHash))
                throw new ArgumentException("Verified artifact set references must not use a zero registry hash when one is present.", nameof(registryHash));

            if (debugMapHash != null && !IsValidHashString(debugMapHash))
                throw new ArgumentException("Verified artifact set references must provide a 32-character hexadecimal debug map hash when one is present.", nameof(debugMapHash));

            if (debugMapHash != null && IsZeroHashString(debugMapHash))
                throw new ArgumentException("Verified artifact set references must not use a zero debug map hash when one is present.", nameof(debugMapHash));

            ArtifactSetId = artifactSetId;
            PlanId = planId;
            KernelIRHash = kernelIRHash;
            RegistryHash = registryHash;
            ProfileHash = profileHash;
            DebugMapHash = debugMapHash;
            FormatVersion = formatVersion;
        }

        public ArtifactSetId ArtifactSetId { get; }

        public PlanId PlanId { get; }

        public string KernelIRHash { get; }

        public string? RegistryHash { get; }

        public string ProfileHash { get; }

        public string? DebugMapHash { get; }

        public int FormatVersion { get; }

        public bool IsValid => ArtifactSetId.Value > 0
            && PlanId.Value > 0
            && FormatVersion > 0
            && IsValidHashString(KernelIRHash)
            && !IsZeroHashString(KernelIRHash)
            && IsValidHashString(ProfileHash)
            && !IsZeroHashString(ProfileHash)
            && (RegistryHash == null || IsValidHashString(RegistryHash))
            && (RegistryHash == null || !IsZeroHashString(RegistryHash))
            && (DebugMapHash == null || IsValidHashString(DebugMapHash))
            && (DebugMapHash == null || !IsZeroHashString(DebugMapHash));

        public bool Equals(VerifiedArtifactSetRef other)
        {
            return ArtifactSetId == other.ArtifactSetId
                && PlanId == other.PlanId
                && KernelIRHash == other.KernelIRHash
                && Nullable.Equals(RegistryHash, other.RegistryHash)
                && ProfileHash == other.ProfileHash
                && Nullable.Equals(DebugMapHash, other.DebugMapHash)
                && FormatVersion == other.FormatVersion;
        }

        public override bool Equals(object? obj)
        {
            return obj is VerifiedArtifactSetRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ArtifactSetId.GetHashCode();
                hash = (hash * 397) ^ PlanId.GetHashCode();
                hash = (hash * 397) ^ KernelIRHash.GetHashCode();
                hash = (hash * 397) ^ (RegistryHash != null ? RegistryHash.GetHashCode() : 0);
                hash = (hash * 397) ^ ProfileHash.GetHashCode();
                hash = (hash * 397) ^ (DebugMapHash != null ? DebugMapHash.GetHashCode() : 0);
                hash = (hash * 397) ^ FormatVersion;
                return hash;
            }
        }

        public override string ToString()
        {
            return "VerifiedArtifactSetRef(ArtifactSetId=" + ArtifactSetId + ", PlanId=" + PlanId + ", KernelIRHash=" + KernelIRHash + ", RegistryHash=" + (RegistryHash ?? "null") + ", ProfileHash=" + ProfileHash + ", DebugMapHash=" + (DebugMapHash ?? "null") + ", FormatVersion=" + FormatVersion + ")";
        }

        public static bool operator ==(VerifiedArtifactSetRef left, VerifiedArtifactSetRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VerifiedArtifactSetRef left, VerifiedArtifactSetRef right)
        {
            return !left.Equals(right);
        }

        static bool IsValidHashString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }

            return true;
        }

        static bool IsZeroHashString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '0')
                    return false;
            }

            return true;
        }
    }

    public sealed class KernelBootManifest : IEquatable<KernelBootManifest>
    {
        public KernelBootManifest(
            ManifestId manifestId,
            KernelProfileId profileId,
            VerifiedArtifactSetRef artifactSet,
            BootPolicyId bootPolicyId,
            BootDiagnosticsPolicy diagnosticsPolicy)
        {
            if (manifestId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(manifestId), manifestId.Value, "Kernel boot manifests must provide a positive manifest identity.");

            if (profileId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(profileId), profileId.Value, "Kernel boot manifests must provide a positive profile identity.");

            if (!artifactSet.IsValid)
                throw new ArgumentException("Kernel boot manifests must provide a valid verified artifact set reference.", nameof(artifactSet));

            if (bootPolicyId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(bootPolicyId), bootPolicyId.Value, "Kernel boot manifests must provide a positive boot policy identity.");

            ManifestId = manifestId;
            ProfileId = profileId;
            ArtifactSet = artifactSet;
            BootPolicyId = bootPolicyId;
            DiagnosticsPolicy = diagnosticsPolicy ?? throw new ArgumentNullException(nameof(diagnosticsPolicy));
        }

        public ManifestId ManifestId { get; }

        public KernelProfileId ProfileId { get; }

        public VerifiedArtifactSetRef ArtifactSet { get; }

        public BootPolicyId BootPolicyId { get; }

        public BootDiagnosticsPolicy DiagnosticsPolicy { get; }

        public bool Equals(KernelBootManifest? other)
        {
            return other != null
                && ManifestId == other.ManifestId
                && ProfileId == other.ProfileId
                && ArtifactSet == other.ArtifactSet
                && BootPolicyId == other.BootPolicyId
                && DiagnosticsPolicy.Equals(other.DiagnosticsPolicy);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelBootManifest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ManifestId.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ ArtifactSet.GetHashCode();
                hash = (hash * 397) ^ BootPolicyId.GetHashCode();
                hash = (hash * 397) ^ DiagnosticsPolicy.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "KernelBootManifest(ManifestId=" + ManifestId + ", ProfileId=" + ProfileId + ", ArtifactSet=" + ArtifactSet + ", BootPolicyId=" + BootPolicyId + ", DiagnosticsPolicy=" + DiagnosticsPolicy + ")";
        }

        public static bool operator ==(KernelBootManifest? left, KernelBootManifest? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KernelBootManifest? left, KernelBootManifest? right)
        {
            return !Equals(left, right);
        }
    }

    public static class KernelBootManifestHashing
    {
        public static string ComputeManifestHash(KernelBootManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            List<string> tokens = new List<string>(16)
            {
                "KernelBootManifestV1",
                "ManifestId:" + manifest.ManifestId.Value,
                "ProfileId:" + manifest.ProfileId.Value,
                "BootPolicyId:" + manifest.BootPolicyId.Value,
                "DiagnosticsKind:" + manifest.DiagnosticsPolicy.Kind,
                "DiagnosticsFailureBoundaryBehavior:" + manifest.DiagnosticsPolicy.FailureBoundaryBehavior,
                "DiagnosticsDetail:" + manifest.DiagnosticsPolicy.DiagnosticsDetail,
                "DiagnosticsEditorInspectionMode:" + manifest.DiagnosticsPolicy.EditorInspectionMode,
                "DiagnosticsTestDeterminismMode:" + manifest.DiagnosticsPolicy.TestDeterminismMode,
            };

            tokens.AddRange(BuildVerifiedArtifactSetTokens(manifest.ArtifactSet));
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens).ToString();
        }

        public static string ComputeVerifiedArtifactSetRefHash(VerifiedArtifactSetRef artifactSet)
        {
            List<string> tokens = BuildVerifiedArtifactSetTokens(artifactSet);
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens).ToString();
        }

        static List<string> BuildVerifiedArtifactSetTokens(VerifiedArtifactSetRef artifactSet)
        {
            List<string> tokens = new List<string>(12)
            {
                "ArtifactSetId:" + artifactSet.ArtifactSetId.Value,
                "PlanId:" + artifactSet.PlanId.Value,
                "FormatVersion:" + artifactSet.FormatVersion,
                "KernelIRHash:" + artifactSet.KernelIRHash,
                "RegistryHashPresent:" + (artifactSet.RegistryHash != null),
                "ProfileHash:" + artifactSet.ProfileHash,
                "DebugMapHashPresent:" + (artifactSet.DebugMapHash != null),
            };

            if (artifactSet.RegistryHash != null)
                tokens.Add("RegistryHash:" + artifactSet.RegistryHash);

            if (artifactSet.DebugMapHash != null)
                tokens.Add("DebugMapHash:" + artifactSet.DebugMapHash);

            return tokens;
        }
    }
    }