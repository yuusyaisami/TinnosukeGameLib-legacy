#nullable enable
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    [DisallowMultipleComponent]
    public sealed class WorldPointerTargetMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Target")]
        [LabelText("Colliders")]
        [SerializeField]
        List<Collider2D> _colliders = new();

        [BoxGroup("Target")]
        [LabelText("Use Child Colliders")]
        [SerializeField]
        bool _useChildColliders = true;

        public IReadOnlyList<Collider2D> ResolveColliders()
        {
            if (_colliders != null && _colliders.Count > 0)
                return _colliders;

            if (!_useChildColliders)
                return System.Array.Empty<Collider2D>();

            return GetComponentsInChildren<Collider2D>(true);
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<WorldPointerTargetBridgeService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(this);
        }
    }
}
