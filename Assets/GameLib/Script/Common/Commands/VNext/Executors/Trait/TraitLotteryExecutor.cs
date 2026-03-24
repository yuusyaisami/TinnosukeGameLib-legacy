#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TraitLotteryExecutor : ICommandExecutor
    {
        readonly ITraitLotteryService _traitLotteryService;

        public TraitLotteryExecutor(ITraitLotteryService traitLotteryService)
        {
            _traitLotteryService = traitLotteryService;
        }

        public int CommandId => CommandIds.TraitLottery;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TraitLotteryCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TraitLotteryCommandData is required.");

            var holder = await ResolveHolderAsync(typed, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            var resolvedCandidates = ResolveCandidates(typed, dynCtx);
            if (resolvedCandidates.Count == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Trait lottery candidates are empty.");

            var drawCount = typed.DrawCount.GetOrDefault(dynCtx, 1);
            if (drawCount <= 0)
                return;

            var request = new TraitLotteryRequest(
                resolvedCandidates,
                drawCount,
                typed.AllowDuplicates,
                typed.ExcludeExistingHolderTraits,
                typed.ShortageMode);

            var result = _traitLotteryService.Draw(request, holder);
            if (result.Count <= 0)
                return;

            _traitLotteryService.Apply(holder, result.Selected, typed.ApplyMode);
        }

        static async UniTask<ITraitHolderService?> ResolveHolderAsync(
            TraitLotteryCommandData data,
            CommandContext ctx,
            CancellationToken ct)
        {
            var (hubScope, error) = await ActorScopeResolver.ResolveAsync(data.HolderHubSource, ctx, ct);
            if (hubScope == null)
            {
                _ = error;
                return null;
            }

            EnsureScopeBuiltIfNeeded(hubScope);
            var resolver = hubScope.Resolver;
            if (resolver == null || !resolver.TryResolve<ITraitHolderHubService>(out var hub) || hub == null)
                return null;

            if (!hub.TryGetHolder(data.HolderKey, out var holder) || holder == null)
                return null;

            return holder;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static List<TraitDefinitionSO> ResolveCandidates(TraitLotteryCommandData data, IDynamicContext dynCtx)
        {
            var resolved = new List<TraitDefinitionSO>();

            if (data.Candidates != null && data.Candidates.Count > 0)
            {
                for (var i = 0; i < data.Candidates.Count; i++)
                {
                    var candidate = data.Candidates[i];
                    if (candidate != null)
                        resolved.Add(candidate);
                }
            }

            if (data.ConditionalCandidates == null || data.ConditionalCandidates.Count == 0)
                return resolved;

            for (var i = 0; i < data.ConditionalCandidates.Count; i++)
            {
                var group = data.ConditionalCandidates[i];
                if (group == null)
                    continue;

                if (!group.Condition.TryGet(dynCtx, out var enabled) || !enabled)
                    continue;

                if (group.Traits == null || group.Traits.Count == 0)
                    continue;

                for (var j = 0; j < group.Traits.Count; j++)
                {
                    var dynamicTrait = group.Traits[j];
                    if (!dynamicTrait.TryGet(dynCtx, out var trait) || trait == null)
                        continue;

                    resolved.Add(trait);
                }
            }

            return resolved;
        }
    }
}
