#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Channel
{
    sealed class MeshRegularTrackRuntimeState
    {
        public string Key = string.Empty;
        public string Tag = string.Empty;
        public int Priority;
        public bool RequestedEnabled = true;
        public bool ConditionEnabled = true;
        public bool Enabled = true;
        public MeshTrackPlayerPresetBase PlayerPreset = new MeshLineTrackPlayerPreset();
        public MeshTrackVisualizerPresetBase VisualizerPreset = new MeshLineTrackVisualizerPreset();
        public MeshTrackColliderPresetBase ColliderPreset = new MeshPolygonTrackColliderPreset();
        public MeshTrackMaterialPreset MaterialPreset = new();
        public IMeshTrackPlayerRuntime PlayerRuntime = new MeshLineTrackPlayerRuntime(new MeshLineTrackPlayerPreset());
        public IMeshTrackVisualizerRuntime VisualizerRuntime = new MeshLineTrackVisualizerRuntime(new MeshLineTrackVisualizerPreset());
        public IMeshTrackColliderRuntime ColliderRuntime = new MeshPolygonTrackColliderRuntime(new MeshPolygonTrackColliderPreset());

        public void RecalculateEnabled()
        {
            Enabled = RequestedEnabled && ConditionEnabled;
        }
    }

    sealed class MeshSimulationTrackRuntimeState
    {
        public string Key = string.Empty;
        public int Priority;
        public bool Enabled;
        public MeshSimulationPresetBase Preset = new MeshClayTransientSimulationPreset();
        public IMeshSimulationTrackRuntime Runtime = new MeshClayTransientSimulationRuntime(new MeshClayTransientSimulationPreset());
    }

    sealed class MeshRuntimeDefinitionState
    {
        public MeshRenderPipelinePreset RenderPipeline = new();
        public readonly Dictionary<string, MeshRegularTrackRuntimeState> RegularTracksByKey = new(StringComparer.Ordinal);
        public readonly Dictionary<string, MeshSimulationTrackRuntimeState> SimulationTracksByKey = new(StringComparer.Ordinal);

        public List<MeshRegularTrackRuntimeState> GetSortedRegularTracks()
        {
            var result = new List<MeshRegularTrackRuntimeState>(RegularTracksByKey.Values);
            result.Sort(static (a, b) =>
            {
                var priority = b.Priority.CompareTo(a.Priority);
                if (priority != 0)
                    return priority;
                return StringComparer.Ordinal.Compare(a.Key, b.Key);
            });
            return result;
        }

        public List<MeshSimulationTrackRuntimeState> GetSortedSimulationTracks()
        {
            var result = new List<MeshSimulationTrackRuntimeState>(SimulationTracksByKey.Values);
            result.Sort(static (a, b) =>
            {
                var priority = b.Priority.CompareTo(a.Priority);
                if (priority != 0)
                    return priority;
                return StringComparer.Ordinal.Compare(a.Key, b.Key);
            });
            return result;
        }
    }

    sealed class MeshCompositeDraft
    {
        public string Tag = string.Empty;
        public int HighestPriority = int.MinValue;
        public MeshTrackMaterialPreset MaterialPreset = new();
        public MeshPolygonTrackColliderPreset ColliderPreset = new();
        public readonly List<MeshRuntimePath> Paths = new();
    }

    readonly struct MeshRegularTrackContributor
    {
        public readonly MeshRegularTrackRuntimeState Track;
        public readonly List<MeshRuntimePath> Paths;

        public MeshRegularTrackContributor(MeshRegularTrackRuntimeState track, List<MeshRuntimePath> sourcePaths)
        {
            Track = track;
            Paths = new List<MeshRuntimePath>(sourcePaths.Count);
            for (var i = 0; i < sourcePaths.Count; i++)
                Paths.Add(sourcePaths[i].Clone());
        }
    }
}
