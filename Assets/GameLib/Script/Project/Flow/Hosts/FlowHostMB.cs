#nullable enable

using UnityEngine;
using VContainer;

namespace Game.Flow
{
    /// <summary>
    /// Flow 繝帙せ繝医し繝ｼ繝薙せ繧・DI 繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ MonoBehaviour 繧､繝ｳ繧ｹ繝医・繝ｩ縲・
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowHostMB : MonoBehaviour, Game.IFeatureInstaller
    {
        /// <summary>
        /// IFeatureInstaller 螳溯｣・・lowHostService 繧偵す繝ｳ繧ｰ繝ｫ繝医Φ縺ｨ縺励※逋ｻ骭ｲ縺励∪縺吶・
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, Game.IScopeNode scope)
        {
            builder.Register<FlowHostService>(RuntimeLifetime.Singleton)
                .As<IFlowHost>()
                .As<Game.Commands.VNext.IFlowHostCommandBridge>()
                .As<Game.IScopeAcquireHandler>()
                .As<Game.IScopeReleaseHandler>();
        }
    }
}
