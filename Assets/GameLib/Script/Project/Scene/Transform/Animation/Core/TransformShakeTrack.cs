#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// Perlin noise ベースの位置・回転揺れ track。
    /// LocalPosition / LocalRotation に対して additive 寄与を出す。
    /// </summary>
    public sealed class TransformShakeTrack : ITransformModifierTrack
    {
        readonly float _amplitudeX;
        readonly float _amplitudeY;
        readonly float _frequency;
        readonly bool _enableRotation;
        readonly float _rotationAmplitudeDeg;
        readonly float _duration; // <= 0 で無限
        readonly float _seed;

        float _elapsed;
        float _time;
        bool _stopped;

        Vector3 _positionOffset;
        float _rotationOffsetZ;

        public bool IsAlive => !_stopped && (_duration <= 0f || _elapsed < _duration);
        public int Priority => 0;
        public TransformContributionMask ContributedProperties
        {
            get
            {
                var mask = TransformContributionMask.LocalPosition;
                if (_enableRotation && _rotationAmplitudeDeg > 0f)
                    mask |= TransformContributionMask.LocalRotation;
                return mask;
            }
        }

        public TransformShakeTrack(in Channel.TransformShakeSettings settings)
        {
            _amplitudeX = settings.AmplitudeX;
            _amplitudeY = settings.AmplitudeY;
            _frequency = Mathf.Max(0.01f, settings.Frequency);
            _enableRotation = settings.EnableRotation;
            _rotationAmplitudeDeg = settings.RotationAmplitudeDeg;
            _duration = Mathf.Max(0f, settings.DurationSeconds);
            _seed = Random.value * 1000f;
        }

        public void Tick(float deltaTime)
        {
            if (_stopped)
                return;

            _elapsed += deltaTime;
            if (_duration > 0f && _elapsed >= _duration)
            {
                _positionOffset = Vector3.zero;
                _rotationOffsetZ = 0f;
                _stopped = true;
                return;
            }

            _time += deltaTime * _frequency;

            _positionOffset = new Vector3(
                (Mathf.PerlinNoise(_seed + 1.23f, _time) - 0.5f) * 2f * _amplitudeX,
                (Mathf.PerlinNoise(_seed + 4.56f, _time) - 0.5f) * 2f * _amplitudeY,
                0f);

            _rotationOffsetZ = _enableRotation
                ? (Mathf.PerlinNoise(_seed + 7.89f, _time) - 0.5f) * 2f * _rotationAmplitudeDeg
                : 0f;
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (_stopped)
                return;

            if (_positionOffset != Vector3.zero)
            {
                accumulator.Apply(TransformPoseContribution.LocalPosition(
                    _positionOffset, TransformComposeMode.Add, Priority));
            }

            if (_enableRotation && !Mathf.Approximately(_rotationOffsetZ, 0f))
            {
                accumulator.Apply(TransformPoseContribution.LocalRotation(
                    new Vector3(0f, 0f, _rotationOffsetZ), TransformComposeMode.Add, Priority));
            }
        }

        public void Stop()
        {
            _stopped = true;
            _positionOffset = Vector3.zero;
            _rotationOffsetZ = 0f;
        }

        public void Reset()
        {
            _elapsed = 0f;
            _time = 0f;
            _stopped = false;
            _positionOffset = Vector3.zero;
            _rotationOffsetZ = 0f;
        }
    }
}
