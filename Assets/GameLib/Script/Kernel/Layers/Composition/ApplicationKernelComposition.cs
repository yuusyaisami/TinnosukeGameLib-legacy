#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;

namespace Game.Kernel.Layers.Composition
{
    public sealed class ApplicationKernelComposition : IApplicationKernelComposition
    {
        readonly ApplicationKernelBootBoundaryAdapter bootBoundary;
        readonly IKernelDiagnosticService diagnosticService;

        KernelBootManifest? selectedManifest;
        KernelProfile? selectedProfile;
        ManifestId? selectedManifestId;
        KernelProfileId? selectedProfileId;

        public ApplicationKernelComposition(
            IKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory,
            IKernelDiagnosticService diagnosticService)
        {
            if (runtimeSurfaceFactory == null)
                throw new ArgumentNullException(nameof(runtimeSurfaceFactory));

            this.diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
            bootBoundary = new ApplicationKernelBootBoundaryAdapter(runtimeSurfaceFactory);
        }

        public static ApplicationKernelComposition CreateDefault(int inMemoryDiagnosticCapacity = 256)
        {
            if (inMemoryDiagnosticCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(inMemoryDiagnosticCapacity), inMemoryDiagnosticCapacity, "Default kernel composition diagnostic capacity must be positive.");

            return new ApplicationKernelComposition(
                new KernelBootRuntimeSurfaceFactory(),
                new KernelDiagnosticService(new IKernelDiagnosticSink[]
                {
                    new InMemoryDiagnosticSink(inMemoryDiagnosticCapacity),
                }));
        }

        public IReadOnlyList<KernelComponentPlacementDescriptor> Placements => KernelComponentPlacementCatalog.Application;

        public ApplicationKernelBootBoundaryAdapter BootBoundary => bootBoundary;

        public IKernelBootRuntimeSurfaceFactory RuntimeSurfaceFactory => bootBoundary.RuntimeSurfaceFactory;

        public IKernelDiagnosticService DiagnosticService => diagnosticService;

        public KernelBootManifest? SelectedManifest => selectedManifest;

        public KernelProfile? SelectedProfile => selectedProfile;

        public void SetSelectedBootIdentity(ManifestId? manifestId, KernelProfileId? profileId)
        {
            selectedManifestId = manifestId;
            selectedProfileId = profileId;

            if (selectedManifest != null && (!manifestId.HasValue || selectedManifest.ManifestId != manifestId.Value))
                selectedManifest = null;

            if (selectedProfile != null && (!profileId.HasValue || selectedProfile.Id != profileId.Value))
                selectedProfile = null;
        }

        public bool TryGetSelectedManifestId(out ManifestId manifestId)
        {
            if (selectedManifestId.HasValue)
            {
                manifestId = selectedManifestId.Value;
                return true;
            }

            manifestId = default;
            return false;
        }

        public bool TryGetSelectedProfileId(out KernelProfileId profileId)
        {
            if (selectedProfileId.HasValue)
            {
                profileId = selectedProfileId.Value;
                return true;
            }

            profileId = default;
            return false;
        }

        public KernelBootBoundaryResult ExecuteBoot(BootValidationInput input)
        {
            KernelBootBoundaryResult result = bootBoundary.Execute(input);
            if (result is KernelBootBoundaryResult.Success success)
                SetSelectedBootState(success.Context.Manifest, success.Context.SelectedProfile);

            return result;
        }

        public SceneKernelComposition CreateSceneComposition(KernelBootBoundaryResult.Success success)
        {
            if (success == null)
                throw new ArgumentNullException(nameof(success));

            SetSelectedBootState(success.Context.Manifest, success.Context.SelectedProfile);
            return SceneKernelComposition.FromRuntimeSurface(success.RuntimeSurface);
        }

        public SceneKernelComposition CreateSceneComposition(IKernelBootRuntimeSurface runtimeSurface)
        {
            return SceneKernelComposition.FromRuntimeSurface(runtimeSurface);
        }

        public void SetSelectedBootState(KernelBootManifest manifest, KernelProfile profile)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (manifest.ProfileId != profile.Id)
            {
                throw new ArgumentException(
                    "ApplicationKernel selected manifest/profile must agree on KernelProfileId. Manifest=" + manifest.ProfileId + ", Profile=" + profile.Id + ".",
                    nameof(profile));
            }

            selectedManifest = manifest;
            selectedProfile = profile;
            selectedManifestId = manifest.ManifestId;
            selectedProfileId = profile.Id;
        }

        public bool TryGetBoundary(ApplicationKernelBoundaryKind boundaryKind, out object? boundary)
        {
            switch (boundaryKind)
            {
                case ApplicationKernelBoundaryKind.BootBoundary:
                    boundary = bootBoundary;
                    return true;
                case ApplicationKernelBoundaryKind.RuntimeSurfaceFactory:
                    boundary = RuntimeSurfaceFactory;
                    return true;
                case ApplicationKernelBoundaryKind.Diagnostics:
                    boundary = diagnosticService;
                    return true;
                case ApplicationKernelBoundaryKind.SelectedManifest:
                    boundary = selectedManifest;
                    return selectedManifest != null;
                case ApplicationKernelBoundaryKind.SelectedProfile:
                    boundary = selectedProfile;
                    return selectedProfile != null;
                case ApplicationKernelBoundaryKind.Unknown:
                default:
                    boundary = null;
                    return false;
            }
        }
    }
}
