#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// 速度ベースの回転 track。
    /// 角速度を保持し、毎フレームの回転差分を additive 寄与として出力する。
    /// </summary>
    public sealed class TransformRotateTrack : ITransformModifierTrack
    {
        const float SpeedEpsilonSqr = 0.000001f;

        readonly Transform _ownerTransform;

        Vector3 _currentSpeed;
        Vector3 _targetSpeed;
        Vector3 _fadeStartSpeed;
        Vector3 _frameDelta;

        float _fadeDuration;
        float _fadeElapsed;
        float _dampingRate = 1f;
        bool _active;
        bool _stopRequested;

        public TransformRotateTrack(Transform ownerTransform)
        {
            _ownerTransform = ownerTransform;
        }

        public bool IsAlive => _active;
        public int Priority => 55;
        public TransformContributionMask ContributedProperties => TransformContributionMask.LocalRotation;

        public Vector3 CurrentSpeed => _currentSpeed;
        public Vector3 TargetSpeed => _targetSpeed;
        public bool HasActivity => _active;

        public void SetSpeed(Vector3 speed, bool add, float fadeSeconds, float dampingRate)
        {
            var resolvedFadeSeconds = Mathf.Max(0f, fadeSeconds);
            var resolvedDampingRate = Mathf.Max(0f, dampingRate);
            var nextTargetSpeed = add ? _targetSpeed + speed : speed;
            var shouldReinitializeImmediate =
                resolvedFadeSeconds <= 0f &&
                (add ||
                 !_active ||
                 _stopRequested ||
                 !IsApproximatelyEqual(_targetSpeed, nextTargetSpeed) ||
                 !Mathf.Approximately(_dampingRate, resolvedDampingRate));

            if (!shouldReinitializeImmediate && resolvedFadeSeconds <= 0f)
            {
                _targetSpeed = nextTargetSpeed;
                _dampingRate = resolvedDampingRate;
                _active = true;
                _stopRequested = false;
                return;
            }

            var currentSpeed = _currentSpeed;
            _targetSpeed = nextTargetSpeed;
            _fadeStartSpeed = currentSpeed;
            _fadeElapsed = 0f;
            _fadeDuration = resolvedFadeSeconds;
            _dampingRate = resolvedDampingRate;
            _active = true;
            _stopRequested = false;

            if (resolvedFadeSeconds <= 0f && shouldReinitializeImmediate)
            {
                _currentSpeed = _targetSpeed;
                _frameDelta = Vector3.zero;
            }
        }

        public void Stop(bool immediate, float fadeSeconds)
        {
            if (immediate)
            {
                ClearState();
                return;
            }

            _active = true;
            _stopRequested = true;
            _fadeStartSpeed = _currentSpeed;
            _targetSpeed = Vector3.zero;
            _fadeElapsed = 0f;
            _fadeDuration = Mathf.Max(0f, fadeSeconds);
        }

        public void Stop()
        {
            ClearState();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                _frameDelta = Vector3.zero;
                return;
            }

            if (_fadeDuration > 0f)
            {
                _fadeElapsed = Mathf.Min(_fadeElapsed + deltaTime, _fadeDuration);
                var t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
                _currentSpeed = Vector3.LerpUnclamped(_fadeStartSpeed, _targetSpeed, t);

                if (_fadeElapsed >= _fadeDuration)
                {
                    _fadeDuration = 0f;
                    _fadeStartSpeed = _currentSpeed;
                }
            }

            if (!_stopRequested && _dampingRate > 0f && !Mathf.Approximately(_dampingRate, 1f))
            {
                var damping = Mathf.Pow(_dampingRate, deltaTime);
                _currentSpeed *= damping;
            }

            _frameDelta = _currentSpeed * deltaTime;

            if (_stopRequested && _fadeDuration <= 0f && _currentSpeed.sqrMagnitude <= SpeedEpsilonSqr)
                ClearState();
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (_frameDelta.sqrMagnitude <= SpeedEpsilonSqr)
                return;

            accumulator.Apply(TransformPoseContribution.LocalRotation(_frameDelta, TransformComposeMode.Add, Priority));
        }

        public void Reset()
        {
            ClearState();
        }

        static bool IsApproximatelyEqual(in Vector3 left, in Vector3 right)
        {
            return (left - right).sqrMagnitude <= SpeedEpsilonSqr;
        }

        void ClearState()
        {
            _active = false;
            _currentSpeed = Vector3.zero;
            _targetSpeed = Vector3.zero;
            _fadeStartSpeed = Vector3.zero;
            _frameDelta = Vector3.zero;
            _fadeDuration = 0f;
            _fadeElapsed = 0f;
            _dampingRate = 1f;
            _stopRequested = false;
        }
    }
}