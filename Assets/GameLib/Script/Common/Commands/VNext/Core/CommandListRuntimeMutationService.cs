#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum CommandListMutationOperation
    {
        Append = 0,
        Override = 1,
        ClearAppended = 2,
        ClearOverride = 3,
        ClearAll = 4,
    }

    [Serializable]
    public sealed class CommandListMutationStep
    {
        [LabelText("Operation")]
        public CommandListMutationOperation Operation = CommandListMutationOperation.Append;

        [ShowIf(nameof(RequiresCommands))]
        [LabelText("Commands")]
        [CommandListFunctionName("CommandList.Mutation.Commands")]
        public CommandListData Commands = new();

        public bool RequiresCommands()
            => Operation == CommandListMutationOperation.Append
            || Operation == CommandListMutationOperation.Override;
    }

    public static class CommandListRuntimeMutationPipeline
    {
        public static bool Apply(CommandListData? target, CommandListMutationStep? step, ICommandListRuntimeMutationService? mutationService)
        {
            if (target == null || step == null)
                return false;

            mutationService?.Register(target);
            switch (step.Operation)
            {
                case CommandListMutationOperation.Append:
                    target.AddRuntimeCommands(step.Commands);
                    return true;
                case CommandListMutationOperation.Override:
                    target.SetRuntimeOverride(step.Commands);
                    return true;
                case CommandListMutationOperation.ClearAppended:
                    target.ClearRuntimeAppendedCommands();
                    return true;
                case CommandListMutationOperation.ClearOverride:
                    target.ClearRuntimeOverride();
                    return true;
                case CommandListMutationOperation.ClearAll:
                    target.ClearRuntimeMutations();
                    return true;
                default:
                    return false;
            }
        }
    }

    public interface ICommandListRuntimeMutationService
    {
        void Register(CommandListData? list);
    }

    public sealed class CommandListRuntimeMutationService :
        ICommandListRuntimeMutationService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly HashSet<CommandListData> _mutatedLists = new();

        public void Register(CommandListData? list)
        {
            if (list == null)
                return;

            _mutatedLists.Add(list);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            ClearAll();
        }

        void ClearAll()
        {
            if (_mutatedLists.Count == 0)
                return;

            foreach (var list in _mutatedLists)
            {
                list?.ClearRuntimeMutations();
            }
            _mutatedLists.Clear();
        }
    }
}
