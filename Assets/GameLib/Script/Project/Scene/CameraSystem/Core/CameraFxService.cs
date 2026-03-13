#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Times;

namespace Game.CameraSystem
{
    public sealed class CameraFxService : ICameraFxService
    {
        readonly List<ShakeState> _shakes = new();
        readonly List<CameraShakeInstance> _view = new();
        int _nextHandle = 1;
        float _globalIntensity = 1f;

        Vector3 _currentOffset;
        float _currentRotationZ;

        public Vector3 CurrentOffset => _currentOffset;
        public float CurrentRotationZ => _currentRotationZ;
        public IReadOnlyList<CameraShakeInstance> ActiveShakes => _view;

        public int PlayShake(CameraShakePreset preset, int priority = 0)
        {
            int handle = _nextHandle++;
            var state = new ShakeState(handle, preset, priority);
            _shakes.Add(state);
            BuildView();
            return handle;
        }

        public void StopShake(int handle, float fadeOutSeconds = 0.1f)
        {
            for (int i = 0; i < _shakes.Count; i++)
            {
                if (_shakes[i].Handle != handle)
                    continue;

                if (fadeOutSeconds <= 0f)
                {
                    _shakes.RemoveAt(i);
                    BuildView();
                    return;
                }

                var s = _shakes[i];
                s.RequestStop(fadeOutSeconds);
                _shakes[i] = s;
                return;
            }
        }

        public void StopAll(float fadeOutSeconds = 0.1f)
        {
            if (fadeOutSeconds <= 0f)
            {
                _shakes.Clear();
                BuildView();
                return;
            }

            for (int i = 0; i < _shakes.Count; i++)
            {
                var s = _shakes[i];
                s.RequestStop(fadeOutSeconds);
                _shakes[i] = s;
            }
        }

        public void SetGlobalIntensity(float scale)
        {
            _globalIntensity = Mathf.Max(0f, scale);
        }

        public void Tick(float dtScaled, float dtUnscaled)
        {
            _currentOffset = Vector3.zero;
            _currentRotationZ = 0f;

            if (_shakes.Count == 0)
                return;

            for (int i = _shakes.Count - 1; i >= 0; i--)
            {
                var s = _shakes[i];
                float dt = s.TimeScaleBehavior == TimeScaleBehavior.Unscaled ? dtUnscaled : dtScaled;
                if (dt <= 0f)
                    continue;

                s.Tick(dt);

                if (s.IsFinished)
                {
                    _shakes.RemoveAt(i);
                    continue;
                }

                var offset = SampleOffset(s);
                _currentOffset += offset;
                _currentRotationZ += SampleRotation(s);
                _shakes[i] = s;
            }

            _currentOffset *= _globalIntensity;
            _currentRotationZ *= _globalIntensity;
            BuildView();
        }

        static Vector3 SampleOffset(in ShakeState s)
        {
            float phase = s.Elapsed * s.Frequency;
            float amp = s.AmplitudePos * s.GetDecay();

            float x = HashNoise(s.Seed, phase);
            float y = HashNoise(s.Seed + 1u, phase + 11.3f);
            return new Vector3(x, y, 0f) * amp;
        }

        static float SampleRotation(in ShakeState s)
        {
            float phase = s.Elapsed * s.Frequency;
            float amp = s.AmplitudeRotDeg * s.GetDecay();
            float noise = HashNoise(s.Seed + 2u, phase + 23.7f);
            return noise * amp;
        }

        static float HashNoise(uint seed, float phase)
        {
            float x = phase * 0.21f + seed * 0.001f;
            float y = phase * 0.37f + seed * 0.002f;
            float n = Mathf.PerlinNoise(x, y);
            return n * 2f - 1f;
        }

        void BuildView()
        {
            _view.Clear();
            for (int i = 0; i < _shakes.Count; i++)
            {
                _view.Add(_shakes[i].ToPublic());
            }
        }

        struct ShakeState
        {
            public readonly int Handle;
            public readonly float AmplitudePos;
            public readonly float AmplitudeRotDeg;
            public readonly float Frequency;
            public readonly float Duration;
            public readonly CameraShakeDecayMode DecayMode;
            public readonly float LambdaOrDecay;
            public readonly uint Seed;
            public readonly TimeScaleBehavior TimeScaleBehavior;
            public readonly int Priority;

            public float Elapsed;
            float _stopFadeDuration;
            float _stopElapsed;
            bool _stopRequested;

            public ShakeState(int handle, CameraShakePreset preset, int priority)
            {
                Handle = handle;
                AmplitudePos = preset.AmplitudePosition;
                AmplitudeRotDeg = preset.AmplitudeRotationDeg;
                Frequency = Mathf.Max(0.01f, preset.Frequency);
                Duration = Mathf.Max(0f, preset.Duration);
                DecayMode = preset.DecayMode;
                LambdaOrDecay = preset.LambdaOrDecay;
                Seed = preset.Seed;
                TimeScaleBehavior = preset.TimeScaleBehavior;
                Priority = priority;
                Elapsed = 0f;
                _stopFadeDuration = 0f;
                _stopElapsed = 0f;
                _stopRequested = false;
            }

            public void Tick(float dt)
            {
                Elapsed += dt;
                if (_stopRequested)
                    _stopElapsed += dt;
            }

            public void RequestStop(float fadeOut)
            {
                _stopRequested = true;
                _stopFadeDuration = Mathf.Max(0f, fadeOut);
                _stopElapsed = 0f;
            }

            public float GetDecay()
            {
                float baseDecay;
                if (Duration <= 0f)
                {
                    baseDecay = 1f;
                }
                else if (DecayMode == CameraShakeDecayMode.Linear)
                {
                    baseDecay = Mathf.Clamp01(1f - (Elapsed / Duration));
                }
                else
                {
                    baseDecay = Mathf.Exp(-Mathf.Max(0f, LambdaOrDecay) * Elapsed);
                }

                if (!_stopRequested || _stopFadeDuration <= 0f)
                    return baseDecay;

                float fade = Mathf.Clamp01(1f - (_stopElapsed / _stopFadeDuration));
                return baseDecay * fade;
            }

            public bool IsFinished
            {
                get
                {
                    if (_stopRequested && _stopFadeDuration <= 0f)
                        return true;

                    if (_stopRequested && _stopElapsed >= _stopFadeDuration)
                        return true;

                    if (Duration <= 0f)
                        return false;

                    return Elapsed >= Duration;
                }
            }

            public CameraShakeInstance ToPublic()
            {
                return new CameraShakeInstance(
                    Handle,
                    AmplitudePos,
                    AmplitudeRotDeg,
                    Frequency,
                    Duration,
                    DecayMode,
                    LambdaOrDecay,
                    Seed,
                    TimeScaleBehavior,
                    Priority);
            }
        }
    }
}
