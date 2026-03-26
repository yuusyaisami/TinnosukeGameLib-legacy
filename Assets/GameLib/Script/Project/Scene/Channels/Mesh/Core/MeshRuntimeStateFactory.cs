#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using VContainer;

namespace Game.Channel
{
    static class MeshRuntimeStateFactory
    {
        public static MeshRuntimeDefinitionState BuildRuntimeState(MeshDefinitionPreset preset, IDynamicContext context)
        {
            var state = new MeshRuntimeDefinitionState
            {
                RenderPipeline = ResolveRenderPipeline(preset.RenderPipeline, context, new MeshRenderPipelinePreset()),
            };

            for (var i = 0; i < preset.RegularTracks.Count; i++)
            {
                var runtime = ResolveRegularTrack(preset.RegularTracks[i], context);
                state.RegularTracksByKey[runtime.Key] = runtime;
            }

            for (var i = 0; i < preset.SimulationTracks.Count; i++)
            {
                var runtime = ResolveSimulationTrack(preset.SimulationTracks[i], context);
                state.SimulationTracksByKey[runtime.Key] = runtime;
            }

            return state;
        }

        public static MeshRuntimeDefinitionState CloneRuntimeState(MeshRuntimeDefinitionState source)
        {
            var clone = new MeshRuntimeDefinitionState
            {
                RenderPipeline = source.RenderPipeline.CreateRuntimeCopy(),
            };

            foreach (var pair in source.RegularTracksByKey)
            {
                var src = pair.Value;
                var copy = new MeshRegularTrackRuntimeState
                {
                    Key = src.Key,
                    Tag = src.Tag,
                    Priority = src.Priority,
                    RequestedEnabled = src.RequestedEnabled,
                    ConditionEnabled = src.ConditionEnabled,
                    Enabled = src.Enabled,
                    PlayerPreset = src.PlayerPreset.CreateRuntimeCopy(),
                    VisualizerPreset = src.VisualizerPreset.CreateRuntimeCopy(),
                    ColliderPreset = src.ColliderPreset.CreateRuntimeCopy(),
                    MaterialPreset = src.MaterialPreset.CreateRuntimeCopy(),
                };
                copy.PlayerRuntime = CreatePlayerRuntime(copy.PlayerPreset);
                copy.VisualizerRuntime = CreateVisualizerRuntime(copy.VisualizerPreset);
                copy.ColliderRuntime = CreateColliderRuntime(copy.ColliderPreset);
                clone.RegularTracksByKey[pair.Key] = copy;
            }

            foreach (var pair in source.SimulationTracksByKey)
            {
                var src = pair.Value;
                var copy = new MeshSimulationTrackRuntimeState
                {
                    Key = src.Key,
                    Priority = src.Priority,
                    Enabled = src.Enabled,
                    Preset = src.Preset.CreateRuntimeCopy(),
                };
                copy.Runtime = CreateSimulationRuntime(copy.Preset);
                clone.SimulationTracksByKey[pair.Key] = copy;
            }

            return clone;
        }

        public static MeshRegularTrackRuntimeState ResolveRegularTrack(MeshTrackDefinition authored, IDynamicContext context)
        {
            var key = string.IsNullOrWhiteSpace(authored.Key) ? Guid.NewGuid().ToString("N") : authored.Key.Trim();
            var runtime = new MeshRegularTrackRuntimeState
            {
                Key = key,
                Tag = string.IsNullOrWhiteSpace(authored.Tag) ? key : authored.Tag.Trim(),
                Priority = authored.Priority,
                RequestedEnabled = authored.Enabled,
                PlayerPreset = ResolvePlayerPreset(authored.Player, context, new MeshLineTrackPlayerPreset()),
                VisualizerPreset = ResolveVisualizerPreset(authored.Visualizer, context, new MeshLineTrackVisualizerPreset()),
                ColliderPreset = ResolveColliderPreset(authored.Collider, context, new MeshPolygonTrackColliderPreset()),
                MaterialPreset = ResolveMaterialPreset(authored.Material, context, new MeshTrackMaterialPreset()),
            };
            runtime.ConditionEnabled = EvaluateConditionEnabled(runtime.PlayerPreset, context, runtime.ConditionEnabled);
            runtime.RecalculateEnabled();

            runtime.PlayerRuntime = CreatePlayerRuntime(runtime.PlayerPreset);
            runtime.VisualizerRuntime = CreateVisualizerRuntime(runtime.VisualizerPreset);
            runtime.ColliderRuntime = CreateColliderRuntime(runtime.ColliderPreset);
            return runtime;
        }

        public static MeshSimulationTrackRuntimeState ResolveSimulationTrack(MeshSimulationTrackDefinition authored, IDynamicContext context)
        {
            var key = string.IsNullOrWhiteSpace(authored.Key) ? Guid.NewGuid().ToString("N") : authored.Key.Trim();
            var runtime = new MeshSimulationTrackRuntimeState
            {
                Key = key,
                Priority = authored.Priority,
                Enabled = authored.Enabled,
                Preset = ResolveSimulationPreset(authored.Preset, context, new MeshClayTransientSimulationPreset()),
            };
            runtime.Runtime = CreateSimulationRuntime(runtime.Preset);
            return runtime;
        }

        public static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        public static MeshDefinitionPreset ResolveDefinition(DynamicValue<MeshDefinitionPreset> value, IDynamicContext context, MeshDefinitionPreset fallback)
        {
            if (value.TryGet(context, out MeshDefinitionPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static MeshTrackPlayerPresetBase ResolvePlayerPreset(DynamicValue<MeshTrackPlayerPresetBase> value, IDynamicContext context, MeshTrackPlayerPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackPlayerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static MeshTrackVisualizerPresetBase ResolveVisualizerPreset(DynamicValue<MeshTrackVisualizerPresetBase> value, IDynamicContext context, MeshTrackVisualizerPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackVisualizerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static MeshTrackColliderPresetBase ResolveColliderPreset(DynamicValue<MeshTrackColliderPresetBase> value, IDynamicContext context, MeshTrackColliderPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackColliderPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static MeshTrackMaterialPreset ResolveMaterialPreset(DynamicValue<MeshTrackMaterialPreset> value, IDynamicContext context, MeshTrackMaterialPreset fallback)
        {
            if (value.TryGet(context, out MeshTrackMaterialPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static bool EvaluateConditionEnabled(MeshTrackPlayerPresetBase preset, IDynamicContext context, bool fallback)
        {
            if (preset is not MeshLineTrackPlayerPreset line)
                return true;

            if (line.Condition.TryGet(context, out bool enabled))
                return enabled;

            return fallback;
        }

        static MeshRenderPipelinePreset ResolveRenderPipeline(DynamicValue<MeshRenderPipelinePreset> value, IDynamicContext context, MeshRenderPipelinePreset fallback)
        {
            if (value.TryGet(context, out MeshRenderPipelinePreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static MeshSimulationPresetBase ResolveSimulationPreset(DynamicValue<MeshSimulationPresetBase> value, IDynamicContext context, MeshSimulationPresetBase fallback)
        {
            if (value.TryGet(context, out MeshSimulationPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        public static IMeshTrackPlayerRuntime CreatePlayerRuntime(MeshTrackPlayerPresetBase preset)
        {
            return preset switch
            {
                MeshTrailTrackPlayerPreset trail => new MeshTrailTrackPlayerRuntime(trail),
                MeshAreaFillTrackPlayerPreset area => new MeshAreaFillTrackPlayerRuntime(area),
                MeshLineTrackPlayerPreset line => new MeshLineTrackPlayerRuntime(line),
                _ => new MeshLineTrackPlayerRuntime(new MeshLineTrackPlayerPreset()),
            };
        }

        public static IMeshTrackVisualizerRuntime CreateVisualizerRuntime(MeshTrackVisualizerPresetBase preset)
        {
            return preset switch
            {
                MeshAreaFillTrackVisualizerPreset area => new MeshAreaFillTrackVisualizerRuntime(area),
                MeshLineTrackVisualizerPreset line => new MeshLineTrackVisualizerRuntime(line),
                _ => new MeshLineTrackVisualizerRuntime(new MeshLineTrackVisualizerPreset()),
            };
        }

        public static IMeshTrackColliderRuntime CreateColliderRuntime(MeshTrackColliderPresetBase preset)
        {
            return preset switch
            {
                MeshPolygonTrackColliderPreset polygon => new MeshPolygonTrackColliderRuntime(polygon),
                _ => new MeshPolygonTrackColliderRuntime(new MeshPolygonTrackColliderPreset()),
            };
        }

        public static IMeshSimulationTrackRuntime CreateSimulationRuntime(MeshSimulationPresetBase preset)
        {
            return preset switch
            {
                MeshClayPersistentSimulationPreset persistent => new MeshClayPersistentSimulationRuntime(persistent),
                MeshFluidSimulationPreset fluid => new MeshFluidSimulationRuntime(fluid),
                MeshClayTransientSimulationPreset clay => new MeshClayTransientSimulationRuntime(clay),
                _ => new MeshClayTransientSimulationRuntime(new MeshClayTransientSimulationPreset()),
            };
        }
    }
}
