#nullable enable
using Game;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Trait
{
    [DisallowMultipleComponent]
    public sealed class RuntimeTraitMB : MonoBehaviour, IFeatureInstaller
    {
        TraitRuntimeLinkData? _linkData;

        [BoxGroup("Blackboard")]
        [LabelText("Write Trait Data On Link")]
        [Tooltip("Trait が配線された時に、Trait の CommonVars と基本データを Blackboard に書き込みます。WriteTraitData と同等のキーで書き込みます。")]
        [SerializeField] bool _writeTraitDataOnLink = true;

        [BoxGroup("Presentation")]
        [LabelText("Condition")]
        [Tooltip("右クリックで Hidden を適用するかを判定する条件です。DynamicValue<bool> なので Blackboard や式を参照できます。")]
        [SerializeField] DynamicValue<bool> _condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Blackboard")]
        [LabelText("Presentation State Key")]
        [Tooltip("現在の Hidden / Visible 状態を書き込む VarKey です。既定は traitRuntime.presentationState です。")]
        [SerializeField] VarKeyRef _presentationStateKey = new(0, TraitRuntimeLinkVarKeys.PresentationState);

        [BoxGroup("Presentation Commands")]
        [LabelText("On Hidden Commands")]
        [InlineProperty]
        [HideLabel]
        [SerializeField] DynamicValue<CommandListData> _onHiddenCommands = DynamicValueExtensions.FromLiteral(new CommandListData());

        [BoxGroup("Presentation Commands")]
        [LabelText("On Visible Commands")]
        [InlineProperty]
        [HideLabel]
        [SerializeField] DynamicValue<CommandListData> _onVisibleCommands = DynamicValueExtensions.FromLiteral(new CommandListData());

        [ShowInInspector, ReadOnly]
        public string SourceScopeId => _linkData?.SourceScopeId ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string HolderKey => _linkData?.HolderKey ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string TraitKey => _linkData?.TraitKey ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string TraitDefinitionId => _linkData?.TraitDefinitionId ?? string.Empty;

        public bool WriteTraitDataOnLink => _writeTraitDataOnLink;

        public DynamicValue<bool> Condition => _condition;

        public TraitRuntimeLinkData? LinkData => _linkData?.Clone();

        public VarKeyRef PresentationStateKey => _presentationStateKey;

        public DynamicValue<CommandListData> OnHiddenCommands => _onHiddenCommands;

        public DynamicValue<CommandListData> OnVisibleCommands => _onVisibleCommands;

        public void SetLinkData(TraitRuntimeLinkData linkData)
        {
            _linkData = linkData?.Clone();
        }

        public void ClearLinkData()
        {
            _linkData = null;
        }

        public bool CanHideOnRightClick(IScopeNode? scope)
        {
            IVarStore vars = NullVarStore.Instance;
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null &&
                blackboard.LocalVars != null)
            {
                vars = blackboard.LocalVars;
            }

            var context = new SimpleDynamicContext(vars, scope);
            if (_condition.TryGet(context, out var allowHide))
                return allowHide;

            return true;
        }

        public bool TryResolvePresentationStateVarId(out int varId)
        {
            if (_presentationStateKey.VarId > 0)
            {
                varId = _presentationStateKey.VarId;
                return true;
            }

            var stableKey = _presentationStateKey.StableKey;
            if (!string.IsNullOrEmpty(stableKey) &&
                VarIdResolver.TryResolve(stableKey, out var resolvedVarId) &&
                resolvedVarId > 0)
            {
                varId = resolvedVarId;
                return true;
            }

            varId = 0;
            return false;
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<RuntimeTraitBridgeService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(this);

            builder.Register<RuntimeTraitPresentationBridgeService>(Lifetime.Singleton)
                .As<IRuntimeTraitPresentationCommandMutationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(this);
        }
    }
}
