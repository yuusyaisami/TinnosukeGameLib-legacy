using Game.Spawn;
using UnityEngine;
using VContainer;
// 推奨の使い方は、EmitterMB を Prefab にアタッチしておき、
// それを SceneSpawnerInstallerMB 等で Spawn する形です。
namespace Game.Spawn
{
    /// <summary>
    /// エミッターを保有したGameObjectを作成するためのMonoBehaviour。(本人が持っていても問題ない)
    /// </summary>
    public class EmitterMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] Transform originTransform = null;
        public Vector3 Origin => originTransform != null ? originTransform.position : transform.position;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var ownerScope = ScopeNodeUtility.FindNearestBaseLifetimeScope(scope);

            builder.Register<EmitterService>(Lifetime.Singleton)
                .As<IEmitterService>()
                .WithParameter(transform)
                .WithParameter(scope)
                .WithParameter(ownerScope);
        }
    }
}
