#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonChannelHubMB))]
    public sealed class UIScrollBarMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Scene")]
        [LabelText("Track Rect")]
        [SerializeField]
        RectTransform? _trackRect;

        [BoxGroup("Scene")]
        [LabelText("Handle Rect")]
        [SerializeField]
        RectTransform? _handleRect;

        [BoxGroup("Scene")]
        [LabelText("Visibility Root")]
        [SerializeField]
        GameObject? _visibilityRoot;

        public RectTransform? TrackRect => _trackRect;
        public RectTransform? HandleRect => _handleRect;
        public GameObject? VisibilityRoot => _visibilityRoot != null ? _visibilityRoot : gameObject;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UIScrollBarBindingService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IUIScrollBarBindingService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            _trackRect ??= transform as RectTransform;
            _visibilityRoot ??= gameObject;

            if (_handleRect == null)
            {
                var handle = transform.Find("Handle");
                if (handle is RectTransform handleRect)
                    _handleRect = handleRect;
            }
        }
    }

    public sealed class UIScrollBarBindingService :
        IUIScrollBarBindingService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly UIScrollBarMB _mb;

        IButtonChannelHubService? _buttonChannelHub;

        public RectTransform? TrackRect => _mb.TrackRect;
        public RectTransform? HandleRect => _mb.HandleRect;
        public GameObject? VisibilityRoot => _mb.VisibilityRoot;
        public IButtonChannelHubService? ButtonChannelHub => _buttonChannelHub;

        public UIScrollBarBindingService(IScopeNode owner, UIScrollBarMB mb)
        {
            _owner = owner;
            _mb = mb;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            _buttonChannelHub = null;

            if (_mb.VisibilityRoot != null && _mb.VisibilityRoot.activeSelf)
                _mb.VisibilityRoot.SetActive(false);

            if (_owner.Resolver != null && _owner.Resolver.TryResolve<IButtonChannelHubService>(out var resolvedHub) && resolvedHub != null)
            {
                _buttonChannelHub = resolvedHub;
                return;
            }

            if (scope.Resolver != null && scope.Resolver.TryResolve<IButtonChannelHubService>(out var fallbackHub) && fallbackHub != null)
                _buttonChannelHub = fallbackHub;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _buttonChannelHub = null;
        }
    }
}
