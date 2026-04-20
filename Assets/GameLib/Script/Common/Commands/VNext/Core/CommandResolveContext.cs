#nullable enable
using Game;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public readonly struct CommandResolveContext : IDynamicContext
    {
        public IScopeNode Scope { get; }
        public IVarStore Vars { get; }
        public IScopeNode? CommandRootScope { get; }
        public IRuntimeResolver? Resolver { get; }
        public ICommandCatalog Catalog { get; }
        public ICommandKeyResolver KeyResolver { get; }
        public ICommandResolveLogger Logger { get; }
        public bool AllowRuntimeKeyFallback { get; }
        public CommandContext? RuntimeContext { get; }

        public CommandResolveContext(
            IScopeNode scope,
            IVarStore vars,
            IScopeNode? commandRootScope,
            IRuntimeResolver? resolver,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            bool allowRuntimeKeyFallback,
            CommandContext? runtimeContext = null)
        {
            Scope = scope;
            Vars = vars ?? NullVarStore.Instance;
            CommandRootScope = commandRootScope;
            Resolver = resolver;
            Catalog = catalog;
            KeyResolver = keyResolver;
            Logger = logger;
            AllowRuntimeKeyFallback = allowRuntimeKeyFallback;
            RuntimeContext = runtimeContext;
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            if (Scope?.Resolver == null)
                return null!;

            if (!Scope.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) || registry == null)
                return null!;

            return registry.Resolve(filter, Scope);
        }
    }
}
