#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandChannelExecutor : ICommandExecutor
    {
        static readonly object BackgroundGateLock = new();
        static readonly HashSet<string> RunningBackgroundKeys = new(StringComparer.Ordinal);
        static readonly AsyncLocal<List<string>?> ExecutionStack = new();

        public int CommandId => CommandIds.CommandChannelExecute;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandChannelCommandData is required.");

            if (ctx == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandContext is required.");

            if (string.IsNullOrWhiteSpace(typed.Tag))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Tag is empty.");

            var channelOwnerScope = ResolveActorScope(typed.ActorSource, ctx);
            if (typed.ActorSource.Kind != ActorSourceKind.Current && channelOwnerScope == null)
            {
                var actorResolveMessage =
                    $"CommandChannel owner resolve failed. RequestedOwner={typed.ActorSource.Kind}, Tag={typed.Tag}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[CommandChannelExecutor] {actorResolveMessage}");
#endif
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, actorResolveMessage);
            }

            channelOwnerScope ??= ctx.Actor ?? ctx.Scope;
            return ExecuteForTargetsAsync(typed, ctx, channelOwnerScope, ct);
        }

        async UniTask ExecuteForTargetsAsync(
            CommandChannelCommandData typed,
            CommandContext ctx,
            IScopeNode channelOwnerScope,
            CancellationToken ct)
        {
            foreach (var targetOwnerScope in GetExecutionTargets(channelOwnerScope, typed.ExecutionScope))
            {
                if (targetOwnerScope == null)
                    continue;

                var executionScope = ResolveExecutionScope(typed, ctx, targetOwnerScope);
                if (typed.ExecutionActorMode == CommandChannelExecutionActorMode.UseActorSource && executionScope == null)
                {
                    var actorResolveMessage =
                        $"CommandChannel execution actor resolve failed. RequestedActor={typed.ExecutorActorSource.Kind}, Tag={typed.Tag}";
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, actorResolveMessage);
                }

                var channelCtx = BuildExecutionContext(ctx, executionScope);

                var hub = ResolveCommandChannelHub(ctx, targetOwnerScope, typed.ActorSource, typed.Tag);
                if (hub == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandChannelHubService is missing.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var executeScope = channelCtx.Actor ?? channelCtx.Scope;
                var scopeName = executeScope?.Identity?.SelfTransform != null ? executeScope.Identity.SelfTransform.name : "(null)";
                //Debug.Log($"[CommandChannelExecutor] Execute Tag={typed.Tag}, Await={typed.AwaitMode}, ActorSource={typed.ActorSource.Kind}, Scope={scopeName}");
#endif

                var runTask = ExecuteInternalAsync(hub, typed.Tag, channelCtx, ct);
                if (typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion)
                {
                    await runTask;
                    continue;
                }

                var backgroundKey = BuildBackgroundKey(hub, typed.Tag, channelCtx);
                if (!TryEnterBackground(backgroundKey))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[CommandChannelExecutor] Skip RunInBackground duplicate. Key={backgroundKey}");
#endif
                    continue;
                }

                RunInBackground(runTask, backgroundKey).Forget();
            }
        }

        static IEnumerable<IScopeNode> GetExecutionTargets(IScopeNode actorScope, WithActorExecutionScope executionScope)
        {
            return executionScope switch
            {
                WithActorExecutionScope.ActorAndDescendants => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: true),
                WithActorExecutionScope.DescendantsOnly => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: false),
                _ => new[] { actorScope }
            };
        }

        static CommandContext BuildExecutionContext(CommandContext ctx, IScopeNode? executionScope)
        {
            if (executionScope == null)
                return ctx;

            var runner = ResolveRunner(executionScope) ?? ctx.Runner;
            if (runner == null)
                return ctx;

            return new CommandContext(
                executionScope,
                ctx.Vars,
                runner,
                actor: executionScope,
                options: ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.Actor);
        }

        static IScopeNode? ResolveExecutionScope(
            CommandChannelCommandData typed,
            CommandContext ctx,
            IScopeNode? channelOwnerScope)
        {
            switch (typed.ExecutionActorMode)
            {
                case CommandChannelExecutionActorMode.UseChannelOwner:
                    return channelOwnerScope ?? ctx.Actor ?? ctx.Scope;

                case CommandChannelExecutionActorMode.UseCurrentActor:
                    return ctx.Actor ?? ctx.Scope;

                case CommandChannelExecutionActorMode.UseActorSource:
                    return ResolveActorScope(typed.ExecutorActorSource, ctx);

                default:
                    return channelOwnerScope ?? ctx.Actor ?? ctx.Scope;
            }
        }

        static IScopeNode? ResolveActorScope(ActorSource source, CommandContext ctx)
        {
            var commandRoot = ctx.CommandRootScope;

            IScopeNode? TryResolveFrom(IScopeNode? origin, string label)
            {
                if (origin == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[CommandChannelExecutor] ActorResolve skip: {label}=null, Source={DescribeActorSource(source)}");
#endif
                    return null;
                }

                var resolved = ActorSourceFastResolver.Resolve(origin, source, commandRoot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log(
                //    $"[CommandChannelExecutor] ActorResolve try: from={label}({DescribeScope(origin)}) " +
                //    $"source={DescribeActorSource(source)} => {(resolved != null ? DescribeScope(resolved) : "null")}");
#endif
                return resolved;
            }

            // Primary path
            var resolved = TryResolveFrom(ctx.Actor, "ctx.Actor") ?? TryResolveFrom(ctx.Scope, "ctx.Scope");
            if (resolved != null)
                return resolved;

            // Fallback roots for nested command execution contexts.
            resolved = TryResolveFrom(commandRoot, "ctx.CommandRootScope");
            if (resolved != null)
                return resolved;

            resolved = TryResolveFrom(ctx.RootActor, "ctx.RootActor");
            if (resolved != null)
                return resolved;

            resolved = TryResolveFrom(ctx.CallerActor, "ctx.CallerActor");
            if (resolved != null)
                return resolved;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[CommandChannelExecutor] ActorResolve failed. Source={DescribeActorSource(source)}");
#endif
            return null;
        }

        static string DescribeActorSource(in ActorSource source)
        {
            if (source.Kind != ActorSourceKind.ByIdentity)
                return source.Kind.ToString();

            var f = source.Identity;
            return $"ByIdentity(kind={f.kind}, id='{f.id}', category='{f.category}', requireActive={f.requireActive}, searchScope={f.searchScope})";
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "null";

            var t = scope.Identity?.SelfTransform;
            var name = t != null ? t.name : "(no-transform)";
            var id = scope.Identity?.Id ?? "(no-id)";
            var category = scope.Identity?.Category ?? "(no-category)";
            return $"{name}|Kind={scope.Kind}|Id={id}|Category={category}";
        }

        static ICommandRunner? ResolveRunner(IScopeNode scope)
        {
            var resolver = scope.Resolver;
            if (resolver == null)
                return null;

            if (resolver.TryResolve<ICommandRunner>(out var runner) && runner != null)
                return runner;

            return null;
        }

        static ICommandChannelHubService? ResolveCommandChannelHub(
            CommandContext originalCtx,
            IScopeNode? ownerScope,
            ActorSource actorSource,
            string tag)
        {
            var preferOwnerHub = actorSource.Kind != ActorSourceKind.Current;
            var ownerResolver = ownerScope?.Resolver;
            if (ownerResolver != null)
            {
                if (TryResolveHubWithTag(ownerResolver, tag, out var ownerTaggedHub))
                    return ownerTaggedHub;

                if (TryResolveHub(ownerResolver, out var ownerAnyHub))
                    return ownerAnyHub;
            }

            // Ownerを明示指定した場合は、元コンテキストへのフォールバックを行わない。
            // これにより「誰の CommandChannel を使うか」を明確化する。
            if (preferOwnerHub)
                return null;

            // Current指定時は従来互換で元コンテキストにもフォールバック。
            if (TryResolveHubWithTag(originalCtx.Resolver, tag, out var originalTagged))
                return originalTagged;

            if (TryResolveHub(originalCtx.Resolver, out var originalAny))
                return originalAny;

            return null;
        }

        static bool TryResolveHubWithTag(IObjectResolver? resolver, string tag, out ICommandChannelHubService? hub)
        {
            hub = null;
            if (!TryResolveHub(resolver, out var resolved) || resolved == null)
                return false;

            if (!resolved.TryGetCommands(tag, out var commands) || commands == null)
                return false;

            hub = resolved;
            return true;
        }

        static bool TryResolveHub(IObjectResolver? resolver, out ICommandChannelHubService? hub)
        {
            hub = null;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<ICommandChannelHubService>(out var resolved) || resolved == null)
                return false;

            hub = resolved;
            return true;
        }

        static UniTask RunInBackground(UniTask task, string backgroundKey)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                }
                finally
                {
                    ExitBackground(backgroundKey);
                }
            });

            return UniTask.CompletedTask;
        }

        static string BuildBackgroundKey(ICommandChannelHubService hub, string tag, CommandContext ctx)
        {
            var scope = ctx.Actor ?? ctx.Scope;
            var hubKey = RuntimeHelpers.GetHashCode(hub);
            var scopeKey = scope != null ? RuntimeHelpers.GetHashCode(scope) : 0;
            return $"{hubKey}:{scopeKey}:{tag}";
        }

        static bool TryEnterBackground(string key)
        {
            lock (BackgroundGateLock)
            {
                if (RunningBackgroundKeys.Contains(key))
                    return false;

                RunningBackgroundKeys.Add(key);
                return true;
            }
        }

        static void ExitBackground(string key)
        {
            lock (BackgroundGateLock)
            {
                RunningBackgroundKeys.Remove(key);
            }
        }

        static async UniTask ExecuteInternalAsync(
            ICommandChannelHubService hub,
            string tag,
            CommandContext ctx,
            CancellationToken ct)
        {
            var executeKey = BuildBackgroundKey(hub, tag, ctx);
            if (!TryEnterExecution(executeKey, out var chain))
            {
                var recursionMessage =
                    $"Recursive CommandChannel detected. tag='{tag}' key='{executeKey}'. Chain={chain}";
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, recursionMessage);
            }

            try
            {
                var result = await hub.ExecuteAsync(tag, ctx, ct);
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                {
                    var msg = $"CommandChannel failed for tag='{tag}'. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                    throw new CommandExecutionException(result.FailureKind, msg);
                }
            }
            catch (CommandExecutionException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[CommandChannelExecutor] CommandChannel execution was canceled for tag='{tag}'.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"[CommandChannelExecutor] Exception executing CommandChannel. Tag='{tag}'", ex));
                throw;
            }
            finally
            {
                ExitExecution(executeKey);
            }
        }

        static bool TryEnterExecution(string key, out string chain)
        {
            var stack = ExecutionStack.Value;
            if (stack == null)
            {
                stack = new List<string>(4);
                ExecutionStack.Value = stack;
            }

            if (stack.Contains(key))
            {
                chain = stack.Count > 0 ? string.Join(" -> ", stack) : "<empty>";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[CommandChannelExecutor] Recursive execution blocked. Key={key}, Chain={chain}");
#endif
                return false;
            }

            stack.Add(key);
            chain = string.Join(" -> ", stack);
            return true;
        }

        static void ExitExecution(string key)
        {
            var stack = ExecutionStack.Value;
            if (stack == null || stack.Count == 0)
                return;

            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(stack[i], key, StringComparison.Ordinal))
                    continue;

                stack.RemoveAt(i);
                break;
            }

            if (stack.Count == 0)
                ExecutionStack.Value = null;
        }
    }
}
