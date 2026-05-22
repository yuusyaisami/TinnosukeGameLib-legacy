#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Actions;
using Game.Commands.VNext;
using Game.Common;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class GameStateMachineMigrationTests
    {
        const string M4GameStateMachineDiagnosticCode = "[V22-M4-GSM-001]";

        static readonly MethodInfo ResolveServiceMethod = typeof(ChangeGameStateExecutor).Assembly
            .GetType("Game.Commands.VNext.GameStateMachineCommandExecutorUtility", throwOnError: true)!
            .GetMethod("ResolveServiceOrThrow", BindingFlags.Public | BindingFlags.Static)!;

        [Test]
        public void ResolveServiceOrThrow_UsesOriginScopeRegistration()
        {
            TestGameStateMachineService service = new TestGameStateMachineService();
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            resolver.Register<IGameStateMachineService>(service);

            TestScopeNode origin = new TestScopeNode(resolver, kind: LifetimeScopeKind.Scene);

            IGameStateMachineService resolved = InvokeResolveService(origin);

            Assert.That(resolved, Is.SameAs(service));
        }

        [Test]
        public void ResolveServiceOrThrow_RejectsAncestorFallback()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            parentResolver.Register<IGameStateMachineService>(new TestGameStateMachineService());

            TestScopeNode parent = new TestScopeNode(parentResolver, kind: LifetimeScopeKind.Scene);
            TestScopeNode origin = new TestScopeNode(new TestRuntimeResolver(), parent, LifetimeScopeKind.Entity);

            CommandExecutionException exception = Assert.Throws<CommandExecutionException>(() => InvokeResolveService(origin));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.Contain(M4GameStateMachineDiagnosticCode));
            Assert.That(exception.Message, Does.Contain("resolved state-machine source scope"));
        }

        [Test]
        public void ResolveServiceOrThrow_RejectsNullOrigin()
        {
            CommandExecutionException exception = Assert.Throws<CommandExecutionException>(() => InvokeResolveService(null!));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.Contain(M4GameStateMachineDiagnosticCode));
            Assert.That(exception.Message, Does.Contain("Resolved state-machine source scope is null."));
        }

        [Test]
        public void ChangeGameStateCommandData_DefaultsToGameLogicRoot()
        {
            ChangeGameStateCommandData data = new ChangeGameStateCommandData();

            Assert.That(data.StateMachineSource.Kind, Is.EqualTo(ActorSourceKind.GameLogicRoot));
        }

        [Test]
        public async System.Threading.Tasks.Task ChangeGameStateExecutor_RejectsUnresolvedGameLogicRoot()
        {
            ChangeGameStateExecutor executor = new ChangeGameStateExecutor();
            TestScopeNode scope = new TestScopeNode(new TestRuntimeResolver(), kind: LifetimeScopeKind.Scene);
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);

            CommandExecutionException exception = Assert.ThrowsAsync<CommandExecutionException>(async () =>
                await executor.Execute(new ChangeGameStateCommandData(), context, CancellationToken.None));

            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.Contain(M4GameStateMachineDiagnosticCode));
            Assert.That(exception.Message, Does.Contain("GameLogicRoot scope was not found."));
        }

        static IGameStateMachineService InvokeResolveService(IScopeNode origin)
        {
            try
            {
                return (IGameStateMachineService)ResolveServiceMethod.Invoke(null, new object[] { origin })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        sealed class TestGameStateMachineService : IGameStateMachineService
        {
            GameState currentState;

            public void ChangeState(GameState newState)
            {
                currentState = newState;
            }

            public GameState GetCurrentState()
            {
                return currentState;
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

        sealed class TestScopeNode : IScopeNode
        {
            readonly IRuntimeResolver resolver;
            readonly IScopeNode? parent;
            readonly LifetimeScopeKind kind;

            public TestScopeNode(IRuntimeResolver resolver, IScopeNode? parent = null, LifetimeScopeKind kind = LifetimeScopeKind.Project)
            {
                this.resolver = resolver;
                this.parent = parent;
                this.kind = kind;
            }

            public IScopeNode? Parent => parent;

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind => kind;

            public IRuntimeResolver? Resolver => resolver;

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
                if (parent == null)
                    return new[] { this };

                IReadOnlyList<IScopeNode>? parentPath = parent.GetPathFromRoot();
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
