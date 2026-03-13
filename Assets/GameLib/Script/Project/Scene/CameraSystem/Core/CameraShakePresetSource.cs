#nullable enable
using System;
using Game.Times;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.CameraSystem
{
    public enum CameraShakePresetSourceKind
    {
        Asset = 0,
        Inline = 1,
    }

    [Serializable]
    public sealed class InlineCameraShakePreset
    {
        [BoxGroup("Amplitude")]
        [LabelText("Position")]
        [MinValue(0f)]
        public float amplitudePosition = 0.5f;

        [BoxGroup("Amplitude")]
        [LabelText("Rotation (deg)")]
        [MinValue(0f)]
        public float amplitudeRotationDeg = 1f;

        [BoxGroup("Timing")]
        [LabelText("Frequency")]
        [MinValue(0.01f)]
        public float frequency = 8f;

        [BoxGroup("Timing")]
        [LabelText("Duration")]
        [MinValue(0f)]
        public float duration = 0.4f;

        [BoxGroup("Decay")]
        [LabelText("Mode")]
        public CameraShakeDecayMode decayMode = CameraShakeDecayMode.Exponential;

        [BoxGroup("Decay")]
        [LabelText("Lambda Or Decay")]
        [MinValue(0f)]
        public float lambdaOrDecay = 8f;

        [BoxGroup("Behavior")]
        [LabelText("Seed")]
        public uint seed = 1;

        [BoxGroup("Behavior")]
        [LabelText("Time Scale")]
        public TimeScaleBehavior timeScaleBehavior = TimeScaleBehavior.Unscaled;

        public CameraShakePreset ToPreset()
        {
            return new CameraShakePreset(
                amplitudePosition,
                amplitudeRotationDeg,
                frequency,
                duration,
                decayMode,
                lambdaOrDecay,
                seed,
                timeScaleBehavior);
        }
    }

    [Serializable]
    public sealed class CameraShakePresetSource
    {
        [HorizontalGroup("Header", Width = 80), HideLabel]
        public CameraShakePresetSourceKind kind = CameraShakePresetSourceKind.Asset;

        [ShowIf(nameof(IsAsset))]
        [LabelText("Preset")]
        [AssetOrInternal]
        public CameraShakePresetSO? asset;

        [ShowIf(nameof(IsInline))]
        [HideLabel]
        [InlineProperty]
        public InlineCameraShakePreset inline = new();

        bool IsAsset => kind == CameraShakePresetSourceKind.Asset;
        bool IsInline => kind == CameraShakePresetSourceKind.Inline;

        public bool TryGet(out CameraShakePreset preset)
        {
            switch (kind)
            {
                case CameraShakePresetSourceKind.Asset:
                    if (asset == null)
                    {
                        preset = default;
                        return false;
                    }

                    preset = asset.ToPreset();
                    return true;

                case CameraShakePresetSourceKind.Inline:
                    preset = inline.ToPreset();
                    return true;

                default:
                    preset = default;
                    return false;
            }
        }

        public static bool TryGet(CameraShakePresetSource? source, out CameraShakePreset preset)
        {
            if (source == null)
            {
                preset = default;
                return false;
            }

            return source.TryGet(out preset);
        }
    }
}
