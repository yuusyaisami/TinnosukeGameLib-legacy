// Game.Animation.Presets

using System;
using UnityEngine;
using Game.Animation;
using Sirenix.OdinInspector;
namespace Game.Channel
{
    public enum AnimationPlayMode
    {
        Once,
        Loop,
        OnceToLoop,
        CrossFade,
    }

    [Serializable]
    public sealed class AnimationSpritePreset
    {
        [Header("Clip")]
        public AnimationDataSource animationA = new();

        [ShowIf(nameof(showAnimationB))]
        public AnimationDataSource animationB = new();

        [Header("Playback")]
        [SerializeField]
        public AnimationPlayMode playMode = AnimationPlayMode.Once;
        private bool showAnimationB() => playMode == AnimationPlayMode.OnceToLoop || playMode == AnimationPlayMode.CrossFade;
        [SerializeField]
        public bool flipX;

        [LabelText("Use Clip Switch Flip")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        public bool useClipSwitchFlip = false;

        [ShowIf(nameof(useClipSwitchFlip))]
        [LabelText("Switch Flip Duration")]
        [Tooltip("Inspector setting.")]
        [SerializeField, MinValue(0.01f)]
        public float switchFlipDuration = 0.2f;

        [ShowIf(nameof(useClipSwitchFlip))]
        [LabelText("Switch Flip Full Rotation")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        public bool switchFlipFullRotation = false;

        [LabelText("Use Playback Speed Multiplier")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        public bool usePlaybackSpeedMultiplier = true;

        [Header("CrossFade")]
        [Tooltip("Inspector setting.")]
        [SerializeReference]
        [InlineProperty]
        public ITransitionProfile crossFadeProfile;
    }

    [CreateAssetMenu(menuName = "Game/Channel/Animation Sprite Preset")]
    public sealed class AnimationSpritePresetAssetSO : ScriptableObject
    {
        [InlineProperty]
        public AnimationSpritePreset preset = new();
    }
}
