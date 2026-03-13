#nullable enable
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    [DisallowMultipleComponent]
    public sealed class CollisionPipelineModeMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        [Tooltip("プロジェクト全体で使用する Collision パイプラインを選択します。")]
        CollisionPipelineKind mode = CollisionPipelineKind.Unity;

        public CollisionPipelineKind Mode => mode;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (scope.Kind != LifetimeScopeKind.Project)
                return;

            builder.RegisterInstance<ICollisionPipelineModeService>(new CollisionPipelineModeService(mode));
        }
    }
}
