#nullable enable
using Game;
using VContainer;

namespace Game.Commands.VNext
{
    public readonly struct CommandResolveContext
    {
        public readonly IScopeNode Scope;
        public readonly IObjectResolver? Resolver;
        public readonly ICommandCatalog Catalog;
        public readonly ICommandKeyResolver KeyResolver;
        public readonly ICommandResolveLogger Logger;
        public readonly bool AllowRuntimeKeyFallback;

        public CommandResolveContext(
            IScopeNode scope,
            IObjectResolver? resolver,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            bool allowRuntimeKeyFallback)
        {
            Scope = scope;
            Resolver = resolver;
            Catalog = catalog;
            KeyResolver = keyResolver;
            Logger = logger;
            AllowRuntimeKeyFallback = allowRuntimeKeyFallback;
        }
    }
}
