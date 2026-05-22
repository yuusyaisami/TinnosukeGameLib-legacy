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
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var state = new MeshRuntimeDefinitionState
            {
                RenderPipeline = ResolveRenderPipeline(preset.RenderPipeline, context),
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
            if (authored == null)
                throw new ArgumentNullException(nameof(authored));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var key = NormalizeKey(authored.Key, nameof(authored.Key));
            var tag = NormalizeKey(authored.Tag, nameof(authored.Tag));
            var runtime = new MeshRegularTrackRuntimeState
            {
                Key = key,
                Tag = tag,
                Priority = authored.Priority,
                RequestedEnabled = authored.Enabled,
                PlayerPreset = ResolvePlayerPreset(authored.Player, context),
                VisualizerPreset = ResolveVisualizerPreset(authored.Visualizer, context),
                ColliderPreset = ResolveColliderPreset(authored.Collider, context),
                MaterialPreset = ResolveMaterialPreset(authored.Material, context),
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
            if (authored == null)
                throw new ArgumentNullException(nameof(authored));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var key = NormalizeKey(authored.Key, nameof(authored.Key));
            var runtime = new MeshSimulationTrackRuntimeState
            {
                Key = key,
                Priority = authored.Priority,
                Enabled = authored.Enabled,
                Preset = ResolveSimulationPreset(authored.Preset, context),
            };
            runtime.Runtime = CreateSimulationRuntime(runtime.Preset);
            return runtime;
        }

        public static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            throw new InvalidOperationException($"Mesh channel scope '{scope.GetType().Name}' is missing an IVarStore resolver.");
        }

        public static MeshDefinitionPreset ResolveDefinition(DynamicValue<MeshDefinitionPreset> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshDefinitionPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh definition preset could not be resolved.");
        }

        public static MeshTrackPlayerPresetBase ResolvePlayerPreset(DynamicValue<MeshTrackPlayerPresetBase> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshTrackPlayerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh track player preset could not be resolved.");
        }

        public static MeshTrackVisualizerPresetBase ResolveVisualizerPreset(DynamicValue<MeshTrackVisualizerPresetBase> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshTrackVisualizerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh track visualizer preset could not be resolved.");
        }

        public static MeshTrackColliderPresetBase ResolveColliderPreset(DynamicValue<MeshTrackColliderPresetBase> value, IDynamicContext context)
        {
            if (!value.HasSource)
                throw new InvalidOperationException("Mesh track collider preset has no authored source.");

            var variant = value.Evaluate(context);
            if (variant.IsNull)
                return new MeshNoColliderTrackColliderPreset();

            if (variant.TryGet(out MeshTrackColliderPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh track collider preset could not be resolved.");
        }

        public static MeshTrackMaterialPreset ResolveMaterialPreset(DynamicValue<MeshTrackMaterialPreset> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshTrackMaterialPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh track material preset could not be resolved.");
        }

        public static bool EvaluateConditionEnabled(MeshTrackPlayerPresetBase preset, IDynamicContext context, bool fallback)
        {
            if (preset is MeshLineTrackPlayerPreset line)
            {
                if (line.Condition.TryGet(context, out bool enabled))
                    return enabled;

                return fallback;
            }

            if (preset is MeshTargetLinkTrackPlayerPreset targetLink)
            {
                if (targetLink.Condition.TryGet(context, out bool enabled))
                    return enabled;

                return fallback;
            }

            return true;
        }

        static MeshRenderPipelinePreset ResolveRenderPipeline(DynamicValue<MeshRenderPipelinePreset> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshRenderPipelinePreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh render pipeline preset could not be resolved.");
        }

        public static MeshSimulationPresetBase ResolveSimulationPreset(DynamicValue<MeshSimulationPresetBase> value, IDynamicContext context)
        {
            if (value.HasSource && value.TryGet(context, out MeshSimulationPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            throw new InvalidOperationException("Mesh simulation preset could not be resolved.");
        }

        public static IMeshTrackPlayerRuntime CreatePlayerRuntime(MeshTrackPlayerPresetBase preset)
        {
            return preset switch
            {
                MeshTargetLinkTrackPlayerPreset targetLink => new MeshTargetLinkTrackPlayerRuntime(targetLink),
                MeshTrailTrackPlayerPreset trail => new MeshTrailTrackPlayerRuntime(trail),
                MeshAreaFillTrackPlayerPreset area => new MeshAreaFillTrackPlayerRuntime(area),
                MeshLineTrackPlayerPreset line => new MeshLineTrackPlayerRuntime(line),
                _ => throw new InvalidOperationException($"Unsupported mesh track player preset type '{preset?.GetType().FullName ?? "<null>"}'."),
            };
        }

        public static IMeshTrackVisualizerRuntime CreateVisualizerRuntime(MeshTrackVisualizerPresetBase preset)
        {
            return preset switch
            {
                MeshAreaFillTrackVisualizerPreset area => new MeshAreaFillTrackVisualizerRuntime(area),
                MeshLineTrackVisualizerPreset line => new MeshLineTrackVisualizerRuntime(line),
                _ => throw new InvalidOperationException($"Unsupported mesh track visualizer preset type '{preset?.GetType().FullName ?? "<null>"}'."),
            };
        }

        public static IMeshTrackColliderRuntime CreateColliderRuntime(MeshTrackColliderPresetBase preset)
        {
            return preset switch
            {
                MeshNoColliderTrackColliderPreset => new MeshNoColliderTrackColliderRuntime(),
                MeshPolygonTrackColliderPreset polygon => new MeshPolygonTrackColliderRuntime(polygon),
                _ => throw new InvalidOperationException($"Unsupported mesh track collider preset type '{preset?.GetType().FullName ?? "<null>"}'."),
            };
        }

        public static IMeshSimulationTrackRuntime CreateSimulationRuntime(MeshSimulationPresetBase preset)
        {
            return preset switch
            {
                MeshClayPersistentSimulationPreset persistent => new MeshClayPersistentSimulationRuntime(persistent),
                MeshFluidSimulationPreset fluid => new MeshFluidSimulationRuntime(fluid),
                MeshClayTransientSimulationPreset clay => new MeshClayTransientSimulationRuntime(clay),
                _ => throw new InvalidOperationException($"Unsupported mesh simulation preset type '{preset?.GetType().FullName ?? "<null>"}'."),
            };
        }

        static string NormalizeKey(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Mesh channel runtime state requires a non-empty {parameterName}.");

            return value.Trim();
        }
    }
}
