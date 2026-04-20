#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum UIScrollBarSourceMode
    {
        ExistingScope = 10,
        RuntimeTemplate = 20,
    }

    public enum UIScrollBarRangeSourceMode
    {
        TrackRect = 10,
        RectTransform = 20,
        AreaChannel = 30,
    }

    public enum UIScrollRectMovementType
    {
        Clamped = 10,
        Elastic = 20,
    }

    public enum UIScrollRectAxisKind
    {
        Horizontal = 10,
        Vertical = 20,
    }

    public readonly struct UIScrollRectSnapshot
    {
        public readonly Vector2 NormalizedPosition;
        public readonly Vector2 Velocity;
        public readonly Vector2 ViewportSize;
        public readonly Vector2 ContentSize;
        public readonly bool HorizontalVisible;
        public readonly bool VerticalVisible;

        public UIScrollRectSnapshot(
            Vector2 normalizedPosition,
            Vector2 velocity,
            Vector2 viewportSize,
            Vector2 contentSize,
            bool horizontalVisible,
            bool verticalVisible)
        {
            NormalizedPosition = normalizedPosition;
            Velocity = velocity;
            ViewportSize = viewportSize;
            ContentSize = contentSize;
            HorizontalVisible = horizontalVisible;
            VerticalVisible = verticalVisible;
        }
    }

    public interface IUIScrollRectService
    {
        UIScrollRectSnapshot Snapshot { get; }
        void RefreshLayout();
        bool SetNormalizedPosition(Vector2 value);
        bool SetHorizontalNormalized(float value);
        bool SetVerticalNormalized(float value);
    }

    public interface IUIScrollBarBindingService
    {
        RectTransform? TrackRect { get; }
        RectTransform? HandleRect { get; }
        GameObject? VisibilityRoot { get; }
        IButtonChannelHubService? ButtonChannelHub { get; }
    }

    [Serializable]
    public sealed class UIScrollRectAxisPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Axis")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("ScrollBar")]
        [ShowIf(nameof(CanEditScrollBarSource))]
        [LabelText("ScrollBar Source")]
        [SerializeField]
        UIScrollBarSourceMode _scrollBarSourceMode = UIScrollBarSourceMode.ExistingScope;

        [BoxGroup("ScrollBar")]
        [ShowIf(nameof(CanEditExistingScrollBarSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"ScrollBar Source\", _scrollBarActorSource)")]
        [SerializeField]
        ActorSource _scrollBarActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("ScrollBar")]
        [ShowIf(nameof(CanEditRuntimeTemplate))]
        [LabelText("Runtime Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset =
            DynamicValue<BaseRuntimeTemplatePreset>.FromSource(
                new ManagedRefLiteralSource<BaseRuntimeTemplatePreset>(new BaseRuntimeTemplatePreset()));

        [BoxGroup("Range")]
        [ShowIf(nameof(CanEditRangeSource))]
        [LabelText("Range Source")]
        [SerializeField]
        UIScrollBarRangeSourceMode _rangeSourceMode = UIScrollBarRangeSourceMode.TrackRect;

        [BoxGroup("Range")]
        [ShowIf(nameof(CanEditRangeRectTransform))]
        [LabelText("Range RectTransform")]
        [SerializeField]
        RectTransform? _rangeRectTransform;

        [BoxGroup("Range")]
        [ShowIf(nameof(CanEditAreaActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Range")]
        [ShowIf(nameof(CanEditAreaChannelTag))]
        [LabelText("Area Channel Tag")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Display")]
        [ShowIf(nameof(CanEditAutoHide))]
        [LabelText("Auto Hide")]
        [SerializeField]
        bool _autoHide = true;

        public bool Enabled => _enabled;
        public UIScrollBarSourceMode ScrollBarSourceMode => _scrollBarSourceMode;
        public ActorSource ScrollBarActorSource => _scrollBarActorSource;
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePresetValue => _runtimeTemplatePreset;
        public UIScrollBarRangeSourceMode RangeSourceMode => _rangeSourceMode;
        public RectTransform? RangeRectTransform => _rangeRectTransform;
        public ActorSource AreaActorSource => _areaActorSource;
        public string AreaChannelTag => string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim();
        public bool AutoHide => _autoHide;

        bool CanEditScrollBarSource() => _enabled;
        bool CanEditExistingScrollBarSource() => _enabled && _scrollBarSourceMode == UIScrollBarSourceMode.ExistingScope;
        bool CanEditRuntimeTemplate() => _enabled && _scrollBarSourceMode == UIScrollBarSourceMode.RuntimeTemplate;
        bool CanEditRangeSource() => _enabled;
        bool CanEditRangeRectTransform() => _enabled && _rangeSourceMode == UIScrollBarRangeSourceMode.RectTransform;
        bool CanEditAreaActorSource() => _enabled && _rangeSourceMode == UIScrollBarRangeSourceMode.AreaChannel;
        bool CanEditAreaChannelTag() => _enabled && _rangeSourceMode == UIScrollBarRangeSourceMode.AreaChannel;
        bool CanEditAutoHide() => _enabled;

        public UIScrollRectAxisPreset CreateRuntimeCopy()
        {
            return new UIScrollRectAxisPreset
            {
                _enabled = _enabled,
                _scrollBarSourceMode = _scrollBarSourceMode,
                _scrollBarActorSource = _scrollBarActorSource,
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _rangeSourceMode = _rangeSourceMode,
                _rangeRectTransform = _rangeRectTransform,
                _areaActorSource = _areaActorSource,
                _areaChannelTag = _areaChannelTag,
                _autoHide = _autoHide,
            };
        }
    }

    [Serializable]
    public sealed class UIScrollRectInteractionPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Wheel")]
        [MinValue(0f)]
        [LabelText("Wheel Sensitivity")]
        [SerializeField]
        float _wheelSensitivity = 0.1f;

        [BoxGroup("Page")]
        [MinValue(0f)]
        [LabelText("Page Size")]
        [SerializeField]
        float _pageSize = 0.9f;

        [BoxGroup("Page")]
        [MinValue(0f)]
        [LabelText("Page Repeat Delay")]
        [SerializeField]
        float _pageRepeatDelay = 0.35f;

        [BoxGroup("Page")]
        [MinValue(0.001f)]
        [LabelText("Page Repeat Interval")]
        [SerializeField]
        float _pageRepeatInterval = 0.08f;

        [BoxGroup("Swipe")]
        [MinValue(0f)]
        [LabelText("Swipe Threshold Pixels")]
        [SerializeField]
        float _swipeThresholdPixels = 12f;

        [BoxGroup("Swipe")]
        [MinValue(0f)]
        [LabelText("Drag Sensitivity")]
        [SerializeField]
        float _dragSensitivity = 1f;

        [BoxGroup("Swipe")]
        [Range(0f, 1f)]
        [LabelText("Over Drag Damping")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _overDragDamping = 0.75f;

        public float WheelSensitivity => Mathf.Max(0f, _wheelSensitivity);
        public float PageSize => Mathf.Max(0f, _pageSize);
        public float PageRepeatDelay => Mathf.Max(0f, _pageRepeatDelay);
        public float PageRepeatInterval => Mathf.Max(0.001f, _pageRepeatInterval);
        public float SwipeThresholdPixels => Mathf.Max(0f, _swipeThresholdPixels);
        public float DragSensitivity => Mathf.Max(0f, _dragSensitivity);
        public float OverDragDamping => Mathf.Clamp01(_overDragDamping);

        public UIScrollRectInteractionPreset CreateRuntimeCopy()
        {
            return new UIScrollRectInteractionPreset
            {
                _wheelSensitivity = _wheelSensitivity,
                _pageSize = _pageSize,
                _pageRepeatDelay = _pageRepeatDelay,
                _pageRepeatInterval = _pageRepeatInterval,
                _swipeThresholdPixels = _swipeThresholdPixels,
                _dragSensitivity = _dragSensitivity,
                _overDragDamping = _overDragDamping,
            };
        }
    }

    [Serializable]
    public sealed class UIScrollRectPhysicsPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Physics")]
        [LabelText("Movement Type")]
        [SerializeField]
        UIScrollRectMovementType _movementType = UIScrollRectMovementType.Elastic;

        [BoxGroup("Physics")]
        [LabelText("Inertia")]
        [SerializeField]
        bool _inertia = true;

        [BoxGroup("Physics")]
        [ShowIf(nameof(_inertia))]
        [MinValue(0f)]
        [LabelText("Deceleration Rate")]
        [SerializeField]
        float _decelerationRate = 0.135f;

        [BoxGroup("Physics")]
        [ShowIf(nameof(UsesElasticMovement))]
        [MinValue(0.001f)]
        [LabelText("Elasticity")]
        [SerializeField]
        float _elasticity = 0.1f;

        public UIScrollRectMovementType MovementType => _movementType;
        public bool Inertia => _inertia;
        public float DecelerationRate => Mathf.Max(0f, _decelerationRate);
        public float Elasticity => Mathf.Max(0.001f, _elasticity);

        bool UsesElasticMovement() => _movementType == UIScrollRectMovementType.Elastic;

        public UIScrollRectPhysicsPreset CreateRuntimeCopy()
        {
            return new UIScrollRectPhysicsPreset
            {
                _movementType = _movementType,
                _inertia = _inertia,
                _decelerationRate = _decelerationRate,
                _elasticity = _elasticity,
            };
        }
    }

    [Serializable]
    public sealed class UIScrollRectPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Axis")]
        [LabelText("Horizontal")]
        [InlineProperty]
        [SerializeField]
        UIScrollRectAxisPreset _horizontal = new();

        [BoxGroup("Axis")]
        [LabelText("Vertical")]
        [InlineProperty]
        [SerializeField]
        UIScrollRectAxisPreset _vertical = new();

        [BoxGroup("Interaction")]
        [ShowIf(nameof(HasAnyAxisEnabled))]
        [LabelText("Interaction")]
        [InlineProperty]
        [SerializeField]
        UIScrollRectInteractionPreset _interaction = new();

        [BoxGroup("Physics")]
        [ShowIf(nameof(HasAnyAxisEnabled))]
        [LabelText("Physics")]
        [InlineProperty]
        [SerializeField]
        UIScrollRectPhysicsPreset _physics = new();

        public UIScrollRectAxisPreset Horizontal => _horizontal;
        public UIScrollRectAxisPreset Vertical => _vertical;
        public UIScrollRectInteractionPreset Interaction => _interaction;
        public UIScrollRectPhysicsPreset Physics => _physics;

        bool HasAnyAxisEnabled() => _horizontal.Enabled || _vertical.Enabled;

        public UIScrollRectPreset CreateRuntimeCopy()
        {
            return new UIScrollRectPreset
            {
                _horizontal = _horizontal.CreateRuntimeCopy(),
                _vertical = _vertical.CreateRuntimeCopy(),
                _interaction = _interaction.CreateRuntimeCopy(),
                _physics = _physics.CreateRuntimeCopy(),
            };
        }
    }

    [CreateAssetMenu(menuName = "Game/UI/ScrollRect/Preset", fileName = "UIScrollRectPreset")]
    public sealed class UIScrollRectPresetSO : ScriptableObject, IDynamicValueAsset<UIScrollRectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [SerializeField]
        UIScrollRectPreset? _preset = new();

        public UIScrollRectPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable() => EnsurePreset();
        void OnValidate() => EnsurePreset();

        void EnsurePreset()
        {
            _preset ??= new UIScrollRectPreset();
        }
    }
}
