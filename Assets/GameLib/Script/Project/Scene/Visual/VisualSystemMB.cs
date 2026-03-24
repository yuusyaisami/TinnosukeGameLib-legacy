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
    /// VisualSystem の FeatureInstaller。
    /// FieldLifetimeScope 推奨だが、任意の LifetimeScope 配下でも動く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualSystemMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Default")]
        [LabelText("Default Payload")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        DynamicValue<MaterialFxPayload> defaultMaterialFxSource = DynamicValue<MaterialFxPayload>.FromSource(new LiteralMaterialFxPayloadSource());

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;
            builder.Register<VisualSystemService>(Lifetime.Singleton)
                .WithParameter(defaultMaterialFxSource)
                .As<IVisualSystem>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();
        }
    }
}
