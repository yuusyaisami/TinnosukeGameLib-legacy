#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIScrollBarMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Scene")]
        [LabelText("Track Rect")]
        [SerializeField]
        RectTransform? _trackRect;

        [BoxGroup("Scene")]
        [LabelText("Handle Rect")]
        [Tooltip("ButtonChannelHubMB はこの RectTransform の GameObject に配置します。")]
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
        readonly UIScrollBarMB _mb;

        IButtonChannelHubService? _buttonChannelHub;

        public RectTransform? TrackRect => _mb.TrackRect;
        public RectTransform? HandleRect => _mb.HandleRect;
        public GameObject? VisibilityRoot => _mb.VisibilityRoot;
        public IButtonChannelHubService? ButtonChannelHub => _buttonChannelHub;

        public UIScrollBarBindingService(UIScrollBarMB mb)
        {
            _mb = mb;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _buttonChannelHub = null;

            // VisibilityRoot が ScrollBar 自身の場合は scope 自体を無効化してしまうため、
            // 自動非表示は行わない。
            if (_mb.VisibilityRoot != null &&
                _mb.VisibilityRoot.activeSelf &&
                !ReferenceEquals(_mb.VisibilityRoot, _mb.gameObject))
                _mb.VisibilityRoot.SetActive(false);

            var handleRect = _mb.HandleRect;
            var hubHost = handleRect != null ? handleRect.GetComponent<ButtonChannelHubMB>() : null;
            if (hubHost?.Hub != null)
            {
                _buttonChannelHub = hubHost.Hub;
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _buttonChannelHub = null;
        }
    }
}
