#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;

namespace Game.Kernel.V21
{
    public sealed class ApplicationKernel
    {
        ManifestId? selectedManifestId;
        KernelProfileId? selectedProfileId;

        public ApplicationKernel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("ApplicationKernel must provide a non-empty name.", nameof(name));

            Name = name;
            State = KernelLayerState.Created;
        }

        public string Name { get; }

        public KernelLayerKind LayerKind => KernelLayerKind.Application;

        public KernelLayerState State { get; private set; }

        public ManifestId? SelectedManifestId
        {
            get
            {
                if (Composition != null && Composition.TryGetSelectedManifestId(out ManifestId manifestId))
                    return manifestId;

                return selectedManifestId;
            }
        }

        public KernelProfileId? SelectedProfileId
        {
            get
            {
                if (Composition != null && Composition.TryGetSelectedProfileId(out KernelProfileId profileId))
                    return profileId;

                return selectedProfileId;
            }
        }

        public IApplicationKernelComposition? Composition { get; private set; }

        public SceneKernel? CurrentSceneKernel { get; private set; }

        public void Initialize(ManifestId? manifestId = null, KernelProfileId? profileId = null)
        {
            if (State != KernelLayerState.Created)
                throw new InvalidOperationException("ApplicationKernel can only be initialized from the Created state.");

            selectedManifestId = manifestId;
            selectedProfileId = profileId;
            State = KernelLayerState.Initialized;
        }

        public void SelectBootManifest(ManifestId manifestId)
        {
            EnsureSelectionMutable();
            selectedManifestId = manifestId;
            Composition?.SetSelectedBootIdentity(selectedManifestId, selectedProfileId);
        }

        public void SelectKernelProfile(KernelProfileId profileId)
        {
            EnsureSelectionMutable();
            selectedProfileId = profileId;
            Composition?.SetSelectedBootIdentity(selectedManifestId, selectedProfileId);
        }

        public void AttachComposition(IApplicationKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            EnsureOperational();

            if (Composition != null)
                throw new InvalidOperationException("ApplicationKernel already has an attached composition.");

            ValidateApplicationCompositionPlacements(composition.Placements);
            composition.SetSelectedBootIdentity(selectedManifestId, selectedProfileId);
            Composition = composition;
        }

        public void DetachComposition(IApplicationKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            if (!ReferenceEquals(Composition, composition))
                throw new InvalidOperationException("ApplicationKernel can only detach its currently attached composition.");

            if (CurrentSceneKernel != null)
                throw new InvalidOperationException("ApplicationKernel must detach its SceneKernel before detaching the application composition.");

            if (composition.TryGetSelectedManifestId(out ManifestId manifestId))
                selectedManifestId = manifestId;

            if (composition.TryGetSelectedProfileId(out KernelProfileId profileId))
                selectedProfileId = profileId;

            Composition = null;
        }

        public bool TryGetBoundary(ApplicationKernelBoundaryKind boundaryKind, out object? boundary)
        {
            if (boundaryKind == ApplicationKernelBoundaryKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, "ApplicationKernel boundary queries must target a defined boundary kind.");

            if (Composition == null)
            {
                boundary = null;
                return false;
            }

            return Composition.TryGetBoundary(boundaryKind, out boundary);
        }

        public void AttachSceneKernel(SceneKernel sceneKernel)
        {
            if (sceneKernel == null)
                throw new ArgumentNullException(nameof(sceneKernel));

            EnsureOperational();

            if (CurrentSceneKernel == sceneKernel)
                return;

            if (CurrentSceneKernel != null)
                throw new InvalidOperationException("ApplicationKernel already has an attached SceneKernel.");

            sceneKernel.AttachToApplicationKernel(this);
            CurrentSceneKernel = sceneKernel;
        }

        public void DetachSceneKernel(SceneKernel sceneKernel)
        {
            if (sceneKernel == null)
                throw new ArgumentNullException(nameof(sceneKernel));

            if (CurrentSceneKernel != sceneKernel)
                throw new InvalidOperationException("ApplicationKernel can only detach the currently attached SceneKernel.");

            sceneKernel.DetachFromApplicationKernel(this);
            CurrentSceneKernel = null;
        }

        public void Shutdown()
        {
            if (CurrentSceneKernel != null)
                throw new InvalidOperationException("ApplicationKernel must detach its SceneKernel before shutdown.");

            if (State == KernelLayerState.Shutdown)
                return;

            Composition = null;
            State = KernelLayerState.Shutdown;
        }

        void EnsureOperational()
        {
            if (State != KernelLayerState.Initialized)
                throw new InvalidOperationException("ApplicationKernel must be initialized before scene attachment.");
        }

        void EnsureSelectionMutable()
        {
            if (State == KernelLayerState.Shutdown)
                throw new InvalidOperationException("ApplicationKernel cannot change boot selection after shutdown.");

            if (CurrentSceneKernel != null)
                throw new InvalidOperationException("ApplicationKernel cannot change boot selection while a SceneKernel is attached.");
        }

        static void ValidateApplicationCompositionPlacements(IReadOnlyList<KernelComponentPlacementDescriptor> placements)
        {
            if (placements == null)
                throw new ArgumentNullException(nameof(placements));

            for (int index = 0; index < placements.Count; index++)
            {
                KernelComponentPlacementDescriptor placement = placements[index];
                if (placement.PlacementScope != KernelComponentPlacementScope.Application)
                {
                    throw new ArgumentException(
                        "ApplicationKernel compositions may only expose Application placement entries. Invalid placement: " + placement,
                        nameof(placements));
                }
            }
        }
    }
}
