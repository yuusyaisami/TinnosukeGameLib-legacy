#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Health;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class HealthApplyDamageExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HealthApplyDamage;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HealthApplyDamageCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HealthApplyDamageCommandData is required.");

            var health = await HealthCommandExecutorUtility.ResolveHealthServiceAsync(typed.Target, ctx, ct);
            if (health == null)
                return;

            var amount = typed.Amount.Resolve(ctx);
            var source = typed.UseCommandVarsAsSource ? (ctx.Vars ?? NullVarStore.Instance) : new VarStore();

            var damageContext = DamageContext.Create(amount, typed.DamageType, source);
            damageContext.IsCritical = typed.IsCritical;
            damageContext.Tag = typed.Tag;
            damageContext.ExtraPayload = typed.UseCommandVarsAsExtraPayload ? (ctx.Vars ?? NullVarStore.Instance) : null;
            health.ApplyDamage(ref damageContext);
        }
    }

    public sealed class HealthApplyHealExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HealthApplyHeal;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HealthApplyHealCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HealthApplyHealCommandData is required.");

            var health = await HealthCommandExecutorUtility.ResolveHealthServiceAsync(typed.Target, ctx, ct);
            if (health == null)
                return;

            var amount = typed.Amount.Resolve(ctx);
            var source = typed.UseCommandVarsAsSource ? (ctx.Vars ?? NullVarStore.Instance) : new VarStore();

            var healContext = HealContext.Create(amount, typed.HealType, source);
            healContext.Tag = typed.Tag;
            healContext.ExtraPayload = typed.UseCommandVarsAsExtraPayload ? (ctx.Vars ?? NullVarStore.Instance) : null;
            health.ApplyHeal(ref healContext);
        }
    }

    public sealed class HealthControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HealthControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HealthControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HealthControlCommandData is required.");

            if (typed.Action == HealthControlAction.MutateEventCommands)
            {
                await ApplyEventCommandMutationAsync(typed, ctx, ct);
                return;
            }

            var health = await HealthCommandExecutorUtility.ResolveHealthServiceAsync(typed.Target, ctx, ct);
            if (health == null)
                return;

            switch (typed.Action)
            {
                case HealthControlAction.Kill:
                    health.Kill();
                    break;
                case HealthControlAction.Revive:
                    health.Revive(typed.ReviveHPRatio.Resolve(ctx));
                    break;
                case HealthControlAction.SetHP:
                    health.SetHP(typed.HPValue.Resolve(ctx));
                    break;
                case HealthControlAction.SetMaxHP:
                    health.SetMaxHP(typed.MaxHPValue.Resolve(ctx));
                    break;
                case HealthControlAction.SetInvincibleLayer:
                    if (!string.IsNullOrEmpty(typed.InvincibleLayerKey))
                        health.InvincibleLayer.Set(typed.InvincibleLayerKey, typed.InvincibleValue.Resolve(ctx));
                    break;
                case HealthControlAction.RemoveInvincibleLayer:
                    if (!string.IsNullOrEmpty(typed.InvincibleLayerKey))
                        health.InvincibleLayer.Remove(typed.InvincibleLayerKey);
                    break;
                case HealthControlAction.RegisterModifier:
                    if (typed.Modifier != null)
                        health.RegisterModifier(typed.Modifier);
                    break;
                case HealthControlAction.UnregisterModifier:
                    if (!string.IsNullOrEmpty(typed.ModifierId))
                        health.UnregisterModifier(typed.ModifierId);
                    break;
            }
        }

        static async UniTask ApplyEventCommandMutationAsync(HealthControlCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, _) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            var resolvedScope = targetScope ?? ctx.Scope;

            EnsureScopeBuiltIfNeeded(resolvedScope);

            if (!TryResolve(resolvedScope, out HealthEventCommandSettings? settings) || settings == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "HealthEventCommandSettings is missing on target scope.");

            TryResolve(resolvedScope, out ICommandListRuntimeMutationService? mutationService);
            ApplyHealthEventCommandProgram(settings, typed.EventCommandProgram, mutationService);
        }

        static void ApplyHealthEventCommandProgram(
            HealthEventCommandSettings settings,
            HealthEventCommandMutationProgram? program,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (program?.Steps == null || program.Steps.Count == 0)
                return;

            for (int i = 0; i < program.Steps.Count; i++)
            {
                var step = program.Steps[i];
                if (step.Targets == HealthEventCommandTargets.None)
                    continue;

                ApplyToTarget(settings, step, HealthEventCommandTargets.OnDamaged, mutationService);
                ApplyToTarget(settings, step, HealthEventCommandTargets.OnHealed, mutationService);
                ApplyToTarget(settings, step, HealthEventCommandTargets.OnDied, mutationService);
                ApplyToTarget(settings, step, HealthEventCommandTargets.OnRevived, mutationService);
                ApplyToTarget(settings, step, HealthEventCommandTargets.OnInvincibleStarted, mutationService);
                ApplyToTarget(settings, step, HealthEventCommandTargets.OnInvincibleEnded, mutationService);
            }
        }

        static void ApplyToTarget(
            HealthEventCommandSettings settings,
            HealthEventCommandMutationStep step,
            HealthEventCommandTargets target,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (!HasTarget(step.Targets, target))
                return;

            var bindings = GetOrCreateBindings(settings, target);

            switch (step.BindingSelect)
            {
                case HealthEventCommandBindingSelectMode.All:
                    if (bindings.Count == 0)
                    {
                        if (!TryCreateBindingIfAllowed(bindings, step))
                            return;
                    }

                    for (int i = 0; i < bindings.Count; i++)
                    {
                        ApplyMutation(bindings[i], step, mutationService);
                    }
                    break;

                case HealthEventCommandBindingSelectMode.First:
                {
                    if (bindings.Count == 0 && !TryCreateBindingIfAllowed(bindings, step))
                        return;

                    ApplyMutation(bindings[0], step, mutationService);
                    break;
                }

                case HealthEventCommandBindingSelectMode.Index:
                {
                    var index = step.BindingIndex;
                    if (index < 0)
                        index = 0;

                    if (index >= bindings.Count)
                    {
                        if (!step.CreateBindingIfMissing || !step.Mutation.RequiresCommands())
                            return;

                        while (bindings.Count <= index)
                            bindings.Add(new HealthEventCommandBinding());
                    }

                    ApplyMutation(bindings[index], step, mutationService);
                    break;
                }
            }
        }

        static bool TryCreateBindingIfAllowed(List<HealthEventCommandBinding> bindings, HealthEventCommandMutationStep step)
        {
            if (!step.CreateBindingIfMissing || !step.Mutation.RequiresCommands())
                return false;

            bindings.Add(new HealthEventCommandBinding());
            return true;
        }

        static void ApplyMutation(
            HealthEventCommandBinding binding,
            HealthEventCommandMutationStep step,
            ICommandListRuntimeMutationService? mutationService)
        {
            CommandListRuntimeMutationPipeline.Apply(binding.Commands, step.Mutation, mutationService);
        }

        static bool HasTarget(HealthEventCommandTargets value, HealthEventCommandTargets target)
        {
            return (value & target) != 0;
        }

        static List<HealthEventCommandBinding> GetOrCreateBindings(
            HealthEventCommandSettings settings,
            HealthEventCommandTargets target)
        {
            switch (target)
            {
                case HealthEventCommandTargets.OnDamaged:
                    return settings.OnDamaged;
                case HealthEventCommandTargets.OnHealed:
                    return settings.OnHealed;
                case HealthEventCommandTargets.OnDied:
                    return settings.OnDied;
                case HealthEventCommandTargets.OnRevived:
                    return settings.OnRevived;
                case HealthEventCommandTargets.OnInvincibleStarted:
                    return settings.OnInvincibleStarted;
                case HealthEventCommandTargets.OnInvincibleEnded:
                    return settings.OnInvincibleEnded;
                default:
                    return settings.OnDamaged;
            }
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

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }
    }

    static class HealthCommandExecutorUtility
    {
        public static async UniTask<IHealthService?> ResolveHealthServiceAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            var resolvedScope = scope ?? ctx.Scope;
            if (resolvedScope?.Resolver == null)
                return null;

            return resolvedScope.Resolver.TryResolve<IHealthService>(out var health) ? health : null;
        }
    }
}
