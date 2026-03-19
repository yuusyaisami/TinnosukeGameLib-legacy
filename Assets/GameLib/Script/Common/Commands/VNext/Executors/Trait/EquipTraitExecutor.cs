#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class EquipTraitExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.EquipTrait;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not EquipTraitCommandData cmd)
                return;

            // EquipTraitHolderHubService を解決
            var hub = ResolveHub(cmd, ctx);
            if (hub == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning("[EquipTraitExecutor] IEquipTraitHolderHubService could not be resolved.");
#endif
                return;
            }

            if (string.IsNullOrWhiteSpace(cmd.SlotKey))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning("[EquipTraitExecutor] SlotKey is null or empty.");
#endif
                return;
            }

            if (!hub.TryGetSlot(cmd.SlotKey, out var slot) || slot == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[EquipTraitExecutor] Slot '{cmd.SlotKey}' not found.");
#endif
                return;
            }

            switch (cmd.Op)
            {
                case EquipTraitOp.Equip:
                    await ExecuteEquip(cmd, ctx, hub, slot, ct);
                    break;

                case EquipTraitOp.Unequip:
                    await slot.UnequipAsync(ct);
                    break;
            }
        }

        async UniTask ExecuteEquip(
            EquipTraitCommandData cmd,
            CommandContext ctx,
            IEquipTraitHolderHubService hub,
            EquipTraitSlotRuntime slot,
            CancellationToken ct)
        {
            // Hub を EquipTraitHolderHubService にキャストして Trait 解決
            if (hub is not EquipTraitHolderHubService hubService)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning("[EquipTraitExecutor] Hub is not EquipTraitHolderHubService.");
#endif
                return;
            }

            // TargetKind に応じて Trait を解決
            ITraitDefinition? targetDef = null;
            if (cmd.TargetKind == EquipTraitTargetKind.ByDefinition)
            {
                if (!cmd.DefinitionSource.TryResolve(ctx.Vars, out var defSO) || defSO == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.LogWarning("[EquipTraitExecutor] TraitDefinition could not be resolved for ByDefinition target.");
#endif
                    return;
                }

                targetDef = defSO;
            }

            int targetIndex = 0;
            if (cmd.TargetKind == EquipTraitTargetKind.ByIndex)
            {
                var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
                if (cmd.TargetIndex.TryGet(dynCtx, out var idx))
                    targetIndex = idx;
            }

            if (!hubService.TryResolveTraitFromHolder(
                    cmd.SlotKey,
                    cmd.TargetKind,
                    targetDef,
                    cmd.TargetDefinitionId,
                    targetIndex,
                    out var instance) || instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[EquipTraitExecutor] Could not resolve trait for slot '{cmd.SlotKey}' with target {cmd.TargetKind}.");
#endif
                return;
            }

            VarStorePayload? payload = (cmd.ApplyPayload && cmd.Payload != null) ? cmd.Payload : null;
            await slot.EquipAsync(instance, cmd.AwaitUnequip, payload, ct);
        }

        static IEquipTraitHolderHubService? ResolveHub(EquipTraitCommandData cmd, CommandContext ctx)
        {
            IScopeNode? scope;

            if (cmd.UseSelfScope)
            {
                scope = ctx.Scope;
            }
            else
            {
                scope = ActorSourceFastResolver.Resolve(ctx, cmd.HubActorSource);
            }

            if (scope?.Resolver == null) return null;

            if (scope.Resolver.TryResolve<IEquipTraitHolderHubService>(out var hub) && hub != null)
                return hub;

            return null;
        }
    }
}
