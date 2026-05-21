#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Kernel.Abstractions
{
    public readonly struct KernelProfilerMarkerFamily : IEquatable<KernelProfilerMarkerFamily>
    {
        public KernelProfilerMarkerFamily(string taxonomyName, string markerPrefix)
        {
            if (string.IsNullOrWhiteSpace(taxonomyName))
                throw new ArgumentException("Taxonomy name must not be blank.", nameof(taxonomyName));

            if (string.IsNullOrWhiteSpace(markerPrefix))
                throw new ArgumentException("Marker prefix must not be blank.", nameof(markerPrefix));

            TaxonomyName = taxonomyName;
            MarkerPrefix = markerPrefix;
        }

        public string TaxonomyName { get; }

        public string MarkerPrefix { get; }

        public string CreateMarkerName(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Marker operation must not be blank.", nameof(operation));

            return MarkerPrefix + "." + operation;
        }

        public bool Equals(KernelProfilerMarkerFamily other)
        {
            return string.Equals(TaxonomyName, other.TaxonomyName, StringComparison.Ordinal)
                && string.Equals(MarkerPrefix, other.MarkerPrefix, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelProfilerMarkerFamily other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(TaxonomyName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(MarkerPrefix);
                return hash;
            }
        }

        public override string ToString()
        {
            return TaxonomyName + " -> " + MarkerPrefix;
        }

        public static bool operator ==(KernelProfilerMarkerFamily left, KernelProfilerMarkerFamily right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelProfilerMarkerFamily left, KernelProfilerMarkerFamily right)
        {
            return !left.Equals(right);
        }
    }

    public static class KernelProfilerMarkerTaxonomy
    {
        public static readonly KernelProfilerMarkerFamily Boot = new KernelProfilerMarkerFamily("Kernel.Boot", "KernelBoot");
        public static readonly KernelProfilerMarkerFamily ServiceGraph = new KernelProfilerMarkerFamily("Kernel.ServiceGraph", "ServiceGraph");
        public static readonly KernelProfilerMarkerFamily ScopeGraph = new KernelProfilerMarkerFamily("Kernel.ScopeGraph", "ScopeGraph");
        public static readonly KernelProfilerMarkerFamily Lifecycle = new KernelProfilerMarkerFamily("Kernel.Lifecycle", "Lifecycle");
        public static readonly KernelProfilerMarkerFamily CommandCatalog = new KernelProfilerMarkerFamily("Kernel.CommandCatalog", "CommandCatalog");
        public static readonly KernelProfilerMarkerFamily Command = new KernelProfilerMarkerFamily("Kernel.Command", "Command");
        public static readonly KernelProfilerMarkerFamily ValueStore = new KernelProfilerMarkerFamily("Kernel.ValueStore", "ValueStore");
        public static readonly KernelProfilerMarkerFamily RuntimeQuery = new KernelProfilerMarkerFamily("Kernel.RuntimeQuery", "RuntimeQuery");
        public static readonly KernelProfilerMarkerFamily DynamicEvaluation = new KernelProfilerMarkerFamily("Kernel.DynamicEvaluation", "DynamicEvaluation");
        public static readonly KernelProfilerMarkerFamily Diagnostics = new KernelProfilerMarkerFamily("Kernel.Diagnostics", "Diagnostics");
        public static readonly KernelProfilerMarkerFamily UnityBridge = new KernelProfilerMarkerFamily("Kernel.UnityBridge", "AuthoringBridge");
        public static readonly KernelProfilerMarkerFamily LegacyCompat = new KernelProfilerMarkerFamily("Kernel.LegacyCompat", "LegacyCompat");

        static readonly KernelProfilerMarkerFamily[] FamiliesSnapshot = new[]
        {
            Boot,
            ServiceGraph,
            ScopeGraph,
            Lifecycle,
            CommandCatalog,
            ValueStore,
            DynamicEvaluation,
            Diagnostics,
            UnityBridge,
            LegacyCompat,
        };

        public static IReadOnlyList<KernelProfilerMarkerFamily> Families => FamiliesSnapshot;
    }
}