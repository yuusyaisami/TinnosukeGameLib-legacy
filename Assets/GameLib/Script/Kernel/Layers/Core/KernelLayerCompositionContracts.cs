#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;

namespace Game.Kernel.Layers
{
    public enum KernelComponentPlacementScope
    {
        Unknown = 0,
        SharedGeneration = 10,
        Application = 20,
        Scene = 30,
    }

    public enum KernelMappedComponentKind
    {
        Unknown = 0,
        ProjectionGenerator = 10,
        BootBoundary = 20,
        BootRuntimeSurfaceFactory = 30,
        BootRuntimeSurface = 40,
        RuntimeServiceGraph = 50,
        RuntimeScopeGraph = 60,
        LifecycleDispatcher = 70,
        LifecyclePlanResolver = 80,
        BootManifest = 90,
        KernelProfile = 100,
        DiagnosticService = 110,
    }

    public enum ApplicationKernelBoundaryKind
    {
        Unknown = 0,
        BootBoundary = 10,
        RuntimeSurfaceFactory = 20,
        Diagnostics = 30,
        SelectedManifest = 40,
        SelectedProfile = 50,
    }

    public enum SceneKernelBoundaryKind
    {
        Unknown = 0,
        RuntimeSurface = 10,
        RuntimeServiceGraph = 20,
        RuntimeScopeGraph = 30,
        LifecycleDispatcher = 40,
        LifecyclePlanResolver = 50,
    }

    public readonly struct KernelComponentPlacementDescriptor : IEquatable<KernelComponentPlacementDescriptor>
    {
        public KernelComponentPlacementDescriptor(
            string sourceTypeName,
            KernelMappedComponentKind componentKind,
            KernelComponentPlacementScope placementScope,
            string notes)
        {
            if (string.IsNullOrWhiteSpace(sourceTypeName))
                throw new ArgumentException("Kernel component placements must provide a non-empty source type name.", nameof(sourceTypeName));

            if (componentKind == KernelMappedComponentKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(componentKind), componentKind, "Kernel component placements must target a defined component kind.");

            if (placementScope == KernelComponentPlacementScope.Unknown)
                throw new ArgumentOutOfRangeException(nameof(placementScope), placementScope, "Kernel component placements must target a defined placement scope.");

            if (string.IsNullOrWhiteSpace(notes))
                throw new ArgumentException("Kernel component placements must provide non-empty mapping notes.", nameof(notes));

            SourceTypeName = sourceTypeName;
            ComponentKind = componentKind;
            PlacementScope = placementScope;
            Notes = notes;
        }

        public string SourceTypeName { get; }

        public KernelMappedComponentKind ComponentKind { get; }

        public KernelComponentPlacementScope PlacementScope { get; }

        public string Notes { get; }

        public bool Equals(KernelComponentPlacementDescriptor other)
        {
            return StringComparer.Ordinal.Equals(SourceTypeName, other.SourceTypeName)
                && ComponentKind == other.ComponentKind
                && PlacementScope == other.PlacementScope
                && StringComparer.Ordinal.Equals(Notes, other.Notes);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelComponentPlacementDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(SourceTypeName);
                hash = (hash * 397) ^ (int)ComponentKind;
                hash = (hash * 397) ^ (int)PlacementScope;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Notes);
                return hash;
            }
        }

        public override string ToString()
        {
            return "KernelComponentPlacementDescriptor(SourceTypeName=" + SourceTypeName + ", ComponentKind=" + ComponentKind + ", PlacementScope=" + PlacementScope + ", Notes=" + Notes + ")";
        }

        public static bool operator ==(KernelComponentPlacementDescriptor left, KernelComponentPlacementDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelComponentPlacementDescriptor left, KernelComponentPlacementDescriptor right)
        {
            return !left.Equals(right);
        }
    }

    public interface IApplicationKernelComposition
    {
        IReadOnlyList<KernelComponentPlacementDescriptor> Placements { get; }

        void SetSelectedBootIdentity(ManifestId? manifestId, KernelProfileId? profileId);

        bool TryGetSelectedManifestId(out ManifestId manifestId);

        bool TryGetSelectedProfileId(out KernelProfileId profileId);

        bool TryGetBoundary(ApplicationKernelBoundaryKind boundaryKind, out object? boundary);
    }

    public interface ISceneKernelComposition
    {
        IReadOnlyList<KernelComponentPlacementDescriptor> Placements { get; }

        bool TryGetBoundary(SceneKernelBoundaryKind boundaryKind, out object? boundary);
    }
}
