using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Game.Rotation
{
    public class RotateChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField] List<RotateChannelDef> channelDefs = new();

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode baseLTS)
        {
            if (channelDefs == null)
                channelDefs = new List<RotateChannelDef>();

            builder.RegisterInstance(channelDefs);

            builder.Register<IRotateChannelHub, RotateChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter("channelDefs", channelDefs);
        }
    }
}

