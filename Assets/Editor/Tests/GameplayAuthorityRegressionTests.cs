#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.Common;
using Game.Times;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class GameplayAuthorityRegressionTests
    {
        static readonly MethodInfo ResolveActorScopeMethod = typeof(ConversationFlowExecutor).Assembly
            .GetType("Game.Commands.VNext.ActorScopeResolver", throwOnError: true)!
            .GetMethod("ResolveAsync", BindingFlags.Public | BindingFlags.Static)!;

        static readonly MethodInfo ResolveConversationTargetScopeMethod = typeof(ConversationFlowExecutor).Assembly
            .GetType("Game.Commands.VNext.ConversationExecutorUtility", throwOnError: true)!
            .GetMethod("ResolveTargetScopeAsync", BindingFlags.Public | BindingFlags.Static)!;

        static readonly MethodInfo ResolveGridTargetScopeMethod = typeof(BindGridObjectChannelExecutor).Assembly
            .GetType("Game.Commands.VNext.GridObjectChannelExecutorUtility", throwOnError: true)!
            .GetMethod("ResolveTargetScopeAsync", BindingFlags.Public | BindingFlags.Static)!;

        static readonly MethodInfo ResolveTraitListTargetScopeMethod = typeof(BindTraitListChannelExecutor).Assembly
            .GetType("Game.Commands.VNext.TraitListChannelExecutorUtility", throwOnError: true)!
            .GetMethod("ResolveTargetScopeAsync", BindingFlags.Public | BindingFlags.Static)!;

        static readonly CommandRunOptions AllowFallbackOptions = new(
            CommandFailurePolicy.FailFast,
            CommandFailureBoundary.FailFrame,
            CommandExecutionDomain.Project,
            allowActorFallback: true,
            CommandTracePolicy.OnFailure,
            maxTraceDepth: 8,
            maxTraceFrames: 64,
            suppressCancelLog: false,
            timeoutMilliseconds: 0,
            allowDetachedExecution: false,
            CommandDetachedCancellationMode.FollowCaller);

        [Test]
        public void ConversationResolveTargetScopeAsync_DoesNotFallbackToCurrentScope()
        {
            CommandExecutionException exception = Assert.ThrowsAsync<CommandExecutionException>(async () =>
                await InvokeResolveTargetScopeAsync(ResolveConversationTargetScopeMethod));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-CONV-001] "));
            Assert.That(exception.Message, Does.Contain("GameLogicRoot scope was not found."));
        }

        [Test]
        public void GridObjectResolveTargetScopeAsync_DoesNotFallbackToCurrentScope()
        {
            CommandExecutionException exception = Assert.ThrowsAsync<CommandExecutionException>(async () =>
                await InvokeResolveTargetScopeAsync(ResolveGridTargetScopeMethod));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-GRID-001] "));
            Assert.That(exception.Message, Does.Contain("GameLogicRoot scope was not found."));
        }

        [Test]
        public void TraitListResolveTargetScopeAsync_DoesNotFallbackToCurrentScope()
        {
            CommandExecutionException exception = Assert.ThrowsAsync<CommandExecutionException>(async () =>
                await InvokeResolveTargetScopeAsync(ResolveTraitListTargetScopeMethod));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-TRAIT-001] "));
            Assert.That(exception.Message, Does.Contain("GameLogicRoot scope was not found."));
        }

        [Test]
        public async System.Threading.Tasks.Task ActorScopeResolver_ResolveAsync_ByIdentity_UsesDirectOriginRegistryOnly()
        {
            var (_, origin, target, identity, identityObject) = CreateIdentityRegistryFixture(registerRegistryOnOrigin: true);
            try
            {
                CommandContext context = new CommandContext(origin, new VarStore(initialCapacity: 4), new TestCommandRunner(origin));
                ActorSource source = CreateByIdentitySource(identity.Id, identity.Kind);

                var (resolved, error) = await InvokeActorScopeResolveAsync(source, context);

                Assert.That(resolved, Is.SameAs(target));
                Assert.That(error, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(identityObject);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task ActorScopeResolver_ResolveAsync_ByIdentity_DoesNotUseAncestorRegistryFallback()
        {
            var (_, origin, _, identity, identityObject) = CreateIdentityRegistryFixture(registerRegistryOnOrigin: false);
            try
            {
                TestScopeNode child = new TestScopeNode(origin, LifetimeScopeKind.Entity, new TestRuntimeResolver());
                CommandContext context = new CommandContext(child, new VarStore(initialCapacity: 4), new TestCommandRunner(child));
                ActorSource source = CreateByIdentitySource(identity.Id, identity.Kind);

                var (resolved, error) = await InvokeActorScopeResolveAsync(source, context);

                Assert.That(resolved, Is.Null);
                Assert.That(error, Does.Contain("Scope registry is not available."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(identityObject);
            }
        }

        [Test]
        public void ActorSourceFastResolver_ResolveByIdentity_DoesNotUseAncestorRegistryFallback()
        {
            var (_, origin, _, identity, identityObject) = CreateIdentityRegistryFixture(registerRegistryOnOrigin: false);
            try
            {
                TestScopeNode child = new TestScopeNode(origin, LifetimeScopeKind.Entity, new TestRuntimeResolver());
                ActorSource source = CreateByIdentitySource(identity.Id, identity.Kind);

                IScopeNode? resolved = ActorSourceFastResolver.Resolve(child, source, commandRootScope: null);

                Assert.That(resolved, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(identityObject);
            }
        }

        static ActorSource CreateByIdentitySource(string id, LifetimeScopeKind kind)
        {
            return new ActorSource
            {
                Kind = ActorSourceKind.ByIdentity,
                Identity = new CommandTargetIdentityFilter
                {
                    id = id,
                    kind = kind,
                    searchScope = CommandTargetSearchScope.All,
                    requireActive = true,
                },
            };
        }

        static async Task<(IScopeNode? scope, string error)> InvokeActorScopeResolveAsync(ActorSource source, CommandContext context)
        {
            try
            {
                UniTask<(IScopeNode? scope, string error)> task = (UniTask<(IScopeNode? scope, string error)>)ResolveActorScopeMethod.Invoke(null, new object[] { source, context, CancellationToken.None })!;
                return await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        static (BaseLifetimeScopeRegistry registry, TestScopeNode origin, TestScopeNode target, TestIdentity identity, GameObject identityObject) CreateIdentityRegistryFixture(bool registerRegistryOnOrigin)
        {
            BaseLifetimeScopeRegistry registry = new BaseLifetimeScopeRegistry();
            TestIdentity identity = new TestIdentity("identity-target", LifetimeScopeKind.Scene);
            TestScopeNode target = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver(), identity: identity);
            registry.RegisterScope(target, identity);

            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestRuntimeResolver originResolver = new TestRuntimeResolver();

            if (registerRegistryOnOrigin)
                originResolver.Register<IBaseLifetimeScopeRegistry>(registry);
            else
                parentResolver.Register<IBaseLifetimeScopeRegistry>(registry);

            TestScopeNode parent = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode origin = new TestScopeNode(parent: parent, kind: LifetimeScopeKind.Entity, resolver: originResolver);
            return (registry, origin, target, identity, identity.Owner);
        }

        static async Task<IScopeNode> InvokeResolveTargetScopeAsync(MethodInfo method)
        {
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner).WithOptions(AllowFallbackOptions);
            ActorSource source = new ActorSource { Kind = ActorSourceKind.GameLogicRoot };

            try
            {
                UniTask<IScopeNode> task = (UniTask<IScopeNode>)method.Invoke(null, new object[] { source, context, CancellationToken.None })!;
                return await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        sealed class TestCommandRunner : ICommandRunner
        {
            public TestCommandRunner(IScopeNode scope)
            {
                Scope = scope;
            }

            public IScopeNode Scope { get; }

            public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                _ = options;
                return UniTask.FromResult(CommandRunResult.Completed(0, 0, CommandRunFailureKind.None, -1, string.Empty, null, null));
            }

            public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = list;
                _ = ctx;
                _ = ct;
                _ = options;
                return UniTask.FromResult(CommandRunResult.Completed(0, 0, CommandRunFailureKind.None, -1, string.Empty, null, null));
            }

            public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = list;
                _ = onCanceled;
                _ = ctx;
                _ = ct;
                _ = options;
                return UniTask.FromResult(CommandRunResult.Completed(0, 0, CommandRunFailureKind.None, -1, string.Empty, null, null));
            }
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object> services = new();

            public void Register<T>(T instance)
            {
                services[typeof(T)] = instance!;
            }

            public void Dispose()
            {
            }

            public void Inject(object instance)
            {
                _ = instance;
            }

            public object Resolve(Type type)
            {
                if (services.TryGetValue(type, out object instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {type.FullName}.");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out T instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {typeof(T).FullName}.");
            }

            public object? ResolveOrDefault(Type type)
            {
                return services.TryGetValue(type, out object instance)
                    ? instance
                    : null;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                bool found = services.TryGetValue(type, out object resolved);
                instance = resolved;
                return found;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (services.TryGetValue(typeof(T), out object resolved) && resolved is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }
        }

        sealed class TestIdentity : IScopeIdentityService
        {
            readonly GameObject owner;

            public TestIdentity(string id, LifetimeScopeKind kind, string category = "")
            {
                owner = new GameObject($"TestIdentity_{id}");
                Id = id;
                Kind = kind;
                Category = category;
                IsActive = true;
            }

            public GameObject Owner => owner;

            public LifetimeScopeKind Kind { get; }

            public string Id { get; }

            public string Category { get; }

            public bool IsActive { get; set; }

            public Transform SelfTransform => owner.transform;

            public float Radius => 0f;

            public TimeScaleBehavior TimeScaleBehavior => TimeScaleBehavior.Scaled;
        }

        sealed class TestScopeNode : IScopeNode
        {
            public TestScopeNode(IScopeNode? parent, LifetimeScopeKind kind, IRuntimeResolver resolver, IScopeIdentityService? identity = null)
            {
                Parent = parent;
                Kind = kind;
                Resolver = resolver;
                Identity = identity;
            }

            public IScopeNode? Parent { get; }

            public IScopeIdentityService? Identity { get; }

            public LifetimeScopeKind Kind { get; }

            public IRuntimeResolver? Resolver { get; }

            public bool IsVisible => true;

            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                if (Parent == null)
                    return new[] { this };

                IReadOnlyList<IScopeNode>? parentPath = Parent.GetPathFromRoot();
                List<IScopeNode> path = new(parentPath?.Count + 1 ?? 1);
                if (parentPath != null)
                {
                    for (int i = 0; i < parentPath.Count; i++)
                        path.Add(parentPath[i]);
                }

                path.Add(this);
                return path;
            }
        }
    }
}
