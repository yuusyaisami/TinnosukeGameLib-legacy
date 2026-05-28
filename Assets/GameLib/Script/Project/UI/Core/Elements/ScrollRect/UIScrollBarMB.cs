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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        RectTransform? _handleRect;

        [BoxGroup("Scene")]
        [LabelText("Visibility Root")]
        [SerializeField]
        GameObject? _visibilityRoot;

        [BoxGroup("Binding")]
        [LabelText("Button Channel Hub Source")]
        [SerializeField]
        ButtonChannelHubDeclarationMB? _buttonChannelHubSource;

        public RectTransform? TrackRect => _trackRect;
        public RectTransform? HandleRect => _handleRect;
        public GameObject? VisibilityRoot => _visibilityRoot != null ? _visibilityRoot : gameObject;
        public ButtonChannelHubDeclarationMB? ButtonChannelHubSource => _buttonChannelHubSource;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UIScrollBarBindingService>(RuntimeLifetime.Singleton)
                .WithParameter(this)
                .As<IUIScrollBarBindingService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            _trackRect ??= transform as RectTransform;
            _visibilityRoot ??= gameObject;
            _buttonChannelHubSource ??= GetComponent<ButtonChannelHubDeclarationMB>() ?? GetComponentInParent<ButtonChannelHubDeclarationMB>(true);

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

            // VisibilityRoot 縺・ScrollBar 閾ｪ霄ｫ縺ｮ蝣ｴ蜷医・ scope 閾ｪ菴薙ｒ辟｡蜉ｹ蛹悶＠縺ｦ縺励∪縺・◆繧√・
            // 閾ｪ蜍暮撼陦ｨ遉ｺ縺ｯ陦後ｏ縺ｪ縺・・
            if (_mb.VisibilityRoot != null &&
                _mb.VisibilityRoot.activeSelf &&
                !ReferenceEquals(_mb.VisibilityRoot, _mb.gameObject))
                _mb.VisibilityRoot.SetActive(false);

            if (ButtonChannelBindingResolver.TryResolveFromAnchor(_mb.ButtonChannelHubSource, out IButtonChannelHubService? anchoredHub) && anchoredHub != null)
            {
                _buttonChannelHub = anchoredHub;
                return;
            }

            if (ButtonChannelBindingResolver.TryResolveFromScope(scope, out IButtonChannelHubService? localHub) && localHub != null)
                _buttonChannelHub = localHub;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _buttonChannelHub = null;
        }
    }
}
