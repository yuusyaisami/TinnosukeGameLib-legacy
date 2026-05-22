#nullable enable
using System;
using Game.Kernel.Boot;
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public enum KernelLiveBootLoadingParentKind
    {
        None = 0,
        ProjectRoot = 10,
        PlatformRoot = 20,
        GlobalRoot = 30,
    }

    public static class KernelLiveBootRuntime
    {
        static bool s_legacyAutoBootstrapSuppressed;
        static bool s_verifiedBootInProgress;
        static bool s_verifiedBootReady;
        static bool s_sceneHandoffInProgress;
        static bool s_sceneHandoffReady;
        static bool s_legacyFallbackAttempted;
        static bool s_runtimeDiscoveryAttempted;
        static bool s_resourcesFallbackAttempted;
        static bool s_defaultRootCreationAttempted;
        static bool s_duplicateRootCleanupAttempted;
        static Transform? s_explicitLoadingParent;
        static KernelLiveBootLoadingParentKind s_loadingParentKind;

        public static bool IsLegacyAutoBootstrapSuppressed => s_legacyAutoBootstrapSuppressed;

        public static bool IsVerifiedLiveBootActive => s_verifiedBootInProgress || s_verifiedBootReady;

        public static bool IsVerifiedBootInProgress => s_verifiedBootInProgress;

        public static bool IsVerifiedLiveBootReady => s_verifiedBootReady;

        public static bool IsSceneHandoffInProgress => s_sceneHandoffInProgress;

        public static bool IsSceneHandoffReady => s_sceneHandoffReady;

        public static KernelLiveBootLoadingParentKind LoadingParentKind => s_loadingParentKind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_legacyAutoBootstrapSuppressed = false;
            s_verifiedBootInProgress = false;
            s_verifiedBootReady = false;
            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = false;
            s_legacyFallbackAttempted = false;
            s_runtimeDiscoveryAttempted = false;
            s_resourcesFallbackAttempted = false;
            s_defaultRootCreationAttempted = false;
            s_duplicateRootCleanupAttempted = false;
            s_explicitLoadingParent = null;
            s_loadingParentKind = KernelLiveBootLoadingParentKind.None;
        }

        public static void BeginVerifiedBoot(KernelLiveBootLoadingParentKind loadingParentKind)
        {
            if (s_verifiedBootInProgress || s_verifiedBootReady)
                throw new InvalidOperationException("Kernel live boot is already active for this runtime session.");

            s_legacyAutoBootstrapSuppressed = true;
            s_verifiedBootInProgress = true;
            s_verifiedBootReady = false;
            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = false;
            s_legacyFallbackAttempted = false;
            s_runtimeDiscoveryAttempted = false;
            s_resourcesFallbackAttempted = false;
            s_defaultRootCreationAttempted = false;
            s_duplicateRootCleanupAttempted = false;
            s_explicitLoadingParent = null;
            s_loadingParentKind = loadingParentKind;
        }

        public static void CompleteVerifiedBoot(Transform? explicitLoadingParent)
        {
            if (s_loadingParentKind != KernelLiveBootLoadingParentKind.None && explicitLoadingParent == null)
                throw new InvalidOperationException("Kernel live boot requires an explicit loading parent before it can become ready.");

            s_verifiedBootInProgress = false;
            s_verifiedBootReady = true;
            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = false;
            s_explicitLoadingParent = explicitLoadingParent;
        }

        public static void BeginSceneHandoff()
        {
            if (!s_verifiedBootReady)
                throw new InvalidOperationException("Kernel live boot requires a ready verified host before scene handoff can begin.");

            if (s_sceneHandoffReady || s_sceneHandoffInProgress)
                return;

            s_sceneHandoffInProgress = true;
        }

        public static void CompleteSceneHandoff()
        {
            if (!s_verifiedBootReady)
                throw new InvalidOperationException("Kernel live boot requires a ready verified host before scene handoff can complete.");

            if (s_sceneHandoffReady)
                return;

            if (!s_sceneHandoffInProgress)
                throw new InvalidOperationException("Kernel live boot scene handoff must begin before it can complete.");

            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = true;
        }

        public static void CancelSceneHandoff()
        {
            if (!s_verifiedBootReady)
                return;

            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = false;
        }

        public static void AbortVerifiedBoot()
        {
            s_legacyAutoBootstrapSuppressed = false;
            s_verifiedBootInProgress = false;
            s_verifiedBootReady = false;
            s_sceneHandoffInProgress = false;
            s_sceneHandoffReady = false;
            s_explicitLoadingParent = null;
            s_loadingParentKind = KernelLiveBootLoadingParentKind.None;
        }

        public static bool ShouldSuppressLegacyAutoBootstrap()
        {
            return s_legacyAutoBootstrapSuppressed;
        }

        public static void ThrowLegacyAutoBootstrapForbidden(string ownerName)
        {
            if (string.IsNullOrWhiteSpace(ownerName))
                throw new ArgumentException("Legacy auto-bootstrap owner name is required.", nameof(ownerName));

            RecordLegacyFallbackAttempt();
            throw new InvalidOperationException($"Legacy {ownerName} auto-bootstrap is quarantined. Use KernelLiveBootOrchestrator with verified boot input.");
        }

        public static void RecordLegacyFallbackAttempt()
        {
            s_legacyFallbackAttempted = true;
        }

        public static void RecordRuntimeDiscoveryAttempt()
        {
            s_runtimeDiscoveryAttempted = true;
        }

        public static void RecordResourcesFallbackAttempt()
        {
            s_resourcesFallbackAttempted = true;
        }

        public static void RecordDefaultRootCreationAttempt()
        {
            s_defaultRootCreationAttempted = true;
        }

        public static void RecordDuplicateRootCleanupAttempt()
        {
            s_duplicateRootCleanupAttempted = true;
        }

        public static bool TryGetExplicitLoadingParent(out Transform? loadingParent)
        {
            if (s_explicitLoadingParent != null && s_explicitLoadingParent.gameObject != null)
            {
                loadingParent = s_explicitLoadingParent;
                return true;
            }

            loadingParent = null;
            return false;
        }

        public static BootFallbackValidationState CreateFallbackStateSnapshot()
        {
            return new BootFallbackValidationState(
                legacyFallbackAttempted: s_legacyFallbackAttempted,
                runtimeDiscoveryAttempted: s_runtimeDiscoveryAttempted,
                resourcesFallbackAttempted: s_resourcesFallbackAttempted,
                defaultRootCreationAttempted: s_defaultRootCreationAttempted,
                duplicateRootCleanupAttempted: s_duplicateRootCleanupAttempted,
                nonDeterministicTestPolicy: false);
        }
    }
}