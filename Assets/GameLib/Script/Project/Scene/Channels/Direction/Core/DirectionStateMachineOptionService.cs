#nullable enable
using System.Diagnostics.CodeAnalysis;
using Game.StateMachine;
using Game.StateMachine.Generated;
using UnityEngine;
using VContainer.Unity;

namespace Game.Direction
{
    public sealed class DirectionStateMachineOptionService :
        IScopeTickHandler,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        enum Cardinal
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 3,
            Right = 4,
        }

        enum Horizontal
        {
            None = 0,
            Left = 1,
            Right = 2,
        }

        enum Vertical
        {
            None = 0,
            Up = 1,
            Down = 2,
        }

        readonly DirectionStateMachineOptionConfig _config;
        readonly IStateMachine _stateMachine;
        readonly IDirectionChannelHub _hub;

        readonly float _activationThreshold;
        readonly float _holdThreshold;
        readonly float _switchThreshold;
        readonly float _zeroHoldThreshold;
        readonly bool _outputToGlobal;
        readonly bool _useCustomCardinalAngles;

        readonly float _upCenterDeg;
        readonly float _upHalfRangeDeg;
        readonly float _leftCenterDeg;
        readonly float _leftHalfRangeDeg;
        readonly float _rightCenterDeg;
        readonly float _rightHalfRangeDeg;
        readonly float _downCenterDeg;
        readonly float _downHalfRangeDeg;

        readonly Vector2 _upCenterDir;
        readonly Vector2 _leftCenterDir;
        readonly Vector2 _rightCenterDir;
        readonly Vector2 _downCenterDir;

        uint _lastVersion;
        bool _enabled;

        Cardinal _lastCardinal = Cardinal.None;
        Horizontal _lastHorizontal = Horizontal.None;
        Vertical _lastVertical = Vertical.None;

        public DirectionStateMachineOptionService(
            DirectionStateMachineOptionConfig config,
            IStateMachine stateMachine,
            IDirectionChannelHub hub)
        {
            _config = config;
            _stateMachine = stateMachine;
            _hub = hub;

            // Normalize thresholds for stable hysteresis.
            // - activation: enter threshold
            // - hold: stay threshold (must be <= activation)
            // - switch: stricter threshold to flip from the opposite direction (prevents chattering near 0)
            _activationThreshold = Mathf.Clamp01(config.ActivationThreshold);
            _holdThreshold = Mathf.Min(_activationThreshold, Mathf.Clamp01(config.HoldThreshold));
            var margin = Mathf.Max(0f, _activationThreshold - _holdThreshold);
            _switchThreshold = Mathf.Clamp01(_activationThreshold + margin * 0.5f);

            _zeroHoldThreshold = Mathf.Max(0f, config.ZeroHoldThreshold);
            _outputToGlobal = config.OutputToGlobal;

            var angleConfig = config.CardinalAngleConfig;
            _useCustomCardinalAngles = angleConfig.Enabled;

            _upCenterDeg = NormalizeSignedAngle(angleConfig.UpCenterDeg);
            _upHalfRangeDeg = ClampHalfRange(angleConfig.UpHalfRangeDeg);
            _leftCenterDeg = NormalizeSignedAngle(angleConfig.LeftCenterDeg);
            _leftHalfRangeDeg = ClampHalfRange(angleConfig.LeftHalfRangeDeg);
            _rightCenterDeg = NormalizeSignedAngle(angleConfig.RightCenterDeg);
            _rightHalfRangeDeg = ClampHalfRange(angleConfig.RightHalfRangeDeg);
            _downCenterDeg = NormalizeSignedAngle(angleConfig.DownCenterDeg);
            _downHalfRangeDeg = ClampHalfRange(angleConfig.DownHalfRangeDeg);

            _upCenterDir = DirectionFromAngle(_upCenterDeg);
            _leftCenterDir = DirectionFromAngle(_leftCenterDeg);
            _rightCenterDir = DirectionFromAngle(_rightCenterDeg);
            _downCenterDir = DirectionFromAngle(_downCenterDeg);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _enabled = true;
            if (isReset)
            {
                _lastVersion = 0;
                _lastCardinal = Cardinal.None;
                _lastHorizontal = Horizontal.None;
                _lastVertical = Vertical.None;
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _enabled = false;
            ResetOptions();
            if (isReset)
            {
                _lastVersion = 0;
                _lastCardinal = Cardinal.None;
                _lastHorizontal = Horizontal.None;
                _lastVertical = Vertical.None;
            }
        }

        public void Tick()
        {
            if (!_enabled)
                return;

            var output = _hub?.Output;
            if (output == null)
                return;

            if (!output.HasChanged(_lastVersion))
                return;

            _lastVersion = output.Version;
            EvaluateOutput(output.OutputValue);
        }

        void EvaluateOutput(Vector2 value)
        {
            float zeroThreshold = _zeroHoldThreshold;
            if (zeroThreshold > 0f)
            {
                float zeroThresholdSq = zeroThreshold * zeroThreshold;
                if (value.sqrMagnitude <= zeroThresholdSq)
                {
                    if (_config.ZeroSpeedPolicy == ZeroSpeedOptionPolicy.Clear)
                    {
                        ResetOptions();
                    }
                    return;
                }
            }

            if (_useCustomCardinalAngles)
            {
                switch (_config.DiagonalPolicy)
                {
                    case DiagonalOptionPolicy.DualAxis:
                        EvaluateDualAxisCustom(value);
                        break;
                    default:
                        EvaluateSingleCustom(value);
                        break;
                }
                return;
            }

            switch (_config.DiagonalPolicy)
            {
                case DiagonalOptionPolicy.DualAxis:
                    EvaluateDualAxis(value);
                    break;
                default:
                    EvaluateSingle(value);
                    break;
            }
        }

        void EvaluateSingle(Vector2 value)
        {
            var prev = _lastCardinal;

            float x = value.x;
            float y = value.y;

            bool posX = EvaluatePositive(x, prev == Cardinal.Right, prev == Cardinal.Left);
            bool negX = EvaluateNegative(x, prev == Cardinal.Left, prev == Cardinal.Right);
            bool posY = EvaluatePositive(y, prev == Cardinal.Up, prev == Cardinal.Down);
            bool negY = EvaluateNegative(y, prev == Cardinal.Down, prev == Cardinal.Up);

            bool hasH = posX || negX;
            bool hasV = posY || negY;

            if (!hasH && !hasV)
            {
                // 縺励″縺・､譛ｪ貅縺ｪ繧峨∵婿蜷代ｒ譖ｴ譁ｰ縺励↑縺・ｼ・ption縺ｪ縺励ｒ菴懊ｉ縺ｪ縺・◆繧・ｼ・
                return;
            }

            if (hasH && hasV && _config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferPrevious)
            {
                if (IsCardinalHeld(prev, x, y))
                {
                    // 蜑榊屓縺ｮ譁ｹ蜷代′縺ｾ縺 hold 譚｡莉ｶ繧呈ｺ縺溘☆縺ｪ繧臥ｶｭ謖・
                    return;
                }
            }

            Cardinal next = SelectSingleCardinal(x, y, hasH, hasV, posX, negX, posY, negY);

            if (next == Cardinal.None || next == prev)
                return;

            ApplyCardinal(prev, next);
            _lastCardinal = next;
        }

        Cardinal SelectSingleCardinal(
            float x,
            float y,
            bool hasH,
            bool hasV,
            bool posX,
            bool negX,
            bool posY,
            bool negY)
        {
            if (hasH && hasV)
            {
                if (_config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferHorizontal ||
                    _config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferPrevious)
                {
                    return posX ? Cardinal.Right : Cardinal.Left;
                }

                float ax = Mathf.Abs(x);
                float ay = Mathf.Abs(y);
                if (ax >= ay)
                {
                    // 蜷檎紫縺ｯ豌ｴ蟷ｳ蜆ｪ蜈・
                    return posX ? Cardinal.Right : Cardinal.Left;
                }

                return posY ? Cardinal.Up : Cardinal.Down;
            }

            if (hasH)
                return posX ? Cardinal.Right : Cardinal.Left;

            if (hasV)
                return posY ? Cardinal.Up : Cardinal.Down;

            return Cardinal.None;
        }

        bool IsCardinalHeld(Cardinal c, float x, float y)
        {
            switch (c)
            {
                case Cardinal.Right:
                    return x >= _holdThreshold;
                case Cardinal.Left:
                    return x <= -_holdThreshold;
                case Cardinal.Up:
                    return y >= _holdThreshold;
                case Cardinal.Down:
                    return y <= -_holdThreshold;
                default:
                    return false;
            }
        }

        void EvaluateDualAxis(Vector2 value)
        {
            float x = value.x;
            float y = value.y;

            bool posX = EvaluatePositive(x, _lastHorizontal == Horizontal.Right, _lastHorizontal == Horizontal.Left);
            bool negX = EvaluateNegative(x, _lastHorizontal == Horizontal.Left, _lastHorizontal == Horizontal.Right);
            bool posY = EvaluatePositive(y, _lastVertical == Vertical.Up, _lastVertical == Vertical.Down);
            bool negY = EvaluateNegative(y, _lastVertical == Vertical.Down, _lastVertical == Vertical.Up);

            bool hasH = posX || negX;
            bool hasV = posY || negY;

            if (!hasH && !hasV)
            {
                // 縺励″縺・､譛ｪ貅縺ｪ繧峨∵婿蜷代ｒ譖ｴ譁ｰ縺励↑縺・ｼ・ption縺ｪ縺励ｒ菴懊ｉ縺ｪ縺・◆繧・ｼ・
                return;
            }
            // NOTE:
            // Movement.left / up 縺ｪ縺ｩ縺ｯ隕ｪ OptionKey 縺悟酔縺倥〒繧ゅ・
            // full OptionValue 繧ｭ繝ｼ・・ovement.left, Movement.up・峨ｒ菴ｵ逕ｨ縺励※菫晄戟縺ｧ縺阪ｋ縺溘ａ
            // DualAxis 縺ｧ縺ｯ豌ｴ蟷ｳ繝ｻ蝙ら峩繧貞酔譎ゅ↓驕ｩ逕ｨ縺励※繧医＞縲・

            if (hasH)
            {
                var nextH = posX ? Horizontal.Right : Horizontal.Left;
                if (nextH != _lastHorizontal)
                {
                    // Ensure mutually exclusive options on this axis don't accumulate.
                    ClearAxisOption(Options.Movement.left);
                    ClearAxisOption(Options.Movement.right);
                    ApplyAxisOption(nextH == Horizontal.Right ? Options.Movement.right : Options.Movement.left);
                    _lastHorizontal = nextH;
                }
            }
            else
            {
                ClearAxisOption(Options.Movement.left);
                ClearAxisOption(Options.Movement.right);
                _lastHorizontal = Horizontal.None;
            }

            if (hasV)
            {
                var nextV = posY ? Vertical.Up : Vertical.Down;
                if (nextV != _lastVertical)
                {
                    // Ensure mutually exclusive options on this axis don't accumulate.
                    ClearAxisOption(Options.Movement.up);
                    ClearAxisOption(Options.Movement.down);
                    ApplyAxisOption(nextV == Vertical.Up ? Options.Movement.up : Options.Movement.down);
                    _lastVertical = nextV;
                }
            }
            else
            {
                ClearAxisOption(Options.Movement.up);
                ClearAxisOption(Options.Movement.down);
                _lastVertical = Vertical.None;
            }
        }

        void EvaluateSingleCustom(Vector2 value)
        {
            var prev = _lastCardinal;

            bool right = EvaluateCardinalCustom(value, Cardinal.Right, prev, out var rightScore);
            bool left = EvaluateCardinalCustom(value, Cardinal.Left, prev, out var leftScore);
            bool up = EvaluateCardinalCustom(value, Cardinal.Up, prev, out var upScore);
            bool down = EvaluateCardinalCustom(value, Cardinal.Down, prev, out var downScore);

            var bestH = ResolveBestHorizontal(right, left, rightScore, leftScore, out var hScore);
            var bestV = ResolveBestVertical(up, down, upScore, downScore, out var vScore);
            bool hasH = bestH != Cardinal.None;
            bool hasV = bestV != Cardinal.None;

            // When no sector matches, keep last direction and do not update options.
            if (!hasH && !hasV)
                return;

            if (hasH && hasV && _config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferPrevious)
            {
                if (IsCardinalStillActive(prev, right, left, up, down))
                    return;
            }

            Cardinal next;
            if (hasH && hasV)
            {
                if (_config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferHorizontal ||
                    _config.DiagonalPolicy == DiagonalOptionPolicy.SinglePreferPrevious)
                {
                    next = bestH;
                }
                else
                {
                    next = hScore >= vScore ? bestH : bestV;
                }
            }
            else
            {
                next = hasH ? bestH : bestV;
            }

            if (next == Cardinal.None || next == prev)
                return;

            ApplyCardinal(prev, next);
            _lastCardinal = next;
        }

        void EvaluateDualAxisCustom(Vector2 value)
        {
            bool right = EvaluateHorizontalCustom(value, Horizontal.Right, _lastHorizontal, out var rightScore);
            bool left = EvaluateHorizontalCustom(value, Horizontal.Left, _lastHorizontal, out var leftScore);
            bool up = EvaluateVerticalCustom(value, Vertical.Up, _lastVertical, out var upScore);
            bool down = EvaluateVerticalCustom(value, Vertical.Down, _lastVertical, out var downScore);

            var nextHorizontal = ResolveBestHorizontalAxis(right, left, rightScore, leftScore);
            var nextVertical = ResolveBestVerticalAxis(up, down, upScore, downScore);
            bool hasH = nextHorizontal != Horizontal.None;
            bool hasV = nextVertical != Vertical.None;

            // When no sector matches, keep last direction and do not update options.
            if (!hasH && !hasV)
                return;
            // NOTE:
            // Custom mapping + DualAxis 縺ｧ繧ゅ∝酔荳隕ｪ OptionKey 繧堤炊逕ｱ縺ｫ Single 縺ｸ關ｽ縺ｨ縺輔↑縺・・
            // full OptionValue 繧ｭ繝ｼ繧剃ｽｿ縺・％縺ｨ縺ｧ驥崎､・婿蜷托ｼ・eft+down 遲会ｼ峨ｒ菫晄戟縺ｧ縺阪ｋ縲・

            if (hasH)
            {
                if (nextHorizontal != _lastHorizontal)
                {
                    ClearAxisOption(Options.Movement.left);
                    ClearAxisOption(Options.Movement.right);
                    ApplyAxisOption(nextHorizontal == Horizontal.Right ? Options.Movement.right : Options.Movement.left);
                    _lastHorizontal = nextHorizontal;
                }
            }
            else
            {
                ClearAxisOption(Options.Movement.left);
                ClearAxisOption(Options.Movement.right);
                _lastHorizontal = Horizontal.None;
            }

            if (hasV)
            {
                if (nextVertical != _lastVertical)
                {
                    ClearAxisOption(Options.Movement.up);
                    ClearAxisOption(Options.Movement.down);
                    ApplyAxisOption(nextVertical == Vertical.Up ? Options.Movement.up : Options.Movement.down);
                    _lastVertical = nextVertical;
                }
            }
            else
            {
                ClearAxisOption(Options.Movement.up);
                ClearAxisOption(Options.Movement.down);
                _lastVertical = Vertical.None;
            }
        }

        bool EvaluateCardinalCustom(Vector2 value, Cardinal candidate, Cardinal previous, out float score)
        {
            score = 0f;
            if (!TryGetCardinalSector(candidate, out var centerDeg, out var halfRangeDeg, out var centerDir))
                return false;

            bool previousSame = previous == candidate;
            bool previousOpposite = IsOppositeCardinal(candidate, previous);
            return EvaluateDirectionalSector(value, centerDeg, halfRangeDeg, centerDir, previousSame, previousOpposite, out score);
        }

        bool EvaluateHorizontalCustom(Vector2 value, Horizontal candidate, Horizontal previous, out float score)
        {
            score = 0f;
            if (!TryGetHorizontalSector(candidate, out var centerDeg, out var halfRangeDeg, out var centerDir))
                return false;

            bool previousSame = previous == candidate;
            bool previousOpposite = previous != Horizontal.None && previous != candidate;
            return EvaluateDirectionalSector(value, centerDeg, halfRangeDeg, centerDir, previousSame, previousOpposite, out score);
        }

        bool EvaluateVerticalCustom(Vector2 value, Vertical candidate, Vertical previous, out float score)
        {
            score = 0f;
            if (!TryGetVerticalSector(candidate, out var centerDeg, out var halfRangeDeg, out var centerDir))
                return false;

            bool previousSame = previous == candidate;
            bool previousOpposite = previous != Vertical.None && previous != candidate;
            return EvaluateDirectionalSector(value, centerDeg, halfRangeDeg, centerDir, previousSame, previousOpposite, out score);
        }

        bool EvaluateDirectionalSector(
            Vector2 value,
            float centerDeg,
            float halfRangeDeg,
            Vector2 centerDir,
            bool previousSame,
            bool previousOpposite,
            out float directionalScore)
        {
            directionalScore = 0f;
            if (value.sqrMagnitude <= 0.0000001f)
                return false;

            var inputAngle = Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg;
            if (!ContainsAngle(inputAngle, centerDeg, halfRangeDeg))
                return false;

            directionalScore = Vector2.Dot(value, centerDir);
            if (directionalScore <= 0f)
                return false;

            if (previousSame)
                return directionalScore >= _holdThreshold;

            if (previousOpposite)
                return directionalScore >= _switchThreshold;

            return directionalScore >= _activationThreshold;
        }

        bool IsCardinalStillActive(Cardinal prev, bool right, bool left, bool up, bool down)
        {
            return prev switch
            {
                Cardinal.Right => right,
                Cardinal.Left => left,
                Cardinal.Up => up,
                Cardinal.Down => down,
                _ => false,
            };
        }

        static Cardinal ResolveBestHorizontal(bool right, bool left, float rightScore, float leftScore, out float score)
        {
            score = 0f;
            if (right && left)
            {
                if (rightScore >= leftScore)
                {
                    score = rightScore;
                    return Cardinal.Right;
                }

                score = leftScore;
                return Cardinal.Left;
            }

            if (right)
            {
                score = rightScore;
                return Cardinal.Right;
            }

            if (left)
            {
                score = leftScore;
                return Cardinal.Left;
            }

            return Cardinal.None;
        }

        static Cardinal ResolveBestVertical(bool up, bool down, float upScore, float downScore, out float score)
        {
            score = 0f;
            if (up && down)
            {
                if (upScore >= downScore)
                {
                    score = upScore;
                    return Cardinal.Up;
                }

                score = downScore;
                return Cardinal.Down;
            }

            if (up)
            {
                score = upScore;
                return Cardinal.Up;
            }

            if (down)
            {
                score = downScore;
                return Cardinal.Down;
            }

            return Cardinal.None;
        }

        static Horizontal ResolveBestHorizontalAxis(bool right, bool left, float rightScore, float leftScore)
        {
            if (right && left)
                return rightScore >= leftScore ? Horizontal.Right : Horizontal.Left;

            if (right)
                return Horizontal.Right;
            if (left)
                return Horizontal.Left;
            return Horizontal.None;
        }

        static Vertical ResolveBestVerticalAxis(bool up, bool down, float upScore, float downScore)
        {
            if (up && down)
                return upScore >= downScore ? Vertical.Up : Vertical.Down;

            if (up)
                return Vertical.Up;
            if (down)
                return Vertical.Down;
            return Vertical.None;
        }

        bool TryGetCardinalSector(Cardinal cardinal, out float centerDeg, out float halfRangeDeg, out Vector2 centerDir)
        {
            switch (cardinal)
            {
                case Cardinal.Up:
                    centerDeg = _upCenterDeg;
                    halfRangeDeg = _upHalfRangeDeg;
                    centerDir = _upCenterDir;
                    return true;
                case Cardinal.Down:
                    centerDeg = _downCenterDeg;
                    halfRangeDeg = _downHalfRangeDeg;
                    centerDir = _downCenterDir;
                    return true;
                case Cardinal.Left:
                    centerDeg = _leftCenterDeg;
                    halfRangeDeg = _leftHalfRangeDeg;
                    centerDir = _leftCenterDir;
                    return true;
                case Cardinal.Right:
                    centerDeg = _rightCenterDeg;
                    halfRangeDeg = _rightHalfRangeDeg;
                    centerDir = _rightCenterDir;
                    return true;
                default:
                    centerDeg = 0f;
                    halfRangeDeg = 0f;
                    centerDir = Vector2.right;
                    return false;
            }
        }

        bool TryGetHorizontalSector(Horizontal horizontal, out float centerDeg, out float halfRangeDeg, out Vector2 centerDir)
        {
            switch (horizontal)
            {
                case Horizontal.Right:
                    centerDeg = _rightCenterDeg;
                    halfRangeDeg = _rightHalfRangeDeg;
                    centerDir = _rightCenterDir;
                    return true;
                case Horizontal.Left:
                    centerDeg = _leftCenterDeg;
                    halfRangeDeg = _leftHalfRangeDeg;
                    centerDir = _leftCenterDir;
                    return true;
                default:
                    centerDeg = 0f;
                    halfRangeDeg = 0f;
                    centerDir = Vector2.right;
                    return false;
            }
        }

        bool TryGetVerticalSector(Vertical vertical, out float centerDeg, out float halfRangeDeg, out Vector2 centerDir)
        {
            switch (vertical)
            {
                case Vertical.Up:
                    centerDeg = _upCenterDeg;
                    halfRangeDeg = _upHalfRangeDeg;
                    centerDir = _upCenterDir;
                    return true;
                case Vertical.Down:
                    centerDeg = _downCenterDeg;
                    halfRangeDeg = _downHalfRangeDeg;
                    centerDir = _downCenterDir;
                    return true;
                default:
                    centerDeg = 0f;
                    halfRangeDeg = 0f;
                    centerDir = Vector2.up;
                    return false;
            }
        }

        static bool ContainsAngle(float angleDeg, float centerDeg, float halfRangeDeg)
        {
            if (halfRangeDeg >= 180f)
                return true;

            var delta = Mathf.Abs(Mathf.DeltaAngle(centerDeg, angleDeg));
            return delta <= halfRangeDeg;
        }

        static bool IsOppositeCardinal(Cardinal candidate, Cardinal previous)
        {
            return (candidate == Cardinal.Up && previous == Cardinal.Down) ||
                   (candidate == Cardinal.Down && previous == Cardinal.Up) ||
                   (candidate == Cardinal.Left && previous == Cardinal.Right) ||
                   (candidate == Cardinal.Right && previous == Cardinal.Left);
        }

        static float NormalizeSignedAngle(float degrees)
        {
            return Mathf.DeltaAngle(0f, degrees);
        }

        static float ClampHalfRange(float degrees)
        {
            return Mathf.Clamp(degrees, 0f, 180f);
        }

        static Vector2 DirectionFromAngle(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        bool EvaluatePositive(float axisValue, bool previousSameSign, bool previousOppositeSign)
        {
            if (previousSameSign)
                return axisValue >= _holdThreshold;

            // When switching from the opposite sign, require a stricter threshold to avoid flip-flopping.
            if (previousOppositeSign)
                return axisValue >= _switchThreshold;

            return axisValue >= _activationThreshold;
        }

        bool EvaluateNegative(float axisValue, bool previousSameSign, bool previousOppositeSign)
        {
            if (previousSameSign)
                return axisValue <= -_holdThreshold;

            // When switching from the opposite sign, require a stricter threshold to avoid flip-flopping.
            if (previousOppositeSign)
                return axisValue <= -_switchThreshold;

            return axisValue <= -_activationThreshold;
        }

        void ApplyCardinal(Cardinal prev, Cardinal next)
        {
            var prevValue = GetOptionValue(prev);
            var nextValue = GetOptionValue(next);

            // Always clear the previous direction option when switching.
            // This prevents value-key accumulation like Movement.right + Movement.left both staying set.
            if (!string.IsNullOrEmpty(prevValue) && !string.IsNullOrEmpty(nextValue) && prevValue != nextValue)
                ClearOptionValue(prevValue);

            ApplyAxisOption(nextValue);
        }

        string GetOptionValue(Cardinal c)
        {
            return c switch
            {
                Cardinal.Up => Options.Movement.up,
                Cardinal.Down => Options.Movement.down,
                Cardinal.Left => Options.Movement.left,
                Cardinal.Right => Options.Movement.right,
                _ => "",
            };
        }

        void ResetOptions()
        {
            ClearOptionValue(Options.Movement.up);
            ClearOptionValue(Options.Movement.down);
            ClearOptionValue(Options.Movement.left);
            ClearOptionValue(Options.Movement.right);
            _lastCardinal = Cardinal.None;
            _lastHorizontal = Horizontal.None;
            _lastVertical = Vertical.None;
        }

        void ClearOptionValue(string optionValue)
        {
            if (string.IsNullOrEmpty(optionValue))
                return;

            if (_outputToGlobal)
            {
                ClearGlobalOptionValue(optionValue);
                return;
            }

            if (TryGetOptionKey(optionValue, out var layerKey, out var optionKey))
            {
                _stateMachine.SetLocalOption(layerKey, optionKey, null);
                _stateMachine.SetLocalOption(layerKey, optionValue, null);
                return;
            }

            _stateMachine.SetGlobalOption(optionValue, null);
        }

        void ApplyAxisOption(string optionValue)
        {
            if (string.IsNullOrEmpty(optionValue))
                return;

            if (_outputToGlobal)
            {
                ApplyGlobalOptionValue(optionValue);
                return;
            }

            if (TryGetOptionKey(optionValue, out var layerKey, out var optionKey))
            {
                // Keep both the parent OptionKey (for FlipX evaluation) and the full OptionValue (for option rules).
                _stateMachine.SetLocalOption(layerKey, optionKey, optionValue);
                _stateMachine.SetLocalOption(layerKey, optionValue, optionValue);
                return;
            }

            _stateMachine.SetGlobalOption(optionValue, "true");
        }

        void ClearGlobalOptionValue(string optionValue)
        {
            if (TryGetOptionKey(optionValue, out _, out var optionKey))
            {
                _stateMachine.SetGlobalOption(optionKey, null);
                _stateMachine.SetGlobalOption(optionValue, null);
                return;
            }

            _stateMachine.SetGlobalOption(optionValue, null);
        }

        void ApplyGlobalOptionValue(string optionValue)
        {
            if (TryGetOptionKey(optionValue, out _, out var optionKey))
            {
                // Keep both the parent OptionKey and full OptionValue key in global map.
                _stateMachine.SetGlobalOption(optionKey, optionValue);
                _stateMachine.SetGlobalOption(optionValue, optionValue);
                return;
            }

            _stateMachine.SetGlobalOption(optionValue, "true");
        }

        void ClearAxisOption(string optionValue)
        {
            ClearOptionValue(optionValue);
        }

        static bool TryGetOptionKey(
            string optionValue,
            [NotNullWhen(true)] out string? layerKey,
            [NotNullWhen(true)] out string? optionKey)
        {
            layerKey = null;
            optionKey = null;

            if (!StateKeyUtils.SplitOptionKeyAndValue(optionValue, out optionKey, out _))
                return false;

            layerKey = optionKey;
            if (StateKeyUtils.SplitLayerAndLeaf(optionKey, out var maybeLayerKey, out _))
                layerKey = maybeLayerKey;

            return !string.IsNullOrEmpty(layerKey) && !string.IsNullOrEmpty(optionKey);
        }
    }
}
