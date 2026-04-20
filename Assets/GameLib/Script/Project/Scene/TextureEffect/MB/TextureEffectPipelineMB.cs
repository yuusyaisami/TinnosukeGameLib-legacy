#nullable enable
using System.Collections.Generic;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;

namespace Game.TextureEffect
{
    [DisallowMultipleComponent]
    public sealed class TextureEffectPipelineMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Initial Layers")]
        [SerializeField] List<TextureEffectLayerDef> initialLayers = new();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // Register individual effects
            builder.Register<BlurEffect>(RuntimeLifetime.Singleton).As<ITextureEffect>();
            builder.Register<MosaicEffect>(RuntimeLifetime.Singleton).As<ITextureEffect>();
            builder.Register<DistortEffect>(RuntimeLifetime.Singleton).As<ITextureEffect>();
            builder.Register<ColorShiftEffect>(RuntimeLifetime.Singleton).As<ITextureEffect>();

            var layersCopy = new List<TextureEffectLayerDef>(initialLayers);

            builder.Register<TextureEffectPipelineService>(RuntimeLifetime.Singleton)
                .As<ITextureEffectPipeline>()
                .As<ITextureEffectLayerRegistry>()
                .As<ITextureEffectMaskRegistry>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();

            // Register initial layers after build
            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<ITextureEffectLayerRegistry>();
                foreach (var layer in layersCopy)
                {
                    if (!string.IsNullOrEmpty(layer.LayerTag))
                        registry.RegisterLayer(layer);
                }
            });
        }
    }
}
