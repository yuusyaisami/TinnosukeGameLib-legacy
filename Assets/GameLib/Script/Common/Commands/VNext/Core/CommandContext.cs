#nullable enable
using Game;
using Game.Commands;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandContext : IDynamicContext
    {
        public IScopeNode Scope { get; }
        public IObjectResolver Resolver => Scope.Resolver!;
        public IVarStore Vars { get; }
        public ICommandRunner Runner { get; }
        public IScopeNode? Actor { get; }
        public IScopeNode? CommandRootScope { get; }
        public IScopeNode? RootActor { get; }
        public IScopeNode? CallerActor { get; }
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
            IScopeNode? callerActor = null)
        {
            Scope = scope ?? throw new System.ArgumentNullException(nameof(scope));
            if (Scope.Resolver == null)
                throw new System.ArgumentException($"{nameof(CommandContext)} requires a non-null Resolver on {nameof(IScopeNode)}.", nameof(scope));

            Vars = vars ?? NullVarStore.Instance;
            Runner = runner;
            Actor = actor ?? scope;
            CommandRootScope = commandRootScope ?? Scope;
            RootActor = rootActor ?? Actor;
            CallerActor = callerActor ?? Actor;
            Options = options;
        }

        public CommandContext WithOptions(CommandRunOptions options)
        {
            return new CommandContext(Scope, Vars, Runner, Actor, options, CommandRootScope, RootActor, CallerActor);
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
