#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using NUnit.Framework;
using VContainer;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class GridObjectAuthorityMigrationTests
    {
        static readonly Type GridRuntimeUtilityType = typeof(BindGridObjectChannelExecutor).Assembly
            .GetType("Game.Channel.GridObjectChannelRuntimeUtility", throwOnError: true)!;

        static readonly Type GridSourceRuntimeType = typeof(BindGridObjectChannelExecutor).Assembly
            .GetType("Game.Channel.GridObjectChannelGridBlackboardSourceRuntime", throwOnError: true)!;

        static readonly MethodInfo TryResolveFromScopeMethod = GridRuntimeUtilityType
            .GetMethod("TryResolveFromScope", BindingFlags.Public | BindingFlags.Static)!;

        static readonly MethodInfo TryResolveGridBlackboardMethod = GridSourceRuntimeType
            .GetMethod("TryResolveGridBlackboard", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Test]
        public async System.Threading.Tasks.Task BindGridObjectChannelExecutor_FailsWithV22DiagnosticWhenHubIsMissing()
        {
            BindGridObjectChannelExecutor executor = new BindGridObjectChannelExecutor();
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
            BindGridObjectChannelCommandData data = new BindGridObjectChannelCommandData
            {
                Target = new ActorSource { Kind = ActorSourceKind.Current },
            };

            CommandExecutionException? exception = null;
            try
            {
                await executor.Execute(data, context, CancellationToken.None);
            }
            catch (CommandExecutionException ex)
            {
                exception = ex;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ExecutorMissing));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-GRID-001] "));
        }

        [Test]
        public void GridRuntimeUtility_TryResolveFromScope_DoesNotUseAncestorFallback()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parent = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            parentResolver.Register<ICommandRunner>(new TestCommandRunner(parent));

            TestScopeNode child = new TestScopeNode(parent: parent, kind: LifetimeScopeKind.Entity, resolver: new TestRuntimeResolver());
            object?[] arguments = { child, null };

            bool resolved = (bool)TryResolveFromScopeMethod.MakeGenericMethod(typeof(ICommandRunner)).Invoke(null, arguments)!;

            Assert.That(resolved, Is.False);
            Assert.That(arguments[1], Is.Null);
        }

        [Test]
        public void GridBlackboardSourceRuntime_TryResolveGridBlackboard_DoesNotUseAncestorFallback()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parent = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            parentResolver.Register<IGridBlackboardService>(new GridBlackboardService());

            TestScopeNode child = new TestScopeNode(parent: parent, kind: LifetimeScopeKind.Entity, resolver: new TestRuntimeResolver());
            object?[] arguments = { child, null };

            bool resolved = (bool)TryResolveGridBlackboardMethod.Invoke(null, arguments)!;

            Assert.That(resolved, Is.False);
            Assert.That(arguments[1], Is.Null);
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
            readonly Dictionary<Type, object> _services = new();

            public void Register<T>(T instance)
            {
                _services[typeof(T)] = instance!;
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
                if (_services.TryGetValue(type, out object instance))
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
                return _services.TryGetValue(type, out object instance)
                    ? instance
                    : null;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                bool found = _services.TryGetValue(type, out object resolved);
                instance = resolved;
                return found;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out object resolved) && resolved is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            public TestScopeNode(IScopeNode? parent, LifetimeScopeKind kind, IRuntimeResolver resolver)
            {
                Parent = parent;
                Kind = kind;
                Resolver = resolver;
            }

            public IScopeNode? Parent { get; }

            public IScopeIdentityService? Identity => null;

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
                return Parent == null
                    ? new IScopeNode[] { this }
                    : null;
            }
        }
    }
}
