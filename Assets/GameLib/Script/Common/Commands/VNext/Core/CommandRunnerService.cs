#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;

namespace Game.Commands.VNext
{
    public sealed class CommandRunnerService :
        ICommandRunnerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _ownerScope;
        readonly VarStorePayload? _defaultVars;

        IScopeNode? _scope;
        bool _isStarted;

        public CommandRunnerService(
            IScopeNode ownerScope,
            VarStorePayload? defaultVars = null)
        {
            _ownerScope = ownerScope ?? throw new ArgumentNullException(nameof(ownerScope));
            _defaultVars = defaultVars;
        }

        public IScopeNode OwnerScope => _ownerScope;
        public IScopeNode? Scope => _scope;
        public bool IsStarted => _isStarted;
        public int RunningExecutionCount => 0;
        public bool IsExecuting => false;

        IScopeNode ICommandRunner.Scope => _scope ?? _ownerScope;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _scope = scope ?? _ownerScope;
            _isStarted = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _scope = null;
            _isStarted = false;
        }

        public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            => UniTask.FromResult(CreateUnavailableResult(options, CreateUnavailableMessage()));

        public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            => UniTask.FromResult(CreateUnavailableResult(options, CreateUnavailableMessage()));

        public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            => UniTask.FromResult(CreateUnavailableResult(options, CreateUnavailableMessage()));

        public UniTask WaitUntilIdleAsync(CancellationToken ct = default)
            => UniTask.CompletedTask;

        public UniTask WaitUntilScopeIdleAsync(IScopeNode scope, CancellationToken ct = default)
            => UniTask.CompletedTask;

        public CommandRunResult StartDetached(
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            Func<CommandContext, CancellationToken, UniTask<CommandRunResult>> work)
            => CreateUnavailableResult(ctx?.Options ?? CommandRunOptions.Default, CreateUnavailableMessage());

        public CommandRunResult StartDetachedList(
            CommandListData list,
            CommandListData onCanceled,
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            CommandRunOptions options)
            => CreateUnavailableResult(options, CreateUnavailableMessage());

        public void ApplyDefaultVars(IVarStore dest, bool overwrite = false)
        {
            if (dest == null)
                return;

            _defaultVars?.ApplyTo(dest, overwrite);
        }

        string CreateUnavailableMessage()
            => _isStarted
                ? "CommandRunnerService shell has no execution bridge."
                : "CommandRunnerService is not started.";

        internal static CommandRunResult CreateUnavailableResult(CommandRunOptions options, string message)
        {
            CommandRunOptions normalized = CommandRunOptions.ResolveOrDefault(options);
            return new CommandRunResult(
                CommandRunStatus.Error,
                CommandRunFailureKind.ResolveFailed,
                1,
                -1,
                0,
                message,
                trace: null,
                exception: null,
                rootFrame: default,
                appliedFailureBoundary: normalized.FailureBoundary);
        }
    }

    public sealed class ProvisionalRunnerBridge :
        ICommandRunner,
        ICommandRunnerActivity,
        ICommandDetachedRunner,
        ICommandRunnerDefaultVarsProvider,
        IProjectCommandRunner,
        IPlatformCommandRunner,
        IGlobalCommandRunner,
        ISceneCommandRunner,
        IFieldCommandRunner,
        IEntityCommandRunner,
        IUICommandRunner,
        IUIElementCommandRunner,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly ICommandRunnerService _service;
        readonly IScopeNode _ownerScope;
        readonly ICommandExecutorCatalog _executorCatalog;
        readonly ICommandCatalog _catalog;
        readonly ICommandKeyResolver _keyResolver;
        readonly ICommandResolveLogger _logger;
        readonly ICommandPayloadFieldReaderProvider _payloadFieldReaderProvider;
        readonly ICommandPayloadReferenceValidator _payloadReferenceValidator;
        readonly VarStorePayload? _defaultVars;

        CommandRunner? _engine;
        IScopeNode? _scope;

        public ProvisionalRunnerBridge(
            ICommandRunnerService service,
            IScopeNode ownerScope,
            ICommandExecutorCatalog executorCatalog,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            ICommandPayloadFieldReaderProvider payloadFieldReaderProvider,
            ICommandPayloadReferenceValidator payloadReferenceValidator,
            VarStorePayload? defaultVars = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _ownerScope = ownerScope ?? throw new ArgumentNullException(nameof(ownerScope));
            _executorCatalog = executorCatalog ?? throw new ArgumentNullException(nameof(executorCatalog));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _payloadFieldReaderProvider = payloadFieldReaderProvider ?? throw new ArgumentNullException(nameof(payloadFieldReaderProvider));
            _payloadReferenceValidator = payloadReferenceValidator ?? throw new ArgumentNullException(nameof(payloadReferenceValidator));
            _defaultVars = defaultVars;
        }

        public IScopeNode Scope => _scope ?? _service.Scope ?? _ownerScope;
        public int RunningExecutionCount => _engine?.RunningExecutionCount ?? 0;
        public bool IsExecuting => _engine?.IsExecuting ?? false;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            IScopeNode effectiveScope = scope ?? _service.Scope ?? _ownerScope;

            if (_engine != null && !ReferenceEquals(_engine.Scope, effectiveScope))
            {
                _engine.OnRelease(_scope ?? _service.Scope ?? _ownerScope, isReset);
                _engine = null;
            }

            _scope = effectiveScope;
            EnsureEngine(effectiveScope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_engine != null)
            {
                _engine.OnRelease(_scope ?? scope ?? _service.Scope ?? _ownerScope, isReset);
                _engine = null;
            }

            _scope = null;
        }

        public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return UniTask.FromResult(CommandRunnerService.CreateUnavailableResult(options, "ProvisionalRunnerBridge is not started."));

            return engine.ExecuteSingleAsync(data, ctx, ct, options);
        }

        public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return UniTask.FromResult(CommandRunnerService.CreateUnavailableResult(options, "ProvisionalRunnerBridge is not started."));

            return engine.ExecuteListAsync(list, ctx, ct, options);
        }

        public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return UniTask.FromResult(CommandRunnerService.CreateUnavailableResult(options, "ProvisionalRunnerBridge is not started."));

            return engine.ExecuteWithCancelAsync(list, onCanceled, ctx, ct, options);
        }

        public UniTask WaitUntilIdleAsync(CancellationToken ct = default)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return UniTask.CompletedTask;

            return engine.WaitUntilIdleAsync(ct);
        }

        public UniTask WaitUntilScopeIdleAsync(IScopeNode scope, CancellationToken ct = default)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return UniTask.CompletedTask;

            return engine.WaitUntilScopeIdleAsync(scope, ct);
        }

        public CommandRunResult StartDetached(
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            Func<CommandContext, CancellationToken, UniTask<CommandRunResult>> work)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return CommandRunnerService.CreateUnavailableResult(ctx?.Options ?? CommandRunOptions.Default, "ProvisionalRunnerBridge is not started.");

            return engine.StartDetached(ctx, policy, callerToken, work);
        }

        public CommandRunResult StartDetachedList(
            CommandListData list,
            CommandListData onCanceled,
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            CommandRunOptions options)
        {
            if (!TryGetEngine(out CommandRunner? engine) || engine == null)
                return CommandRunnerService.CreateUnavailableResult(options, "ProvisionalRunnerBridge is not started.");

            return engine.StartDetachedList(list, onCanceled, ctx, policy, callerToken, options);
        }

        public void ApplyDefaultVars(IVarStore dest, bool overwrite = false)
        {
            if (dest == null)
                return;

            if (TryGetEngine(out CommandRunner? engine))
            {
                engine.ApplyDefaultVars(dest, overwrite);
                return;
            }

            _service.ApplyDefaultVars(dest, overwrite);
        }

        CommandRunner EnsureEngine(IScopeNode scope, bool isReset)
        {
            if (_engine == null)
            {
                _engine = CreateEngine(scope);
                _engine.OnAcquire(scope, isReset);
                return _engine!;
            }

            _engine.OnAcquire(scope, isReset);
            return _engine!;
        }

        CommandRunner CreateEngine(IScopeNode scope)
        {
            if (scope.Kind == LifetimeScopeKind.UIElement)
            {
                return new UIElementCommandRunner(
                    scope,
                    _executorCatalog,
                    _catalog,
                    _keyResolver,
                    _logger,
                    _payloadFieldReaderProvider,
                    _payloadReferenceValidator,
                    _defaultVars);
            }

            return new CommandRunner(
                scope,
                _executorCatalog,
                _catalog,
                _keyResolver,
                _logger,
                _payloadFieldReaderProvider,
                _payloadReferenceValidator,
                _defaultVars);
        }

        bool TryGetEngine(out CommandRunner? engine)
        {
            if (_engine == null && _service.IsStarted)
            {
                IScopeNode effectiveScope = _service.Scope ?? _ownerScope;
                _scope = effectiveScope;
                _engine = CreateEngine(effectiveScope);
                _engine.OnAcquire(effectiveScope, false);
            }

            engine = _service.IsStarted ? _engine : null;
            return engine != null;
        }

    }
}