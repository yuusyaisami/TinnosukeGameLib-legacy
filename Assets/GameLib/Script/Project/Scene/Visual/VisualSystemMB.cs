#nullable enable

using Game;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Visual
{
    /// <summary>
    /// VisualSystem の FeatureInstaller、E
    /// FieldLifetimeScope 推奨だが、任意�E LifetimeScope 配下でも動く、E
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualSystemMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("Default")]
        [LabelText("Default Payload")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        DynamicValue<MaterialFxPayload> defaultMaterialFxSource = DynamicValue<MaterialFxPayload>.FromSource(new LiteralMaterialFxPayloadSource());

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;
            builder.Register<VisualSystemService>(RuntimeLifetime.Singleton)
                .WithParameter(defaultMaterialFxSource)
                .As<IVisualSystem>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();
        }
    }
}

