#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum VisualBoundsReactiveOutputKind
    {
        RectTransform = 10,
        Channel = 20,
    }

    public enum VisualBoundsReactiveSpriteApplyMode
    {
        SpriteOnly = 10,
        TransformOnly = 20,
        Both = 30,
    }

    [Serializable]
    public sealed class VisualBoundsReactiveInputEffectPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Input Effect")]
        [LabelText("Offset")]
        [SerializeField]
        Vector3 _offset;

        [BoxGroup("Input Effect")]
        [LabelText("Expand Left")]
        [SerializeField]
        float _expandLeft;

        [BoxGroup("Input Effect")]
        [LabelText("Expand Right")]
        [SerializeField]
        float _expandRight;

        [BoxGroup("Input Effect")]
        [LabelText("Expand Top")]
        [SerializeField]
        float _expandTop;

        [BoxGroup("Input Effect")]
        [LabelText("Expand Bottom")]
        [SerializeField]
        float _expandBottom;

        public Vector3 Offset => _offset;
        public float ExpandLeft => _expandLeft;
        public float ExpandRight => _expandRight;
        public float ExpandTop => _expandTop;
        public float ExpandBottom => _expandBottom;

        public Rect ApplyToLocalRect(in Rect localRect)
        {
            var center = localRect.center;
            center += new Vector2(_offset.x, _offset.y);

            var size = localRect.size;
            var expandedSize = new Vector2(
                Mathf.Max(0f, size.x + _expandLeft + _expandRight),
                Mathf.Max(0f, size.y + _expandTop + _expandBottom));

            center += new Vector2(
                (_expandRight - _expandLeft) * 0.5f,
                (_expandTop - _expandBottom) * 0.5f);

            return new Rect(center - expandedSize * 0.5f, expandedSize);
        }

        public Bounds ApplyToWorldBounds(in Bounds worldBounds)
        {
            var center = worldBounds.center + _offset;
            var size = worldBounds.size;

            size.x = Mathf.Max(0f, size.x + _expandLeft + _expandRight);
            size.y = Mathf.Max(0f, size.y + _expandTop + _expandBottom);

            center.x += (_expandRight - _expandLeft) * 0.5f;
            center.y += (_expandTop - _expandBottom) * 0.5f;

            return new Bounds(center, size);
        }

        public VisualBoundsReactiveInputEffectPreset CreateRuntimeCopy()
        {
            return new VisualBoundsReactiveInputEffectPreset
            {
                _offset = _offset,
                _expandLeft = _expandLeft,
                _expandRight = _expandRight,
                _expandTop = _expandTop,
                _expandBottom = _expandBottom,
            };
        }
    }

    [Serializable]
    public sealed class VisualBoundsReactiveTargetBinding : IDynamicManagedRefValue
    {
        [BoxGroup("Target")]
        [LabelText("Use Actor Source")]
        [SerializeField]
        bool _useActorSource;

        [BoxGroup("Target")]
        [ShowIf(nameof(_useActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor Source\", _actorSource)")]
        [SerializeField]
        ActorSource _actorSource = new() { Kind = ActorSourceKind.Current };

        public bool UseActorSource => _useActorSource;
        public ActorSource ActorSource => _actorSource;

        public VisualBoundsReactiveTargetBinding CreateRuntimeCopy()
        {
            return new VisualBoundsReactiveTargetBinding
            {
                _useActorSource = _useActorSource,
                _actorSource = _actorSource,
            };
        }
    }

    [Serializable]
    public abstract class VisualBoundsReactiveOutputPreset : IDynamicManagedRefValue
    {
        public abstract VisualBoundsReactiveOutputKind OutputKind { get; }
        public abstract VisualBoundsReactiveOutputPreset CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class VisualBoundsReactiveRectTransformOutputPreset : VisualBoundsReactiveOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target RectTransform")]
        [SerializeField]
        RectTransform? _targetRectTransform;

        [BoxGroup("Output")]
        [LabelText("Apply Anchored Position")]
        [SerializeField]
        bool _applyAnchoredPosition = true;

        [BoxGroup("Output")]
        [LabelText("Apply Size Delta")]
        [SerializeField]
        bool _applySizeDelta = true;

        public override VisualBoundsReactiveOutputKind OutputKind => VisualBoundsReactiveOutputKind.RectTransform;
        public RectTransform? TargetRectTransform => _targetRectTransform;
        public bool ApplyAnchoredPosition => _applyAnchoredPosition;
        public bool ApplySizeDelta => _applySizeDelta;

        public override VisualBoundsReactiveOutputPreset CreateRuntimeCopy()
        {
            return new VisualBoundsReactiveRectTransformOutputPreset
            {
                _targetRectTransform = _targetRectTransform,
                _applyAnchoredPosition = _applyAnchoredPosition,
                _applySizeDelta = _applySizeDelta,
            };
        }
    }

    [Serializable]
    public sealed class VisualBoundsReactiveChannelOutputPreset : VisualBoundsReactiveOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Sprite Mode")]
        [EnumToggleButtons]
        [SerializeField]
        VisualBoundsReactiveSpriteApplyMode _spriteMode = VisualBoundsReactiveSpriteApplyMode.Both;

        [BoxGroup("Output")]
        [LabelText("Sprite Channel Tag")]
        [SerializeField]
        string _spriteChannelTag = "default";

        [BoxGroup("Output")]
        [LabelText("Transform Channel Tag")]
        [SerializeField]
        string _transformChannelTag = "default";

        [BoxGroup("Output")]
        [LabelText("Force Sliced DrawMode")]
        [SerializeField]
        bool _forceSlicedSpriteRenderer = true;

        [BoxGroup("Output")]
        [LabelText("Apply Sprite Size")]
        [SerializeField]
        bool _applySpriteSize = true;

        [BoxGroup("Output")]
        [LabelText("Apply Transform Position")]
        [SerializeField]
        bool _applyTransformPosition = true;

        public override VisualBoundsReactiveOutputKind OutputKind => VisualBoundsReactiveOutputKind.Channel;
        public VisualBoundsReactiveSpriteApplyMode SpriteMode => _spriteMode;
        public string SpriteChannelTag => VisualBoundsReactiveTagUtility.Normalize(_spriteChannelTag);
        public string TransformChannelTag => VisualBoundsReactiveTagUtility.Normalize(_transformChannelTag);
        public bool ForceSlicedSpriteRenderer => _forceSlicedSpriteRenderer;
        public bool ApplySpriteSize => _applySpriteSize;
        public bool ApplyTransformPosition => _applyTransformPosition;

        public override VisualBoundsReactiveOutputPreset CreateRuntimeCopy()
        {
            return new VisualBoundsReactiveChannelOutputPreset
            {
                _spriteMode = _spriteMode,
                _spriteChannelTag = _spriteChannelTag,
                _transformChannelTag = _transformChannelTag,
                _forceSlicedSpriteRenderer = _forceSlicedSpriteRenderer,
                _applySpriteSize = _applySpriteSize,
                _applyTransformPosition = _applyTransformPosition,
            };
        }
    }

    [Serializable]
    public sealed class VisualBoundsReactiveChannelPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Preset")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("Preset")]
        [LabelText("Target")]
        [InlineProperty]
        [SerializeField]
        VisualBoundsReactiveTargetBinding _target = new();

        [BoxGroup("Preset")]
        [LabelText("Input Effect")]
        [InlineProperty]
        [SerializeField]
        VisualBoundsReactiveInputEffectPreset _inputEffect = new();

        [BoxGroup("Preset")]
        [LabelText("Output")]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        VisualBoundsReactiveOutputPreset _output = new VisualBoundsReactiveRectTransformOutputPreset();

        public bool Enabled => _enabled;
        public VisualBoundsReactiveTargetBinding Target => _target;
        public VisualBoundsReactiveInputEffectPreset InputEffect => _inputEffect;
        public VisualBoundsReactiveOutputPreset Output => _output ?? new VisualBoundsReactiveRectTransformOutputPreset();

        public VisualBoundsReactiveChannelPreset CreateRuntimeCopy()
        {
            return new VisualBoundsReactiveChannelPreset
            {
                _enabled = _enabled,
                _target = _target != null ? _target.CreateRuntimeCopy() : new VisualBoundsReactiveTargetBinding(),
                _inputEffect = _inputEffect != null ? _inputEffect.CreateRuntimeCopy() : new VisualBoundsReactiveInputEffectPreset(),
                _output = Output.CreateRuntimeCopy(),
            };
        }
    }

    [CreateAssetMenu(menuName = "Game/UI/Visual Bounds Reactive Channel Preset", fileName = "VisualBoundsReactiveChannelPreset")]
    public sealed class VisualBoundsReactiveChannelPresetSO : ScriptableObject, IDynamicValueAsset<VisualBoundsReactiveChannelPreset>
    {
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        VisualBoundsReactiveChannelPreset? _preset = new();

        public VisualBoundsReactiveChannelPreset? Preset
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
            _preset ??= new VisualBoundsReactiveChannelPreset();
        }
    }

    [Serializable]
    public sealed class VisualBoundsReactiveChannelOptions
    {
        public DynamicValue<VisualBoundsReactiveChannelPreset> PresetValue { get; set; } =
            DynamicValue<VisualBoundsReactiveChannelPreset>.FromSource(
                new ManagedRefLiteralSource<VisualBoundsReactiveChannelPreset>(new VisualBoundsReactiveChannelPreset()));
    }

    [Serializable]
    public sealed class VisualBoundsReactiveChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Preset")]
        [SerializeField]
        DynamicValue<VisualBoundsReactiveChannelPreset> _presetValue =
            DynamicValue<VisualBoundsReactiveChannelPreset>.FromSource(
                new ManagedRefLiteralSource<VisualBoundsReactiveChannelPreset>(new VisualBoundsReactiveChannelPreset()));

        public string ChannelTag => VisualBoundsReactiveTagUtility.Normalize(_channelTag);

        internal VisualBoundsReactiveChannelOptions CreateOptions()
        {
            return new VisualBoundsReactiveChannelOptions
            {
                PresetValue = _presetValue,
            };
        }
    }

    public interface IVisualBoundsReactiveHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        bool RegisterOrReplace(string tag, VisualBoundsReactiveChannelPreset preset);
        bool Unregister(string tag);
        void Clear();
        bool ResetRuntimeOverrides(string tag);
        void ResetAllRuntimeOverrides();
        void GetTags(List<string> output);
    }

    public static class VisualBoundsReactiveTagUtility
    {
        public static string Normalize(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}