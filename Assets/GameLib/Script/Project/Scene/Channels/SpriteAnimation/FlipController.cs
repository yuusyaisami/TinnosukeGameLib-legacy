using DG.Tweening;
using Game.MaterialFx;
using Game.MaterialFx.Generated;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class FlipController
    {
        readonly IMaterialFxService _fx;
        readonly string _context;
        readonly int _priority;

        float _current;
        float _target;
        Tween _tween;

        bool _baselineApplied;
        float _lastEulerY = float.NaN;

        public FlipController(IMaterialFxService fx, string context, int priority)
        {
            _fx = fx;
            _context = context;
            _priority = priority;
        }

        public void SetTarget(bool flipX, float duration, Ease ease)
        {
            SetTargetAngle(flipX ? 180f : 0f, duration, ease);
        }

        public void SetTargetAngle(float eulerY, float duration, Ease ease)
        {
            _target = eulerY;

            KillTween();

            if (_fx == null)
            {
                _current = _target;
                return;
            }

            if (Mathf.Approximately(_current, _target) || duration <= 0f)
            {
                ApplyImmediate(_target);
                return;
            }

            _tween = DOTween.To(
                    () => _current,
                    v =>
                    {
                        _current = v;
                        ApplyToShader(v);
                    },
                    _target,
                    duration)
                .SetEase(ease)
                .SetUpdate(_fx != null && _fx.UseUnscaledTime)
                .OnComplete(() =>
                {
                    ApplyImmediate(_target);
                    _tween = null;
                });
        }

        public void Trigger(float duration, Ease ease)
        {
            var currentTarget = _target;
            var next = Mathf.Abs(Mathf.DeltaAngle(currentTarget, 180f)) <= 90f ? 0f : 180f;
            SetTargetAngle(next, duration, ease);
        }

        public void StopAndSnap(bool disableLayer)
        {
            KillTween();
            ApplyImmediate(_target);

            if (disableLayer && _fx != null)
            {
                RemoveAllFlipLayers();
            }

            _baselineApplied = false;
            _lastEulerY = float.NaN;
        }

        void KillTween()
        {
            if (_tween != null && _tween.IsActive())
            {
                _tween.Kill(false);
            }
            _tween = null;
        }

        void ApplyImmediate(float value)
        {
            _current = value;
            _target = value;
            ApplyToShader(value);
        }

        void EnsureBaseline()
        {
            if (_fx == null)
                return;
            if (_baselineApplied)
                return;

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.Enabled,
                _context,
                MaterialFxTypedValue.FromBool(true),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.PerspectiveSign,
                _context,
                MaterialFxTypedValue.FromFloat(-1f),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.X,
                _context,
                MaterialFxTypedValue.FromFloat(0f),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.Z,
                _context,
                MaterialFxTypedValue.FromFloat(0f),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.PivotLocal,
                _context,
                MaterialFxTypedValue.FromVector2(Vector2.zero),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.Perspective,
                _context,
                MaterialFxTypedValue.FromFloat(0f),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.DepthScale,
                _context,
                MaterialFxTypedValue.FromFloat(1f),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.ScaleClamp,
                _context,
                MaterialFxTypedValue.FromVector2(new Vector2(0.01f, 100f)),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.FallbackHalfSize,
                _context,
                MaterialFxTypedValue.FromVector2(new Vector2(0.5f, 0.5f)),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvanceWarp.Shear,
                _context,
                MaterialFxTypedValue.FromVector2(Vector2.zero),
                MaterialFxBlendMode.Override,
                _priority);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvanceWarp.Bend,
                _context,
                MaterialFxTypedValue.FromVector2(Vector2.zero),
                MaterialFxBlendMode.Override,
                _priority);

            _baselineApplied = true;
        }

        void ApplyToShader(float eulerY)
        {
            if (_fx == null)
                return;

            EnsureBaseline();

            if (Mathf.Approximately(_lastEulerY, eulerY))
                return;
            _lastEulerY = eulerY;

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.Y,
                _context,
                MaterialFxTypedValue.FromFloat(eulerY),
                MaterialFxBlendMode.Override,
                _priority);
        }

        void RemoveAllFlipLayers()
        {
            if (_fx == null)
                return;

            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.Enabled, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.PerspectiveSign, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.X, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.Y, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.EulerDeg.Z, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.PivotLocal, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.Perspective, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.DepthScale, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.ScaleClamp, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvancedFlip2D.FallbackHalfSize, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvanceWarp.Shear, _context);
            _fx.RemoveLayer(MaterialFxKeys.BaseShader.AdvanceWarp.Bend, _context);
        }
    }
}
