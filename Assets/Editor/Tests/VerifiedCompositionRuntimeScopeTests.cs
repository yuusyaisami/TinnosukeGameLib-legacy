#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class VerifiedCompositionRuntimeScopeTests
    {
        [SetUp]
        public void SetUp()
        {
            CountingFeatureInstaller.Reset();
            CountingAcquireHandler.Reset();
            VerifiedCompositionRuntime.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            CountingFeatureInstaller.Reset();
            CountingAcquireHandler.Reset();
            VerifiedCompositionRuntime.Deactivate();
        }

        [Test]
        public void ScopeBuild_UsesLocalInstallerProjection_WhenVerifiedCompositionIsInactive()
        {
            GameObject rootObject = new GameObject("verified-scope-root");
            GameObject childObject = new GameObject("verified-scope-child");
            try
            {
                childObject.transform.SetParent(rootObject.transform, false);

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();
                rootObject.AddComponent<CountingFeatureInstaller>().InstallerKind = CountingInstallerKind.Root;
                childObject.AddComponent<CountingFeatureInstaller>().InstallerKind = CountingInstallerKind.Child;

                scope.EnsureScopeBuilt();

                Assert.That(CountingFeatureInstaller.RootInstallCount, Is.EqualTo(1));
                Assert.That(CountingFeatureInstaller.ChildInstallCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(childObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeBuild_RejectsLocalInstallerProjection_WhenVerifiedCompositionIsActive()
        {
            GameObject rootObject = new GameObject("verified-scope-root");
            GameObject childObject = new GameObject("verified-scope-child");
            try
            {
                childObject.transform.SetParent(rootObject.transform, false);

                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();
                rootObject.AddComponent<CountingFeatureInstaller>().InstallerKind = CountingInstallerKind.Root;
                childObject.AddComponent<CountingFeatureInstaller>().InstallerKind = CountingInstallerKind.Child;

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scope.EnsureScopeBuilt());

                Assert.That(exception!.Message, Does.Contain(typeof(CountingFeatureInstaller).FullName));
                Assert.That(CountingFeatureInstaller.RootInstallCount, Is.EqualTo(0));
                Assert.That(CountingFeatureInstaller.ChildInstallCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(childObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeBuild_DisablesHandlerCollectionResolve_WhenVerifiedCompositionIsActive()
        {
            GameObject rootObject = new GameObject("verified-scope-root");
            try
            {
                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();

                scope.EnsureScopeBuilt();

                Assert.That(scope.Resolver, Is.InstanceOf<RuntimeResolver>());
                RuntimeResolver resolver = (RuntimeResolver)scope.Resolver!;

                Assert.That(resolver.TryResolve<IReadOnlyList<IScopeAcquireHandler>>(out IReadOnlyList<IScopeAcquireHandler> handlers), Is.False);
                Assert.That(handlers, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeBuild_RejectsHandlerInstallerMutation_WhenVerifiedCompositionIsActive()
        {
            GameObject rootObject = new GameObject("verified-scope-root");
            try
            {
                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();
                rootObject.AddComponent<HandlerFeatureInstaller>();

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scope.EnsureScopeBuilt());

                Assert.That(exception!.Message, Does.Contain(typeof(HandlerFeatureInstaller).FullName));
                Assert.That(CountingAcquireHandler.AcquireCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeBuild_AllowsDedicatedHostAcceptedInstallerProjection_WhenVerifiedCompositionIsActive()
        {
            GameObject rootObject = new GameObject("verified-project-root");
            try
            {
                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();
                rootObject.AddComponent<ProjectRootInstallerContributionHostMB>();
                rootObject.AddComponent<Game.Collision.CollisionPipelineModeMB>();

                ScopeIdentityMB identity = rootObject.GetComponent<ScopeIdentityMB>();
                Assert.That(identity, Is.Not.Null);
                identity.kind = LifetimeScopeKind.Project;

                Assert.That(scope.Kind, Is.EqualTo(LifetimeScopeKind.Project));

                Assert.That(() => scope.EnsureScopeBuilt(), Throws.Nothing);
                Assert.That(scope.Resolver, Is.Not.Null);
                Assert.That(scope.Resolver!.TryResolve<Game.Collision.ICollisionPipelineModeService>(out var collisionMode), Is.True);
                Assert.That(collisionMode, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeBuild_RejectsDedicatedHostAcceptedInstaller_WhenHostIsMissingDuringVerifiedComposition()
        {
            GameObject rootObject = new GameObject("verified-project-root-no-host");
            try
            {
                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope scope = rootObject.AddComponent<TestVerifiedScope>();
                rootObject.AddComponent<Game.Collision.CollisionPipelineModeMB>();

                ScopeIdentityMB identity = rootObject.GetComponent<ScopeIdentityMB>();
                Assert.That(identity, Is.Not.Null);
                identity.kind = LifetimeScopeKind.Project;

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => scope.EnsureScopeBuilt());

                Assert.That(exception!.Message, Does.Contain(typeof(Game.Collision.CollisionPipelineModeMB).FullName));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ScopeParent_UsesExplicitBindingOnly_WhenVerifiedCompositionIsActive()
        {
            GameObject rootObject = new GameObject("verified-parent-root");
            GameObject childObject = new GameObject("verified-parent-child");
            try
            {
                childObject.transform.SetParent(rootObject.transform, false);

                VerifiedCompositionRuntime.Activate();

                TestVerifiedScope rootScope = rootObject.AddComponent<TestVerifiedScope>();
                TestVerifiedScope childScope = childObject.AddComponent<TestVerifiedScope>();

                Assert.That(childScope.Parent, Is.Null);

                childScope.SetExplicitBuildParent(rootScope);

                Assert.That(childScope.Parent, Is.SameAs(rootScope));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(childObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [DisallowMultipleComponent]
        sealed class TestVerifiedScope : KernelScopeHost
        {
            protected override bool UseBuildCoordinator => false;
            protected override bool AutoBuildOnAwake => false;

            protected override void ConfigureBase(IRuntimeContainerBuilder builder)
            {
            }
        }

        enum CountingInstallerKind
        {
            Root = 10,
            Child = 20,
        }

        sealed class CountingFeatureInstaller : MonoBehaviour, IScopeInstaller
        {
            public static int RootInstallCount { get; private set; }

            public static int ChildInstallCount { get; private set; }

            public CountingInstallerKind InstallerKind { get; set; }

            public static void Reset()
            {
                RootInstallCount = 0;
                ChildInstallCount = 0;
            }

            public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
            {
                _ = builder ?? throw new ArgumentNullException(nameof(builder));
                _ = scope ?? throw new ArgumentNullException(nameof(scope));

                switch (InstallerKind)
                {
                    case CountingInstallerKind.Root:
                        RootInstallCount++;
                        break;
                    case CountingInstallerKind.Child:
                        ChildInstallCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(InstallerKind), InstallerKind, "Unexpected installer kind.");
                }
            }
        }

        sealed class HandlerFeatureInstaller : MonoBehaviour, IScopeInstaller
        {
            public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
            {
                _ = scope ?? throw new ArgumentNullException(nameof(scope));

                builder.RegisterInstance(new CountingAcquireHandler())
                    .As<IScopeAcquireHandler>();
            }
        }

        sealed class CountingAcquireHandler : IScopeAcquireHandler
        {
            public static int AcquireCount { get; private set; }

            public static void Reset()
            {
                AcquireCount = 0;
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = scope ?? throw new ArgumentNullException(nameof(scope));
                _ = isReset;
                AcquireCount++;
            }
        }
    }
}

