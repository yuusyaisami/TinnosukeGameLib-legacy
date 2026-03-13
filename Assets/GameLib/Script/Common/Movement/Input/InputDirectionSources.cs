#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Input;

namespace Game.Movement
{
    public enum InputDirectionSourceType
    {
        Move = 0,
        Swipe = 1,
    }

    public enum InputDirectionEmitMode
    {
        Continuous = 0,
        OnPress = 1,
        OnRelease = 2,
    }

    [Serializable]
    public struct InputDirectionSourceConfig
    {
        [SerializeField] public InputDirectionSourceType Type;
        [SerializeField] public InputDirectionEmitMode EmitMode;
        [SerializeField] public bool ConsumeInput;

        [SerializeField, ShowIf("@Type == InputDirectionSourceType.Swipe"), Min(0f)] public float MinDistance;
        [SerializeField, ShowIf("@Type == InputDirectionSourceType.Swipe"), Min(0.0001f)] public float Scale;
        [SerializeField, ShowIf("@Type == InputDirectionSourceType.Swipe")] public bool Normalize;
        [SerializeField, ShowIf("@Type == InputDirectionSourceType.Swipe"), Min(0f)] public float MaxMagnitude;
    }

    public interface IInputDirectionSource
    {
        bool TryGetDirection(ref InputFrame frame, out Vector2 direction);
    }


    public readonly struct MoveInputSourceSettings
    {
        public readonly InputDirectionEmitMode EmitMode;
        public readonly bool ConsumeInput;

        public MoveInputSourceSettings(InputDirectionEmitMode emitMode, bool consumeInput)
        {
            EmitMode = emitMode;
            ConsumeInput = consumeInput;
        }
    }

    public readonly struct SwipeInputSourceSettings
    {
        public readonly InputDirectionEmitMode EmitMode;
        public readonly bool ConsumeInput;
        public readonly float MinDistance;
        public readonly float Scale;
        public readonly bool Normalize;
        public readonly float MaxMagnitude;

        public SwipeInputSourceSettings(
            InputDirectionEmitMode emitMode,
            bool consumeInput,
            float minDistance,
            float scale,
            bool normalize,
            float maxMagnitude)
        {
            EmitMode = emitMode;
            ConsumeInput = consumeInput;
            MinDistance = Mathf.Max(0f, minDistance);
            Scale = Mathf.Max(0.0001f, scale);
            Normalize = normalize;
            MaxMagnitude = Mathf.Max(0f, maxMagnitude);
        }
    }

    public sealed class MoveInputDirectionSource : IInputDirectionSource
    {
        readonly MoveInputSourceSettings _settings;
        bool _active;
        Vector2 _lastDirection;

        public MoveInputDirectionSource(in MoveInputSourceSettings settings)
        {
            _settings = settings;
            _active = false;
            _lastDirection = Vector2.zero;
        }

        public bool TryGetDirection(ref InputFrame frame, out Vector2 direction)
        {
            direction = Vector2.zero;
            var move = frame.Move;
            var hasMove = move != Vector2.zero;

            switch (_settings.EmitMode)
            {
                case InputDirectionEmitMode.Continuous:
                    {
                        if (!hasMove)
                            return false;

                        if (_settings.ConsumeInput)
                            frame.TryConsumeMove();

                        direction = move;
                        return true;
                    }

                case InputDirectionEmitMode.OnPress:
                    {
                        if (!hasMove)
                        {
                            _active = false;
                            return false;
                        }

                        if (_active)
                            return false;

                        if (_settings.ConsumeInput && !frame.TryConsumeMove())
                            return false;

                        _active = true;
                        _lastDirection = move;
                        direction = _lastDirection;
                        return true;
                    }

                case InputDirectionEmitMode.OnRelease:
                    {
                        if (hasMove)
                        {
                            if (_settings.ConsumeInput)
                                frame.TryConsumeMove();
                            _active = true;
                            _lastDirection = move;
                            return false;
                        }

                        if (_active)
                        {
                            _active = false;
                            direction = _lastDirection;
                            return direction != Vector2.zero;
                        }
                        return false;
                    }

                default:
                    return false;
            }
        }
    }

    public sealed class SwipeInputDirectionSource : IInputDirectionSource
    {
        readonly SwipeInputSourceSettings _settings;
        bool _tracking;
        bool _consumed;
        Vector2 _start;
        Vector2 _last;

        public SwipeInputDirectionSource(in SwipeInputSourceSettings settings)
        {
            _settings = settings;
            _tracking = false;
            _consumed = false;
            _start = Vector2.zero;
            _last = Vector2.zero;
        }

        public bool TryGetDirection(ref InputFrame frame, out Vector2 direction)
        {
            direction = Vector2.zero;
            var click = frame.Click;
            var pointer = frame.PointerScreen;

            if (click.Down)
            {
                if (_settings.ConsumeInput && !click.TryConsumeDown())
                {
                    _tracking = false;
                    _consumed = false;
                    return false;
                }

                _tracking = true;
                _consumed = _settings.ConsumeInput;
                _start = pointer;
                _last = pointer;

                if (_settings.EmitMode == InputDirectionEmitMode.OnPress)
                {
                    direction = Vector2.zero;
                    return false;
                }
            }

            if (_tracking)
            {
                _last = pointer;

                if (_settings.EmitMode == InputDirectionEmitMode.Continuous && click.Held)
                {
                    var delta = _last - _start;
                    if (!ValidateDelta(delta))
                        return false;
                    direction = ApplyDelta(delta);
                    return true;
                }

                if (click.Up)
                {
                    var delta = _last - _start;
                    _tracking = false;
                    _consumed = false;

                    if (_settings.EmitMode == InputDirectionEmitMode.OnRelease)
                    {
                        if (!ValidateDelta(delta))
                            return false;
                        direction = ApplyDelta(delta);
                        return true;
                    }
                }
            }

            if (!click.Held && !click.Down)
            {
                _tracking = false;
                _consumed = false;
            }

            return false;
        }

        bool ValidateDelta(Vector2 delta)
        {
            return delta.magnitude >= _settings.MinDistance;
        }

        Vector2 ApplyDelta(Vector2 delta)
        {
            var v = delta * _settings.Scale;
            if (_settings.Normalize && v.sqrMagnitude > 0.0001f)
                v = v.normalized;
            if (_settings.MaxMagnitude > 0f)
                v = Vector2.ClampMagnitude(v, _settings.MaxMagnitude);
            return v;
        }
    }
}
