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
        [Tooltip("アニメーション切替時に専用フリップアニメーションを再生する。")]
        [SerializeField]
        public bool useClipSwitchFlip = false;

        [ShowIf(nameof(useClipSwitchFlip))]
        [LabelText("Switch Flip Duration")]
        [Tooltip("切替フリップ全体時間（秒）。90度到達まで前半、残りを後半として使用。")]
        [SerializeField, MinValue(0.01f)]
        public float switchFlipDuration = 0.2f;

        [ShowIf(nameof(useClipSwitchFlip))]
        [LabelText("Switch Flip Full Rotation")]
        [Tooltip("true: 90度到達後に1周して戻る。false: 90度到達後にそのまま戻る（半周相当）。")]
        [SerializeField]
        public bool switchFlipFullRotation = false;

        [LabelText("Use Playback Speed Multiplier")]
        [Tooltip("true: チャネルの再生速度倍率(0=停止,1=通常,2=倍速)の影響を受ける。false: 常に等倍再生。")]
        [SerializeField]
        public bool usePlaybackSpeedMultiplier = true;

        [Header("CrossFade")]
        [Tooltip("CrossFade モード用のトランジション設定。null ならデフォルト挙動。")]
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
