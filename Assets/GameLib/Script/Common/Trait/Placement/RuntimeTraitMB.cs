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

        public TraitRuntimeLinkData? LinkData => _linkData?.Clone();

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
