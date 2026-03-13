#nullable enable
using System;
using System.Collections.Generic;

namespace Game.MaterialFx
{
    /// <summary>
    /// Read-only telemetry API for debugging/inspector.
    /// Keep this separate from IMaterialFxService to avoid polluting runtime call sites.
    /// </summary>
    public interface IMaterialFxTelemetry
    {
        /// <summary>
        /// Optional identifier for diagnostics (e.g., owner GameObject / channel tag).
        /// </summary>
        string TelemetryId { get; }

        /// <summary>
        /// Fills dst with a snapshot of all stacks/layers.
        /// </summary>
        void GetSnapshot(List<MaterialFxStackTelemetry> dst);
    }

    [Serializable]
    public sealed class MaterialFxStackTelemetry
    {
        public string StableKey = string.Empty;
        public ValueKind ValueType;
        public int LayerCount;
        public List<MaterialFxLayerTelemetry> Layers = new();
    }

    [Serializable]
    public sealed class MaterialFxLayerTelemetry
    {
        public string ContextTag = string.Empty;
        public int Priority;
        public MaterialFxBlendMode BlendMode;
        public string CurrentValue = string.Empty;

        public bool IsValueFading;
        public bool IsWeightFading;

        public float RemainingLifetimeSeconds;
    }

    internal interface IMaterialFxLayerTelemetry
    {
        void GetTelemetrySnapshot(List<MaterialFxStackTelemetry> dst);
    }
}
