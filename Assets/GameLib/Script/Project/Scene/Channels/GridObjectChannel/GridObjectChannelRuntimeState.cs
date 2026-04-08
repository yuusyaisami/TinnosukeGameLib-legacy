#nullable enable
using System.Collections.Generic;
using System.Threading;
using Game.Commands.VNext;
using Game.DI;
using Game.UI;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class GridObjectChannelRuntimeState
    {
        readonly GridObjectChannelDefinition _definition;

        public GridObjectChannelRuntimeState(GridObjectChannelDefinition definition)
        {
            _definition = definition;
        }

        public readonly GridObjectChannelVisualCollection Visuals = new();
        public readonly AsyncLocal<int> OperationContextStamp = new();

        public CancellationTokenSource? LifecycleCts;
        public GridObjectChannelBindRequest BindRequest = new();
        public GridObjectChannelPlayerPresetBase ResolvedPlayerPreset = new GridObjectChannelStandalonePlayerPreset();
        public GridObjectChannelLayoutPreset ResolvedLayoutPreset = new();
        public GridObjectChannelVisualizerPreset ResolvedVisualizerPreset = new();
        public BaseRuntimeTemplateSO? ResolvedRuntimeTemplate;
        public IGridObjectChannelItemSourceRuntime? ItemSourceRuntime;
        public IScopeNode? ActiveScope;
        public Transform? ListRoot;
        public Transform? LayoutReferenceTransform;
        public RectTransform? LayoutRectTransform;
        public Canvas? Canvas;
        public ActorSourceResolveCache LayoutAreaSourceCache;
        public ActorSourceResolveCache FixedAnchorSourceCache;
        public TransformGridEnvironmentKind EnvironmentKind;
        public bool HasBinding;
        public bool IsBuilt;
        public bool IsActive;
        public IReadOnlyList<GridObjectChoiceEntry>? ActiveChoiceEntries;

        public bool EnableDebugLog => _definition.EnableDebugLog;
        public bool EnableVerboseLayoutLog => _definition.EnableVerboseLayoutLog;
        public bool EnableVerboseBlackboardLog => _definition.EnableVerboseBlackboardLog;

        public void ResetResolvedState()
        {
            ResolvedPlayerPreset = new GridObjectChannelStandalonePlayerPreset();
            ResolvedLayoutPreset = new GridObjectChannelLayoutPreset();
            ResolvedVisualizerPreset = new GridObjectChannelVisualizerPreset();
            ResolvedRuntimeTemplate = null;
        }
    }
}
