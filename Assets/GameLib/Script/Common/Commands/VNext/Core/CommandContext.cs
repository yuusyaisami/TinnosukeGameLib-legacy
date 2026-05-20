#nullable enable
using Game;
using Game.Commands;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandContext : IDynamicContext, IDynamicDependencyTokenSource, IDynamicEvaluationOriginProvider
    {
        readonly IScopeNode?[] _ltsSlots;
        readonly CommandLocal _local;
        readonly CommandFrameSnapshot _currentFrame;

        public IScopeNode Scope { get; }
        public IRuntimeResolver Resolver => Scope.Resolver!;
        public IVarStore Vars { get; }
        public ICommandRunner Runner { get; }
        public IScopeNode? Actor => GetScope(CommandLtsSlot.Actor) ?? Scope;
        public IScopeNode? CommandRootScope => GetScope(CommandLtsSlot.CommandRoot) ?? Scope;
        public IScopeNode? RootActor => GetScope(CommandLtsSlot.RootActor) ?? Actor;
        public IScopeNode? CallerActor => GetScope(CommandLtsSlot.CallerActor) ?? Actor;
        public CommandRunOptions Options { get; }
        public CommandLocal Local => _local;
        public CommandFrameSnapshot CurrentFrame => _currentFrame;

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
            : this(scope, vars, runner, actor, options, commandRootScope, rootActor, callerActor, sourceContext, local: null, currentFrame: default)
        {
        }

        internal CommandContext(
            IScopeNode scope,
            IVarStore vars,
            ICommandRunner runner,
            IScopeNode? actor,
            CommandRunOptions options,
            IScopeNode? commandRootScope,
            IScopeNode? rootActor,
            IScopeNode? callerActor,
            CommandContext? sourceContext,
            CommandLocal? local,
            CommandFrameSnapshot currentFrame)
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
                _local = local ?? sourceContext._local;
                _currentFrame = currentFrame.FrameId.IsValid ? currentFrame : sourceContext._currentFrame;
            }
            else
            {
                _local = local ?? new CommandLocal(CommandLocalScope.Sequence, default);
                _currentFrame = currentFrame;
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

        internal CommandContext CreateExecutionContext()
        {
            var rootLocal = new CommandLocal(CommandLocalScope.Sequence, default, _local);
            return new CommandContext(Scope, Vars, Runner, Actor, Options, CommandRootScope, RootActor, CallerActor, this, rootLocal, _currentFrame);
        }

        internal CommandContext AttachFrame(CommandFrame frame, CommandFailureBoundary failureBoundary, bool isDetached, bool isTimedOut)
        {
            if (frame == null)
                throw new System.ArgumentNullException(nameof(frame));

            var snapshot = frame.ToSnapshot(failureBoundary, isDetached, isTimedOut);
            return new CommandContext(Scope, Vars, Runner, Actor, Options, CommandRootScope, RootActor, CallerActor, this, frame.Local, snapshot);
        }

        internal void RequestBreak()
        {
            _local.RequestBreak();
        }

        internal void RequestCancel()
        {
            _local.RequestCancel();
        }

        internal bool TryConsumeBreakRequest()
        {
            return _local.TryConsumeBreakRequest();
        }

        internal bool TryConsumeCancelRequest()
        {
            return _local.TryConsumeCancelRequest();
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

            return _local.TryEnterChannelExecution(key, out chain);
        }

        internal void ExitChannelExecution(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _local.ExitChannelExecution(key);
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

        public DynamicDependencyTokenSet GetDynamicDependencyTokens()
        {
            return new DynamicDependencyTokenSet(
                runtimeQueryVersion: 0,
                scopeVersion: 0,
                commandVersion: _currentFrame.FrameId.Value,
                extraVersion: 0);
        }

        public DynamicEvaluationOrigin GetDynamicEvaluationOrigin()
        {
            return DynamicEvaluationOrigin.FromScopeNodes(Scope, CommandRootScope, _currentFrame.FrameId.Value);
        }
    }
}
