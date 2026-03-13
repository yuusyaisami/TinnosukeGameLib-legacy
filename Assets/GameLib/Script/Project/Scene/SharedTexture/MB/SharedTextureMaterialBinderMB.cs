#nullable enable
using System.Collections.Generic;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;

namespace Game.SharedTexture
{
    [DisallowMultipleComponent]
    public sealed class SharedTextureMaterialBinderMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Bindings")]
        [SerializeField] List<SharedTextureBindingDef> bindings = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var options = new SharedTextureBinderOptions(new List<SharedTextureBindingDef>(bindings));

            builder.Register<SharedTextureMaterialBinderService>(Lifetime.Singleton)
                .WithParameter(options)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
