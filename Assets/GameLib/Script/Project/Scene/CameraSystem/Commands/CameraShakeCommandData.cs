#nullable enable
using System;
using Game.CameraSystem;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum CameraShakeCommandAction
    {
        Play = 0,
        StopHandle = 1,
        StopAll = 2,
        SetGlobalIntensity = 3,
    }

    [Serializable]
    public sealed class CameraShakeCommandData : ICommandData
    {
        public int CommandId => CommandIds.CameraShake;
        public string DebugData
        {
            get
            {
                var preset = Preset.kind == CameraShakePresetSourceKind.Asset && Preset.asset != null
                    ? Preset.asset.name
                    : Preset.kind.ToString();
                return $"Action={Action} Preset={preset}";
            }
        }

        [BoxGroup("Action")]
        [LabelText("Action")]
        [EnumToggleButtons]
        public CameraShakeCommandAction Action = CameraShakeCommandAction.Play;

        [BoxGroup("Play")]
        [LabelText("Preset")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.Play)]
        [InlineProperty, HideLabel]
        public CameraShakePresetSource Preset = new();

        [BoxGroup("Play")]
        [LabelText("Priority")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.Play)]
        public DynamicValue<int> Priority;

        [BoxGroup("Stop Handle")]
        [LabelText("Handle")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.StopHandle)]
        public DynamicValue<int> Handle;

        [BoxGroup("Stop Handle")]
        [LabelText("Fade Out Sec")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.StopHandle)]
        public DynamicValue<float> FadeOutSeconds;

        [BoxGroup("Stop All")]
        [LabelText("Fade Out Sec")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.StopAll)]
        public DynamicValue<float> FadeOutAllSeconds;

        [BoxGroup("Intensity")]
        [LabelText("Scale")]
        [ShowIf(nameof(Action), CameraShakeCommandAction.SetGlobalIntensity)]
        public DynamicValue<float> Intensity;
    }
}
