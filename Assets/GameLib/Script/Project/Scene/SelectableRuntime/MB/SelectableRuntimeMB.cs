#nullable enable
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldPointerTargetMB))]
    public sealed class SelectableRuntimeMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Target")]
        [LabelText("Pointer Target")]
        [SerializeField]
        WorldPointerTargetMB? _target;

        [BoxGroup("Commands")]
        [LabelText("On Selected")]
        [SerializeField]
        CommandListData _onSelectedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Deselected")]
        [SerializeField]
        CommandListData _onDeselectedCommands = new();

        public WorldPointerTargetMB? Target => ResolveTarget();
        public CommandListData OnSelectedCommands => _onSelectedCommands;
        public CommandListData OnDeselectedCommands => _onDeselectedCommands;

        public WorldPointerTargetMB? ResolveTarget()
        {
            if (_target != null)
                return _target;

            _target = GetComponent<WorldPointerTargetMB>();
            if (_target != null)
                return _target;

            _target = GetComponentInChildren<WorldPointerTargetMB>(true);
            return _target;
        }

        public bool TryResolveActorScope(out IScopeNode? scope)
        {
            return ScopeFeatureInstallerUtility.TryGetNearestScopeNode(this, includeInactive: true, out scope);
        }

        void OnEnable()
        {
            NotifyBridgeRefresh();
        }

        void OnDisable()
        {
            NotifyBridgeRelease();
        }

        void OnTransformParentChanged()
        {
            NotifyBridgeRefresh();
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<SelectableRuntimeBridgeService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(this);
        }

        void NotifyBridgeRefresh()
        {
            if (!TryResolveActorScope(out var scope) || scope?.Resolver == null)
                return;

            if (scope.Resolver.TryResolve<SelectableRuntimeBridgeService>(out var bridge) && bridge != null)
                bridge.RefreshBinding();
        }

        void NotifyBridgeRelease()
        {
            if (!TryResolveActorScope(out var scope) || scope?.Resolver == null)
                return;

            if (scope.Resolver.TryResolve<SelectableRuntimeBridgeService>(out var bridge) && bridge != null)
                bridge.ReleaseBinding();
        }
    }
}
