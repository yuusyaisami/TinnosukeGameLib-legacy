#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class CommandExecutorRegistry
    {
        readonly Dictionary<int, ICommandExecutor> _map = new();

        public CommandExecutorRegistry(IReadOnlyList<ICommandExecutor> executors)
        {
            if (executors == null)
                return;

            for (int i = 0; i < executors.Count; i++)
            {
                var executor = executors[i];
                if (executor == null)
                    continue;

                var commandId = executor.CommandId;
                if (commandId <= 0)
                {
                    Debug.LogError($"[CommandExecutorRegistry] Invalid CommandId on executor: {executor.GetType().Name}");
                    continue;
                }

                if (_map.TryGetValue(commandId, out var existing))
                {
                    if (existing != null && existing.GetType() == executor.GetType())
                        continue;

                    Debug.LogError($"[CommandExecutorRegistry] Duplicate CommandId {commandId} on executor: {executor.GetType().Name}");
                    continue;
                }

                _map.Add(commandId, executor);
            }
        }

        public bool TryGet(int commandId, out ICommandExecutor executor)
        {
            return _map.TryGetValue(commandId, out executor!);
        }
    }
}
