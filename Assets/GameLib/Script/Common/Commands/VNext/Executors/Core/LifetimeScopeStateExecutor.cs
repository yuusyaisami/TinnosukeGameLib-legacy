#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;

namespace Game.Commands.VNext
{
    public sealed class LifetimeScopeStateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetLifetimeScopeState;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not LifetimeScopeStateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "LifetimeScopeStateCommandData is required.");

            var hasActive = typed.Active.TryGetValue(out var activeValue);
            var hasVisible = typed.Visible.TryGetValue(out var visibleValue);
            if (!hasActive && !hasVisible)
                return;

            var (resolvedScope, error) = await ActorScopeResolver.ResolveAsync(typed.ActorSource, ctx, ct);
            if (resolvedScope == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Actor scope could not be resolved.");
            }

            foreach (var targetScope in GetExecutionTargets(resolvedScope, typed.ExecutionScope))
            {
                if (targetScope == null)
                    continue;

                if (hasActive && !targetScope.TrySetActive(activeValue))
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"Scope {DescribeScope(targetScope)} does not support setting active state.");
                }

                if (hasVisible && !targetScope.TrySetVisible(visibleValue))
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"Scope {DescribeScope(targetScope)} does not support setting visible state.");
                }
            }
        }

        static IEnumerable<IScopeNode> GetExecutionTargets(IScopeNode scope, WithActorExecutionScope executionScope)
        {
            return executionScope switch
            {
                WithActorExecutionScope.ActorAndDescendants => ScopeNodeHierarchy.EnumerateSubtree(scope, includeSelf: true),
                WithActorExecutionScope.DescendantsOnly => ScopeNodeHierarchy.EnumerateSubtree(scope, includeSelf: false),
                _ => new[] { scope }
            };
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "null";

            try
            {
                var id = scope.Identity?.Id ?? "(no id)";
                return $"{scope.Kind}:{id}";
            }
            catch
            {
                return scope.Kind.ToString();
            }
        }
    }
}
