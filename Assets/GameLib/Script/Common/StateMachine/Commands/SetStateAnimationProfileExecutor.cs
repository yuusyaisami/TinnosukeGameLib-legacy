#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.StateMachine;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetStateAnimationProfileExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.StateAnimationSetProfile;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetStateAnimationProfileCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetStateAnimationProfileCommandData is required.");

            typed.EnsurePresetMigrated();

            if (!typed.Preset.TryGet(ctx, out var profile) || profile == null)
            {
                Debug.LogWarning("[SetStateAnimationProfileExecutor] Preset could not be resolved.");
                return UniTask.CompletedTask;
            }

            var owner = ctx.Actor ?? ctx.Scope;
            if (owner.Resolver.TryResolve(out StateAnimationController controller) && controller != null)
            {
                var restart = typed.RestartImmediately.GetOrDefault(ctx, true);
                controller.SetProfile(profile, restart);
            }
            else
            {
                Debug.LogWarning("[SetStateAnimationProfileExecutor] No StateAnimationController found on scope.");
            }

            return UniTask.CompletedTask;
        }
    }
}
