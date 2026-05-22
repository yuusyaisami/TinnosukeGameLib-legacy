#nullable enable
using Game;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class LTSIdentityMBTests
    {
        [Test]
        public void PredictKindFromComponent_PrefersAttachedIdentityKind_ForGenericPersistentRootHosts()
        {
            GameObject gameObject = new GameObject("PersistentRootHost");
            try
            {
                TestPersistentRootScope scope = gameObject.AddComponent<TestPersistentRootScope>();
                ScopeIdentityMB identity = gameObject.GetComponent<ScopeIdentityMB>();
                Assert.That(identity, Is.Not.Null);

                identity.kind = LifetimeScopeKind.Project;

                LifetimeScopeKind resolvedKind = ScopeIdentityMB.PredictKindFromComponent(scope, scope.Kind);

                Assert.That(resolvedKind, Is.EqualTo(LifetimeScopeKind.Project));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PredictKindFromType_UsesDeterministicNameHeuristics_ForGenericPersistentRootHosts()
        {
            Assert.That(ScopeIdentityMB.PredictKindFromType(typeof(TestProjectRootScope)), Is.EqualTo(LifetimeScopeKind.Project));
            Assert.That(ScopeIdentityMB.PredictKindFromType(typeof(TestPlatformRootScope)), Is.EqualTo(LifetimeScopeKind.Platform));
            Assert.That(ScopeIdentityMB.PredictKindFromType(typeof(TestGlobalRootScope)), Is.EqualTo(LifetimeScopeKind.Global));
        }

        [Test]
        public void PredictKindFromType_PreservesExplicitCurrentKind_WhenTypeNameIsGeneric()
        {
            LifetimeScopeKind resolvedKind = ScopeIdentityMB.PredictKindFromType(typeof(TestPersistentRootScope), LifetimeScopeKind.Global);

            Assert.That(resolvedKind, Is.EqualTo(LifetimeScopeKind.Global));
        }

        sealed class TestPersistentRootScope : KernelScopeHost
        {
        }

        sealed class TestProjectRootScope : KernelScopeHost
        {
        }

        sealed class TestPlatformRootScope : KernelScopeHost
        {
        }

        sealed class TestGlobalRootScope : KernelScopeHost
        {
        }
    }
}

