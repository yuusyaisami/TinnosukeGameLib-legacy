#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using Game.StateMachine;

namespace Game.Commands.VNext
{
    public sealed class StateMachineExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.StateMachine;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not StateMachineCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "StateMachineCommandData is required.");

            if (!ctx.Resolver.TryResolve<IStateMachine>(out var sm) || sm == null)
                return UniTask.CompletedTask;

            switch (typed.Action)
            {
                case StateMachineAction.SetState:
                    if (!string.IsNullOrEmpty(typed.StateKey))
                        sm.SetState(typed.StateKey, typed.Tag ?? string.Empty, typed.OwnerId ?? string.Empty);
                    break;
                case StateMachineAction.ReleaseState:
                    var tag = typed.Tag ?? string.Empty;
                    if (!string.IsNullOrEmpty(tag))
                    {
                        if (!string.IsNullOrEmpty(typed.StateKey))
                            sm.ReleaseState(typed.StateKey, tag);
                        else
                            sm.ReleaseStatesByTag(tag);
                    }
                    break;
                case StateMachineAction.FirePulse:
                    if (!string.IsNullOrEmpty(typed.StateKey))
                    {
                        if (!string.IsNullOrEmpty(typed.RequiredTag))
                            sm.FirePulse(typed.StateKey, typed.RequiredTag);
                        else
                            sm.FirePulse(typed.StateKey);
                    }
                    break;
                case StateMachineAction.SetGlobalOption:
                    if (!string.IsNullOrEmpty(typed.OptionValue))
                    {
                        var optionKey = StateKeyUtils.GetOptionKey(typed.OptionValue);
                        if (!string.IsNullOrEmpty(optionKey))
                            sm.SetGlobalOption(optionKey, typed.OptionValue);
                    }
                    break;
                case StateMachineAction.SetLocalOption:
                    if (!string.IsNullOrEmpty(typed.OptionValue))
                    {
                        var optionKey = StateKeyUtils.GetOptionKey(typed.OptionValue);
                        var layerKey = ResolveLocalLayerKey(sm, typed.LocalOptionLayerKey);
                        if (!string.IsNullOrEmpty(optionKey) && !string.IsNullOrEmpty(layerKey))
                            sm.SetLocalOption(layerKey, optionKey, typed.OptionValue);
                    }
                    break;
                case StateMachineAction.ReleaseGlobalOption:
                    if (!string.IsNullOrEmpty(typed.OptionKey))
                        sm.SetGlobalOption(typed.OptionKey, string.Empty);
                    break;
                case StateMachineAction.ReleaseLocalOption:
                    {
                        var layerKey = ResolveLocalLayerKey(sm, typed.LocalOptionLayerKey);
                        if (!string.IsNullOrEmpty(typed.OptionKey) && !string.IsNullOrEmpty(layerKey))
                            sm.SetLocalOption(layerKey, typed.OptionKey, string.Empty);
                    }
                    break;
            }

            return UniTask.CompletedTask;
        }

        static string ResolveLocalLayerKey(IStateMachine sm, string? layerKey)
        {
            if (!string.IsNullOrEmpty(layerKey))
                return layerKey;

            return sm.CurrentLayer;
        }
    }
}
