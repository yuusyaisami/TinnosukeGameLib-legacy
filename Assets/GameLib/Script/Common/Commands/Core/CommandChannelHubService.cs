#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands
{
    [Serializable]
    public sealed class CommandChannelEntry
    {
        [TableColumnWidth(180, Resizable = false)]
        public string Tag = string.Empty;

        [TableColumnWidth(720, Resizable = true)]
        public CommandListData Commands = new();
    }

    public interface ICommandChannelHubSettings
    {
        CommandChannelEntry[] Entries { get; }
    }

    public interface ICommandChannelHubService
    {
        bool TryGetCommands(string tag, out CommandListData commands);
        bool RegisterOrUpdate(string tag, CommandListData commands);
        bool Unregister(string tag);
        void Clear();
        bool MutateCommands(string tag, CommandListMutationStep mutation, ICommandListRuntimeMutationService? mutationService);
        bool SetPayload(string tag, VarStorePayload payload, bool overwriteExistingVars);
        bool ClearPayload(string tag);
        UniTask<CommandRunResult> ExecuteAsync(string tag, CommandContext ctx, CancellationToken ct);
    }

    /// <summary>
    /// Tag -> CommandListData のHub。
    /// </summary>
    public sealed class CommandChannelHubService :
        ICommandChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        sealed class RuntimeEntry
        {
            public CommandListData Commands = new();
            public VarStorePayload? Payload;
            public bool PayloadOverwriteExistingVars;
        }

        readonly ICommandChannelHubSettings _settings;
        readonly Dictionary<string, RuntimeEntry> _commandLookup = new(StringComparer.Ordinal);

        public CommandChannelHubService(ICommandChannelHubSettings settings)
        {
            _settings = settings;
        }

        public bool TryGetCommands(string tag, out CommandListData commands)
        {
            commands = null!;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!_commandLookup.TryGetValue(tag.Trim(), out var entry) || entry == null || entry.Commands == null)
                return false;

            commands = entry.Commands;
            return true;
        }

        public bool RegisterOrUpdate(string tag, CommandListData commands)
        {
            if (string.IsNullOrWhiteSpace(tag) || commands == null)
                return false;

            var key = tag.Trim();
            if (_commandLookup.TryGetValue(key, out var existing) && existing != null)
            {
                existing.Commands = commands;
                return true;
            }

            _commandLookup[key] = new RuntimeEntry
            {
                Commands = commands,
                Payload = null,
                PayloadOverwriteExistingVars = false,
            };
            return true;
        }

        public bool Unregister(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return _commandLookup.Remove(tag.Trim());
        }

        public void Clear()
        {
            _commandLookup.Clear();
        }

        public bool MutateCommands(string tag, CommandListMutationStep mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return false;

            if (!TryGetRuntimeEntry(tag, out var entry) || entry?.Commands == null)
                return false;

            return entry.Commands.ApplyRuntimeMutation(mutation, mutationService);
        }

        public bool SetPayload(string tag, VarStorePayload payload, bool overwriteExistingVars)
        {
            if (payload == null)
                return false;

            if (!TryGetRuntimeEntry(tag, out var entry) || entry == null)
                return false;

            entry.Payload = payload;
            entry.PayloadOverwriteExistingVars = overwriteExistingVars;
            return true;
        }

        public bool ClearPayload(string tag)
        {
            if (!TryGetRuntimeEntry(tag, out var entry) || entry == null)
                return false;

            entry.Payload = null;
            entry.PayloadOverwriteExistingVars = false;
            return true;
        }

        public async UniTask<CommandRunResult> ExecuteAsync(string tag, CommandContext ctx, CancellationToken ct)
        {
            if (ctx == null)
                return CommandRunResult.Error(0, 0, CommandRunFailureKind.InvalidArgs, "CommandContext is null.", null, null);

            if (!TryGetRuntimeEntry(tag, out var entry) || entry == null || entry.Commands == null)
                return CommandRunResult.Error(0, 0, CommandRunFailureKind.ResolveFailed, "CommandChannel tag not found.", null, null);

            var runContext = BuildExecutionContext(ctx, entry);
            return await runContext.Runner.ExecuteListAsync(entry.Commands, runContext, ct, runContext.Options);
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            BuildCommandLookup();
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            Clear();
        }

        bool TryGetRuntimeEntry(string tag, out RuntimeEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return _commandLookup.TryGetValue(tag.Trim(), out entry) && entry != null;
        }

        static CommandContext BuildExecutionContext(CommandContext sourceContext, RuntimeEntry entry)
        {
            if (entry == null || entry.Payload == null)
                return sourceContext;

            var mergedVars = new VarStore();
            (sourceContext.Vars ?? NullVarStore.Instance).MergeInto(mergedVars, overwrite: true);
            entry.Payload.ApplyTo(mergedVars, entry.PayloadOverwriteExistingVars);

            return new CommandContext(
                sourceContext.Scope,
                mergedVars,
                sourceContext.Runner,
                sourceContext.Actor,
                sourceContext.Options,
                sourceContext.CommandRootScope,
                sourceContext.RootActor,
                sourceContext.CallerActor,
                sourceContext);
        }

        void BuildCommandLookup()
        {
            _commandLookup.Clear();

            var entries = _settings?.Entries;
            if (entries == null || entries.Length == 0)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var tag = entry.Tag;
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                _commandLookup[tag.Trim()] = new RuntimeEntry
                {
                    Commands = entry.Commands,
                    Payload = null,
                    PayloadOverwriteExistingVars = false,
                };
            }
        }
    }
}
