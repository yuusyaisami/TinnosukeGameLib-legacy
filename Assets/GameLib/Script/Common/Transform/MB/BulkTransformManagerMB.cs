using System;
using Game.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
namespace Game.TransformSystem
{
    /// <summary>
    /// BulkTransformManager の MonoBehaviour 実装。
    /// </summary>
    public class BulkTransformManagerMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] int _initialCapacity = 8192;

        public void InstallFeature(IContainerBuilder builder, IScopeNode lifetimeScope)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<BulkTransformManagerWebGL>(Lifetime.Singleton)
                .As<IBulkTransformManager>()
                .As<IBulkTransformTransformBridge>()
                .As<ITickable>()
                .As<IDisposable>()
                .WithParameter("maxCapacity", _initialCapacity);
#else
            builder.Register<BulkTransformManagerJobs>(Lifetime.Singleton)
                .As<IBulkTransformManager>()
                .As<IBulkTransformTransformBridge>()
                .As<ITickable>()
                .As<IDisposable>()
                .WithParameter("maxCapacity", _initialCapacity);
#endif
        }


    }
}
