#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game;
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
        readonly ICommandChannelHubSettings _settings;
        readonly Dictionary<string, CommandListData> _commandLookup = new(StringComparer.Ordinal);

        public CommandChannelHubService(ICommandChannelHubSettings settings)
        {
            _settings = settings;
        }

        public bool TryGetCommands(string tag, out CommandListData commands)
        {
            commands = null!;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return _commandLookup.TryGetValue(tag.Trim(), out commands) && commands != null;
        }

        public async UniTask<CommandRunResult> ExecuteAsync(string tag, CommandContext ctx, CancellationToken ct)
        {
            if (ctx == null)
                return CommandRunResult.Error(0, 0, CommandRunFailureKind.InvalidArgs, "CommandContext is null.", null, null);

            if (!TryGetCommands(tag, out var commands) || commands == null)
                return CommandRunResult.Error(0, 0, CommandRunFailureKind.ResolveFailed, "CommandChannel tag not found.", null, null);

            return await ctx.Runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
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
            _commandLookup.Clear();
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

                _commandLookup[tag.Trim()] = entry.Commands;
            }
        }
    }
}
