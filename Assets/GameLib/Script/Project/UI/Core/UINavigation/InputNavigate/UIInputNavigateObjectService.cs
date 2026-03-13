#nullable enable
using VContainer;
using VContainer.Unity;
namespace Game.UI
{
    /// <summary>
    /// UIInputNavigateObjectMB の実体サービス。
    /// 自身の Owner と Trigger を保持し、Manager へ登録/解除する。
    /// </summary>
    public sealed class UIInputNavigateObjectService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IUIInputNavigateObjectOptions _options;

        IUIInputNavigateService? _manager;

        public IScopeNode Owner => _owner;
        public bool IsEnabled => _options.Enabled;
        public UIInputTrigger Trigger => _options.Trigger;
        public bool ResendInputOnSelect => _options.ResendInputOnSelect;

        public UIInputNavigateObjectService(IScopeNode owner, IUIInputNavigateObjectOptions options)
        {
            _owner = owner;
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // Resolve from current scope (parent scopes included)
            if (scope.Resolver != null && scope.Resolver.TryResolve<IUIInputNavigateService>(out var mgr))
            {
                _manager = mgr;
                _manager.Register(this);
            }
        }

        public void OnRelease(IScopeNode scope, bool isDestroy)
        {
            if (_manager != null)
            {
                _manager.Unregister(this);
                _manager = null;
            }
        }
    }
}
