#nullable enable
using UnityEngine;

namespace Game.CameraSystem
{
    public struct SmoothedFloat
    {
        float _current;
        float _target;
        float _lambda;

        public float Current => _current;
        public float Target => _target;

        public SmoothedFloat(float value)
        {
            _current = value;
            _target = value;
            _lambda = 0f;
        }

        public void SetTarget(float value, float lambda)
        {
            _target = value;
            _lambda = Mathf.Max(0f, lambda);
        }

        public void SetImmediate(float value)
        {
            _current = value;
            _target = value;
        }

        public void Tick(float dt)
        {
            if (_current.Equals(_target))
                return;

            if (_lambda <= 0f || dt <= 0f)
            {
                _current = _target;
                return;
            }

            float alpha = 1f - Mathf.Exp(-_lambda * dt);
            _current = Mathf.Lerp(_current, _target, alpha);
        }

        public void Tick(float dt, float epsilon)
        {
            float eps = Mathf.Max(0f, epsilon);
            if (eps > 0f && Mathf.Abs(_current - _target) <= eps)
            {
                _current = _target;
                return;
            }

            Tick(dt);

            if (eps > 0f && Mathf.Abs(_current - _target) <= eps)
                _current = _target;
        }

        public bool IsConverged(float epsilon)
        {
            return Mathf.Abs(_current - _target) <= epsilon;
        }
    }

    public struct SmoothedVector3
    {
        Vector3 _current;
        Vector3 _target;
        float _lambda;

        public Vector3 Current => _current;
        public Vector3 Target => _target;

        public SmoothedVector3(Vector3 value)
        {
            _current = value;
            _target = value;
            _lambda = 0f;
        }

        public void SetTarget(Vector3 value, float lambda)
        {
            _target = value;
            _lambda = Mathf.Max(0f, lambda);
        }

        public void SetImmediate(Vector3 value)
        {
            _current = value;
            _target = value;
        }

        public void Tick(float dt)
        {
            if (_current == _target)
                return;

            if (_lambda <= 0f || dt <= 0f)
            {
                _current = _target;
                return;
            }

            float alpha = 1f - Mathf.Exp(-_lambda * dt);
            _current = Vector3.Lerp(_current, _target, alpha);
        }

        public void TickWithLambda(float dt, float lambdaOverride)
        {
            if (_current == _target)
                return;

            var lambda = Mathf.Max(0f, lambdaOverride);
            if (lambda <= 0f || dt <= 0f)
            {
                _current = _target;
                return;
            }

            float alpha = 1f - Mathf.Exp(-lambda * dt);
            _current = Vector3.Lerp(_current, _target, alpha);
        }

        public void Tick(float dt, float epsilon)
        {
            float eps = Mathf.Max(0f, epsilon);
            if (eps > 0f && (_current - _target).sqrMagnitude <= eps * eps)
            {
                _current = _target;
                return;
            }

            Tick(dt);

            if (eps > 0f && (_current - _target).sqrMagnitude <= eps * eps)
                _current = _target;
        }

        public bool IsConverged(float epsilon)
        {
            return (_current - _target).sqrMagnitude <= epsilon * epsilon;
        }
    }
}
