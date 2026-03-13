#nullable enable
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// IUIElementState の Visible 変更を購読し、IUIVisibilityAdapter をDOTweenでFade制御する。
    /// </summary>
    public sealed class UIElementFadeService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IUIElementState _state;
        readonly IUIVisibilityAdapter _adapter;
        readonly UIFadeOptions _options;

        Tween? _tween;
        bool _subscribed;
        bool _lastVisible;

        public UIElementFadeService(IUIElementState state, IUIVisibilityAdapter adapter, UIFadeOptions options)
        {
            _state = state;
            _adapter = adapter;
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            KillTween();

            if (!_subscribed)
            {
                _subscribed = true;
                _state.OnStateChanged += HandleStateChanged;
            }

            _lastVisible = _state.IsVisible;
            ApplyInitial(_lastVisible);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            KillTween();

            if (_subscribed)
            {
                _subscribed = false;
                _state.OnStateChanged -= HandleStateChanged;
            }
        }

        void HandleStateChanged(UIElementStateChangedArgs args)
        {
            if (!args.VisibleChanged) return;
            StartFade(args.CurrentVisible);
        }

        void ApplyInitial(bool visible)
        {
            if (visible)
            {
                _adapter.SetRenderEnabled(true);
                _adapter.Visibility = 1f;
                _adapter.SetInteractable(true);
                return;
            }

            _adapter.SetInteractable(false);
            _adapter.Visibility = 0f;
            if (_options.DisableRenderWhenHidden)
            {
                _adapter.SetRenderEnabled(false);
            }
            else
            {
                _adapter.SetRenderEnabled(true);
            }
        }

        void StartFade(bool visible)
        {
            if (visible == _lastVisible) return;
            _lastVisible = visible;

            KillTween();

            if (visible)
            {
                FadeIn();
            }
            else
            {
                FadeOut();
            }
        }

        void FadeIn()
        {
            _adapter.SetRenderEnabled(true);

            if (_options.DisableInteractionDuringFade)
            {
                _adapter.SetInteractable(false);
            }
            else
            {
                _adapter.SetInteractable(true);
            }

            var duration = _options.FadeInSeconds;
            if (duration <= 0f)
            {
                _adapter.Visibility = 1f;
                _adapter.SetInteractable(true);
                return;
            }

            _tween = DOTween
                .To(() => _adapter.Visibility, a => _adapter.Visibility = a, 1f, duration)
                .SetEase(_options.FadeInEase)
                .SetUpdate(_options.UseUnscaledTime)
                .SetTarget(_adapter)
                .OnComplete(() =>
                {
                    _tween = null;
                    _adapter.SetInteractable(true);
                });
        }

        void FadeOut()
        {
            if (_options.DisableInteractionDuringFade)
            {
                _adapter.SetInteractable(false);
            }

            var duration = _options.FadeOutSeconds;
            if (duration <= 0f)
            {
                _adapter.Visibility = 0f;
                _adapter.SetInteractable(false);
                if (_options.DisableRenderWhenHidden)
                {
                    _adapter.SetRenderEnabled(false);
                }
                return;
            }

            _tween = DOTween
                .To(() => _adapter.Visibility, a => _adapter.Visibility = a, 0f, duration)
                .SetEase(_options.FadeOutEase)
                .SetUpdate(_options.UseUnscaledTime)
                .SetTarget(_adapter)
                .OnComplete(() =>
                {
                    _tween = null;
                    _adapter.SetInteractable(false);
                    if (_options.DisableRenderWhenHidden)
                    {
                        _adapter.SetRenderEnabled(false);
                    }
                });
        }

        void KillTween()
        {
            if (_tween == null) return;
            _tween.Kill(complete: false);
            _tween = null;
        }
    }
}
