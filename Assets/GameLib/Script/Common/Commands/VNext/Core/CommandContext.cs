#nullable enable
using System.Collections.Generic;
using Game;
using Game.Commands;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandContext : IDynamicContext
    {
        readonly IScopeNode?[] _ltsSlots;
        readonly object _channelExecutionGate;
        readonly List<string> _channelExecutionStack;

        public IScopeNode Scope { get; }
        public IObjectResolver Resolver => Scope.Resolver!;
        public IVarStore Vars { get; }
        public ICommandRunner Runner { get; }
        public IScopeNode? Actor => GetScope(CommandLtsSlot.Actor) ?? Scope;
        public IScopeNode? CommandRootScope => GetScope(CommandLtsSlot.CommandRoot) ?? Scope;
        public IScopeNode? RootActor => GetScope(CommandLtsSlot.RootActor) ?? Actor;
        public IScopeNode? CallerActor => GetScope(CommandLtsSlot.CallerActor) ?? Actor;
        public CommandRunOptions Options { get; }

        public CommandContext(IScopeNode scope, IVarStore vars, ICommandRunner runner)
            : this(scope, vars, runner, actor: scope, options: CommandRunOptions.Default, commandRootScope: scope, rootActor: scope, callerActor: scope)
        {
        }

        public CommandContext(IScopeNode scope, IVarStore vars, ICommandRunner runner, IScopeNode? actor, CommandRunOptions options)
            : this(scope, vars, runner, actor, options, commandRootScope: scope, rootActor: actor ?? scope, callerActor: actor ?? scope)
        {
        }

        public CommandContext(
            IScopeNode scope,
            IVarStore vars,
            ICommandRunner runner,
            IScopeNode? actor,
            CommandRunOptions options,
            IScopeNode? commandRootScope,
            IScopeNode? rootActor,
            IScopeNode? callerActor = null,
            CommandContext? sourceContext = null)
        {
            Scope = scope ?? throw new System.ArgumentNullException(nameof(scope));
            if (Scope.Resolver == null)
                throw new System.ArgumentException($"{nameof(CommandContext)} requires a non-null Resolver on {nameof(IScopeNode)}.", nameof(scope));

            Vars = vars ?? NullVarStore.Instance;
            Runner = runner;
            Options = options;

            _ltsSlots = new IScopeNode?[CommandLtsSlotUtility.SlotCount];
            if (sourceContext != null)
            {
                System.Array.Copy(sourceContext._ltsSlots, _ltsSlots, _ltsSlots.Length);
                _channelExecutionGate = sourceContext._channelExecutionGate;
                _channelExecutionStack = sourceContext._channelExecutionStack;
            }
            else
            {
                _channelExecutionGate = new object();
                _channelExecutionStack = new List<string>(4);
            }

            SetStoredScope(CommandLtsSlot.Actor, actor ?? scope);
            SetStoredScope(CommandLtsSlot.CommandRoot, commandRootScope ?? Scope);
            SetStoredScope(CommandLtsSlot.RootActor, rootActor ?? Actor);
            SetStoredScope(CommandLtsSlot.CallerActor, callerActor ?? Actor);
        }

        public CommandContext WithOptions(CommandRunOptions options)
        {
            return new CommandContext(Scope, Vars, Runner, Actor, options, CommandRootScope, RootActor, CallerActor, this);
        }

        public IScopeNode? GetScope(CommandLtsSlot slot)
        {
            if (slot == CommandLtsSlot.Scope)
                return Scope;

            var index = CommandLtsSlotUtility.ToStorageIndex(slot);
            if ((uint)index >= _ltsSlots.Length)
                return null;

            return _ltsSlots[index];
        }

        public void SetScope(CommandLtsSlot slot, IScopeNode? scope)
        {
            if (slot == CommandLtsSlot.Scope || slot == CommandLtsSlot.None)
                return;

            SetStoredScope(slot, scope);
        }

        void SetStoredScope(CommandLtsSlot slot, IScopeNode? scope)
        {
            var index = CommandLtsSlotUtility.ToStorageIndex(slot);
            if ((uint)index >= _ltsSlots.Length)
                return;

            _ltsSlots[index] = scope;
        }

        internal bool TryEnterChannelExecution(string key, out string chain)
        {
            chain = "<empty>";
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_channelExecutionGate)
            {
                if (_channelExecutionStack.Contains(key))
                {
                    chain = _channelExecutionStack.Count > 0
                        ? string.Join(" -> ", _channelExecutionStack)
                        : "<empty>";
                    return false;
                }

                _channelExecutionStack.Add(key);
                chain = string.Join(" -> ", _channelExecutionStack);
                return true;
            }
        }

        internal void ExitChannelExecution(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_channelExecutionGate)
            {
                for (var i = _channelExecutionStack.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(_channelExecutionStack[i], key, System.StringComparison.Ordinal))
                        continue;

                    _channelExecutionStack.RemoveAt(i);
                    break;
                }
            }
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            var origin = Scope;
            if (origin == null || origin.Resolver == null)
                return origin!;

            if (origin.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) && registry != null)
                return registry.Resolve(filter, origin);

            return origin;
        }
    }
}
