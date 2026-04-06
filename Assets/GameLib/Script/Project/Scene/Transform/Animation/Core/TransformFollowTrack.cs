#nullable enable
using Game.Channel;
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// Follow target 追従 track。
    /// TransformFollowService のアルゴリズムを使い WorldPosition の absolute 寄与を出す。
    /// </summary>
    public sealed class TransformFollowTrack : ITransformModifierTrack
    {
        readonly TransformFollowService _followService;
        readonly Transform _ownerTransform;
        TransformFollowOptions _options;
        bool _stopped;
        Vector3 _currentPosition;

        public bool IsAlive => !_stopped;
        public int Priority => 100; // Follow は高優先度
        public TransformContributionMask ContributedProperties => TransformContributionMask.WorldPosition;

        public TransformFollowService FollowService => _followService;
        public TransformFollowOptions Options => _options;

        public TransformFollowTrack(Transform ownerTransform, TransformFollowOptions options)
        {
            _ownerTransform = ownerTransform;
            _options = options;
            _followService = new TransformFollowService();
            _currentPosition = ownerTransform ? ownerTransform.position : Vector3.zero;
        }

        public void SetTransformTarget(Transform target, in TransformFollowOptions options)
        {
            _options = options;
            _followService.SetTarget(target, options);
        }

        public void SetPositionTarget(Vector3 position)
        {
            _followService.SetTarget(position);
        }

        public bool TrySnap()
        {
            if (_stopped || _ownerTransform == null)
                return false;

            _currentPosition = _followService.ResolveCurrentTargetPosition(_currentPosition, _options);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_stopped || _ownerTransform == null)
                return;

            _currentPosition = _followService.Update(_currentPosition, deltaTime, _options);
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (_stopped)
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
            _stopped = false;
            _currentPosition = _ownerTransform ? _ownerTransform.position : Vector3.zero;
        }
    }
}
