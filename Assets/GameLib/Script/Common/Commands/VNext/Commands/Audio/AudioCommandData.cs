#nullable enable
using System;
using Game.Audio;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum AudioPlaybackPositionMode
    {
        Global = 0,
        Local = 10,
    }

    [Serializable]
    public sealed class PlayAudioCommandData : ICommandData
    {
        public int CommandId => CommandIds.PlayAudio;
        public string DebugData
        {
            get
            {
                var cueName = Cue != null ? Cue.name : "null";
                var tag = string.IsNullOrEmpty(TagOverride) ? "<default>" : TagOverride;
                var speed = OverridePlaybackSpeed ? CommandDebugDataHelper.GetDynamicDebugData(PlaybackSpeedScale, "1") : "<cue>";
                var playbackMode = PlaybackMode.ToString();
                var startOffset = CommandDebugDataHelper.GetDynamicDebugData(PlaybackStartOffsetSeconds, "0");
                return $"Cue={cueName} Tag={tag} Mode={playbackMode} Speed={speed} Start={startOffset}";
            }
        }

        [BoxGroup("Audio")]
        [LabelText("Cue")]
        [Tooltip("Audio cue asset. null means Stop/FadeOut.")]
        [SerializeReference]
        public SimpleAudioCueSO Cue = null!;

        [BoxGroup("Audio")]
        [LabelText("Tag (Override)")]
        [Tooltip("Override tag. If empty, cue default tag is used.")]
        [SerializeField]
        public string TagOverride = string.Empty;

        [BoxGroup("Audio")]
        [LabelText("Ignore If Playing")]
        [SerializeField]
        public bool IgnoreIfAlreadyPlaying;

        [BoxGroup("Volume & Fade")]
        [LabelText("Volume Scale")]
        [Range(0f, 2f)]
        [SerializeField]
        public float VolumeScale = 1f;

        [BoxGroup("Volume & Fade")]
        [LabelText("Fade In (sec)")]
        [SerializeField]
        public DynamicValue<float> FadeInSeconds;

        [BoxGroup("Volume & Fade")]
        [LabelText("Fade Out (sec)")]
        [Min(0f)]
        [SerializeField]
        public float FadeOutSeconds;

        [BoxGroup("Playback Position")]
        [LabelText("Playback Mode")]
        [EnumToggleButtons]
        [SerializeField]
        public AudioPlaybackPositionMode PlaybackMode = AudioPlaybackPositionMode.Global;

        [BoxGroup("Playback Position")]
        [LabelText("Origin Position")]
        [Tooltip("Local playback uses this world position as the sound emitter origin.")]
        [EnableIf(nameof(IsLocalPlayback))]
        [SerializeField]
        public DynamicValue<Vector3> LocalPlaybackOrigin = DynamicValue<Vector3>.FromSource(new ActorWorldPosition3Source());

        [BoxGroup("Playback Position")]
        [LabelText("Position Offset")]
        [Tooltip("Added to Origin Position to produce the final emitter position.")]
        [EnableIf(nameof(IsLocalPlayback))]
        [SerializeField]
        public DynamicValue<Vector3> LocalPlaybackOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Playback Position")]
        [LabelText("Playback Start Offset (sec)")]
        [Tooltip("Starts playback from this time offset. Example: 60 = start from 1 minute.")]
        [SerializeField]
        public DynamicValue<float> PlaybackStartOffsetSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Playback Speed")]
        [LabelText("Override Playback Speed")]
        [SerializeField]
        public bool OverridePlaybackSpeed;

        [BoxGroup("Playback Speed")]
        [LabelText("Playback Speed Scale")]
        [Tooltip("0 = stop, 1 = normal, 0.5 = half speed")]
        [EnableIf(nameof(OverridePlaybackSpeed))]
        [SerializeField]
        public DynamicValue<float> PlaybackSpeedScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Playback Speed")]
        [LabelText("Override Speed -> Pitch")]
        [SerializeField]
        public bool OverrideApplyPlaybackSpeedToPitch;

        [BoxGroup("Playback Speed")]
        [LabelText("Apply Speed To Pitch")]
        [Tooltip("Unity 標準 AudioSource では OFF の場合、再生速度変更自体も適用されません。")]
        [EnableIf(nameof(OverrideApplyPlaybackSpeedToPitch))]
        [SerializeField]
        public bool ApplyPlaybackSpeedToPitch = true;

        [BoxGroup("Bus & Spatialization")]
        [LabelText("Bus Override")]
        [SerializeField]
        public AudioBusKind? BusOverride;

        [BoxGroup("Bus & Spatialization")]
        [LabelText("Spatialize Override")]
        [SerializeField]
        public bool? SpatializeOverride;

        bool IsLocalPlayback() => PlaybackMode == AudioPlaybackPositionMode.Local;

    }

    [Serializable]
    public sealed class StopAudioCommandData : ICommandData
    {
        public int CommandId => CommandIds.StopAudio;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(Tag) ? "<none>" : Tag;
                var bus = BusOverride?.ToString() ?? "<none>";
                return $"Tag={tag} Bus={bus}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Tag")]
        [Tooltip("Stop/FadeOut target tag. If empty, stops the specified Bus (Bus Override required).")]
        [SerializeField]
        public string Tag = string.Empty;

        [BoxGroup("Target")]
        [LabelText("Bus Override")]
        [Tooltip("If set, stop only this bus. If Tag is non-empty and Bus is not set, stops the tag across all buses.")]
        [SerializeField]
        public AudioBusKind? BusOverride;

        [BoxGroup("Fade")]
        [LabelText("Fade Out (sec)")]
        [Min(0f)]
        [SerializeField]
        public float FadeOutSeconds;
    }
}
