#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    public interface IModalStackLayerAffectCommandOptions
    {
        VNext.CommandListData OnBecameVisible { get; }
        VNext.CommandListData OnBecameHidden { get; }
        VNext.CommandListData OnBecameInputActive { get; }
        VNext.CommandListData OnBecameInputInactive { get; }
        VNext.CommandListData OnBecameInactiveInLayer { get; }
        VNext.CommandListData OnBecameSuppressedByOtherLayer { get; }
    }

    [RequireComponent(typeof(UIElementLifetimeScope))]
    public sealed class ModalStackLayerAffectCommandMB : MonoBehaviour, IFeatureInstaller, IModalStackLayerAffectCommandOptions
    {
        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameVisible")]
        VNext.CommandListData _onBecameVisible = new();

        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameHidden")]
        VNext.CommandListData _onBecameHidden = new();

        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameInputActive")]
        VNext.CommandListData _onBecameInputActive = new();

        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameInputInactive")]
        VNext.CommandListData _onBecameInputInactive = new();

        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameInactiveInLayer")]
        VNext.CommandListData _onBecameInactiveInLayer = new();

        [FoldoutGroup("Commands")]
        [SerializeField]
        [VNext.CommandListFunctionName("ModalStackLayerAffect.OnBecameSuppressedByOtherLayer")]
        VNext.CommandListData _onBecameSuppressedByOtherLayer = new();

        public VNext.CommandListData OnBecameVisible => _onBecameVisible;
        public VNext.CommandListData OnBecameHidden => _onBecameHidden;
        public VNext.CommandListData OnBecameInputActive => _onBecameInputActive;
        public VNext.CommandListData OnBecameInputInactive => _onBecameInputInactive;
        public VNext.CommandListData OnBecameInactiveInLayer => _onBecameInactiveInLayer;
        public VNext.CommandListData OnBecameSuppressedByOtherLayer => _onBecameSuppressedByOtherLayer;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ModalStackLayerAffectCommandService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IModalStackLayerAffectCommandOptions>(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Awake()
        {
            BindDebugOwners();
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        void BindDebugOwners()
        {
            _onBecameVisible?.BindDebugOwner(this, nameof(_onBecameVisible));
            _onBecameHidden?.BindDebugOwner(this, nameof(_onBecameHidden));
            _onBecameInputActive?.BindDebugOwner(this, nameof(_onBecameInputActive));
            _onBecameInputInactive?.BindDebugOwner(this, nameof(_onBecameInputInactive));
            _onBecameInactiveInLayer?.BindDebugOwner(this, nameof(_onBecameInactiveInLayer));
            _onBecameSuppressedByOtherLayer?.BindDebugOwner(this, nameof(_onBecameSuppressedByOtherLayer));
        }
    }
}
