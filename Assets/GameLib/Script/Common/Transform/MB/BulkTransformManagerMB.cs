using System;
using Game.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
namespace Game.TransformSystem
{
    /// <summary>
    /// BulkTransformManager „ÅÆ MonoBehaviour ÂÆüË£ÅEÄÅE
    /// </summary>
    public class BulkTransformManagerMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] int _initialCapacity = 8192;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode lifetimeScope)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<BulkTransformManagerWebGL>(RuntimeLifetime.Singleton)
                .As<IBulkTransformManager>()
                .As<IBulkTransformTransformBridge>()
                .As<IScopeTickHandler>()
                .As<IDisposable>()
                .WithParameter("maxCapacity", _initialCapacity);
#else
            builder.Register<BulkTransformManagerJobs>(RuntimeLifetime.Singleton)
                .As<IBulkTransformManager>()
                .As<IBulkTransformTransformBridge>()
                .As<IScopeTickHandler>()
                .As<IDisposable>()
                .WithParameter("maxCapacity", _initialCapacity);
#endif
        }


    }
}
