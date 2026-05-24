#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Kernel.V21
{
    public sealed class SceneKernel
    {
        public SceneKernel(SceneKernelHandle handle, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("SceneKernel must provide a non-empty scene name.", nameof(sceneName));

            Handle = handle;
            SceneName = sceneName;
            State = KernelLayerState.Created;
        }

        public SceneKernelHandle Handle { get; }

        public string SceneName { get; }

        public KernelLayerKind LayerKind => KernelLayerKind.Scene;

        public KernelLayerState State { get; private set; }

        public ApplicationKernel? OwnerApplicationKernel { get; private set; }

        public ISceneKernelComposition? Composition { get; private set; }

        public void Initialize()
        {
            if (State != KernelLayerState.Created)
                throw new InvalidOperationException("SceneKernel can only be initialized from the Created state.");

            State = KernelLayerState.Initialized;
        }

        public void AttachComposition(ISceneKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            EnsureOperational();

            if (Composition != null)
                throw new InvalidOperationException("SceneKernel already has an attached composition.");

            ValidateSceneCompositionPlacements(composition.Placements);
            Composition = composition;
        }

        public void DetachComposition(ISceneKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            if (!ReferenceEquals(Composition, composition))
                throw new InvalidOperationException("SceneKernel can only detach its currently attached composition.");

            if (OwnerApplicationKernel != null)
                throw new InvalidOperationException("SceneKernel must be detached from ApplicationKernel before its scene composition can be detached.");

            Composition = null;
        }

        public bool TryGetBoundary(SceneKernelBoundaryKind boundaryKind, out object? boundary)
        {
            if (boundaryKind == SceneKernelBoundaryKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, "SceneKernel boundary queries must target a defined boundary kind.");

            if (Composition == null)
            {
                boundary = null;
                return false;
            }

            return Composition.TryGetBoundary(boundaryKind, out boundary);
        }

        public bool TryGetApplicationBoundary(ApplicationKernelBoundaryKind boundaryKind, out object? boundary)
        {
            if (boundaryKind == ApplicationKernelBoundaryKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, "SceneKernel application boundary queries must target a defined boundary kind.");

            switch (boundaryKind)
            {
                case ApplicationKernelBoundaryKind.Diagnostics:
                case ApplicationKernelBoundaryKind.SelectedManifest:
                case ApplicationKernelBoundaryKind.SelectedProfile:
                    break;
                case ApplicationKernelBoundaryKind.BootBoundary:
                case ApplicationKernelBoundaryKind.RuntimeSurfaceFactory:
                default:
                    boundary = null;
                    return false;
            }

            ApplicationKernel? owner = OwnerApplicationKernel;
            if (owner == null)
            {
                boundary = null;
                return false;
            }

            return owner.TryGetBoundary(boundaryKind, out boundary);
        }

        public void Shutdown()
        {
            if (OwnerApplicationKernel != null)
                throw new InvalidOperationException("SceneKernel must be detached from ApplicationKernel before shutdown.");

            if (State == KernelLayerState.Shutdown)
                return;

            Composition = null;
            State = KernelLayerState.Shutdown;
        }

        internal void AttachToApplicationKernel(ApplicationKernel applicationKernel)
        {
            if (applicationKernel == null)
                throw new ArgumentNullException(nameof(applicationKernel));

            if (State != KernelLayerState.Initialized)
                throw new InvalidOperationException("SceneKernel must be initialized before attachment.");

            if (OwnerApplicationKernel != null && !ReferenceEquals(OwnerApplicationKernel, applicationKernel))
                throw new InvalidOperationException("SceneKernel is already attached to another ApplicationKernel.");

            OwnerApplicationKernel = applicationKernel;
        }

        internal void DetachFromApplicationKernel(ApplicationKernel applicationKernel)
        {
            if (applicationKernel == null)
                throw new ArgumentNullException(nameof(applicationKernel));

            if (!ReferenceEquals(OwnerApplicationKernel, applicationKernel))
                throw new InvalidOperationException("SceneKernel can only be detached by its owner ApplicationKernel.");

            OwnerApplicationKernel = null;
        }

        void EnsureOperational()
        {
            if (State != KernelLayerState.Initialized)
                throw new InvalidOperationException("SceneKernel must be initialized before scene composition attachment.");
        }

        static void ValidateSceneCompositionPlacements(IReadOnlyList<KernelComponentPlacementDescriptor> placements)
        {
            if (placements == null)
                throw new ArgumentNullException(nameof(placements));

            for (int index = 0; index < placements.Count; index++)
            {
                KernelComponentPlacementDescriptor placement = placements[index];
                if (placement.PlacementScope != KernelComponentPlacementScope.Scene)
                {
                    throw new ArgumentException(
                        "SceneKernel compositions may only expose Scene placement entries. Invalid placement: " + placement,
                        nameof(placements));
                }
            }
        }
    }
}
