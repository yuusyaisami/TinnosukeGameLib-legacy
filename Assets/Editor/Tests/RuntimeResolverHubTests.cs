#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class RuntimeResolverHubTests
    {
        [Test]
        public void TryResolve_UsesExplicitRegistration()
        {
            ExplicitService service = new ExplicitService();
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.RegisterInstance<ExplicitService>(service);

            IRuntimeResolver resolver = builder.Build();
            try
            {
                Assert.That(resolver.TryResolve<ExplicitService>(out ExplicitService resolved), Is.True);
                Assert.That(resolved, Is.SameAs(service));
                Assert.That(resolver.Resolve<ExplicitService>(), Is.SameAs(service));
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void TryResolve_ResolvesHostScopeOnlyByExactType()
        {
            GameObject gameObject = new GameObject("runtime-resolver-host-probe");
            try
            {
                FallbackProbeScopeNode scope = gameObject.AddComponent<FallbackProbeScopeNode>();

                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.SetHostScope(scope);

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<IScopeNode>(out IScopeNode resolvedScope), Is.True);
                    Assert.That(resolvedScope, Is.SameAs(scope));
                    Assert.That(resolver.TryResolve<FallbackProbeScopeNode>(out FallbackProbeScopeNode resolvedConcrete), Is.True);
                    Assert.That(resolvedConcrete, Is.SameAs(scope));
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TryResolve_DoesNotUseHostComponentFallback()
        {
            GameObject gameObject = new GameObject("runtime-resolver-fallback-probe");
            try
            {
                FallbackProbeScopeNode scope = gameObject.AddComponent<FallbackProbeScopeNode>();
                gameObject.AddComponent<ComponentProbeService>();

                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.SetHostScope(scope);

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<ComponentProbeService>(out ComponentProbeService resolved), Is.False);
                    Assert.That(resolved, Is.Null);
                    Assert.That(resolver.ResolveOrDefault(typeof(ComponentProbeService)), Is.Null);
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Resolve_FailsClosedWhenConstructorDependencyIsMissing()
        {
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.Register<MissingDependencyConsumer>(RuntimeLifetime.Transient);

            IRuntimeResolver resolver = builder.Build();
            try
            {
                Assert.That(() => resolver.Resolve<MissingDependencyConsumer>(), Throws.InvalidOperationException);
            }
            finally
            {
                resolver.Dispose();
            }
        }

        sealed class ExplicitService
        {
        }

        sealed class MissingDependencyConsumer
        {
            public MissingDependencyConsumer(ExplicitService dependency)
            {
                Dependency = dependency;
            }

            public ExplicitService Dependency { get; }
        }

        sealed class ComponentProbeService : MonoBehaviour
        {
        }

        sealed class FallbackProbeScopeNode : MonoBehaviour, IScopeNode
        {
            public IScopeNode? Parent { get; set; }

            public ILTSIdentityService? Identity { get; set; }

            public LifetimeScopeKind Kind => LifetimeScopeKind.Runtime;

            public IRuntimeResolver? Resolver { get; set; }

            public bool IsVisible { get; private set; } = true;

            public bool IsActive { get; private set; } = true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                IsVisible = visible;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                IsActive = active;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }
}
