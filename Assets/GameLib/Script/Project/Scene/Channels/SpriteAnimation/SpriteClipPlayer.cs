#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Animation;

namespace Game.Channel
{
    internal sealed class SpriteClipPlayer
    {
        IAnimationData? _clip;
        readonly List<IAnimationFrame> _frames = new();

        int _frameIndex;
        float _frameElapsed;
        float _frameDuration;
        float _defaultDt;
        bool _loop;
        float _playbackSpeedMultiplier = 1f;
        bool _usePlaybackSpeedMultiplier = true;

        public IAnimationData? CurrentClip => _clip;
        public bool IsActive => _clip != null && _frames.Count > 0;
        public float PlaybackSpeedMultiplier => _playbackSpeedMultiplier;
        public bool UsePlaybackSpeedMultiplier => _usePlaybackSpeedMultiplier;

        public void SetPlaybackSpeedMultiplier(float value)
        {
            _playbackSpeedMultiplier = Mathf.Max(0f, value);
        }

        public void SetUsePlaybackSpeedMultiplier(bool value)
        {
            _usePlaybackSpeedMultiplier = value;
        }

        public void Reset()
        {
            _clip = null;
            _frames.Clear();
            _frameIndex = 0;
            _frameElapsed = 0f;
            _frameDuration = 0f;
            _defaultDt = 0f;
            _loop = false;
            _usePlaybackSpeedMultiplier = true;
        }

        public void Start(IAnimationData clip, bool loop)
        {
            Reset();

            if (!IsPlayable(clip))
                return;

            _clip = clip;
            var srcFrames = clip.Frames;
            if (srcFrames != null)
            {
                for (int i = 0; i < srcFrames.Count; i++)
                {
                    var f = srcFrames[i];
                    if (f == null)
                        continue;
                    _frames.Add(f);
                }
            }

            if (_frames.Count == 0)
            {
                Reset();
                return;
            }

            _loop = loop;

            _defaultDt = Mathf.Max(0.001f, clip.DefaultFrameDuration);

            _frameIndex = 0;
            _frameElapsed = 0f;

            var frame = _frames[0];
            _frameDuration = frame.Duration > 0f ? frame.Duration : _defaultDt;
        }

        public bool TryGetCurrentFrame(out IAnimationFrame? frame)
        {
            if (!IsActive)
            {
                frame = null;
                return false;
            }

            frame = _frames[_frameIndex];
            return true;
        }

        public void Tick(float deltaTime, out bool frameChanged, out IAnimationFrame? changedFrame, out bool ended)
        {
            frameChanged = false;
            changedFrame = null;
            ended = false;

            if (!IsActive)
            {
                ended = true;
                return;
            }

            var speed = _usePlaybackSpeedMultiplier ? _playbackSpeedMultiplier : 1f;
            if (speed <= 0f)
                return;

            _frameElapsed += Mathf.Max(0f, deltaTime) * speed;
            while (_frameElapsed >= _frameDuration)
            {
                _frameElapsed -= _frameDuration;

                if (!AdvanceFrame(out var newFrame))
                {
                    ended = true;
                    return;
                }

                frameChanged = true;
                changedFrame = newFrame;
            }
        }

        bool AdvanceFrame(out IAnimationFrame? newFrame)
        {
            newFrame = null;

            _frameIndex++;
            if (_frameIndex >= _frames.Count)
            {
                if (_loop)
                {
                    _frameIndex = 0;
                }
                else
                {
                    return false;
                }
            }

            newFrame = _frames[_frameIndex];
            _frameDuration = newFrame.Duration > 0f ? newFrame.Duration : _defaultDt;
            return true;
        }

        public static bool IsPlayable(IAnimationData? clip)
        {
            var frames = clip?.Frames;
            return frames != null && frames.Count > 0;
        }
    }
}
