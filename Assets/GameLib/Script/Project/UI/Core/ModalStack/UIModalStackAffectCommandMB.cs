#nullable enable
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;
using Sirenix.OdinInspector;

namespace Game.UI
{
    public enum UIModalStackAffectPolicy
    {
        Strict,
        IgnoreDescendantPush,
        IgnoreDescendantPop,
        IgnoreNestedChange,
    }

    public interface IUIModalStackAffectCommandOptions
    {
        VNext.CommandListData OnBecameInScope { get; }
        VNext.CommandListData OnBecameOutOfScope { get; }
        VNext.CommandListData OnBecameImmediateInScope { get; }
        VNext.CommandListData OnBecameImmediateOutOfScope { get; }
        bool ExecuteOnAcquire { get; }
        bool ExecuteOnRelease { get; }
        UIModalStackAffectPolicy Policy { get; }
        bool UseGlobalExecutionLock { get; }
    }

    [RequireComponent(typeof(UIElementLifetimeScope))]
    public sealed class UIModalStackAffectCommandMB : MonoBehaviour, IFeatureInstaller, IUIModalStackAffectCommandOptions
    {
        [FoldoutGroup("Commands")]
        [Tooltip("ModalStack影響下に入ったときに実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIModalStackAffect.OnBecameInScope")]
        VNext.CommandListData _onBecameInScope = new();

        [FoldoutGroup("Commands")]
        [Tooltip("ModalStack影響下から外れたときに実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIModalStackAffect.OnBecameOutOfScope")]
        VNext.CommandListData _onBecameOutOfScope = new();

        [FoldoutGroup("Immediate")]
        [Tooltip("Immediate変更/Acquire時に影響下の場合に実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIModalStackAffect.OnBecameImmediateInScope")]
        VNext.CommandListData _onBecameImmediateInScope = new();

        [FoldoutGroup("Immediate")]
        [Tooltip("Immediate変更/Release時に影響下だった場合に実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIModalStackAffect.OnBecameImmediateOutOfScope")]
        VNext.CommandListData _onBecameImmediateOutOfScope = new();

        [FoldoutGroup("Immediate")]
        [Tooltip("Acquire時にImmediateInScopeを実行するか。")]
        [SerializeField]
        bool _executeOnAcquire = false;

        [FoldoutGroup("Immediate")]
        [Tooltip("Release時にImmediateOutOfScopeを実行するか。")]
        [SerializeField]
        bool _executeOnRelease = false;

        [FoldoutGroup("Policy")]
        [Tooltip("ModalStack変化の扱いポリシー。")]
        [SerializeField]
        UIModalStackAffectPolicy _policy = UIModalStackAffectPolicy.Strict;

        [FoldoutGroup("Policy")]
        [Tooltip("true の場合、全 UIModalStackAffect 間で実行を直列化する。false の場合は各 Affect が独立して同時実行する。")]
        [LabelText("Use Global Execution Lock")]
        [SerializeField]
        bool _useGlobalExecutionLock = true;

        public VNext.CommandListData OnBecameInScope => _onBecameInScope;
        public VNext.CommandListData OnBecameOutOfScope => _onBecameOutOfScope;
        public VNext.CommandListData OnBecameImmediateInScope => _onBecameImmediateInScope;
        public VNext.CommandListData OnBecameImmediateOutOfScope => _onBecameImmediateOutOfScope;
        public bool ExecuteOnAcquire => _executeOnAcquire;
        public bool ExecuteOnRelease => _executeOnRelease;
        public UIModalStackAffectPolicy Policy => _policy;
        public bool UseGlobalExecutionLock => _useGlobalExecutionLock;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UIModalStackAffectCommandService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUIModalStackAffectCommandOptions>(this)
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
            _onBecameInScope?.BindDebugOwner(this, nameof(_onBecameInScope));
            _onBecameOutOfScope?.BindDebugOwner(this, nameof(_onBecameOutOfScope));
            _onBecameImmediateInScope?.BindDebugOwner(this, nameof(_onBecameImmediateInScope));
            _onBecameImmediateOutOfScope?.BindDebugOwner(this, nameof(_onBecameImmediateOutOfScope));
        }
    }
}
