#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.StatusEffect;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class StatusEffectServiceDependencyCaptureTests
    {
        static readonly PropertyInfo CommandRunnerProperty = typeof(StatusEffectService)
            .GetProperty("CommandRunner", BindingFlags.Instance | BindingFlags.NonPublic)!;

        static readonly PropertyInfo BlackboardServiceProperty = typeof(StatusEffectService)
            .GetProperty("BlackboardService", BindingFlags.Instance | BindingFlags.NonPublic)!;

        static readonly PropertyInfo MutationServiceProperty = typeof(StatusEffectService)
            .GetProperty("MutationService", BindingFlags.Instance | BindingFlags.NonPublic)!;

        static readonly MethodInfo TryResolveGlobalBlackboardVarsMethod = typeof(StatusEffectService)
            .GetMethod("TryResolveGlobalBlackboardVars", BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Test]
        public void OnAcquire_FreezesMissingCommandRunnerUntilNextAcquire()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parentScope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode scope = new TestScopeNode(parentScope, LifetimeScopeKind.Entity, new TestRuntimeResolver());

            StatusEffectService service = new StatusEffectService(scope);
            service.OnAcquire(scope, isReset: false);

            parentResolver.Register<ICommandRunner>(new TestCommandRunner(scope));

            Assert.That(GetCommandRunner(service), Is.Null);

            service.OnRelease(scope, isReset: false);
            service.OnAcquire(scope, isReset: false);

            Assert.That(GetCommandRunner(service), Is.Not.Null);
        }

        [Test]
        public void OnAcquire_FreezesMissingBlackboardUntilNextAcquire()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parentScope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode scope = new TestScopeNode(parentScope, LifetimeScopeKind.Entity, new TestRuntimeResolver());

            StatusEffectService service = new StatusEffectService(scope);
            service.OnAcquire(scope, isReset: false);

            BlackboardService blackboard = new BlackboardService(parentScope);
            parentResolver.Register<IBlackboardService>(blackboard);

            Assert.That(GetBlackboardService(service), Is.Null);

            service.OnRelease(scope, isReset: false);
            service.OnAcquire(scope, isReset: false);

            Assert.That(GetBlackboardService(service), Is.SameAs(blackboard));
        }

        [Test]
        public void OnRelease_ClearsPreviouslyCapturedCommandRunner()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parentScope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode scope = new TestScopeNode(parentScope, LifetimeScopeKind.Entity, new TestRuntimeResolver());

            StatusEffectService service = new StatusEffectService(scope);
            TestCommandRunner runner = new TestCommandRunner(scope);
            parentResolver.Register<ICommandRunner>(runner);

            service.OnAcquire(scope, isReset: false);
            Assert.That(GetCommandRunner(service), Is.SameAs(runner));

            service.OnRelease(scope, isReset: false);

            Assert.That(GetCommandRunner(service), Is.Null);
        }

        [Test]
        public void InactiveState_DoesNotResolveMutationServiceBeforeAcquire()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parentScope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode scope = new TestScopeNode(parentScope, LifetimeScopeKind.Entity, new TestRuntimeResolver());

            parentResolver.Register<ICommandListRuntimeMutationService>(new TestMutationService());
            StatusEffectService service = new StatusEffectService(scope);

            Assert.That(GetMutationService(service), Is.Null);
        }

        [Test]
        public async System.Threading.Tasks.Task StatusEffectExecutor_ThrowsWhenServiceIsMissingOnResolvedScope()
        {
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);

            StatusEffectExecutor executor = new StatusEffectExecutor();
            StatusEffectCommandData data = new StatusEffectCommandData
            {
                Op = StatusEffectCommandOp.Remove,
                ServiceScope = StatusEffectServiceScope.Scope,
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
            Assert.That(exception.Message, Does.StartWith("[V22-M4-STATUS-001] "));
        }

        [Test]
        public async System.Threading.Tasks.Task StatusEffectExecutor_ThrowsWhenApplyFailsOnResolvedService()
        {
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
            resolver.Register<IStatusEffectService>(new RejectingStatusEffectService());

            StatusEffectExecutor executor = new StatusEffectExecutor();
            StatusEffectCommandData data = new StatusEffectCommandData
            {
                Op = StatusEffectCommandOp.Apply,
                ServiceScope = StatusEffectServiceScope.Scope,
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
            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-STATUS-001] Status effect apply failed."));
        }

        [Test]
        public async System.Threading.Tasks.Task WriteStatusEffectDataExecutor_ThrowsWhenTargetScopeCannotBeResolved()
        {
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);

            WriteStatusEffectDataExecutor executor = new WriteStatusEffectDataExecutor();
            WriteStatusEffectDataCommandData data = new WriteStatusEffectDataCommandData
            {
                SourceMode = WriteStatusEffectDataSourceMode.Definition,
                TargetActorSource = new ActorSource { Kind = ActorSourceKind.GameLogicRoot },
                Target = VarStoreTarget.CommandVars,
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
            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-SCALAR-001] "));
        }

        [Test]
        public async System.Threading.Tasks.Task WriteStatusEffectDataExecutor_ThrowsWhenRuntimeServiceScopeCannotBeResolved()
        {
            TestRuntimeResolver resolver = new TestRuntimeResolver();
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);

            WriteStatusEffectDataExecutor executor = new WriteStatusEffectDataExecutor();
            WriteStatusEffectDataCommandData data = new WriteStatusEffectDataCommandData
            {
                SourceMode = WriteStatusEffectDataSourceMode.Runtime,
                TargetActorSource = new ActorSource { Kind = ActorSourceKind.Current },
                ServiceActorSource = new ActorSource { Kind = ActorSourceKind.GameLogicRoot },
                Target = VarStoreTarget.CommandVars,
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
            Assert.That(exception!.FailureKind, Is.EqualTo(CommandRunFailureKind.ResolveFailed));
            Assert.That(exception.Message, Does.StartWith("[V22-M4-SCALAR-001] "));
        }

        [Test]
        public void GameScene_StatusEffectAnchorsContainStatusEffectMB()
        {
            string sceneOne = File.ReadAllText(Path.Combine(TestProjectRoot, "Assets", "Scenes", "GameScene.unity"));
            string sceneTwo = File.ReadAllText(Path.Combine(TestProjectRoot, "Assets", "Scenes", "GameScene", "GameScene.unity"));

            Assert.That(sceneOne, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Game.StatusEffect.StatusEffectMB"));
            Assert.That(sceneTwo, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Game.StatusEffect.StatusEffectMB"));
        }

        [Test]
        public void TryResolveGlobalBlackboardVars_ReturnsNullWithoutExplicitBindingOptions()
        {
            TestRuntimeResolver parentResolver = new TestRuntimeResolver();
            TestScopeNode parentScope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: parentResolver);
            TestScopeNode scope = new TestScopeNode(parentScope, LifetimeScopeKind.Entity, new TestRuntimeResolver());
            BlackboardService blackboard = new BlackboardService(parentScope);
            parentResolver.Register<IBlackboardService>(blackboard);

            StatusEffectService service = new StatusEffectService(scope);
            service.OnAcquire(scope, isReset: false);

            object? resolved = TryResolveGlobalBlackboardVarsMethod.Invoke(service, null);

            Assert.That(resolved, Is.Null);
        }

        static string TestProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static ICommandRunner? GetCommandRunner(StatusEffectService service)
        {
            return (ICommandRunner?)CommandRunnerProperty.GetValue(service);
        }

        static IBlackboardService? GetBlackboardService(StatusEffectService service)
        {
            return (IBlackboardService?)BlackboardServiceProperty.GetValue(service);
        }

        static ICommandListRuntimeMutationService? GetMutationService(StatusEffectService service)
        {
            return (ICommandListRuntimeMutationService?)MutationServiceProperty.GetValue(service);
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

        sealed class TestMutationService : ICommandListRuntimeMutationService
        {
            public void Register(CommandListData? list)
            {
                _ = list;
            }
        }

        sealed class RejectingStatusEffectService : IStatusEffectService
        {
            public int ActiveEffectCount => 0;

            public bool TryApply(StatusEffectApplyRequest request, IDynamicContext? evaluationContext, out string instanceId)
            {
                _ = request;
                _ = evaluationContext;
                instanceId = string.Empty;
                return false;
            }

            public int Remove(StatusEffectRuntimeFilter filter)
            {
                _ = filter;
                return 0;
            }

            public int SetEnabled(StatusEffectRuntimeFilter filter, bool enabled)
            {
                _ = filter;
                _ = enabled;
                return 0;
            }

            public int SetOperationEnabled(StatusEffectRuntimeFilter filter, string operationId, bool enabled)
            {
                _ = filter;
                _ = operationId;
                _ = enabled;
                return 0;
            }

            public int Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope = null, CommandContext? sourceContext = null)
            {
                _ = filter;
                _ = userScope;
                _ = sourceContext;
                return 0;
            }

            public int UseGlobal(IScopeNode? userScope = null, CommandContext? sourceContext = null)
            {
                _ = userScope;
                _ = sourceContext;
                return 0;
            }

            public int RestoreState(StatusEffectRuntimeFilter filter, bool restoreGlobalState = false)
            {
                _ = filter;
                _ = restoreGlobalState;
                return 0;
            }

            public void ClearAll()
            {
            }

            public void RefreshServiceSettings(bool resetGlobalState = true)
            {
                _ = resetGlobalState;
            }

            public void ConfigureServiceSettings(StatusEffectServiceSettingsOverrideRequest request, IDynamicContext? evaluationContext = null)
            {
                _ = request;
                _ = evaluationContext;
            }

            public bool HasEffect(string definitionId)
            {
                _ = definitionId;
                return false;
            }

            public bool IsAnyOperationEnabled(StatusEffectRuntimeFilter filter, string operationId)
            {
                _ = filter;
                _ = operationId;
                return false;
            }

            public bool TryGetRegisteredDefinition(StatusEffectRuntimeFilter filter, out BaseStatusEffectDefinitionData definition)
            {
                _ = filter;
                definition = null!;
                return false;
            }

            public void GetActiveEffectStates(List<EffectState> output)
            {
                output.Clear();
            }

            public void GetStates(List<EffectState> output, StatusEffectRuntimeFilter filter)
            {
                _ = filter;
                output.Clear();
            }

            public StatusEffectGlobalRuntimeState GetDebugState()
            {
                return default;
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
