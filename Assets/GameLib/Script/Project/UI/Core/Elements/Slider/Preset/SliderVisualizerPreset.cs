#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [System.Serializable]
    public sealed class SliderBackgroundVisualizerSettings
    {
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _templatePreset;

        [ShowIf(nameof(_enabled))]
        [LabelText("Animation Channel Tag")]
        [SerializeField]
        string _animationChannelTag = "default";

        [ShowIf(nameof(_enabled))]
        [LabelText("Allow Pooling")]
        [SerializeField]
        bool _allowPooling = true;

        [ShowIf(nameof(_enabled))]
        [LabelText("Depth Offset")]
        [MinValue(0f)]
        [SerializeField]
        float _depthOffset = 0.01f;

        [ShowIf(nameof(_enabled))]
        [LabelText("On Background Spawn")]
        [SerializeField]
        [CommandListFunctionName("Slider.Background.OnSpawn")]
        CommandListData _onSpawnCommands = new();

        [ShowIf(nameof(_enabled))]
        [LabelText("Hide When Fill Is Min")]
        [SerializeField]
        bool _hideWhenFillIsMin;

        public bool Enabled => _enabled;
        public DynamicValue<BaseRuntimeTemplatePreset> TemplatePreset => _templatePreset;
        public string AnimationChannelTag => string.IsNullOrWhiteSpace(_animationChannelTag) ? "default" : _animationChannelTag.Trim();
        public bool AllowPooling => _allowPooling;
        public float DepthOffset => Mathf.Max(0f, _depthOffset);
        public CommandListData OnSpawnCommands => _onSpawnCommands;
        public bool HideWhenFillIsMin => _hideWhenFillIsMin;

        internal SliderBackgroundVisualizerSettings CreateRuntimeCopy()
        {
            return new SliderBackgroundVisualizerSettings
            {
                _enabled = _enabled,
                _templatePreset = _templatePreset,
                _animationChannelTag = _animationChannelTag,
                _allowPooling = _allowPooling,
                _depthOffset = _depthOffset,
                _onSpawnCommands = CloneCommandList(_onSpawnCommands),
                _hideWhenFillIsMin = _hideWhenFillIsMin,
            };
        }

        internal void ApplyMutation(
            SliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyBackground)
            {
                _enabled = mutation.BackgroundEnabled;
                _templatePreset = mutation.BackgroundTemplatePreset;
                _animationChannelTag = mutation.BackgroundAnimationChannelTag;
                _allowPooling = mutation.BackgroundAllowPooling;
                _depthOffset = mutation.BackgroundDepthOffset;
                _hideWhenFillIsMin = mutation.BackgroundHideWhenFillIsMin;
            }

            if (mutation.ApplyBackgroundSpawnCommands)
            {
                _onSpawnCommands ??= new CommandListData();
                _onSpawnCommands.ApplyRuntimeMutation(mutation.BackgroundSpawnCommands, mutationService);
            }
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public abstract class SliderSegmentEntryBase
    {
        public abstract float ResolveRawValue(IDynamicContext context, float minValue, float maxValue);
        public abstract void WritePayloadVars(IVarStore vars, int entryIndex, float entryRawValue, float entryNormalizedValue);
        public abstract string GetDebugLabel();
        public abstract CommandListData? GetCommands(SliderSegmentCrossingDirection direction);
        public abstract SliderSegmentEntryBase CreateRuntimeCopy();
    }

    [System.Serializable]
    public sealed class SliderCommandSegmentEntry : SliderSegmentEntryBase
    {
        [LabelText("Raw Value")]
        [SerializeField]
        float _rawValue;

        [LabelText("Display Label")]
        [SerializeField]
        string _displayLabel = string.Empty;

        [LabelText("On Reach Up")]
        [SerializeField]
        [CommandListFunctionName("Slider.Entry.OnReachUp")]
        CommandListData _onReachUpCommands = new();

        [LabelText("On Reach Down")]
        [SerializeField]
        [CommandListFunctionName("Slider.Entry.OnReachDown")]
        CommandListData _onReachDownCommands = new();

        public override float ResolveRawValue(IDynamicContext context, float minValue, float maxValue)
        {
            _ = context;
            _ = minValue;
            _ = maxValue;
            return _rawValue;
        }

        public override void WritePayloadVars(IVarStore vars, int entryIndex, float entryRawValue, float entryNormalizedValue)
        {
            _ = vars;
            _ = entryIndex;
            _ = entryRawValue;
            _ = entryNormalizedValue;
        }

        public override string GetDebugLabel()
        {
            if (!string.IsNullOrWhiteSpace(_displayLabel))
                return _displayLabel;

            return _rawValue.ToString("0.###");
        }

        public override CommandListData? GetCommands(SliderSegmentCrossingDirection direction)
        {
            return direction == SliderSegmentCrossingDirection.Increase
                ? _onReachUpCommands
                : _onReachDownCommands;
        }

        public override SliderSegmentEntryBase CreateRuntimeCopy()
        {
            return new SliderCommandSegmentEntry
            {
                _rawValue = _rawValue,
                _displayLabel = _displayLabel,
                _onReachUpCommands = CloneCommandList(_onReachUpCommands),
                _onReachDownCommands = CloneCommandList(_onReachDownCommands),
            };
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class SliderSegmentedVisualizerSettings
    {
        [BoxGroup("Layout")]
        [LabelText("Fill Axis")]
        [SerializeField]
        SliderAreaFillAxis _fillAxis = SliderAreaFillAxis.SizeX;

        [BoxGroup("Layout")]
        [LabelText("Origin Side")]
        [SerializeField]
        SliderAreaOriginSide _originSide = SliderAreaOriginSide.Min;

        [BoxGroup("Layout")]
        [LabelText("Placement Mode")]
        [SerializeField]
        SliderSegmentPlacementMode _placementMode = SliderSegmentPlacementMode.CustomEntries;

        [BoxGroup("Layout")]
        [ShowIf(nameof(IsEqualInterval))]
        [LabelText("Interval Step")]
        [SerializeField]
        DynamicValue<float> _intervalStep = DynamicValueExtensions.FromLiteral(10f);

        [BoxGroup("Layout")]
        [LabelText("Split Bars By Layout")]
        [SerializeField]
        bool _splitBarsByLayout = true;

        [BoxGroup("Layout")]
        [ShowIf(nameof(ShouldShowBarSpanScale))]
        [LabelText("Bar Span Scale")]
        [MinValue(0f)]
        [SerializeField]
        DynamicValue<float> _barSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Runtime")]
        [LabelText("Spawn Segment Bars")]
        [SerializeField]
        bool _spawnSegmentBars = true;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnSegmentBars))]
        [LabelText("Segment Bar Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _segmentBarTemplatePreset;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnSegmentBars))]
        [LabelText("Animation Channel Tag")]
        [SerializeField]
        string _segmentBarAnimationChannelTag = "default";

        [BoxGroup("Runtime")]
        [LabelText("Spawn Markers")]
        [SerializeField]
        bool _spawnMarkers = true;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnMarkers))]
        [LabelText("Marker Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _markerTemplatePreset;

        [BoxGroup("Runtime")]
        [LabelText("Allow Pooling")]
        [SerializeField]
        bool _allowPooling = true;

        [BoxGroup("Runtime")]
        [LabelText("On Segment Bar Spawn")]
        [SerializeField]
        [CommandListFunctionName("Slider.Segmented.OnBarSpawn")]
        CommandListData _onSegmentBarSpawnCommands = new();

        [BoxGroup("Runtime")]
        [LabelText("On Marker Spawn")]
        [SerializeField]
        [CommandListFunctionName("Slider.Segmented.OnMarkerSpawn")]
        CommandListData _onMarkerSpawnCommands = new();

        [BoxGroup("Entries")]
        [ShowIf(nameof(IsCustomEntries))]
        [LabelText("Entries")]
        [SerializeReference]
        List<SliderSegmentEntryBase> _entries = new();

        public SliderAreaFillAxis FillAxis => _fillAxis;
        public SliderAreaOriginSide OriginSide => _originSide;
        public SliderSegmentPlacementMode PlacementMode => _placementMode;
        public DynamicValue<float> IntervalStep => _intervalStep;
        public bool SplitBarsByLayout => _splitBarsByLayout;
        public DynamicValue<float> BarSpanScale => _barSpanScale;
        public bool SpawnSegmentBars => _spawnSegmentBars;
        public DynamicValue<BaseRuntimeTemplatePreset> SegmentBarTemplatePreset => _segmentBarTemplatePreset;
        public string SegmentBarAnimationChannelTag => string.IsNullOrWhiteSpace(_segmentBarAnimationChannelTag) ? "default" : _segmentBarAnimationChannelTag.Trim();
        public bool SpawnMarkers => _spawnMarkers;
        public DynamicValue<BaseRuntimeTemplatePreset> MarkerTemplatePreset => _markerTemplatePreset;
        public bool AllowPooling => _allowPooling;
        public CommandListData OnSegmentBarSpawnCommands => _onSegmentBarSpawnCommands;
        public CommandListData OnMarkerSpawnCommands => _onMarkerSpawnCommands;
        public IReadOnlyList<SliderSegmentEntryBase> Entries => _entries;

        bool IsEqualInterval() => _placementMode == SliderSegmentPlacementMode.EqualInterval;
        bool IsCustomEntries() => _placementMode == SliderSegmentPlacementMode.CustomEntries;
        bool ShouldShowBarSpanScale() => _splitBarsByLayout;

        internal SliderSegmentedVisualizerSettings CreateRuntimeCopy()
        {
            var copy = new SliderSegmentedVisualizerSettings
            {
                _fillAxis = _fillAxis,
                _originSide = _originSide,
                _placementMode = _placementMode,
                _intervalStep = _intervalStep,
                _splitBarsByLayout = _splitBarsByLayout,
                _barSpanScale = _barSpanScale,
                _spawnSegmentBars = _spawnSegmentBars,
                _segmentBarTemplatePreset = _segmentBarTemplatePreset,
                _segmentBarAnimationChannelTag = _segmentBarAnimationChannelTag,
                _spawnMarkers = _spawnMarkers,
                _markerTemplatePreset = _markerTemplatePreset,
                _allowPooling = _allowPooling,
                _onSegmentBarSpawnCommands = CloneCommandList(_onSegmentBarSpawnCommands),
                _onMarkerSpawnCommands = CloneCommandList(_onMarkerSpawnCommands),
            };

            if (_entries != null)
            {
                copy._entries = new List<SliderSegmentEntryBase>(_entries.Count);
                for (var i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i] == null)
                        continue;
                    copy._entries.Add(_entries[i].CreateRuntimeCopy());
                }
            }

            return copy;
        }

        internal void ApplyMutation(
            SliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplySegmentedLayout)
            {
                _fillAxis = mutation.SegmentedFillAxis;
                _originSide = mutation.SegmentedOriginSide;
                _placementMode = mutation.SegmentedPlacementMode;
                _intervalStep = mutation.SegmentedIntervalStep;
                _splitBarsByLayout = mutation.SegmentedSplitBarsByLayout;
                _barSpanScale = mutation.SegmentedBarSpanScale;
            }

            if (mutation.ApplySegmentedRuntime)
            {
                _spawnSegmentBars = mutation.SpawnSegmentBars;
                _segmentBarTemplatePreset = mutation.SegmentBarTemplatePreset;
                _segmentBarAnimationChannelTag = mutation.SegmentBarAnimationChannelTag;
                _spawnMarkers = mutation.SpawnMarkers;
                _markerTemplatePreset = mutation.MarkerTemplatePreset;
                _allowPooling = mutation.SegmentedAllowPooling;
            }

            if (mutation.ApplySegmentBarSpawnCommands)
            {
                _onSegmentBarSpawnCommands ??= new CommandListData();
                _onSegmentBarSpawnCommands.ApplyRuntimeMutation(mutation.SegmentBarSpawnCommands, mutationService);
            }

            if (mutation.ApplyMarkerSpawnCommands)
            {
                _onMarkerSpawnCommands ??= new CommandListData();
                _onMarkerSpawnCommands.ApplyRuntimeMutation(mutation.MarkerSpawnCommands, mutationService);
            }
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class SliderHandleVisualizerSettings
    {
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _templatePreset;

        [ShowIf(nameof(_enabled))]
        [LabelText("Animation Channel Tag")]
        [SerializeField]
        string _animationChannelTag = "default";

        [ShowIf(nameof(_enabled))]
        [LabelText("Allow Pooling")]
        [SerializeField]
        bool _allowPooling = true;

        [ShowIf(nameof(_enabled))]
        [LabelText("On Handle Spawn")]
        [SerializeField]
        [CommandListFunctionName("Slider.Handle.OnSpawn")]
        CommandListData _onSpawnCommands = new();

        public bool Enabled => _enabled;
        public DynamicValue<BaseRuntimeTemplatePreset> TemplatePreset => _templatePreset;
        public string AnimationChannelTag => string.IsNullOrWhiteSpace(_animationChannelTag) ? "default" : _animationChannelTag.Trim();
        public bool AllowPooling => _allowPooling;
        public CommandListData OnSpawnCommands => _onSpawnCommands;

        internal SliderHandleVisualizerSettings CreateRuntimeCopy()
        {
            return new SliderHandleVisualizerSettings
            {
                _enabled = _enabled,
                _templatePreset = _templatePreset,
                _animationChannelTag = _animationChannelTag,
                _allowPooling = _allowPooling,
                _onSpawnCommands = CloneCommandList(_onSpawnCommands),
            };
        }

        internal void ApplyMutation(
            SliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyHandle)
            {
                _enabled = mutation.HandleEnabled;
                _templatePreset = mutation.HandleTemplatePreset;
                _animationChannelTag = mutation.HandleAnimationChannelTag;
                _allowPooling = mutation.HandleAllowPooling;
            }

            if (mutation.ApplyHandleSpawnCommands)
            {
                _onSpawnCommands ??= new CommandListData();
                _onSpawnCommands.ApplyRuntimeMutation(mutation.HandleSpawnCommands, mutationService);
            }
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class SliderVisualizerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Background")]
        [InlineProperty]
        [SerializeField]
        SliderBackgroundVisualizerSettings _background = new();

        [BoxGroup("Segmented")]
        [InlineProperty]
        [SerializeField]
        SliderSegmentedVisualizerSettings _segmented = new();

        [BoxGroup("Handle")]
        [InlineProperty]
        [SerializeField]
        SliderHandleVisualizerSettings _handle = new();

        public SliderBackgroundVisualizerSettings Background => _background;
        public SliderSegmentedVisualizerSettings Segmented => _segmented;
        public SliderHandleVisualizerSettings Handle => _handle;

        internal SliderVisualizerPreset CreateRuntimeCopy()
        {
            return new SliderVisualizerPreset
            {
                _background = _background?.CreateRuntimeCopy() ?? new SliderBackgroundVisualizerSettings(),
                _segmented = _segmented?.CreateRuntimeCopy() ?? new SliderSegmentedVisualizerSettings(),
                _handle = _handle?.CreateRuntimeCopy() ?? new SliderHandleVisualizerSettings(),
            };
        }

        internal void ApplyMutation(
            SliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            _background ??= new SliderBackgroundVisualizerSettings();
            _segmented ??= new SliderSegmentedVisualizerSettings();
            _handle ??= new SliderHandleVisualizerSettings();
            _background.ApplyMutation(mutation, mutationService);
            _segmented.ApplyMutation(mutation, mutationService);
            _handle.ApplyMutation(mutation, mutationService);
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/Slider/Visualizer Preset",
        fileName = "SliderVisualizerPreset")]
    public sealed class SliderVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<SliderVisualizerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        SliderVisualizerPreset? _preset = new();

        public SliderVisualizerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            _preset ??= new SliderVisualizerPreset();
        }
    }

    [System.Serializable]
    public sealed class SliderVisualizerRuntimeMutation
    {
        [BoxGroup("Background")]
        [ToggleLeft]
        [LabelText("Apply Background")]
        public bool ApplyBackground;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Enabled")]
        public bool BackgroundEnabled;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> BackgroundTemplatePreset;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Animation Channel Tag")]
        public string BackgroundAnimationChannelTag = "default";

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Allow Pooling")]
        public bool BackgroundAllowPooling = true;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Depth Offset")]
        [MinValue(0f)]
        public float BackgroundDepthOffset = 0.01f;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackground))]
        [LabelText("Hide When Fill Is Min")]
        public bool BackgroundHideWhenFillIsMin;

        [BoxGroup("Background")]
        [ToggleLeft]
        [LabelText("Apply Background Spawn Commands")]
        public bool ApplyBackgroundSpawnCommands;

        [BoxGroup("Background")]
        [ShowIf(nameof(ApplyBackgroundSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep BackgroundSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Segmented Layout")]
        [ToggleLeft]
        [LabelText("Apply Segmented Layout")]
        public bool ApplySegmentedLayout;

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Fill Axis")]
        public SliderAreaFillAxis SegmentedFillAxis = SliderAreaFillAxis.SizeX;

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Origin Side")]
        public SliderAreaOriginSide SegmentedOriginSide = SliderAreaOriginSide.Min;

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Placement Mode")]
        public SliderSegmentPlacementMode SegmentedPlacementMode = SliderSegmentPlacementMode.CustomEntries;

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Interval Step")]
        public DynamicValue<float> SegmentedIntervalStep = DynamicValueExtensions.FromLiteral(10f);

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Split Bars By Layout")]
        public bool SegmentedSplitBarsByLayout = true;

        [BoxGroup("Segmented Layout")]
        [ShowIf(nameof(ShouldShowSegmentedBarSpanScale))]
        [MinValue(0f)]
        [LabelText("Bar Span Scale")]
        public DynamicValue<float> SegmentedBarSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Segmented Runtime")]
        [ToggleLeft]
        [LabelText("Apply Segmented Runtime")]
        public bool ApplySegmentedRuntime;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Spawn Segment Bars")]
        public bool SpawnSegmentBars = true;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Segment Bar Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> SegmentBarTemplatePreset;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Animation Channel Tag")]
        public string SegmentBarAnimationChannelTag = "default";

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Spawn Markers")]
        public bool SpawnMarkers = true;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Marker Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> MarkerTemplatePreset;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Allow Pooling")]
        public bool SegmentedAllowPooling = true;

        [BoxGroup("Segmented Runtime")]
        [ToggleLeft]
        [LabelText("Apply Segment Bar Spawn Commands")]
        public bool ApplySegmentBarSpawnCommands;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplySegmentBarSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep SegmentBarSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Segmented Runtime")]
        [ToggleLeft]
        [LabelText("Apply Marker Spawn Commands")]
        public bool ApplyMarkerSpawnCommands;

        [BoxGroup("Segmented Runtime")]
        [ShowIf(nameof(ApplyMarkerSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep MarkerSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Handle")]
        [ToggleLeft]
        [LabelText("Apply Handle")]
        public bool ApplyHandle;

        [BoxGroup("Handle")]
        [ShowIf(nameof(ApplyHandle))]
        [LabelText("Enabled")]
        public bool HandleEnabled;

        [BoxGroup("Handle")]
        [ShowIf(nameof(ApplyHandle))]
        [LabelText("Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> HandleTemplatePreset;

        [BoxGroup("Handle")]
        [ShowIf(nameof(ApplyHandle))]
        [LabelText("Animation Channel Tag")]
        public string HandleAnimationChannelTag = "default";

        [BoxGroup("Handle")]
        [ShowIf(nameof(ApplyHandle))]
        [LabelText("Allow Pooling")]
        public bool HandleAllowPooling = true;

        [BoxGroup("Handle")]
        [ToggleLeft]
        [LabelText("Apply Handle Spawn Commands")]
        public bool ApplyHandleSpawnCommands;

        [BoxGroup("Handle")]
        [ShowIf(nameof(ApplyHandleSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep HandleSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        public bool HasAnyMutation()
        {
            return ApplyBackground ||
                   ApplyBackgroundSpawnCommands ||
                   ApplySegmentedLayout ||
                   ApplySegmentedRuntime ||
                   ApplySegmentBarSpawnCommands ||
                   ApplyMarkerSpawnCommands ||
                   ApplyHandle ||
                   ApplyHandleSpawnCommands;
        }

        bool ShouldShowSegmentedBarSpanScale()
        {
            return ApplySegmentedLayout && SegmentedSplitBarsByLayout;
        }
    }
}
