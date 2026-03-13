#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    public sealed class ChunkAdapterService : IChunkAdapter, IScopeAcquireHandler, IScopeReleaseHandler
    {
        IScopeNode? _owner;
        IBlackboardService? _blackboard;
        ICommandRunner? _runner;

        public ChunkAdapterService(IScopeNode owner)
        {
            _owner = owner;
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _owner = scope;
            _blackboard = ResolveBlackboard(scope);
            _runner = ResolveRunner(scope);
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _owner = null;
            _blackboard = null;
            _runner = null;
        }

        public async UniTask InitializeAsync(ChunkContext context, ChunkPlan plan, CancellationToken ct)
        {
            var owner = _owner;
            if (owner == null || owner.Resolver == null)
                return;

            VarKeyRegistryLocator.GetOrCreate();

            var bb = _blackboard ?? ResolveBlackboard(owner);
            if (bb != null)
                WriteBlackboard(bb, context, plan);

            var runner = _runner ?? ResolveRunner(owner);
            if (runner == null)
                return;

            var vars = bb != null ? bb.LocalVars : new VarStore();
            var cmdContext = new CommandContext(owner, vars, runner, actor: owner, options: CommandRunOptions.Default);

            await ExecuteCommandListAsync(plan.CommonCommands, cmdContext, ct);

            if (plan.ConditionalCommands != null && plan.ConditionalCommands.Count > 0)
            {
                var dynContext = new SimpleDynamicContext(vars, owner);
                for (int i = 0; i < plan.ConditionalCommands.Count; i++)
                {
                    var entry = plan.ConditionalCommands[i];
                    if (entry == null)
                        continue;

                    if (!entry.Condition.TryGet(dynContext, out var conditionValue) || !conditionValue)
                        continue;

                    await ExecuteCommandListAsync(entry.Commands, cmdContext, ct);
                }
            }
        }

        static async UniTask ExecuteCommandListAsync(CommandListData? list, CommandContext context, CancellationToken ct)
        {
            if (list == null || list.Count == 0)
                return;

            try
            {
                await context.Runner.ExecuteListAsync(list, context, ct, CommandRunOptions.Default);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChunkAdapterService] Command execution failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        static void WriteBlackboard(IBlackboardService blackboard, ChunkContext context, ChunkPlan plan)
        {
            TrySet(blackboard, VarIds.GameLib.Chunk.coordX, DynamicVariant.FromInt(context.Coord.X));
            TrySet(blackboard, VarIds.GameLib.Chunk.coordY, DynamicVariant.FromInt(context.Coord.Y));
            TrySet(blackboard, VarIds.GameLib.Chunk.seed, DynamicVariant.FromInt(plan.Seed));
            TrySet(blackboard, VarIds.GameLib.Chunk.sizeX, DynamicVariant.FromInt(context.Settings.ChunkSizeCells.x));
            TrySet(blackboard, VarIds.GameLib.Chunk.sizeY, DynamicVariant.FromInt(context.Settings.ChunkSizeCells.y));
            TrySet(blackboard, VarIds.GameLib.Chunk.biomeId, DynamicVariant.FromString(plan.BiomeId));

            TrySet(blackboard, VarIds.GameLib.Chunk.Origin.worldCellX, DynamicVariant.FromInt(context.OriginSettings.WorldOriginCell.x));
            TrySet(blackboard, VarIds.GameLib.Chunk.Origin.worldCellY, DynamicVariant.FromInt(context.OriginSettings.WorldOriginCell.y));
            TrySet(blackboard, VarIds.GameLib.Chunk.Origin.worldPosX, DynamicVariant.FromFloat(context.OriginSettings.WorldOriginPosition.x));
            TrySet(blackboard, VarIds.GameLib.Chunk.Origin.worldPosY, DynamicVariant.FromFloat(context.OriginSettings.WorldOriginPosition.y));

            var vars = plan.VarBox;
            if (vars != null)
            {
                foreach (var kv in vars.Values)
                {
                    var key = ChunkBlackboardKeys.VarPrefix + kv.Key;
                    TrySet(blackboard, key, DynamicVariant.FromFloat(kv.Value));
                }
            }
        }

        static void TrySet(IBlackboardService blackboard, string key, DynamicVariant value)
        {
            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return;

            blackboard.TryLocalSetVariant(varId, value);
        }
        static void TrySet(IBlackboardService blackboard, int key, DynamicVariant value)
        {
            blackboard.TryLocalSetVariant(key, value);
        }


        static IBlackboardService? ResolveBlackboard(IScopeNode scope)
        {
            if (scope?.Resolver == null)
                return null;

            return scope.Resolver.TryResolve<IBlackboardService>(out var bb) ? bb : null;
        }

        static ICommandRunner? ResolveRunner(IScopeNode scope)
        {
            if (scope?.Resolver == null)
                return null;

            return scope.Resolver.TryResolve<ICommandRunner>(out var runner) ? runner : null;
        }
    }
}
