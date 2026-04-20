#nullable enable
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    [DisallowMultipleComponent]
    public sealed class CollisionPipelineModeMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        [Tooltip("Inspector setting.")]
        CollisionPipelineKind mode = CollisionPipelineKind.Unity;

        public CollisionPipelineKind Mode => mode;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            if (scope.Kind != LifetimeScopeKind.Project)
                return;

            builder.RegisterInstance<ICollisionPipelineModeService>(new CollisionPipelineModeService(mode));
        }
    }
}
