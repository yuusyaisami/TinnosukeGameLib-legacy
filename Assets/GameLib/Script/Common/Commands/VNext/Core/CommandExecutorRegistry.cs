#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Commands.VNext
{
    public interface ICommandExecutorCatalog
    {
        bool TryGet(int commandId, out ICommandExecutor executor);
    }

    public sealed class CommandExecutorCatalog : ICommandExecutorCatalog
    {
        readonly Dictionary<int, ICommandExecutor> _map = new();

        public CommandExecutorCatalog(IReadOnlyList<ICommandExecutor> executors)
        {
            if (executors == null)
                throw new ArgumentNullException(nameof(executors));

            var errors = new StringBuilder();
            for (int i = 0; i < executors.Count; i++)
            {
                var executor = executors[i];
                if (executor == null)
                {
                    errors.AppendLine($"[{i}] Executor entry is null.");
                    continue;
                }

                var commandId = executor.CommandId;
                if (commandId <= 0)
                {
                    errors.AppendLine($"[{i}] Invalid CommandId on executor {executor.GetType().Name}: {commandId}");
                    continue;
                }

                if (_map.TryGetValue(commandId, out var existing))
                {
                    string existingType = existing == null ? "<null>" : existing.GetType().Name;
                    errors.AppendLine($"[{i}] Duplicate CommandId {commandId} on executor {executor.GetType().Name} (existing: {existingType}).");
                    continue;
                }

                _map.Add(commandId, executor);
            }

            if (errors.Length > 0)
            {
                string message = "Command executor catalog binding is invalid and cannot be used:\n" + errors.ToString().TrimEnd();
                Debug.LogError($"[CommandExecutorCatalog] {message}");
                throw new ArgumentException(message, nameof(executors));
            }
        }

        public bool TryGet(int commandId, out ICommandExecutor executor)
        {
            return _map.TryGetValue(commandId, out executor!);
        }
    }

    public sealed class CommandExecutorRegistry
    {
        readonly ICommandExecutorCatalog _catalog;

        public CommandExecutorRegistry(IReadOnlyList<ICommandExecutor> executors)
        {
            _catalog = new CommandExecutorCatalog(executors);
        }

        public bool TryGet(int commandId, out ICommandExecutor executor)
        {
            return _catalog.TryGet(commandId, out executor);
        }
    }
}
