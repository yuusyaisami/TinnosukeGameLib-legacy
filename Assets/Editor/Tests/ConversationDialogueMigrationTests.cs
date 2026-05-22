#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.Conversation;
using Game.Dialogue;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ConversationDialogueMigrationTests
    {
        static readonly FieldInfo ChannelsField = typeof(ConversationChannelHubMB)
            .GetField("_channels", BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Test]
        public async System.Threading.Tasks.Task ConversationFlowExecutor_StrictRunFailsWhenHubIsMissing()
        {
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
            ConversationFlowExecutor executor = new ConversationFlowExecutor();
            ConversationFlowCommandData data = new ConversationFlowCommandData
            {
                Operation = ConversationFlowOperation.Run,
                Strict = true,
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
            Assert.That(exception.Message, Does.Contain("[V22-M4-CONV-001] IConversationChannelHubService is missing on target scope."));
        }

        [Test]
        public async System.Threading.Tasks.Task ConversationFlowExecutor_StrictRunFailsWhenTargetScopeResolutionFails()
        {
            TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
            TestCommandRunner runner = new TestCommandRunner(scope);
            CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
            ConversationFlowExecutor executor = new ConversationFlowExecutor();
            ConversationFlowCommandData data = new ConversationFlowCommandData
            {
                Operation = ConversationFlowOperation.Run,
                Strict = true,
                Target = new ActorSource
                {
                    Kind = ActorSourceKind.GameLogicRoot,
                },
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
            Assert.That(exception.Message, Does.StartWith("[V22-M4-CONV-001] "));
        }

        [Test]
        public async System.Threading.Tasks.Task ConversationFlowExecutor_EndFailsWhenDialogueServiceIsMissing()
        {
            GameObject go = new GameObject("ConversationHub");
            try
            {
                TestRuntimeResolver resolver = new TestRuntimeResolver();
                TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
                ConversationChannelHubMB hubMb = go.AddComponent<ConversationChannelHubMB>();
                ConversationChannelHubService hub = new ConversationChannelHubService(scope, hubMb);
                hub.OnAcquire(scope, isReset: false);
                resolver.Register<IConversationChannelHubService>(hub);

                bool started = hub.TryStartSession("default", new ConversationFlowPreset(), out IConversationRuntimeSession? session, out string message);
                Assert.That(started, Is.True, message);
                Assert.That(session, Is.Not.Null);

                TestCommandRunner runner = new TestCommandRunner(scope);
                CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
                ConversationFlowExecutor executor = new ConversationFlowExecutor();
                ConversationFlowCommandData data = new ConversationFlowCommandData
                {
                    Operation = ConversationFlowOperation.End,
                    Strict = true,
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
                Assert.That(exception.Message, Does.Contain("[V22-M4-CONV-001][CONV-241] IDialogueService is missing on dialogue channel scope."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task ConversationFlowExecutor_EndFailsWhenDialogueCloseFails()
        {
            GameObject go = new GameObject("ConversationHub");
            try
            {
                TestRuntimeResolver resolver = new TestRuntimeResolver();
                TestScopeNode scope = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: resolver);
                ConversationChannelHubMB hubMb = go.AddComponent<ConversationChannelHubMB>();
                ConversationChannelHubService hub = new ConversationChannelHubService(scope, hubMb);
                hub.OnAcquire(scope, isReset: false);
                resolver.Register<IConversationChannelHubService>(hub);
                resolver.Register<IDialogueService>(new TestDialogueService(visible: true, active: true, endResult: false));

                bool started = hub.TryStartSession("default", new ConversationFlowPreset(), out IConversationRuntimeSession? session, out string message);
                Assert.That(started, Is.True, message);
                Assert.That(session, Is.Not.Null);

                TestCommandRunner runner = new TestCommandRunner(scope);
                CommandContext context = new CommandContext(scope, new VarStore(initialCapacity: 4), runner);
                ConversationFlowExecutor executor = new ConversationFlowExecutor();
                ConversationFlowCommandData data = new ConversationFlowCommandData
                {
                    Operation = ConversationFlowOperation.End,
                    Strict = true,
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
                Assert.That(exception.Message, Does.Contain("[V22-M4-CONV-002][CONV-203] Dialogue channel end failed."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConversationChannelHubService_TryStartSessionFailsBeforeAcquire()
        {
            GameObject go = new GameObject("ConversationHub");
            try
            {
                ConversationChannelHubMB hubMb = go.AddComponent<ConversationChannelHubMB>();
                TestScopeNode owner = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
                ConversationChannelHubService hub = new ConversationChannelHubService(owner, hubMb);

                bool started = hub.TryStartSession("default", new ConversationFlowPreset(), out IConversationRuntimeSession? session, out string message);

                Assert.That(started, Is.False);
                Assert.That(session, Is.Null);
                Assert.That(message, Is.EqualTo("[CONV-100] Conversation hub is not acquired."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConversationChannelHubService_TryStartSessionFailsWhenDefinitionIsMissing()
        {
            GameObject go = new GameObject("ConversationHub");
            try
            {
                ConversationChannelHubMB hubMb = go.AddComponent<ConversationChannelHubMB>();
                SetChannels(hubMb, new List<ConversationChannelDefinition>());

                TestScopeNode owner = new TestScopeNode(parent: null, kind: LifetimeScopeKind.Scene, resolver: new TestRuntimeResolver());
                ConversationChannelHubService hub = new ConversationChannelHubService(owner, hubMb);
                hub.OnAcquire(owner, isReset: false);

                bool started = hub.TryStartSession("default", new ConversationFlowPreset(), out IConversationRuntimeSession? session, out string message);

                Assert.That(started, Is.False);
                Assert.That(session, Is.Null);
                Assert.That(message, Is.EqualTo("[V22-M4-CONV-001][CONV-104] Conversation channel definition is missing. tag='default'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task DialogueService_MissingChannelReturnsExplicitFailure()
        {
            DialogueService service = new DialogueService(new EmptyDialogueChannelHubService());

            DialogueMessageResult messageResult = await service.ShowMessageAsync("missing", new DialogueMessageRequest(), CancellationToken.None);
            DialogueChoiceResult choiceResult = await service.ShowChoiceAndWaitAsync("missing", new DialogueChoiceRequest(), CancellationToken.None);

            Assert.That(messageResult.Success, Is.False);
            Assert.That(messageResult.Message, Does.Contain("[DIALOGUE-300] Channel not found."));
            Assert.That(choiceResult.Success, Is.False);
            Assert.That(choiceResult.Message, Does.Contain("[DIALOGUE-301] Channel not found."));
        }

        sealed class EmptyDialogueChannelHubService : IDialogueChannelHubService
        {
            public int ChannelCount => 0;

            public bool Contains(string tag)
            {
                _ = tag;
                return false;
            }

            public void GetTags(List<string> output)
            {
                output?.Clear();
            }

            public IDialogueChannelService GetChannel(string tag)
            {
                throw new KeyNotFoundException(tag);
            }

            public bool TryGetChannel(string tag, out IDialogueChannelService? channel)
            {
                _ = tag;
                channel = null;
                return false;
            }

            public bool RegisterOrReplace(string tag, DialogueChannelPreset preset)
            {
                _ = tag;
                _ = preset;
                return false;
            }

            public bool Unregister(string tag)
            {
                _ = tag;
                return false;
            }
        }

        static void SetChannels(ConversationChannelHubMB hubMb, List<ConversationChannelDefinition> channels)
        {
            ChannelsField.SetValue(hubMb, channels);
        }

        sealed class TestDialogueService : IDialogueService
        {
            readonly DialogueChannelSnapshot _snapshot;
            readonly bool _endResult;

            public TestDialogueService(bool visible, bool active, bool endResult)
            {
                _snapshot = new DialogueChannelSnapshot(
                    tag: "default",
                    isVisible: visible,
                    isActive: active,
                    isInputEnabled: true,
                    dialogueCount: 0,
                    typewriterState: DialogueTypewriterState.Idle,
                    choiceState: DialogueChoiceState.None,
                    activeCharacterAnchor: default);
                _endResult = endResult;
            }

            public bool TryGetSnapshot(string channelTag, out DialogueChannelSnapshot snapshot)
            {
                _ = channelTag;
                snapshot = _snapshot;
                return true;
            }

            public bool SetVisible(string channelTag, bool visible)
            {
                _ = channelTag;
                _ = visible;
                return true;
            }

            public bool SetActive(string channelTag, bool active)
            {
                _ = channelTag;
                _ = active;
                return true;
            }

            public bool SetInputEnabled(string channelTag, bool enabled)
            {
                _ = channelTag;
                _ = enabled;
                return true;
            }

            public bool TryRequestAdvance(string channelTag)
            {
                _ = channelTag;
                return true;
            }

            public bool TryCancelChoice(string channelTag, string reason = "")
            {
                _ = channelTag;
                _ = reason;
                return true;
            }

            public UniTask<bool> SetupAsync(string channelTag, DialogueSetupRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(true);
            }

            public UniTask<DialogueMessageResult> ShowMessageAsync(string channelTag, DialogueMessageRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(DialogueMessageResult.Failed("not used"));
            }

            public UniTask<DialogueChoiceResult> ShowChoiceAndWaitAsync(string channelTag, DialogueChoiceRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(DialogueChoiceResult.Failed("not used"));
            }

            public UniTask<bool> ApplyCharactersAsync(string channelTag, DialogueCharacterFrameRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> RefreshLayoutAsync(string channelTag, DialogueLayoutRefreshRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> EndAsync(string channelTag, DialogueEndRequest request, CancellationToken ct = default)
            {
                _ = channelTag;
                _ = request;
                _ = ct;
                return UniTask.FromResult(_endResult);
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

            public void Register<T>(T instance) where T : class
            {
                services[typeof(T)] = instance;
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
