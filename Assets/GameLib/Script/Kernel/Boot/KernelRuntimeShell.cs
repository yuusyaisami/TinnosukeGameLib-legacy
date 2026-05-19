#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;

namespace Game.Kernel.Boot
{
    public sealed class KernelRuntime
    {
        public KernelRuntime(KernelBootBoundaryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Manifest = context.Manifest;
            SelectedProfile = context.SelectedProfile;
            Diagnostics = new KernelRuntimeDiagnostics(context.ValidationReport, context.Manifest.ArtifactSet.DebugMapHash);
            ServiceGraph = new KernelRuntimeServiceGraph(context.Input.RootState.AvailableRootServices);
            RootScopeGraph = new KernelRuntimeScopeGraph(context.Input.RootState.AvailableRootScopes);
        }

        public KernelBootManifest Manifest { get; }

        public KernelProfile SelectedProfile { get; }

        public KernelRuntimeDiagnostics Diagnostics { get; }

        public KernelRuntimeServiceGraph ServiceGraph { get; }

        public KernelRuntimeScopeGraph RootScopeGraph { get; }
    }

    public sealed class KernelRuntimeDiagnostics
    {
        readonly ReadOnlyCollection<KernelDiagnostic> diagnostics;

        public KernelRuntimeDiagnostics(BootValidationReport validationReport, string? debugMapHash)
        {
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));
            DebugMapHash = debugMapHash;

            KernelDiagnostic[] snapshot = validationReport.Issues.Count == 0
                ? Array.Empty<KernelDiagnostic>()
                : CloneDiagnostics(validationReport.Issues);

            diagnostics = Array.AsReadOnly(snapshot);
        }

        public BootValidationReport ValidationReport { get; }

        public string? DebugMapHash { get; }

        public IReadOnlyList<KernelDiagnostic> Diagnostics => diagnostics;

        public bool HasDiagnostics => diagnostics.Count > 0;

        static KernelDiagnostic[] CloneDiagnostics(IReadOnlyList<KernelDiagnostic> source)
        {
            KernelDiagnostic[] clone = new KernelDiagnostic[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Kernel runtime diagnostics must not contain null items.", nameof(source));
            }

            return clone;
        }
    }

    public sealed class KernelRuntimeServiceGraph
    {
        readonly ReadOnlyCollection<RuntimeIdentityRef> rootServiceIdentities;

        public KernelRuntimeServiceGraph(ReadOnlySpan<RuntimeIdentityRef> rootServiceIdentities)
        {
            RuntimeIdentityRef[] snapshot = CloneIdentities(rootServiceIdentities);
            this.rootServiceIdentities = Array.AsReadOnly(snapshot);
        }

        public IReadOnlyList<RuntimeIdentityRef> RootServiceIdentities => rootServiceIdentities;

        public int RootServiceCount => rootServiceIdentities.Count;

        public bool IsEmpty => rootServiceIdentities.Count == 0;

        static RuntimeIdentityRef[] CloneIdentities(ReadOnlySpan<RuntimeIdentityRef> source)
        {
            if (source.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] snapshot = new RuntimeIdentityRef[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                snapshot[index] = source[index];
            }

            return snapshot;
        }
    }

    public sealed class KernelRuntimeScopeGraph
    {
        readonly ReadOnlyCollection<RuntimeIdentityRef> rootScopeIdentities;

        public KernelRuntimeScopeGraph(ReadOnlySpan<RuntimeIdentityRef> rootScopeIdentities)
        {
            RuntimeIdentityRef[] snapshot = CloneIdentities(rootScopeIdentities);
            this.rootScopeIdentities = Array.AsReadOnly(snapshot);
        }

        public IReadOnlyList<RuntimeIdentityRef> RootScopeIdentities => rootScopeIdentities;

        public int RootScopeCount => rootScopeIdentities.Count;

        public bool IsEmpty => rootScopeIdentities.Count == 0;

        static RuntimeIdentityRef[] CloneIdentities(ReadOnlySpan<RuntimeIdentityRef> source)
        {
            if (source.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] snapshot = new RuntimeIdentityRef[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                snapshot[index] = source[index];
            }

            return snapshot;
        }
    }

    public sealed class KernelBootRuntimeSurface : IKernelBootRuntimeSurface
    {
        public KernelBootRuntimeSurface(KernelRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public KernelRuntime Runtime { get; }
    }

    public sealed class KernelBootRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
    {
        public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return new KernelBootRuntimeSurface(new KernelRuntime(context));
        }
    }
}