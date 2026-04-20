using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Audio
{
    /// <summary>
    /// 繧ｷ繝ｳ繝励Ν縺ｪ AudioCue 螳溯｣・ゅΛ繝ｳ繝繝/蜊倅ｸ繧ｯ繝ｪ繝・・縲＾din 縺ｧ蛻・°繧翫ｄ縺吶￥險ｭ螳壼庄縲・
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Audio/Simple Audio Cue", fileName = "SimpleAudioCue")]
    public sealed class SimpleAudioCueSO : ScriptableObject, IAudioCue
    {
        [BoxGroup("Cue")]
        [LabelText("Default Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _defaultTag = "sfx";

        [BoxGroup("Cue")]
        [LabelText("Bus")]
        [SerializeField]
        AudioBusKind _bus = AudioBusKind.Sfx;

        [BoxGroup("Cue")]
        [LabelText("Loop")]
        [SerializeField]
        bool _loop = false;

        [BoxGroup("Cue")]
        [LabelText("Allow Multiple Instances")]
        [SerializeField]
        bool _allowMultiple = true;

        [BoxGroup("Cue")]
        [LabelText("Restart If Already Playing")]
        [SerializeField]
        bool _restartIfAlreadyPlaying = false;

        [BoxGroup("Volume & Pitch")]
        [LabelText("Volume Multiplier")]
        [MinValue(0f)]
        [SerializeField]
        float _volumeMultiplier = 1f;

        [BoxGroup("Volume & Pitch")]
        [LabelText("Base Pitch")]
        [MinValue(0f)]
        [SerializeField]
        float _basePitch = 1f;

        [BoxGroup("Volume & Pitch")]
        [LabelText("Base Playback Speed")]
        [MinValue(0f)]
        [SerializeField]
        float _basePlaybackSpeed = 1f;

        [BoxGroup("Volume & Pitch")]
        [LabelText("Apply Playback Speed To Pitch")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _applyPlaybackSpeedToPitch = true;

        [BoxGroup("Volume & Pitch")]
        [LabelText("Pitch Random Min/Max")]
        [MinMaxSlider(0.5f, 2.0f, true)]
        [SerializeField]
        Vector2 _pitchRandomRange = new Vector2(0.95f, 1.05f);

        [BoxGroup("TimeScale Sync")]
        [LabelText("Use Bus Defaults")]
        [SerializeField]
        bool _useBusTimeScaleSettings = true;

        [BoxGroup("TimeScale Sync")]
        [LabelText("Apply To Playback Speed")]
        [EnableIf(nameof(ShowCueTimeScaleSettings))]
        [SerializeField]
        bool _applyTimeScaleToPlaybackSpeed;

        [BoxGroup("TimeScale Sync")]
        [LabelText("Apply To Pitch")]
        [EnableIf(nameof(ShowCueTimeScaleSettings))]
        [SerializeField]
        bool _applyTimeScaleToPitch;

        [BoxGroup("TimeScale Sync")]
        [LabelText("Pitch Influence")]
        [Range(0f, 1f)]
        [EnableIf(nameof(ShowPitchInfluence))]
        [SerializeField]
        float _timeScalePitchInfluence = 1f;

        [BoxGroup("Spatial")]
        [LabelText("Spatialize")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _spatialize = false;

        [BoxGroup("Spatial")]
        [LabelText("Spatial Blend")]
        [Range(0f, 1f)]
        [SerializeField]
        float _spatialBlend = 0f;

        [BoxGroup("Spatial")]
        [LabelText("Min Distance")]
        [Tooltip("Inspector setting.")]
        [MinValue(0f)]
        [SerializeField]
        float _minDistance = 1f;

        [BoxGroup("Spatial")]
        [LabelText("Max Distance")]
        [Tooltip("Inspector setting.")]
        [MinValue(0.1f)]
        [SerializeField]
        float _maxDistance = 20f;

        [BoxGroup("Spatial")]
        [LabelText("Rolloff Mode")]
        [SerializeField]
        AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;

        [BoxGroup("Clips")]
        [LabelText("Clips (Random Pick)")]
        [SerializeField]
        AudioClip[] _clips = Array.Empty<AudioClip>();

        public AudioBusKind Bus => _bus;
        public bool Loop => _loop;
        public bool AllowMultipleInstances => _allowMultiple;
        public bool RestartIfAlreadyPlaying => _restartIfAlreadyPlaying;
        public float VolumeMultiplier => Mathf.Max(0f, _volumeMultiplier);
        public float BasePitch => Mathf.Max(0f, _basePitch);
        public float BasePlaybackSpeed => Mathf.Max(0f, _basePlaybackSpeed);
        public bool ApplyPlaybackSpeedToPitch => _applyPlaybackSpeedToPitch;
        public float PitchRandomMin => Mathf.Max(0f, Mathf.Min(_pitchRandomRange.x, _pitchRandomRange.y));
        public float PitchRandomMax => Mathf.Max(PitchRandomMin, _pitchRandomRange.y);
        public bool UseBusTimeScaleSettings => _useBusTimeScaleSettings;
        public bool ApplyTimeScaleToPlaybackSpeed => _applyTimeScaleToPlaybackSpeed;
        public bool ApplyTimeScaleToPitch => _applyTimeScaleToPitch;
        public float TimeScalePitchInfluence => Mathf.Clamp01(_timeScalePitchInfluence);
        public bool Spatialize => _spatialize;
        public float SpatialBlend => Mathf.Clamp01(_spatialBlend);
        public float MinDistance => Mathf.Max(0f, _minDistance);
        public float MaxDistance => Mathf.Max(MinDistance + 0.01f, _maxDistance);
        public AudioRolloffMode RolloffMode => _rolloffMode;
        public string DefaultTag => string.IsNullOrEmpty(_defaultTag) ? name : _defaultTag;

        bool ShowCueTimeScaleSettings() => !_useBusTimeScaleSettings;
        bool ShowPitchInfluence() => !_useBusTimeScaleSettings && _applyTimeScaleToPitch;

        public AudioClip PickClip()
        {
            if (_clips == null || _clips.Length == 0)
                return null;
            if (_clips.Length == 1)
                return _clips[0];
            var idx = UnityEngine.Random.Range(0, _clips.Length);
            return _clips[idx];
        }

#if UNITY_EDITOR
        [Button("Test Play (Preview)")]
        void EditorTestPlay()
        {
            var clip = PickClip();
            if (clip == null)
            {
                Debug.LogWarning("[SimpleAudioCueSO] No clip to play.");
                return;
            }

            // Editor 蜀咲函逕ｨ縺ｮ荳譎・AudioSource 繧堤函謌舌＠縺ｦ縲∝・逕溘′邨ゅｏ繧九∪縺ｧ Polling 縺励※遐ｴ譽・☆繧九・
            // 譌｢縺ｫ蜷悟錐縺ｮ Preview 縺後≠繧句ｴ蜷医・蜈医↓遐ｴ譽・＠縺ｦ縺九ｉ譁ｰ縺励￥菴懊ｋ
            var existing = UnityEngine.GameObject.Find("[Preview] " + name);
            if (existing != null)
            {
                try { UnityEngine.Object.DestroyImmediate(existing); }
                catch { }
            }
            var go = new GameObject("[Preview] " + name);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = false;
            src.volume = VolumeMultiplier;
            var rand = UnityEngine.Random.Range(PitchRandomMin, PitchRandomMax);
            var basePitch = BasePitch * rand;
            var playbackSpeed = ApplyPlaybackSpeedToPitch ? BasePlaybackSpeed : 1f;
            src.pitch = Mathf.Max(0f, basePitch * playbackSpeed);
            Debug.Log($"[SimpleAudioCueSO] EditorTestPlay: pitch={src.pitch:F2} (base={BasePitch:F2}, rand={rand:F2}, speed={playbackSpeed:F2})");
            src.spatialBlend = SpatialBlend;
            src.minDistance = MinDistance;
            src.maxDistance = MaxDistance;
            src.rolloffMode = RolloffMode;
            src.spatialize = Spatialize;

            src.Play();

            // EditorApplication.update 縺ｧ逶｣隕悶＠縲∝・逕溘′邨ゅｏ縺｣縺溘ｉ遐ｴ譽・☆繧・
            void OnUpdate()
            {
                try
                {
                    if (src == null || go == null)
                    {
                        UnityEditor.EditorApplication.update -= OnUpdate;
                        if (go != null) UnityEngine.Object.DestroyImmediate(go);
                        return;
                    }

                    // isPlaying 縺ｯ Editor 繝励Ξ繝薙Η繝ｼ縺ｧ繧よ怏蜉ｹ
                    if (!src.isPlaying)
                    {
                        UnityEditor.EditorApplication.update -= OnUpdate;
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
                catch (Exception ex)
                {
                    UnityEditor.EditorApplication.update -= OnUpdate;
                    UnityEngine.Object.DestroyImmediate(go);
                    Debug.LogException(ex);
                }
            }

            UnityEditor.EditorApplication.update += OnUpdate;
        }
#endif
    }
}
