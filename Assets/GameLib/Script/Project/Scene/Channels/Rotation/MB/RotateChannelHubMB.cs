using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Game.Rotation
{
    public class RotateChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] List<RotateChannelDef> channelDefs = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode baseLTS)
        {
            if (channelDefs == null)
                channelDefs = new List<RotateChannelDef>();

            builder.RegisterInstance(channelDefs);

            builder.Register<IRotateChannelHub, RotateChannelHubService>(Lifetime.Singleton)
                .WithParameter("channelDefs", channelDefs);
        }
    }
}
