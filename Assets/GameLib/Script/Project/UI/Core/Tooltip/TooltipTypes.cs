#nullable enable
using System;
using Game.Common;
using Game.Commands.VNext;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public readonly struct TooltipClampSettings
    {
        public readonly bool EnableClamp;
        public readonly float FlipThresholdX;
        public readonly float FlipThresholdY;

        public TooltipClampSettings(bool enableClamp, float flipThresholdX, float flipThresholdY)
        {
            EnableClamp = enableClamp;
            FlipThresholdX = flipThresholdX;
            FlipThresholdY = flipThresholdY;
        }

        public static TooltipClampSettings Default => new(true, 0.2f, 0.2f);
    }

    public interface ITooltipSystemService
    {
        RectTransform TooltipRoot { get; }
        Transform? WorldRoot { get; }
        RectTransform? ClampArea { get; }
        TooltipChannelInputMode InputMode { get; }
        TooltipClampSettings ClampSettings { get; }
        int SpawnWarmupFrames { get; }
        TooltipSystemSharedDefaults SharedDefaults { get; }
        TooltipHubPreset SharedHubPreset { get; }
    }

    [Serializable]
    public sealed class TooltipSystemRuntimeDefaults
    {
        [BoxGroup("Runtime")]
        [LabelText("Runtime Template")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset;

        [BoxGroup("Runtime")]
        [LabelText("Spawner Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _spawnerTag = string.Empty;

        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePresetValue => _runtimeTemplatePreset;
        public string SpawnerTag => _spawnerTag ?? string.Empty;

        public TooltipSystemRuntimeDefaults CreateRuntimeCopy()
        {
            return new TooltipSystemRuntimeDefaults
            {
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _spawnerTag = _spawnerTag,
            };
        }
    }

    [Serializable]
    public sealed class TooltipSystemInputDefaults
    {
        [BoxGroup("Input")]
        [LabelText("Enable Pointer Hover")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enablePointerHover = true;

        [BoxGroup("Input")]
        [LabelText("Enable Selection Hover")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableSelectionHover = true;

        [BoxGroup("Input")]
        [LabelText("Hover Delay Seconds")]
        [MinValue(0d)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _hoverDelaySeconds = 0.4f;

        [BoxGroup("Input")]
        [LabelText("Selection Delay Seconds")]
        [MinValue(0d)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _selectionDelaySeconds = 0.3f;

        [BoxGroup("Input")]
        [LabelText("Pointer Move Threshold")]
        [MinValue(0d)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _pointerMoveThreshold = 2f;

        public bool EnablePointerHover => _enablePointerHover;
        public bool EnableSelectionHover => _enableSelectionHover;
        public float HoverDelaySeconds => Mathf.Max(0f, _hoverDelaySeconds);
        public float SelectionDelaySeconds => Mathf.Max(0f, _selectionDelaySeconds);
        public float PointerMoveThreshold => Mathf.Max(0f, _pointerMoveThreshold);

        public TooltipSystemInputDefaults CreateRuntimeCopy()
        {
            return new TooltipSystemInputDefaults
            {
                _enablePointerHover = _enablePointerHover,
                _enableSelectionHover = _enableSelectionHover,
                _hoverDelaySeconds = _hoverDelaySeconds,
                _selectionDelaySeconds = _selectionDelaySeconds,
                _pointerMoveThreshold = _pointerMoveThreshold,
            };
        }
    }

    [Serializable]
    public sealed class TooltipSystemPlacementDefaults
    {
        [BoxGroup("Placement")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Anchor Actor\", _anchorActorSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _anchorActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Placement")]
        [LabelText("Spawn Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TooltipChannelSpawnMode _spawnMode = TooltipChannelSpawnMode.FollowPointer;

        [BoxGroup("Placement")]
        [ShowIf(nameof(IsFollowPointer))]
        [LabelText("Follow Pointer Direction Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector3 _followPointerDirectionOffset = Vector3.zero;

        [BoxGroup("Placement")]
        [ShowIf(nameof(IsFollowPointer))]
        [LabelText("Follow Pointer Move Scale")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector2 _followPointerMoveScale = Vector2.one;

        [BoxGroup("Placement")]
        [ShowIf(nameof(IsFixedOffset))]
        [LabelText("Fixed Direction Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector3 _fixedDirectionOffset = Vector3.zero;

        [BoxGroup("Placement")]
        [LabelText("Anchor X")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TooltipChannelAnchorX _anchorX = TooltipChannelAnchorX.Right;

        [BoxGroup("Placement")]
        [LabelText("Anchor Y")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TooltipChannelAnchorY _anchorY = TooltipChannelAnchorY.Up;

        public ActorSource AnchorActorSource => _anchorActorSource;
        public TooltipChannelSpawnMode SpawnMode => _spawnMode;
        public Vector3 FollowPointerDirectionOffset => _followPointerDirectionOffset;
        public Vector2 FollowPointerMoveScale => _followPointerMoveScale;
        public Vector3 FixedDirectionOffset => _fixedDirectionOffset;
        public TooltipChannelAnchorX AnchorX => _anchorX;
        public TooltipChannelAnchorY AnchorY => _anchorY;

        public TooltipSystemPlacementDefaults CreateRuntimeCopy()
        {
            return new TooltipSystemPlacementDefaults
            {
                _anchorActorSource = _anchorActorSource,
                _spawnMode = _spawnMode,
                _followPointerDirectionOffset = _followPointerDirectionOffset,
                _followPointerMoveScale = _followPointerMoveScale,
                _fixedDirectionOffset = _fixedDirectionOffset,
                _anchorX = _anchorX,
                _anchorY = _anchorY,
            };
        }

        bool IsFollowPointer => _spawnMode == TooltipChannelSpawnMode.FollowPointer;
        bool IsFixedOffset => _spawnMode == TooltipChannelSpawnMode.FixedOffset;
    }

    [Serializable]
    public sealed class TooltipSystemSharedDefaults
    {
        [BoxGroup("Shared Defaults")]
        [LabelText("Runtime")]
        [InlineProperty]
        [SerializeField]
        TooltipSystemRuntimeDefaults _runtimeDefaults = new();

        [BoxGroup("Shared Defaults")]
        [LabelText("Input")]
        [InlineProperty]
        [SerializeField]
        TooltipSystemInputDefaults _inputDefaults = new();

        [BoxGroup("Shared Defaults")]
        [LabelText("Placement")]
        [InlineProperty]
        [SerializeField]
        TooltipSystemPlacementDefaults _placementDefaults = new();

        [BoxGroup("Shared Defaults")]
        [LabelText("Hub")]
        [InlineProperty]
        [SerializeField]
        TooltipHubPreset _hubPreset = new();

        public TooltipSystemRuntimeDefaults RuntimeDefaults => _runtimeDefaults;
        public TooltipSystemInputDefaults InputDefaults => _inputDefaults;
        public TooltipSystemPlacementDefaults PlacementDefaults => _placementDefaults;
        public TooltipHubPreset HubPreset => _hubPreset;

        public TooltipSystemSharedDefaults CreateRuntimeCopy()
        {
            return new TooltipSystemSharedDefaults
            {
                _runtimeDefaults = _runtimeDefaults.CreateRuntimeCopy(),
                _inputDefaults = _inputDefaults.CreateRuntimeCopy(),
                _placementDefaults = _placementDefaults.CreateRuntimeCopy(),
                _hubPreset = _hubPreset.CreateRuntimeCopy(),
            };
        }
    }
}
