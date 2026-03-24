#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WriteTraitDataExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WriteTraitData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WriteTraitDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteTraitDataCommandData is required.");

            var origin = ctx.Actor ?? ctx.Scope;
            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource, origin) ?? origin;
            if (!TryResolveBlackboard(targetScope, out var blackboard) || blackboard == null)
                return UniTask.CompletedTask;

            TryResolveGridBlackboard(targetScope, out var grid, out var resolvedGridScope);
            if (grid == null)
            {
                Debug.LogWarning($"[WriteTraitDataExecutor] IGridBlackboardService not found for target scope. CommonGridTable will be skipped if enabled. target={DescribeScope(targetScope)}");
            }
            else if (!ReferenceEquals(targetScope, resolvedGridScope))
            {
                Debug.LogWarning($"[WriteTraitDataExecutor] IGridBlackboardService resolved from parent scope. target={DescribeScope(targetScope)} resolved={DescribeScope(resolvedGridScope)}");
            }

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            switch (typed.SourceMode)
            {
                case WriteTraitDataSourceMode.DirectDefinition:
                    WriteDirectDefinition(typed, dynCtx, targetScope, blackboard.LocalVars, grid, typed.Overwrite);
                    break;

                case WriteTraitDataSourceMode.HolderSelector:
                    WriteFromHolderSelector(typed, ctx, dynCtx, blackboard.LocalVars, grid, typed.Overwrite);
                    break;

                case WriteTraitDataSourceMode.VarStoreTraitData:
                    CopyTraitDataVars(ctx.Vars, blackboard.LocalVars, typed.Overwrite);
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void WriteDirectDefinition(
            WriteTraitDataCommandData typed,
            IDynamicContext dynCtx,
            IScopeNode targetScope,
            IVarStore destination,
            IGridBlackboardService? grid,
            bool overwrite)
        {
            if (!typed.TraitSource.TryGet(dynCtx, out var trait) || trait == null)
                return;

            var traitContext = new TraitInstanceContext(targetScope);
            trait.CreateInstance(traitContext);
            WriteTraitDataToStores(trait, traitContext.Vars, destination, grid, overwrite);
        }

        static void WriteFromHolderSelector(
            WriteTraitDataCommandData typed,
            CommandContext ctx,
            IDynamicContext dynCtx,
            IVarStore destination,
            IGridBlackboardService? grid,
            bool overwrite)
        {
            var hubScope = ActorSourceFastResolver.Resolve(ctx, typed.HubActorSource);
            if (hubScope?.Resolver == null)
                return;

            if (!hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var holderHub) || holderHub == null)
                return;

            if (string.IsNullOrWhiteSpace(typed.HolderKey))
                return;

            if (!holderHub.TryGetHolder(typed.HolderKey, out var holder) || holder == null)
                return;

            if (!typed.Selector.TryResolve(holder, dynCtx, out var traitInstance, out _) || traitInstance == null)
                return;

            var definition = traitInstance.Definition as TraitDefinitionSO;
            WriteTraitDataToStores(definition, traitInstance.Context?.Vars, destination, grid, overwrite);
        }

        static void WriteTraitDataToStores(
            TraitDefinitionSO? traitDefinition,
            IVarStore? source,
            IVarStore destination,
            IGridBlackboardService? grid,
            bool overwrite)
        {
            if (destination == null)
                return;

            traitDefinition?.ApplyCommonVars(destination, overwrite);
            traitDefinition?.ApplyCommonGridTable(grid, overwrite);
            CopyTraitDataVars(source, destination, overwrite);
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            if (resolver.TryResolve<IBlackboardService>(out var resolved) && resolved != null)
            {
                blackboard = resolved;
                return true;
            }

            return false;
        }

        static bool TryResolveGridBlackboard(IScopeNode? scope, out IGridBlackboardService? grid, out IScopeNode? resolvedScope)
        {
            grid = null;
            resolvedScope = null;
            for (var node = scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IGridBlackboardService>(out var resolved) && resolved != null)
                {
                    grid = resolved;
                    resolvedScope = node;
                    return true;
                }
            }

            return false;
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            if (scope.Identity != null)
                return $"{scope.Identity.Id}:{scope.Identity.Kind}";

            return scope.GetType().Name;
        }

        static void CopyTraitDataVars(IVarStore? source, IVarStore destination, bool overwrite)
        {
            if (source == null || destination == null)
                return;

            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.instanceId, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.definitionId, overwrite);
            CopyValue(source, destination, VarIds.GameLib.Base.Trait.Element.definitionAsset, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.weight, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.nameTemplate, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.descriptionTemplate, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.nameKey, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.descriptionKey, overwrite);
        }

        static void CopyVariant(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            if (!source.TryGetVariant(varId, out var value))
                return;

            destination.TrySetVariant(varId, value);
        }

        static void CopyManagedRef(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            if (!source.TryGetManagedRef(varId, out var value) || value == null)
                return;

            destination.TrySetManagedRef(varId, value);
        }

        static void CopyValue(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            var kind = source.GetVarKind(varId);
            if (kind == ValueKind.ManagedRef)
            {
                CopyManagedRef(source, destination, varId, overwrite: true);
                return;
            }

            CopyVariant(source, destination, varId, overwrite: true);
        }
    }
}
