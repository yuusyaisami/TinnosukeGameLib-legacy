using Game.Spawn;
using UnityEngine;
// 謗ｨ螂ｨ縺ｮ菴ｿ縺・婿縺ｯ縲・mitterMB 繧・Prefab 縺ｫ繧｢繧ｿ繝・メ縺励※縺翫″縲・
// 縺昴ｌ繧・SceneSpawnerInstallerMB 遲峨〒 Spawn 縺吶ｋ蠖｢縺ｧ縺吶・
namespace Game.Spawn
{
    /// <summary>
    /// 繧ｨ繝溘ャ繧ｿ繝ｼ繧剃ｿ晄怏縺励◆GameObject繧剃ｽ懈・縺吶ｋ縺溘ａ縺ｮMonoBehaviour縲・譛ｬ莠ｺ縺梧戟縺｣縺ｦ縺・※繧ょ撫鬘後↑縺・
    /// </summary>
    public class EmitterMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField] Transform originTransform = null;
        public Vector3 Origin => originTransform != null ? originTransform.position : transform.position;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var ownerScope = ScopeNodeUtility.FindNearestRuntimeLifetimeScope(scope);

            builder.Register<EmitterService>(RuntimeLifetime.Singleton)
                .As<IEmitterService>()
                .WithParameter(transform)
                .WithParameter(scope)
                .WithParameter(ownerScope);
        }
    }
}

