#nullable enable
using Game;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public readonly struct CommandResolveContext : IDynamicContext, IDynamicDependencyTokenSource, IDynamicEvaluationOriginProvider
    {
        public IScopeNode Scope { get; }
        public IVarStore Vars { get; }
        public IScopeNode? CommandRootScope { get; }
        public IRuntimeResolver? Resolver { get; }
        public ICommandCatalog Catalog { get; }
        public ICommandKeyResolver KeyResolver { get; }
        public ICommandResolveLogger Logger { get; }
        public CommandContext? RuntimeContext { get; }

        public CommandResolveContext(
            IScopeNode scope,
            IVarStore vars,
            IScopeNode? commandRootScope,
            IRuntimeResolver? resolver,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            CommandContext? runtimeContext = null)
        {
            Scope = scope;
            Vars = vars ?? NullVarStore.Instance;
            CommandRootScope = commandRootScope;
            Resolver = resolver;
            Catalog = catalog;
            KeyResolver = keyResolver;
            Logger = logger;
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

        public DynamicDependencyTokenSet GetDynamicDependencyTokens()
        {
            if (RuntimeContext is IDynamicDependencyTokenSource tokenSource)
                return tokenSource.GetDynamicDependencyTokens();

            return default;
        }

        public DynamicEvaluationOrigin GetDynamicEvaluationOrigin()
        {
            if (RuntimeContext is IDynamicEvaluationOriginProvider originProvider)
                return originProvider.GetDynamicEvaluationOrigin();

            return DynamicEvaluationOrigin.FromScopeNodes(Scope, CommandRootScope);
        }
    }
}
