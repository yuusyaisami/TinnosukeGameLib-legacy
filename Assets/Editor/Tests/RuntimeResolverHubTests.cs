#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
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

        [Test]
        public void Resolve_FailsClosedWhenTypeHasNoPublicConstructor()
        {
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.Register<NonPublicConstructorService>(RuntimeLifetime.Transient);

            IRuntimeResolver resolver = builder.Build();
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<NonPublicConstructorService>());
                Assert.That(exception.Message, Does.Contain("must expose a public constructor"));
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void GetAcquireHandlers_UsesExplicitCatalog_WhenHandlerCollectionResolutionIsDisabled()
        {
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            CountingAcquireHandler handler = new CountingAcquireHandler();

            builder.DisableHandlerCollectionResolution();
            builder.RegisterInstance(handler).As<IScopeAcquireHandler>();

            IRuntimeResolver resolver = builder.Build();
            try
            {
                Assert.That(resolver.TryResolve<IReadOnlyList<IScopeAcquireHandler>>(out IReadOnlyList<IScopeAcquireHandler> handlers), Is.False);
                Assert.That(handlers, Is.Null);

                Assert.That(resolver, Is.InstanceOf<RuntimeResolver>());
                RuntimeResolver runtimeResolver = (RuntimeResolver)resolver;
                IScopeAcquireHandler[] explicitHandlers = runtimeResolver.GetAcquireHandlers();

                Assert.That(explicitHandlers, Has.Length.EqualTo(1));
                Assert.That(explicitHandlers[0], Is.SameAs(handler));
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void ScopeHandlerOwnershipUtility_PrefersExplicitOwnerScopeOverTransformFallback()
        {
            GameObject transformOwnerObject = new GameObject("handler-transform-owner");
            GameObject explicitOwnerObject = new GameObject("handler-explicit-owner");
            GameObject handlerObject = new GameObject("handler-component");
            try
            {
                FallbackProbeScopeNode transformOwner = transformOwnerObject.AddComponent<FallbackProbeScopeNode>();
                FallbackProbeScopeNode explicitOwner = explicitOwnerObject.AddComponent<FallbackProbeScopeNode>();
                handlerObject.transform.SetParent(transformOwnerObject.transform, false);

                ExplicitOwnerComponentHandler handler = handlerObject.AddComponent<ExplicitOwnerComponentHandler>();
                handler.SetOwnerScope(explicitOwner);

                MethodInfo shouldInvokeMethod = typeof(IScopeNode).Assembly
                    .GetType("Game.ScopeHandlerOwnershipUtility", throwOnError: true)!
                    .GetMethod("ShouldInvokeHandler", BindingFlags.Public | BindingFlags.Static)!;

                bool usesTransformOwner = (bool)shouldInvokeMethod.Invoke(null, new object?[] { transformOwner, handler })!;
                bool usesExplicitOwner = (bool)shouldInvokeMethod.Invoke(null, new object?[] { explicitOwner, handler })!;

                Assert.That(usesTransformOwner, Is.False);
                Assert.That(usesExplicitOwner, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(handlerObject);
                UnityEngine.Object.DestroyImmediate(explicitOwnerObject);
                UnityEngine.Object.DestroyImmediate(transformOwnerObject);
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

        sealed class NonPublicConstructorService
        {
            NonPublicConstructorService()
            {
            }
        }

        sealed class CountingAcquireHandler : IScopeAcquireHandler
        {
            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
            }
        }

        sealed class ExplicitOwnerComponentHandler : MonoBehaviour, IScopeAcquireHandler
        {
            IScopeNode? _ownerScope;

            public void SetOwnerScope(IScopeNode ownerScope)
            {
                _ownerScope = ownerScope;
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
            }
        }

        sealed class FallbackProbeScopeNode : MonoBehaviour, IScopeNode
        {
            public IScopeNode? Parent { get; set; }

            public IScopeIdentityService? Identity { get; set; }

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

