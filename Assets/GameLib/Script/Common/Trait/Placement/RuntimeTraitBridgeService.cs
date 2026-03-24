#nullable enable
using Game;
using VContainer.Unity;

namespace Game.Trait
{
    public sealed class RuntimeTraitBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly RuntimeTraitMB _owner;
        TraitRuntimeLinkKey _registeredKey;
        bool _hasRegisteredKey;

        public RuntimeTraitBridgeService(RuntimeTraitMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            RefreshRegistration();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnregisterCurrent();
        }

        public void Tick()
        {
            RefreshRegistration();
        }

        void RefreshRegistration()
        {
            var currentLink = _owner.LinkData;
            if (currentLink == null)
            {
                UnregisterCurrent();
                return;
            }

            var currentKey = currentLink.ToLinkKey();
            if (_hasRegisteredKey && _registeredKey.Equals(currentKey))
                return;

            UnregisterCurrent();
            if (TraitPlacementScopeResolver.TryResolvePlacementService(currentLink, out var placementService) &&
                placementService != null)
            {
                placementService.NotifyRuntimeEnabled(currentLink, _owner);
                _registeredKey = currentKey;
                _hasRegisteredKey = true;
            }
        }

        void UnregisterCurrent()
        {
            if (!_hasRegisteredKey)
                return;

            var currentLink = _owner.LinkData;
            if (currentLink != null &&
                TraitPlacementScopeResolver.TryResolvePlacementService(currentLink, out var placementService) &&
                placementService != null)
            {
                placementService.NotifyRuntimeDisabled(currentLink, _owner);
            }

            _hasRegisteredKey = false;
            _registeredKey = default;
        }
    }
}
