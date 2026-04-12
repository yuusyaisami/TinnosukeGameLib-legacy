#nullable enable
using System;
using Game.Common;
using Game.DI;
using UnityEngine;

namespace Game.Channel
{
    internal readonly struct GridObjectChannelResolvedPresetState
    {
        public GridObjectChannelResolvedPresetState(
            GridObjectChannelPlayerPresetBase playerPreset,
            GridObjectChannelLayoutPreset layoutPreset,
            GridObjectChannelVisualizerPreset visualizerPreset,
            BaseRuntimeTemplateSO? runtimeTemplate,
            bool forceFullRebuild)
        {
            PlayerPreset = playerPreset;
            LayoutPreset = layoutPreset;
            VisualizerPreset = visualizerPreset;
            RuntimeTemplate = runtimeTemplate;
            ForceFullRebuild = forceFullRebuild;
        }

        public GridObjectChannelPlayerPresetBase PlayerPreset { get; }
        public GridObjectChannelLayoutPreset LayoutPreset { get; }
        public GridObjectChannelVisualizerPreset VisualizerPreset { get; }
        public BaseRuntimeTemplateSO? RuntimeTemplate { get; }
        public bool ForceFullRebuild { get; }
    }

    internal sealed class GridObjectChannelPresetResolver
    {
        readonly GridObjectChannelDefinition _definition;
        readonly Action<GridObjectChannelRefreshMode> _queueRefresh;

        public GridObjectChannelPresetResolver(
            GridObjectChannelDefinition definition,
            Action<GridObjectChannelRefreshMode> queueRefresh)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _queueRefresh = queueRefresh ?? throw new ArgumentNullException(nameof(queueRefresh));
        }

        public bool TryResolve(
            GridObjectChannelRuntimeState state,
            IDynamicContext dynamicContext,
            out GridObjectChannelResolvedPresetState resolved,
            out string? error)
        {
            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Resolve preset sources. Tag='{state.ChannelTag}' " +
                    $"DefinitionLayoutHasSource={_definition.LayoutPresetValue.HasSource} DefinitionLayoutSourceType={_definition.LayoutPresetValue.SourceTypeName} DefinitionLayoutSource={_definition.LayoutPresetValue.SourceDebugData} " +
                    $"BindLayoutOverride={state.BindRequest.OverrideLayoutPreset} BindLayoutHasSource={state.BindRequest.LayoutPresetValue.HasSource} BindLayoutSourceType={state.BindRequest.LayoutPresetValue.SourceTypeName} BindLayoutSource={state.BindRequest.LayoutPresetValue.SourceDebugData}",
                    state.ListRoot);
            }

            var playerPreset = _definition.PlayerPresetValue.GetOrDefault(
                dynamicContext,
                new GridObjectChannelStandalonePlayerPreset())?.CreateRuntimeCopy() ?? new GridObjectChannelStandalonePlayerPreset();
            if (state.BindRequest.OverridePlayerPreset)
            {
                playerPreset = state.BindRequest.PlayerPresetValue.GetOrDefault(
                    dynamicContext,
                    new GridObjectChannelStandalonePlayerPreset())?.CreateRuntimeCopy() ?? new GridObjectChannelStandalonePlayerPreset();
            }

            var layoutPreset = _definition.LayoutPresetValue.GetOrDefault(dynamicContext, new GridObjectChannelLayoutPreset()).CreateRuntimeCopy();
            if (state.BindRequest.OverrideLayoutPreset)
                layoutPreset = state.BindRequest.LayoutPresetValue.GetOrDefault(dynamicContext, new GridObjectChannelLayoutPreset()).CreateRuntimeCopy();

            if (state.BindRequest.ForceChoiceCompatible)
            {
                var choiceItemCount = state.ActiveChoiceEntries?.Count ?? 0;
                if (choiceItemCount > 0)
                    layoutPreset = layoutPreset.CreateChoiceRuntimeCopy(choiceItemCount);
            }

            var visualizerPreset = _definition.VisualizerPresetValue.GetOrDefault(dynamicContext, new GridObjectChannelVisualizerPreset()).CreateRuntimeCopy();
            if (state.BindRequest.OverrideVisualizerPreset)
                visualizerPreset = state.BindRequest.VisualizerPresetValue.GetOrDefault(dynamicContext, new GridObjectChannelVisualizerPreset()).CreateRuntimeCopy();

            if (state.BindRequest.ForceChoiceCompatible)
                visualizerPreset = visualizerPreset.CreateChoiceRuntimeCopy();

            if (state.BindRequest.SpawnCommands != null && state.BindRequest.SpawnCommands.Count > 0)
                visualizerPreset.SpawnCommands.AddRuntimeCommands(state.BindRequest.SpawnCommands);

            BaseRuntimeTemplateSO? runtimeTemplate = null;
            if (!visualizerPreset.TryResolveRuntimeTemplate(dynamicContext, out runtimeTemplate) || runtimeTemplate == null)
                Debug.LogWarning("[GridObjectChannel] RuntimeTemplate could not be resolved.");

            var previousPlayerType = state.ResolvedPlayerPreset?.GetType();
            var previousRuntimeTemplate = state.ResolvedRuntimeTemplate;
            var forceFullRebuild = previousPlayerType != null &&
                                   (previousPlayerType != playerPreset.GetType() ||
                                    !ReferenceEquals(previousRuntimeTemplate, runtimeTemplate));

            var recreateRuntime = state.ItemSourceRuntime == null ||
                                  state.ItemSourceRuntime.Preset.GetType() != playerPreset.GetType();
            if (recreateRuntime)
            {
                state.ItemSourceRuntime?.Dispose();
                state.ItemSourceRuntime = GridObjectChannelPlayerRuntimeFactory.Create(playerPreset);
            }

            if (state.ItemSourceRuntime == null || state.ActiveScope == null)
            {
                error = "Player runtime is null.";
                resolved = default;
                return false;
            }

            var sourceContext = new GridObjectChannelSourceContext(dynamicContext, state.ActiveScope, _queueRefresh);
            if (!state.ItemSourceRuntime.TryResolve(sourceContext, out error))
            {
                resolved = default;
                return false;
            }

            resolved = new GridObjectChannelResolvedPresetState(
                playerPreset,
                layoutPreset,
                visualizerPreset,
                runtimeTemplate,
                forceFullRebuild);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Resolved preset copy. Tag='{state.ChannelTag}' LayoutPreset={layoutPreset} VisualizerChoice={visualizerPreset.EnableChoiceInput} RuntimeTemplate={(runtimeTemplate != null ? runtimeTemplate.name : "null")}",
                    state.ListRoot);
            }

            return true;
        }
    }
}
