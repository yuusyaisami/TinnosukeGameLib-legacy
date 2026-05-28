#nullable enable

using Game;
using Game.Common;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class BlackboardRuntimeScopeRegistrationTests
    {
        [Test]
        public void EnsureScopeBuilt_RegistersBlackboardServicesWhenAuthoringExists()
        {
            GameObject gameObject = new GameObject("BlackboardScopeWithAuthoring");
            try
            {
                BlackboardProbeScope scope = gameObject.AddComponent<BlackboardProbeScope>();
                gameObject.AddComponent<BlackboardMB>();

                scope.ConfigureForAcquire(template: null, CreateEntityIdentity(scope), ensureBuilt: false);
                scope.EnsureScopeBuilt();

                Assert.That(scope.TryResolveLocal<IBlackboardService>(out IBlackboardService blackboard), Is.True);
                Assert.That(blackboard, Is.Not.Null);
                Assert.That(scope.TryResolveLocal<IGridBlackboardService>(out IGridBlackboardService gridBlackboard), Is.True);
                Assert.That(gridBlackboard, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EnsureScopeBuilt_DoesNotRegisterBlackboardServicesWithoutAuthoring()
        {
            GameObject gameObject = new GameObject("BlackboardScopeWithoutAuthoring");
            try
            {
                BlackboardProbeScope scope = gameObject.AddComponent<BlackboardProbeScope>();

                scope.ConfigureForAcquire(template: null, CreateEntityIdentity(scope), ensureBuilt: false);
                scope.EnsureScopeBuilt();

                Assert.That(scope.TryResolveLocal<IBlackboardService>(out _), Is.False);
                Assert.That(scope.TryResolveLocal<IGridBlackboardService>(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        static RuntimeIdentityData CreateEntityIdentity(Component scope)
        {
            RuntimeIdentityData identity = RuntimeIdentityData.CreateDefault(scope.transform, id: "Entity:BlackboardScope", category: "Test");
            identity.Kind = LifetimeScopeKind.Entity;
            identity.InitiallyActive = false;
            return identity;
        }

        sealed class BlackboardProbeScope : RuntimeLifetimeScopeBase
        {
        }
    }
}