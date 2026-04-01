#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// 直接角度へ収束する track。
    /// 現在角度とターゲット角度の差分を毎フレーム additive 寄与として出力する。
    /// </summary>
    public sealed class TransformAngleConvergeTrack : ITransformModifierTrack
    {
        const float AngleEpsilonSqr = 0.000001f;

        readonly Transform _ownerTransform;

        Vector3 _currentEulerAngles;
        Vector3 _targetEulerAngles;
        Vector3 _smoothVelocity;
        float _smoothTime;
        float _maxSpeed = float.MaxValue;
        bool _active;
        float _stopFadeSeconds;
        float _stopElapsed;
        bool _initialized;

        public TransformAngleConvergeTrack(Transform ownerTransform)
        {
            _ownerTransform = ownerTransform;
        }

        public bool IsAlive => _active;
        public int Priority => 60;
        public TransformContributionMask ContributedProperties => TransformContributionMask.LocalRotation;

        public Vector3 CurrentEulerAngles => _currentEulerAngles;
        public Vector3 TargetEulerAngles => _targetEulerAngles;
        public Vector3 CurrentVelocity => _smoothVelocity;
        public bool HasActivity => _active;

        public void SetTarget(Vector3 targetEulerAngles, float smoothTime, float maxSpeed)
        {
            EnsureInitialized();
            _targetEulerAngles = targetEulerAngles;
            _smoothTime = Mathf.Max(0f, smoothTime);
            _maxSpeed = maxSpeed > 0f ? maxSpeed : float.MaxValue;
            _active = true;
            _stopFadeSeconds = 0f;
            _stopElapsed = 0f;
        }

        public void Stop(bool immediate, float fadeSeconds)
        {
            EnsureInitialized();

            if (immediate)
            {
                _active = false;
                _smoothVelocity = Vector3.zero;
                _smoothTime = 0f;
                _stopFadeSeconds = 0f;
                _stopElapsed = 0f;
                return;
            }

            _stopFadeSeconds = Mathf.Max(0f, fadeSeconds);
            _stopElapsed = 0f;
            if (_stopFadeSeconds <= 0f)
                _active = false;
        }

        public void Stop()
        {
            Reset();
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();

            if (deltaTime <= 0f)
            {
                return;
            }

            if (_stopFadeSeconds > 0f)
            {
                _stopElapsed = Mathf.Min(_stopElapsed + deltaTime, _stopFadeSeconds);
                if (_stopElapsed >= _stopFadeSeconds)
                {
                    _active = false;
                    _stopFadeSeconds = 0f;
                }
            }

            if (!_active)
                return;

            var previous = _currentEulerAngles;

            if (_smoothTime <= 0f)
            {
                _currentEulerAngles = _targetEulerAngles;
                _smoothVelocity = Vector3.zero;
            }
            else
            {
                _currentEulerAngles.x = Mathf.SmoothDampAngle(previous.x, _targetEulerAngles.x, ref _smoothVelocity.x, _smoothTime, _maxSpeed, deltaTime);
                _currentEulerAngles.y = Mathf.SmoothDampAngle(previous.y, _targetEulerAngles.y, ref _smoothVelocity.y, _smoothTime, _maxSpeed, deltaTime);
                _currentEulerAngles.z = Mathf.SmoothDampAngle(previous.z, _targetEulerAngles.z, ref _smoothVelocity.z, _smoothTime, _maxSpeed, deltaTime);
            }
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (!_active)
                return;

            accumulator.Apply(TransformPoseContribution.LocalRotation(_currentEulerAngles, TransformComposeMode.Replace, Priority));
        }

        public void Reset()
        {
            _active = false;
            _initialized = false;
            _currentEulerAngles = Vector3.zero;
            _targetEulerAngles = Vector3.zero;
            _smoothVelocity = Vector3.zero;
            _smoothTime = 0f;
            _maxSpeed = float.MaxValue;
            _stopFadeSeconds = 0f;
            _stopElapsed = 0f;
        }

        void EnsureInitialized()
        {
            if (_initialized)
                return;

            _currentEulerAngles = _ownerTransform ? _ownerTransform.localEulerAngles : Vector3.zero;
            _targetEulerAngles = _currentEulerAngles;
            _smoothVelocity = Vector3.zero;
            _active = false;
            _stopFadeSeconds = 0f;
            _stopElapsed = 0f;
            _initialized = true;
        }

        static bool IsApproximatelyEqual(in Vector3 a, in Vector3 b)
        {
            return Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)) <= 0.0001f
                && Mathf.Abs(Mathf.DeltaAngle(a.y, b.y)) <= 0.0001f
                && Mathf.Abs(Mathf.DeltaAngle(a.z, b.z)) <= 0.0001f;
        }
    }
}