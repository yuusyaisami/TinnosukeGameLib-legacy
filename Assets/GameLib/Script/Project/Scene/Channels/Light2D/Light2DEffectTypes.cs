#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class Light2DEffectEntry
    {
        [BoxGroup("Effect")]
        [LabelText("Effect Id")]
        [SerializeField]
        string _effectId = "default";

        [BoxGroup("Effect")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("Effect")]
        [LabelText("Priority")]
        [SerializeField]
        int _priority;

        [BoxGroup("Effect")]
        [LabelText("Blend Mode")]
        [SerializeField]
        Light2DEffectBlendMode _blendMode = Light2DEffectBlendMode.Override;

        [BoxGroup("Effect")]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        Light2DEffectPresetBase? _preset = new Light2DIntensityFlickerEffectPreset();

        [NonSerialized]
        internal float ElapsedTime;

        [NonSerialized]
        internal int Order;

        public string EffectId => string.IsNullOrWhiteSpace(_effectId) ? "default" : _effectId.Trim();
        public bool Enabled => _enabled;
        public int Priority => _priority;
        public Light2DEffectBlendMode BlendMode => _blendMode;
        public Light2DEffectPresetBase? Preset => _preset;

        public Light2DEffectEntry CreateRuntimeCopy()
        {
            return new Light2DEffectEntry
            {
                _effectId = EffectId,
                _enabled = _enabled,
                _priority = _priority,
                _blendMode = _blendMode,
                _preset = _preset?.CreateRuntimeCopy(),
                ElapsedTime = 0f,
                Order = Order,
            };
        }

        internal void SetRuntimeValues(
            string effectId,
            Light2DEffectPresetBase? preset,
            int priority,
            Light2DEffectBlendMode blendMode,
            bool enabled)
        {
            _effectId = string.IsNullOrWhiteSpace(effectId) ? "default" : effectId.Trim();
            _preset = preset?.CreateRuntimeCopy();
            _priority = priority;
            _blendMode = blendMode;
            _enabled = enabled;
            ElapsedTime = 0f;
        }

        internal void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }
    }

    [Serializable]
    public abstract class Light2DEffectPresetBase : IDynamicManagedRefValue
    {
        public abstract Light2DEffectPresetBase CreateRuntimeCopy();
        internal abstract Light2DContributionState Evaluate(in Light2DEffectEvaluationContext context);
        internal abstract bool ApplyMutation(Light2DEffectRuntimeMutationBase mutation);
    }

    [Serializable]
    public abstract class Light2DEffectRuntimeMutationBase : IDynamicManagedRefValue
    {
        public abstract bool HasAnyMutation();
    }

    [Serializable]
    public sealed class Light2DIntensityFlickerEffectPreset : Light2DEffectPresetBase
    {
        [BoxGroup("Flicker")]
        [LabelText("Base Multiplier")]
        [MinValue(0f)]
        [SerializeField]
        float _baseMultiplier = 1f;

        [BoxGroup("Flicker")]
        [LabelText("Amplitude")]
        [MinValue(0f)]
        [SerializeField]
        float _amplitude = 0.1f;

        [BoxGroup("Flicker")]
        [LabelText("Frequency")]
        [MinValue(0f)]
        [SerializeField]
        float _frequency = 6f;

        [BoxGroup("Flicker")]
        [LabelText("Noise Seed")]
        [SerializeField]
        float _noiseSeed = 17f;

        public override Light2DEffectPresetBase CreateRuntimeCopy()
        {
            return new Light2DIntensityFlickerEffectPreset
            {
                _baseMultiplier = Mathf.Max(0f, _baseMultiplier),
                _amplitude = Mathf.Max(0f, _amplitude),
                _frequency = Mathf.Max(0f, _frequency),
                _noiseSeed = _noiseSeed,
            };
        }

        internal override Light2DContributionState Evaluate(in Light2DEffectEvaluationContext context)
        {
            var time = Mathf.Max(0f, context.ElapsedTime);
            var sample = Mathf.PerlinNoise(_noiseSeed, time * Mathf.Max(0f, _frequency));
            var centered = (sample * 2f) - 1f;
            var multiplier = Mathf.Max(0f, _baseMultiplier + (_amplitude * centered));

            return new Light2DContributionState
            {
                HasIntensity = true,
                Intensity = multiplier,
            };
        }

        internal override bool ApplyMutation(Light2DEffectRuntimeMutationBase mutation)
        {
            if (mutation is not Light2DIntensityFlickerEffectMutation typed || !typed.HasAnyMutation())
                return false;

            if (typed.ApplyBaseMultiplier)
                _baseMultiplier = Mathf.Max(0f, typed.BaseMultiplier);
            if (typed.ApplyAmplitude)
                _amplitude = Mathf.Max(0f, typed.Amplitude);
            if (typed.ApplyFrequency)
                _frequency = Mathf.Max(0f, typed.Frequency);
            if (typed.ApplyNoiseSeed)
                _noiseSeed = typed.NoiseSeed;

            return true;
        }
    }

    [Serializable]
    public sealed class Light2DIntensityFlickerEffectMutation : Light2DEffectRuntimeMutationBase
    {
        [BoxGroup("Flicker")]
        [ToggleLeft]
        [LabelText("Apply Base Multiplier")]
        public bool ApplyBaseMultiplier;

        [BoxGroup("Flicker")]
        [ShowIf(nameof(ApplyBaseMultiplier))]
        [MinValue(0f)]
        public float BaseMultiplier = 1f;

        [BoxGroup("Flicker")]
        [ToggleLeft]
        [LabelText("Apply Amplitude")]
        public bool ApplyAmplitude;

        [BoxGroup("Flicker")]
        [ShowIf(nameof(ApplyAmplitude))]
        [MinValue(0f)]
        public float Amplitude = 0.1f;

        [BoxGroup("Flicker")]
        [ToggleLeft]
        [LabelText("Apply Frequency")]
        public bool ApplyFrequency;

        [BoxGroup("Flicker")]
        [ShowIf(nameof(ApplyFrequency))]
        [MinValue(0f)]
        public float Frequency = 6f;

        [BoxGroup("Flicker")]
        [ToggleLeft]
        [LabelText("Apply Noise Seed")]
        public bool ApplyNoiseSeed;

        [BoxGroup("Flicker")]
        [ShowIf(nameof(ApplyNoiseSeed))]
        public float NoiseSeed = 17f;

        public override bool HasAnyMutation()
        {
            return ApplyBaseMultiplier || ApplyAmplitude || ApplyFrequency || ApplyNoiseSeed;
        }
    }

    [Serializable]
    public sealed class Light2DColorSequenceEffectPreset : Light2DEffectPresetBase
    {
        [BoxGroup("Sequence")]
        [LabelText("Colors")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<Color> _colors = new() { Color.white };

        [BoxGroup("Sequence")]
        [LabelText("Duration Seconds")]
        [MinValue(0.01f)]
        [SerializeField]
        float _durationSeconds = 1f;

        [BoxGroup("Sequence")]
        [LabelText("Loop")]
        [SerializeField]
        bool _loop = true;

        public override Light2DEffectPresetBase CreateRuntimeCopy()
        {
            return new Light2DColorSequenceEffectPreset
            {
                _colors = new List<Color>(_colors),
                _durationSeconds = Mathf.Max(0.01f, _durationSeconds),
                _loop = _loop,
            };
        }

        internal override Light2DContributionState Evaluate(in Light2DEffectEvaluationContext context)
        {
            if (_colors.Count == 0)
                return new Light2DContributionState();

            if (_colors.Count == 1)
            {
                return new Light2DContributionState
                {
                    HasColor = true,
                    Color = _colors[0],
                };
            }

            var segmentCount = _loop ? _colors.Count : _colors.Count - 1;
            if (segmentCount <= 0)
            {
                return new Light2DContributionState
                {
                    HasColor = true,
                    Color = _colors[0],
                };
            }

            var scaled = context.ElapsedTime / Mathf.Max(0.01f, _durationSeconds) * segmentCount;
            if (!_loop && scaled >= segmentCount)
            {
                return new Light2DContributionState
                {
                    HasColor = true,
                    Color = _colors[_colors.Count - 1],
                };
            }

            var wrapped = _loop ? Mathf.Repeat(scaled, segmentCount) : Mathf.Clamp(scaled, 0f, segmentCount);
            var fromIndex = Mathf.Clamp(Mathf.FloorToInt(wrapped), 0, _colors.Count - 1);
            var toIndex = _loop
                ? (fromIndex + 1) % _colors.Count
                : Mathf.Clamp(fromIndex + 1, 0, _colors.Count - 1);
            var lerp = Mathf.Clamp01(wrapped - Mathf.Floor(wrapped));

            return new Light2DContributionState
            {
                HasColor = true,
                Color = Color.Lerp(_colors[fromIndex], _colors[toIndex], lerp),
            };
        }

        internal override bool ApplyMutation(Light2DEffectRuntimeMutationBase mutation)
        {
            if (mutation is not Light2DColorSequenceEffectMutation typed || !typed.HasAnyMutation())
                return false;

            if (typed.ApplyColors)
            {
                _colors = typed.Colors != null && typed.Colors.Count > 0
                    ? new List<Color>(typed.Colors)
                    : new List<Color> { Color.white };
            }

            if (typed.ApplyDurationSeconds)
                _durationSeconds = Mathf.Max(0.01f, typed.DurationSeconds);
            if (typed.ApplyLoop)
                _loop = typed.Loop;

            return true;
        }
    }

    [Serializable]
    public sealed class Light2DColorSequenceEffectMutation : Light2DEffectRuntimeMutationBase
    {
        [BoxGroup("Sequence")]
        [ToggleLeft]
        [LabelText("Apply Colors")]
        public bool ApplyColors;

        [BoxGroup("Sequence")]
        [ShowIf(nameof(ApplyColors))]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        public List<Color> Colors = new() { Color.white };

        [BoxGroup("Sequence")]
        [ToggleLeft]
        [LabelText("Apply Duration Seconds")]
        public bool ApplyDurationSeconds;

        [BoxGroup("Sequence")]
        [ShowIf(nameof(ApplyDurationSeconds))]
        [MinValue(0.01f)]
        public float DurationSeconds = 1f;

        [BoxGroup("Sequence")]
        [ToggleLeft]
        [LabelText("Apply Loop")]
        public bool ApplyLoop;

        [BoxGroup("Sequence")]
        [ShowIf(nameof(ApplyLoop))]
        public bool Loop = true;

        public override bool HasAnyMutation()
        {
            return ApplyColors || ApplyDurationSeconds || ApplyLoop;
        }
    }
}
