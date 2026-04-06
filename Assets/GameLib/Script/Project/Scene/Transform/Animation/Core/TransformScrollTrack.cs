#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// Velocity ベースの Scroll track。
    /// duration 中 velocity を積分して WorldPosition に absolute 寄与を出す。
    /// </summary>
    public sealed class TransformScrollTrack : ITransformModifierTrack
    {
        readonly Vector3 _velocity;
        readonly float _duration;
        readonly bool _useLocalVelocity;
        readonly Transform _ownerTransform;

        float _elapsed;
        Vector3 _currentPosition;
        bool _stopped;

        public bool IsAlive => !_stopped && _elapsed < _duration;
        public int Priority => 50;
        public TransformContributionMask ContributedProperties => TransformContributionMask.WorldPosition;

        public TransformScrollTrack(Transform ownerTransform, Vector3 velocity, float duration, bool useLocalVelocity)
        {
            _ownerTransform = ownerTransform;
            _velocity = velocity;
            _duration = Mathf.Max(0.001f, duration);
            _useLocalVelocity = useLocalVelocity;
            _currentPosition = ownerTransform ? ownerTransform.position : Vector3.zero;
        }

        public void Tick(float deltaTime)
        {
            if (_stopped || !_ownerTransform)
                return;

            var dt = Mathf.Min(deltaTime, _duration - _elapsed);
            if (dt <= 0f)
            {
                _stopped = true;
                return;
            }

            var worldVelocity = _useLocalVelocity
                ? _ownerTransform.TransformVector(_velocity)
                : _velocity;

            _currentPosition += worldVelocity * dt;
            _elapsed += dt;

            if (_elapsed >= _duration)
                _stopped = true;
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (_stopped && _elapsed >= _duration)
                return;

            accumulator.Apply(TransformPoseContribution.WorldPosition(
                _currentPosition, TransformComposeMode.Replace, Priority));
        }

        public void Stop()
        {
            _stopped = true;
        }

        public void Reset()
        {
            _elapsed = 0f;
            _stopped = false;
            _currentPosition = _ownerTransform ? _ownerTransform.position : Vector3.zero;
        }
    }
}
