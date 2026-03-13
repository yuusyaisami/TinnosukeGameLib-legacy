using DG.Tweening;
using Game.MaterialFx;
using Game.MaterialFx.Generated;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class TransitionController
    {
        readonly IMaterialFxService _fx;
        readonly string _fallbackContext;

        Tween _tween;
        float _progress;
        ITransitionProfile _activeProfile;
        bool _endApplied;

        public TransitionController(IMaterialFxService fx, string fallbackContext)
        {
            _fx = fx;
            _fallbackContext = fallbackContext;
        }

        public void Start(Sprite fromSprite, ITransitionProfile profile, float defaultDuration, Ease defaultEase)
        {
            if (_fx == null)
                return;

            Kill();

            _activeProfile = profile;
            _endApplied = false;

            float duration = profile != null ? profile.Duration : defaultDuration;
            Ease ease = profile != null ? profile.Ease : defaultEase;

            if (profile != null)
            {
                profile.ApplyBegin(_fx, fromSprite);
            }
            else
            {
                ApplyDefaultBegin(fromSprite);
            }

            _progress = 0f;

            bool unscaled = _fx?.UseUnscaledTime ?? false;

            _tween = DOTween.To(
                    () => _progress,
                    v =>
                    {
                        _progress = v;
                        if (_activeProfile != null)
                        {
                            _activeProfile.ApplyProgress(_fx, v);
                        }
                        else
                        {
                            ApplyDefaultProgress(v);
                        }
                    },
                    1f,
                    Mathf.Max(0.001f, duration))
                .SetEase(ease)
                .SetUpdate(unscaled)
                .OnComplete(() =>
                {
                    _progress = 1f;
                    ApplyEnd();
                    _activeProfile = null;
                    _tween = null;
                })
                .OnKill(() =>
                {
                    ApplyEnd();
                    _activeProfile = null;
                    _tween = null;
                });
        }

        public void Stop()
        {
            Kill();
            ApplyEnd();
            _activeProfile = null;
        }

        void Kill()
        {
            if (_tween != null && _tween.IsActive())
            {
                _tween.Kill(false);
            }
            _tween = null;
        }

        void ApplyEnd()
        {
            if (_fx == null)
                return;

            if (_endApplied)
                return;

            if (_activeProfile != null)
            {
                _activeProfile.ApplyEnd(_fx);
            }
            else
            {
                _fx.SetLayer(
                    MaterialFxKeys.BaseShader.Transition.Enabled,
                    _fallbackContext,
                    MaterialFxTypedValue.FromBool(false),
                    MaterialFxBlendMode.Override);
            }

            _endApplied = true;
        }

        void ApplyDefaultBegin(Sprite fromSprite)
        {
            string layer = _fallbackContext;

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Enabled,
                layer,
                MaterialFxTypedValue.FromBool(true),
                MaterialFxBlendMode.Override);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.BlendMode,
                layer,
                MaterialFxTypedValue.FromInt(0),
                MaterialFxBlendMode.Override);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Progress,
                layer,
                MaterialFxTypedValue.FromFloat(0f),
                MaterialFxBlendMode.Override);

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Params,
                layer,
                MaterialFxTypedValue.FromVector4(new Vector4(0.1f, 0.05f, 1f, 0f)),
                MaterialFxBlendMode.Override);

            if (fromSprite != null && fromSprite.texture != null)
            {
                _fx.SetLayer(
                    MaterialFxKeys.BaseShader.ExternalTextures.ExtTexA,
                    layer,
                    MaterialFxTypedValue.FromTexture(fromSprite.texture),
                    MaterialFxBlendMode.Override);

                Vector4 uvRect = CalcSpriteUVRect(fromSprite);
                _fx.SetLayer(
                    MaterialFxKeys.BaseShader.Transition.FromSpriteUVRect,
                    layer,
                    MaterialFxTypedValue.FromVector4(uvRect),
                    MaterialFxBlendMode.Override);
            }
        }

        void ApplyDefaultProgress(float t01)
        {
            string layer = _fallbackContext;

            _fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Progress,
                layer,
                MaterialFxTypedValue.FromFloat(Mathf.Clamp01(t01)),
                MaterialFxBlendMode.Override);
        }

        static Vector4 CalcSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(0, 0, 1, 1);

            Rect r = sprite.textureRect;
            float tw = sprite.texture.width;
            float th = sprite.texture.height;

            float minU = r.xMin / tw;
            float minV = r.yMin / th;
            float maxU = r.xMax / tw;
            float maxV = r.yMax / th;

            return new Vector4(minU, minV, maxU, maxV);
        }
    }
}

