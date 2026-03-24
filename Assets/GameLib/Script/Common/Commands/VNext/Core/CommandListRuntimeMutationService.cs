#nullable enable
using System.Collections.Generic;
using Game;

namespace Game.Commands.VNext
{
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
