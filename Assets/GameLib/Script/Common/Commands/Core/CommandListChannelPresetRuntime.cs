#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using VContainer;

namespace Game.Commands
{
    public sealed class CommandListChannelPresetRuntime :
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly ICommandListChannelOptions _options;

        CommandListPreset _baseCommandListPreset = new();
        CommandListPreset _currentCommandListPreset = new();
        CommandListPlayerPreset _basePlayerPreset = new();
        CommandListPlayerPreset _currentPlayerPreset = new();

        public CommandListPreset CurrentCommandListPreset => _currentCommandListPreset;
        public CommandListPlayerPreset CurrentPlayerPreset => _currentPlayerPreset;

        public CommandListChannelPresetRuntime(ICommandListChannelOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var vars = ResolveVars(scope);
            var context = new SimpleDynamicContext(vars, scope);
            var sourcePreset = ResolveSourcePreset(_options.PresetValue, context);
            _baseCommandListPreset = ResolveCommandListPreset(sourcePreset, context);
            _currentCommandListPreset = _baseCommandListPreset.CreateRuntimeCopy();
            _basePlayerPreset = ResolvePlayerPreset(sourcePreset, context);
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _baseCommandListPreset = new CommandListPreset();
            _currentCommandListPreset = new CommandListPreset();
            _basePlayerPreset = new CommandListPlayerPreset();
            _currentPlayerPreset = new CommandListPlayerPreset();
        }

        public bool SwapCommandListPreset(CommandListPreset? preset)
        {
            if (preset == null)
                return false;

            _baseCommandListPreset = preset.CreateRuntimeCopy();
            _currentCommandListPreset = _baseCommandListPreset.CreateRuntimeCopy();
            return true;
        }

        public bool SwapPlayerPreset(CommandListPlayerPreset? preset)
        {
            if (preset == null)
                return false;

            _basePlayerPreset = preset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            return true;
        }

        public bool MutateCommands(CommandListMutationStep? mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return false;

            return _currentCommandListPreset.Commands.ApplyRuntimeMutation(mutation, mutationService);
        }

        public bool ResetRuntimeOverrides(bool resetCommands, bool resetPlayer)
        {
            var changed = false;

            if (resetCommands)
            {
                _currentCommandListPreset = _baseCommandListPreset.CreateRuntimeCopy();
                changed = true;
            }

            if (resetPlayer)
            {
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
                changed = true;
            }

            return changed;
        }

        static CommandListChannelPreset ResolveSourcePreset(DynamicValue<CommandListChannelPreset> value, IDynamicContext context)
        {
            if (value.TryGet(context, out CommandListChannelPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new CommandListChannelPreset();
        }

        static CommandListPreset ResolveCommandListPreset(CommandListChannelPreset sourcePreset, IDynamicContext context)
        {
            if (sourcePreset.CommandListPresetValue.TryGet(context, out CommandListPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new CommandListPreset();
        }

        static CommandListPlayerPreset ResolvePlayerPreset(CommandListChannelPreset sourcePreset, IDynamicContext context)
        {
            if (sourcePreset.PlayerPresetValue.TryGet(context, out CommandListPlayerPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new CommandListPlayerPreset();
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}
