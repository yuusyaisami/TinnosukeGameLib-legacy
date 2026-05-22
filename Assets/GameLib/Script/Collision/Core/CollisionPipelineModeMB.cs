#nullable enable
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    [DisallowMultipleComponent]
    public sealed class CollisionPipelineModeMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField]
        [Tooltip("Inspector setting.")]
        CollisionPipelineKind mode = CollisionPipelineKind.Unity;

        public CollisionPipelineKind Mode => mode;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            if (scope.Kind != LifetimeScopeKind.Project)
                return;

            builder.RegisterInstance<ICollisionPipelineModeService>(new CollisionPipelineModeService(mode));
        }
    }
}

