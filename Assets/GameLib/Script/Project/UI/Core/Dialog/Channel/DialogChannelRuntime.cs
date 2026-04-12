#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Animation;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Conversation;
using Game.DI;
using Game.Project.Scene.Runtime;
using Game.Spawn;
using Game.Times;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Dialogue
{
    internal sealed class DialogueChannelRuntime : IDialogueChannelService
    {
        sealed class ActiveMessageSession
        {
            public readonly UniTaskCompletionSource<DialogueAdvanceResult> Completion = new();
            public bool AwaitInput;
            public bool WaitingForTypewriter;
            public bool AllowTypewriterSkipByInput;
            public bool Completed;
        }

        sealed class CharacterRuntimeRecord
        {
            public string CharacterId = string.Empty;
            public int CharacterDataId;
            public IObjectResolver? Resolver;
            public RuntimeLifetimeScope? RuntimeScope;
            public Transform? Root;
            public IScopeNode? Scope;
            public bool IsOwnedByDialogue = true;
            public bool KeepAliveAfterEnd;
        }

        readonly struct ResolvedCharacterEntry
        {
            public string RuntimeKey { get; }
            public int CharacterDataId { get; }
            public CharacterDataBaseDefinition? Definition { get; }
            public BaseRuntimeTemplatePreset? RuntimeTemplate { get; }

            public ResolvedCharacterEntry(
                string runtimeKey,
                int characterDataId,
                CharacterDataBaseDefinition? definition,
                BaseRuntimeTemplatePreset? runtimeTemplate)
            {
                RuntimeKey = runtimeKey;
                CharacterDataId = characterDataId;
                Definition = definition;
                RuntimeTemplate = runtimeTemplate;
            }
        }

        readonly IScopeNode _owner;
        readonly string _tag;
        readonly bool _enableDebugLog;

        DynamicValue<DialogueChannelPreset> _sourcePreset;
        DialoguePresetRuntimeSnapshot _preset = new();

        IScopeNode? _activeScope;
        IVarStore _vars = NullVarStore.Instance;
        ICommandRunner? _commandRunner;

        Transform? _ownerTransform;
        TransformGridEnvironmentKind _environmentKind = TransformGridEnvironmentKind.World;

        ITextChannelHubService? _textHub;
        IChoiceChannelHubService? _choiceHub;
        ITransformAnimationHubService? _transformHub;
        IAnimationSpriteHubService? _spriteHub;
        IRuntimeLifetimeScopeSpawnerService? _runtimeSpawner;
        ICharacterDataBaseService? _characterDataBase;
        IVisualBoundsOutput? _visualBoundsOutput;
        IModalStackChannelHubService? _modalHub;

        IButtonChannelOutput? _inputOutput;
        ButtonChannelPhase _lastInputPhase = ButtonChannelPhase.Idle;

        readonly Dictionary<string, CharacterRuntimeRecord> _characterRecords = new(StringComparer.Ordinal);
        readonly List<ITextChannelPlayer> _activeTypewriterPlayers = new();

        bool _isVisible;
        bool _isActive;
        bool _isInputEnabled;
        bool _isChoiceInputLock;
        bool _modalRegistered;
        IUIModalRoot? _modalRoot;
        string _activeChoiceChannelTag = string.Empty;

        int _dialogueCount;
        DialogueTypewriterState _typewriterState = DialogueTypewriterState.Idle;
        DialogueChoiceState _choiceState = DialogueChoiceState.None;
        DialogueCharacterAnchor _activeCharacterAnchor = DialogueCharacterAnchor.None;
        string _activeCharacterName = string.Empty;
        AnimationDataSource? _activeCharacterIconAnimPreset;

        ActiveMessageSession? _activeMessageSession;
        DialogueChannelSnapshot _lastSnapshot;

        public string Tag => _tag;

        public DialogueChannelSnapshot Snapshot => BuildSnapshot();

        public event Action<DialogueChannelSnapshot>? OnStateChanged;

        public DialogueChannelRuntime(
            IScopeNode owner,
            string tag,
            DynamicValue<DialogueChannelPreset> sourcePreset,
            bool enableDebugLog)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _tag = DialogueTagUtility.Normalize(tag);
            _sourcePreset = sourcePreset;
            _enableDebugLog = enableDebugLog;
            _lastSnapshot = BuildSnapshot();
        }

        public void SetSourcePreset(DynamicValue<DialogueChannelPreset> sourcePreset)
        {
            _sourcePreset = sourcePreset;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;

            _activeScope = scope;
            _vars = ResolveVars(scope);
            _ownerTransform = scope.Identity?.SelfTransform;
            if (_ownerTransform != null)
                _environmentKind = TransformGridSharedUtility.ResolveEnvironment(_ownerTransform, out _);
            else
                _environmentKind = TransformGridEnvironmentKind.World;

            ResolveServices(scope);
            RebuildPreset(scope);

            _isVisible = false;
            _isActive = false;
            _isInputEnabled = _preset.Input.EnableInput;
            _isChoiceInputLock = false;
            _dialogueCount = 0;
            _typewriterState = DialogueTypewriterState.Idle;
            _choiceState = DialogueChoiceState.None;
            _activeCharacterAnchor = DialogueCharacterAnchor.None;
            _activeCharacterName = string.Empty;
            _activeCharacterIconAnimPreset = null;
            _activeChoiceChannelTag = string.Empty;
            _activeMessageSession = null;
            _activeTypewriterPlayers.Clear();

            TryRebindInputOutput(out _);
            RefreshModalRegistration();
            PublishSnapshot(force: true);
            Trace($"Acquire tag={_tag} env={_environmentKind}");
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;

            CancelActiveMessage("released");
            if (_preset.Choice.CancelChoiceOnEnd)
                TryCancelChoice("released");

            if (_preset.Character.ReleaseSpawnedOnEnd)
                ReleaseAllSpawnedCharactersAsync(CancellationToken.None).Forget();

            UnbindInputOutput();
            ReleaseModalRegistration();

            _activeScope = null;
            _vars = NullVarStore.Instance;
            _commandRunner = null;
            _textHub = null;
            _choiceHub = null;
            _transformHub = null;
            _spriteHub = null;
            _runtimeSpawner = null;
            _characterDataBase = null;
            _visualBoundsOutput = null;
            _modalHub = null;

            _isVisible = false;
            _isActive = false;
            _isInputEnabled = false;
            _isChoiceInputLock = false;
            _dialogueCount = 0;
            _typewriterState = DialogueTypewriterState.Idle;
            _choiceState = DialogueChoiceState.None;
            _activeCharacterAnchor = DialogueCharacterAnchor.None;
            _activeCharacterName = string.Empty;
            _activeCharacterIconAnimPreset = null;
            _activeChoiceChannelTag = string.Empty;

            _ownerTransform = null;

            PublishSnapshot(force: true);
            Trace($"Release tag={_tag}");
        }

        public void Tick()
        {
        }

        public void RebuildPreset(IScopeNode scope)
        {
            var context = new SimpleDynamicContext(ResolveVars(scope), scope);
            _preset = DialoguePresetRuntimeSnapshot.Resolve(_sourcePreset, context);
            _isInputEnabled = _preset.Input.EnableInput && _isInputEnabled;
            TryRebindInputOutput(out _);
            RefreshModalRegistration();
            Trace($"Preset rebuilt tag={_tag}");
        }

        public bool SetVisible(bool visible)
        {
            if (_isVisible == visible)
                return false;

            _isVisible = visible;
            RefreshModalRegistration();

            var commands = visible ? _preset.State.OnVisibleTrue : _preset.State.OnVisibleFalse;
            RunHookCommandsAsync(commands, "Visible", CancellationToken.None).Forget();
            PublishSnapshot(force: true);
            return true;
        }

        public bool SetActive(bool active)
        {
            if (_isActive == active)
                return false;

            _isActive = active;
            RefreshModalRegistration();

            var commands = active ? _preset.State.OnActiveTrue : _preset.State.OnActiveFalse;
            RunHookCommandsAsync(commands, "Active", CancellationToken.None).Forget();
            PublishSnapshot(force: true);
            return true;
        }

        public bool SetInputEnabled(bool enabled)
        {
            if (_isInputEnabled == enabled)
                return false;

            _isInputEnabled = enabled;
            RefreshModalRegistration();
            PublishSnapshot(force: true);
            return true;
        }

        public bool TryRequestAdvance()
        {
            return TryAdvanceFromInput();
        }

        public bool TryCancelChoice(string reason = "")
        {
            var tag = string.IsNullOrWhiteSpace(_activeChoiceChannelTag)
                ? _preset.Choice.ChoiceChannelTag
                : _activeChoiceChannelTag;

            if (string.IsNullOrWhiteSpace(tag))
                tag = _preset.Choice.ChoiceChannelTag;

            if (_choiceHub == null)
                return false;

            return _choiceHub.CancelChoice(tag, reason);
        }

        public async UniTask<bool> SetupAsync(DialogueSetupRequest request, CancellationToken ct)
        {
            if (_activeScope == null)
                return false;

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueSetupRequest();
            var wasVisible = _isVisible;

            if (runtimeRequest.SetVisible)
                SetVisible(runtimeRequest.Visible);

            if (runtimeRequest.SetActive)
                SetActive(runtimeRequest.Active);

            if (runtimeRequest.BeginDialogueStep)
            {
                if (!wasVisible && _isVisible)
                {
                    _dialogueCount = 1;
                    await RunHookCommandsAsync(_preset.State.OnSessionOpened, "SessionOpened", ct);
                }
                else if (_isVisible && runtimeRequest.IncrementIfAlreadyVisible)
                {
                    _dialogueCount = Mathf.Max(1, _dialogueCount) + 1;
                    await RunHookCommandsAsync(_preset.State.OnSessionContinued, "SessionContinued", ct);
                }

                PublishSnapshot(force: true);
            }

            if (runtimeRequest.ApplyLayout && _preset.Layout.EnableLayout && _preset.Layout.RefreshOnSetup)
                await RefreshLayoutAsync(new DialogueLayoutRefreshRequest(), ct);

            return true;
        }

        public async UniTask<DialogueMessageResult> ShowMessageAsync(DialogueMessageRequest request, CancellationToken ct)
        {
            if (_activeScope == null)
                return DialogueMessageResult.Failed($"[DIALOGUE-100] Channel '{_tag}' is inactive.");

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueMessageRequest();
            if (runtimeRequest.AutoSetupIfHidden && !_isVisible)
                await SetupAsync(runtimeRequest.AutoSetup, ct);

            CancelActiveMessage("replaced");

            await RunHookCommandsAsync(_preset.State.OnMessageStarted, "MessageStarted", ct);

            var playMode = runtimeRequest.UsePresetPlayMode
                ? _preset.Text.DefaultPlayMode
                : runtimeRequest.PlayMode;

            var textSettings = runtimeRequest.TextSettings;
            if (playMode == TextPlayMode.Count && !textSettings.UseCounter)
                textSettings.UseCounter = true;

            var runtimeContext = new SimpleDynamicContext(_vars, _activeScope);
            var targetPlayers = new List<ITextChannelPlayer>(8);

            if (!TryApplyMessageLines(runtimeRequest.BodyLines, isNameLine: false, playMode, textSettings, runtimeContext, targetPlayers, out var bodyError))
            {
                LogError(bodyError);
                return DialogueMessageResult.Failed(bodyError);
            }

            var waitTypewriter = playMode == TextPlayMode.Typewriter &&
                                 (runtimeRequest.WaitForTypewriterComplete || _preset.Text.WaitForTypewriterBeforeAdvance);

            _typewriterState = waitTypewriter
                ? DialogueTypewriterState.Playing
                : DialogueTypewriterState.Completed;

            _activeTypewriterPlayers.Clear();
            if (waitTypewriter)
                _activeTypewriterPlayers.AddRange(targetPlayers);

            var session = new ActiveMessageSession
            {
                AwaitInput = runtimeRequest.AwaitInput,
                WaitingForTypewriter = waitTypewriter,
                AllowTypewriterSkipByInput = runtimeRequest.AllowTypewriterSkipByInput && _preset.Text.AllowSkipTypewriterByInput,
            };

            if (session.AwaitInput && !_preset.Input.EnableInput)
            {
                var errorMessage = BuildError("DIALOGUE-110", $"Dialogue input was requested, but input is disabled. tag='{_tag}'");
                LogError(errorMessage);
                return DialogueMessageResult.Failed(errorMessage);
            }

            var requiresInputBinding = session.AwaitInput && CanAcceptDialogueInput();
            if (requiresInputBinding && _inputOutput == null)
            {
                if (!TryRebindInputOutput(out var inputError))
                {
                    LogError(inputError);
                    return DialogueMessageResult.Failed(inputError);
                }
            }

            _activeMessageSession = session;
            PublishSnapshot(force: true);

            var typewriterTask = waitTypewriter
                ? WaitTypewriterCompleteAsync(targetPlayers, ct)
                : UniTask.CompletedTask;

            if (waitTypewriter && (!session.AwaitInput || !CanAcceptDialogueInput()))
            {
                await typewriterTask;
                session.WaitingForTypewriter = false;
                _typewriterState = DialogueTypewriterState.Completed;
                PublishSnapshot(force: true);
            }
            else if (waitTypewriter)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await typewriterTask;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (!ReferenceEquals(_activeMessageSession, session) || session.Completed)
                        return;

                    session.WaitingForTypewriter = false;
                    _typewriterState = DialogueTypewriterState.Completed;
                    PublishSnapshot(force: true);
                });
            }

            DialogueAdvanceResult advance;
            if (session.AwaitInput && CanAcceptDialogueInput())
            {
                advance = await session.Completion.Task.AttachExternalCancellation(ct);
            }
            else
            {
                if (waitTypewriter)
                    await typewriterTask;

                if (runtimeRequest.UseAutoAdvanceDelay)
                {
                    var delaySeconds = Mathf.Max(0f, runtimeRequest.AutoAdvanceDelaySeconds.GetOrDefault(runtimeContext, 0f));
                    if (delaySeconds > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
                }

                advance = DialogueAdvanceResult.FromAuto();
            }

            session.Completed = true;
            if (ReferenceEquals(_activeMessageSession, session))
                _activeMessageSession = null;

            _activeTypewriterPlayers.Clear();
            _typewriterState = DialogueTypewriterState.Completed;
            PublishSnapshot(force: true);

            if (_preset.Layout.EnableLayout && _preset.Layout.RefreshOnMessage)
                await RefreshLayoutAsync(new DialogueLayoutRefreshRequest(), ct);

            await RunHookCommandsAsync(_preset.State.OnMessageCompleted, "MessageCompleted", ct);
            return DialogueMessageResult.Completed(advance);
        }

        public async UniTask<DialogueChoiceResult> ShowChoiceAndWaitAsync(DialogueChoiceRequest request, CancellationToken ct)
        {
            if (_activeScope == null)
                return DialogueChoiceResult.Failed($"[DIALOGUE-200] Channel '{_tag}' is inactive.");

            if (_choiceHub == null)
            {
                var errorMessage = BuildError("DIALOGUE-201", $"Required IChoiceChannelHubService was not found. tag='{_tag}'");
                LogError(errorMessage);
                return DialogueChoiceResult.Failed(errorMessage);
            }

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueChoiceRequest();
            if (runtimeRequest.PlayPreMessage)
            {
                var preResult = await ShowMessageAsync(runtimeRequest.PreMessage, ct);
                if (!preResult.Success)
                    return DialogueChoiceResult.Failed(preResult.Message);
            }

            var gridChoiceRequest = runtimeRequest.GridChoiceRequest;
            if (gridChoiceRequest == null)
            {
                var errorMessage = BuildError("DIALOGUE-202", $"Choice grid request was null. tag='{_tag}'");
                LogError(errorMessage);
                return DialogueChoiceResult.Failed(errorMessage);
            }

            if (_enableDebugLog)
            {
                Trace(
                    $"Choice request begin. Tag='{_tag}' GridEntries={gridChoiceRequest.Entries?.Count ?? 0} " +
                    $"ChoiceSpawnCommands={_preset.Choice.SpawnCommands?.Count ?? 0} " +
                    $"BindSpawnCommandsBeforeAppend={gridChoiceRequest.BindRequest?.SpawnCommands?.Count ?? 0}");
            }

            ApplyChoiceSpawnCommands(runtimeRequest);

            if (_enableDebugLog)
            {
                Trace(
                    $"Choice request after append. Tag='{_tag}' GridEntries={gridChoiceRequest.Entries?.Count ?? 0} " +
                    $"BindSpawnCommandsAfterAppend={gridChoiceRequest.BindRequest?.SpawnCommands?.Count ?? 0}");
            }

            var channelTag = runtimeRequest.UsePresetChannelTag
                ? _preset.Choice.ChoiceChannelTag
                : DialogueTagUtility.Normalize(runtimeRequest.ChannelTag);

            var lockInput = runtimeRequest.UsePresetInputLock
                ? _preset.Choice.LockDialogueInputDuringChoice
                : runtimeRequest.LockDialogueInput;

            _activeChoiceChannelTag = channelTag;
            _choiceState = DialogueChoiceState.Waiting;
            if (lockInput)
                _isChoiceInputLock = true;
            PublishSnapshot(force: true);

            await RunHookCommandsAsync(_preset.State.OnChoiceStarted, "ChoiceStarted", ct);

            Game.Channel.GridObjectChoiceSessionResult choiceResult;
            try
            {
                choiceResult = await _choiceHub.ShowChoiceAndWaitAsync(channelTag, gridChoiceRequest, ct);
            }
            finally
            {
                _choiceState = DialogueChoiceState.Completed;
                if (lockInput)
                    _isChoiceInputLock = false;

                _activeChoiceChannelTag = string.Empty;
                PublishSnapshot(force: true);
                await RunHookCommandsAsync(_preset.State.OnChoiceCompleted, "ChoiceCompleted", CancellationToken.None);

                _choiceState = DialogueChoiceState.None;
                PublishSnapshot(force: true);
            }

            return DialogueChoiceResult.Completed(choiceResult);
        }

        void ApplyChoiceSpawnCommands(DialogueChoiceRequest runtimeRequest)
        {
            var spawnCommands = _preset.Choice.SpawnCommands;
            if (runtimeRequest?.GridChoiceRequest == null || spawnCommands == null || spawnCommands.Count == 0)
                return;

            var bindRequest = runtimeRequest.GridChoiceRequest.BindRequest;
            bindRequest.SpawnCommands.AddRuntimeCommands(spawnCommands);
        }

        public async UniTask<bool> ApplyCharactersAsync(DialogueCharacterFrameRequest request, CancellationToken ct)
        {
            if (_activeScope == null)
                return false;

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueCharacterFrameRequest();
            var context = new SimpleDynamicContext(_vars, _activeScope);

            var resolvedAnchor = DialogueCharacterAnchor.None;
            var activeCharacterName = string.Empty;
            AnimationDataSource? activeCharacterIconAnimPreset = null;
            var capturedActiveCharacterState = false;
            for (var i = 0; i < runtimeRequest.Entries.Count; i++)
            {
                var entry = runtimeRequest.Entries[i];
                if (entry == null)
                    continue;

                if (resolvedAnchor == DialogueCharacterAnchor.None && entry.Anchor != DialogueCharacterAnchor.None)
                    resolvedAnchor = entry.Anchor;

                var resolvedCharacter = ResolveCharacterEntry(entry, i, context);

                if (entry.SpawnIfNeeded || resolvedCharacter.CharacterDataId > 0)
                    await EnsureCharacterRuntimeSpawnedAsync(resolvedCharacter, entry, context, ct);

                var displayName = ResolveDisplayName(entry, context, resolvedCharacter.Definition);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    var nameTag = string.IsNullOrWhiteSpace(entry.NameChannelTag)
                        ? _preset.Character.DefaultNameChannelTag
                        : DialogueTagUtility.Normalize(entry.NameChannelTag);
                    if (!TryApplyNameText(nameTag, displayName, out var nameError))
                    {
                        LogError(nameError);
                        return false;
                    }
                }

                if (TryResolvePortraitAnimation(entry, resolvedCharacter.Definition, out var animationData, out var resolvedSpriteTag, out var resolvedIconAnimPreset) &&
                    animationData != null)
                {
                    await PlayCharacterPortraitAsync(resolvedCharacter.RuntimeKey, resolvedSpriteTag, animationData, entry.PortraitPlayMode, ct);
                }

                if (!capturedActiveCharacterState)
                {
                    activeCharacterName = displayName;
                    activeCharacterIconAnimPreset = resolvedIconAnimPreset;
                    capturedActiveCharacterState = true;
                }

                if (resolvedAnchor == DialogueCharacterAnchor.None && entry.Anchor != DialogueCharacterAnchor.None)
                {
                    resolvedAnchor = entry.Anchor;
                    activeCharacterName = displayName;
                    activeCharacterIconAnimPreset = resolvedIconAnimPreset;
                    capturedActiveCharacterState = true;
                }
            }

            _activeCharacterAnchor = resolvedAnchor;
            _activeCharacterName = activeCharacterName;
            _activeCharacterIconAnimPreset = activeCharacterIconAnimPreset;
            PublishSnapshot(force: true);

            if (runtimeRequest.RefreshLayout && _preset.Layout.EnableLayout && _preset.Layout.RefreshOnCharacterUpdate)
                return await RefreshLayoutAsync(new DialogueLayoutRefreshRequest(), ct);

            return true;
        }

        public async UniTask<bool> RefreshLayoutAsync(DialogueLayoutRefreshRequest request, CancellationToken ct)
        {
            if (_activeScope == null)
                return false;

            if (!_preset.Layout.EnableLayout)
                return true;

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueLayoutRefreshRequest();
            var context = new SimpleDynamicContext(_vars, _activeScope);

            var rootPosition = runtimeRequest.OverrideRootPosition
                ? runtimeRequest.RootPosition
                : _preset.Layout.RootPosition;

            CommandListData? layoutCommands = null;
            if (_preset.Layout is DialogueLayoutCommandOnlyPreset commandOnlyPreset)
                layoutCommands = commandOnlyPreset.Commands;

            var characterCommands = _preset.Character.CharacterLayout.ResolveCommands(_activeCharacterAnchor);

            IVarStore? commandVars = null;
            if ((layoutCommands != null && layoutCommands.Count > 0) ||
                characterCommands.Count > 0)
            {
                commandVars = BuildCommandVars(rootPosition);
            }

            if (_preset.Layout is DialogueLayoutPreset layoutPreset)
            {
                var target = ResolveRootTargetPosition(rootPosition, context);
                var rootMoved = await MoveByTransformChannelAsync(layoutPreset.RootTransformChannelTag, target, context, ct);
                if (!rootMoved)
                    Trace($"Layout root move skipped. tag={_tag} channel={layoutPreset.RootTransformChannelTag}");
            }

            if (layoutCommands != null && layoutCommands.Count > 0)
                await RunHookCommandsAsync(layoutCommands, "LayoutCommandOnly", ct, commandVars);

            if (characterCommands.Count > 0)
                await RunHookCommandsAsync(characterCommands, "CharacterLayout", ct, commandVars);

            return true;
        }

        public async UniTask<bool> EndAsync(DialogueEndRequest request, CancellationToken ct)
        {
            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueEndRequest();

            CancelActiveMessage("ended");
            if (_preset.Choice.CancelChoiceOnEnd)
                TryCancelChoice("ended");

            await RunHookCommandsAsync(_preset.State.OnSessionEnded, "SessionEnded", ct);

            if (runtimeRequest.ReleaseSpawnedCharacters && _preset.Character.ReleaseSpawnedOnEnd)
                await ReleaseAllSpawnedCharactersAsync(ct);

            if (runtimeRequest.ResetVisible && _preset.State.ResetVisibleOnEnd)
                SetVisible(false);

            if (runtimeRequest.ResetActive && _preset.State.ResetActiveOnEnd)
                SetActive(false);

            if (runtimeRequest.ResetDialogueCount && _preset.State.ResetDialogueCountOnEnd)
                _dialogueCount = 0;

            _typewriterState = DialogueTypewriterState.Idle;
            _choiceState = DialogueChoiceState.None;
            _activeCharacterAnchor = DialogueCharacterAnchor.None;
            _activeCharacterName = string.Empty;
            _activeCharacterIconAnimPreset = null;
            PublishSnapshot(force: true);
            return true;
        }

        void ResolveServices(IScopeNode scope)
        {
            TryResolveFromScopeOrAncestors(scope, out _textHub);
            TryResolveFromScopeOrAncestors(scope, out _choiceHub);
            TryResolveFromScopeOrAncestors(scope, out _transformHub);
            TryResolveFromScopeOrAncestors(scope, out _spriteHub);
            TryResolveFromScopeOrAncestors(scope, out _runtimeSpawner);
            TryResolveFromScopeOrAncestors(scope, out _characterDataBase);
            TryResolveFromScopeOrAncestors(scope, out _modalHub);
            TryResolveFromScopeOrAncestors(scope, out _visualBoundsOutput);
            TryResolveFromScopeOrAncestors(scope, out _commandRunner);
        }

        bool TryRebindInputOutput(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (_activeScope == null)
                return false;

            if (!_preset.Input.EnableInput)
                return false;

            if (!TryResolveFromScopeOrAncestors(_activeScope, out IButtonChannelHubService? buttonHub) || buttonHub == null)
            {
                errorMessage = BuildError("DIALOGUE-150", $"Required IButtonChannelHubService was not found. tag='{_tag}' buttonTag='{_preset.Input.ButtonChannelTag}'");
                return false;
            }

            if (!buttonHub.TryGetOutput(_preset.Input.ButtonChannelTag, out var output) || output == null)
            {
                errorMessage = BuildError("DIALOGUE-151", $"Required ButtonChannel output was not found. tag='{_tag}' buttonTag='{_preset.Input.ButtonChannelTag}'");
                return false;
            }

            UnbindInputOutput();
            _inputOutput = output;
            _lastInputPhase = output.Phase;
            _inputOutput.OnUpdated += HandleInputOutputUpdated;
            return true;
        }

        void UnbindInputOutput()
        {
            if (_inputOutput == null)
                return;

            _inputOutput.OnUpdated -= HandleInputOutputUpdated;
            _inputOutput = null;
            _lastInputPhase = ButtonChannelPhase.Idle;
        }

        void HandleInputOutputUpdated(ButtonChannelOutputSnapshot snapshot)
        {
            if (!CanAcceptDialogueInput())
            {
                _lastInputPhase = snapshot.Phase;
                return;
            }

            var transitioned = snapshot.Phase != _lastInputPhase;
            _lastInputPhase = snapshot.Phase;
            if (_preset.Input.RequirePhaseTransition && !transitioned)
                return;

            if (snapshot.Phase != _preset.Input.AdvancePhase)
                return;

            TryAdvanceFromInput();
        }

        bool TryAdvanceFromInput()
        {
            var session = _activeMessageSession;
            if (session == null || session.Completed || !session.AwaitInput)
                return false;

            if (session.WaitingForTypewriter)
            {
                if (!session.AllowTypewriterSkipByInput)
                    return false;

                SkipActiveTypewriter();
                session.WaitingForTypewriter = false;
                _typewriterState = DialogueTypewriterState.Completed;
                RunHookCommandsAsync(_preset.State.OnTypewriterSkipped, "TypewriterSkipped", CancellationToken.None).Forget();
                PublishSnapshot(force: true);
                return true;
            }

            session.Completion.TrySetResult(DialogueAdvanceResult.FromInput());
            return true;
        }

        void SkipActiveTypewriter()
        {
            for (var i = 0; i < _activeTypewriterPlayers.Count; i++)
            {
                var player = _activeTypewriterPlayers[i];
                if (player == null)
                    continue;
                player.Skip();
            }
        }

        async UniTask WaitTypewriterCompleteAsync(List<ITextChannelPlayer> players, CancellationToken ct)
        {
            if (players == null || players.Count == 0)
                return;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;
                await player.WaitForTypewriterCompleteAsync(ct);
            }
        }

        bool TryApplyMessageLines(
            List<DialogueMessageLine> lines,
            bool isNameLine,
            TextPlayMode playMode,
            in SetTextSettings textSettings,
            IDynamicContext context,
            List<ITextChannelPlayer> targetPlayers,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (lines == null || lines.Count == 0)
                return true;

            if (_textHub == null)
            {
                errorMessage = BuildError("DIALOGUE-120", $"Required ITextChannelHubService was not found. tag='{_tag}'");
                return false;
            }

            var resolvedPlayers = new List<ITextChannelPlayer>(lines.Count);
            var resolvedTexts = new List<string>(lines.Count);

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null)
                    continue;

                var lineTag = DialogueTagUtility.Normalize(line.DialogueTag);
                var channelTag = ResolveTextChannelTag(
                    isNameLine ? _preset.Text.NameChannels : _preset.Text.BodyChannels,
                    lineTag,
                    line.ChannelTag,
                    isNameLine ? _preset.Text.DefaultNameChannelTag : _preset.Text.DefaultBodyChannelTag);

                if (string.IsNullOrWhiteSpace(channelTag))
                {
                    errorMessage = BuildError(
                        "DIALOGUE-121",
                        $"Required text channel binding was not found. tag='{_tag}' dialogueTag='{lineTag}' isNameLine={isNameLine} lineIndex={i}");
                    return false;
                }

                if (!_textHub.TryGetPlayer(channelTag, out var player) || player == null)
                {
                    errorMessage = BuildError(
                        "DIALOGUE-122",
                        $"Required text channel '{channelTag}' was not found. tag='{_tag}' dialogueTag='{lineTag}' isNameLine={isNameLine} lineIndex={i}");
                    return false;
                }

                var text = line.Text.GetOrDefault(context, string.Empty);
                resolvedPlayers.Add(player);
                resolvedTexts.Add(text);
            }

            for (var i = 0; i < resolvedPlayers.Count; i++)
            {
                var player = resolvedPlayers[i];
                player.SetText(resolvedTexts[i], playMode, textSettings);
                targetPlayers.Add(player);
            }

            return true;
        }

        bool TryApplyNameText(string channelTag, string text, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_textHub == null)
            {
                errorMessage = BuildError("DIALOGUE-123", $"Required ITextChannelHubService was not found while applying a character name. tag='{_tag}'");
                return false;
            }

            if (string.IsNullOrWhiteSpace(channelTag))
            {
                errorMessage = BuildError("DIALOGUE-124", $"Required name channel binding was not found. tag='{_tag}'");
                return false;
            }

            if (!_textHub.TryGetPlayer(channelTag, out var player) || player == null)
            {
                errorMessage = BuildError("DIALOGUE-125", $"Required name channel '{channelTag}' was not found. tag='{_tag}'");
                return false;
            }

            player.SetText(text, TextPlayMode.Instant, _preset.Text.DefaultTextSettings);
            return true;
        }

        static string ResolveTextChannelTag(
            IReadOnlyList<DialogueTextChannelBinding>? bindings,
            string dialogueTag,
            string explicitChannelTag,
            string defaultChannelTag)
        {
            var normalizedDefaultTag = DialogueTagUtility.Normalize(defaultChannelTag);
            var normalizedExplicitTag = DialogueTagUtility.Normalize(explicitChannelTag);

            if (!string.IsNullOrWhiteSpace(normalizedExplicitTag) &&
                !string.Equals(normalizedExplicitTag, normalizedDefaultTag, StringComparison.Ordinal))
            {
                return normalizedExplicitTag;
            }

            var bindingTag = ResolveBindingChannelTag(bindings, dialogueTag);
            if (!string.IsNullOrWhiteSpace(bindingTag))
                return bindingTag;

            return normalizedExplicitTag;
        }

        static string ResolveBindingChannelTag(
            IReadOnlyList<DialogueTextChannelBinding>? bindings,
            string dialogueTag)
        {
            if (bindings == null || bindings.Count == 0)
                return string.Empty;

            var normalizedDialogueTag = DialogueTagUtility.Normalize(dialogueTag);
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                    continue;

                if (!string.Equals(binding.DialogueTag, normalizedDialogueTag, StringComparison.Ordinal))
                    continue;

                var textChannelTag = binding.TextChannelTag;
                if (!string.IsNullOrWhiteSpace(textChannelTag))
                    return textChannelTag;
            }

            return string.Empty;
        }

        ResolvedCharacterEntry ResolveCharacterEntry(DialogueCharacterEntryRequest entry, int index, IDynamicContext context)
        {
            var runtimeKey = string.IsNullOrWhiteSpace(entry.CharacterId)
                ? $"character-{index}"
                : entry.CharacterId.Trim();

            CharacterDataBaseDefinition? definition = null;
            var characterDataId = 0;
            BaseRuntimeTemplatePreset? resolvedTemplate = null;

            if (_characterDataBase != null)
            {
                if (entry.CharacterDataId > 0)
                {
                    if (_characterDataBase.TryGetDefinition(entry.CharacterDataId, out var byId) && byId != null)
                    {
                        definition = byId;
                        characterDataId = byId.CharacterId;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entry.CharacterStableKey))
                {
                    if (_characterDataBase.TryGetDefinition(entry.CharacterStableKey, out var byKey) && byKey != null)
                    {
                        definition = byKey;
                        characterDataId = byKey.CharacterId;
                    }
                }
            }

            if (definition != null && string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                if (!string.IsNullOrWhiteSpace(definition.StableKey))
                    runtimeKey = definition.StableKey;
                else
                    runtimeKey = $"character-db-{definition.CharacterId}";
            }

            if (definition != null &&
                definition.RuntimeTemplateValue.TryGet(context, out BaseRuntimeTemplatePreset? definitionTemplate) &&
                definitionTemplate != null)
            {
                resolvedTemplate = definitionTemplate;
            }

            if (resolvedTemplate == null &&
                entry.RuntimeTemplate.TryGet(context, out BaseRuntimeTemplatePreset? entryTemplate) &&
                entryTemplate != null)
            {
                resolvedTemplate = entryTemplate;
            }

            return new ResolvedCharacterEntry(runtimeKey, characterDataId, definition, resolvedTemplate);
        }

        static string ResolveDisplayName(
            DialogueCharacterEntryRequest entry,
            IDynamicContext context,
            CharacterDataBaseDefinition? definition)
        {
            var explicitName = entry.DisplayName.GetOrDefault(context, string.Empty);
            if (!string.IsNullOrWhiteSpace(explicitName))
                return explicitName;

            if (definition != null &&
                definition.TryGetModule<CharacterBaseNameModulePreset>(out var baseNameModule) &&
                baseNameModule != null)
            {
                var moduleName = baseNameModule.ResolveBaseName(context);
                if (!string.IsNullOrWhiteSpace(moduleName))
                    return moduleName;
            }

            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                return definition.DisplayName;

            return string.Empty;
        }

        bool TryResolvePortraitAnimation(
            DialogueCharacterEntryRequest entry,
            CharacterDataBaseDefinition? definition,
            out IAnimationData? animationData,
            out string spriteTag,
            out AnimationDataSource? animationSource)
        {
            animationData = null;
            animationSource = entry.PortraitAnimation;
            spriteTag = string.IsNullOrWhiteSpace(entry.SpriteChannelTag)
                ? _preset.Character.DefaultSpriteChannelTag
                : DialogueTagUtility.Normalize(entry.SpriteChannelTag);

            if (entry.PortraitAnimation != null &&
                AnimationDataSource.TryGet(entry.PortraitAnimation, out var explicitAnimationData) &&
                explicitAnimationData != null)
            {
                animationData = explicitAnimationData;
                animationSource = entry.PortraitAnimation;
                return true;
            }

            if (definition != null &&
                !string.IsNullOrWhiteSpace(entry.ExpressionKey) &&
                definition.TryGetModule<CharacterExpressionModulePreset>(out var expressionModule) &&
                expressionModule != null &&
                expressionModule.TryResolve(entry.ExpressionKey, out var expressionEntry) &&
                expressionEntry != null &&
                AnimationDataSource.TryGet(expressionEntry.PortraitAnimation, out var expressionAnimationData) &&
                expressionAnimationData != null)
            {
                animationData = expressionAnimationData;
                animationSource = expressionEntry.PortraitAnimation;
                return true;
            }

            if (!entry.UseDefaultImageFallback || definition == null)
                return false;

            if (!definition.TryGetModule<CharacterDefaultImageModulePreset>(out var defaultImageModule) ||
                defaultImageModule == null)
                return false;

            if (!AnimationDataSource.TryGet(defaultImageModule.DefaultPortraitAnimation, out var fallbackAnimationData) ||
                fallbackAnimationData == null)
                return false;

            animationData = fallbackAnimationData;
            animationSource = defaultImageModule.DefaultPortraitAnimation;
            return true;
        }

        async UniTask PlayCharacterPortraitAsync(
            string characterId,
            string spriteTag,
            IAnimationData animationData,
            Game.Channel.AnimationPlayMode playMode,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(spriteTag) || animationData == null)
                return;

            var hub = ResolveCharacterSpriteHub(characterId) ?? _spriteHub;
            if (hub == null)
            {
                LogError(BuildError("DIALOGUE-160", $"Required IAnimationSpriteHubService was not found while playing a portrait. tag='{_tag}' characterId='{characterId}' spriteTag='{spriteTag}'"));
                return;
            }

            if (!hub.TryGetPlayer(spriteTag, out var player) || player == null)
            {
                LogError(BuildError("DIALOGUE-161", $"Required portrait channel '{spriteTag}' was not found. tag='{_tag}' characterId='{characterId}'"));
                return;
            }

            await player.PlayAsync(animationData, null, playMode, false, ct);
        }

        IAnimationSpriteHubService? ResolveCharacterSpriteHub(string characterId)
        {
            if (!_characterRecords.TryGetValue(characterId, out var record) || record?.Scope == null)
                return null;

            if (!TryResolveFromScopeOrAncestors(record.Scope, out IAnimationSpriteHubService? hub) || hub == null)
                return null;

            return hub;
        }

        async UniTask EnsureCharacterRuntimeSpawnedAsync(
            ResolvedCharacterEntry resolved,
            DialogueCharacterEntryRequest entry,
            IDynamicContext context,
            CancellationToken ct)
        {
            if (_activeScope == null)
                return;

            if (_characterRecords.TryGetValue(resolved.RuntimeKey, out var existing) && existing != null && existing.Resolver != null)
                return;

            if (resolved.CharacterDataId > 0 &&
                _characterDataBase != null &&
                _characterDataBase.TryGetRuntime(resolved.CharacterDataId, out var existingBinding) &&
                existingBinding != null)
            {
                var adopted = new CharacterRuntimeRecord
                {
                    CharacterId = resolved.RuntimeKey,
                    CharacterDataId = resolved.CharacterDataId,
                    Scope = existingBinding.Scope,
                    Resolver = existingBinding.Resolver,
                    Root = existingBinding.SelfTransform,
                    IsOwnedByDialogue = false,
                    KeepAliveAfterEnd = true,
                };

                if (existingBinding.Scope is RuntimeLifetimeScope adoptedRuntimeScope)
                    adopted.RuntimeScope = adoptedRuntimeScope;

                _characterRecords[resolved.RuntimeKey] = adopted;
                return;
            }

            if (!_preset.Character.EnableRuntimeSpawn)
                return;

            if (resolved.RuntimeTemplate == null)
            {
                LogError(BuildError("DIALOGUE-163", $"Required character runtime template was not found while spawning character '{resolved.RuntimeKey}'. tag='{_tag}'"));
                return;
            }

            var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(resolved.RuntimeTemplate);
            if (runtimeTemplate == null)
            {
                LogError(BuildError("DIALOGUE-164", $"Required character runtime template resolution failed while spawning character '{resolved.RuntimeKey}'. tag='{_tag}'"));
                return;
            }

            if (_runtimeSpawner == null)
            {
                LogError(BuildError("DIALOGUE-162", $"Required IRuntimeLifetimeScopeSpawnerService was not found while spawning character '{resolved.RuntimeKey}'. tag='{_tag}'"));
                return;
            }

            var parent = _preset.Character.RuntimeParent != null
                ? _preset.Character.RuntimeParent
                : _ownerTransform;

            if (parent == null)
            {
                LogError(BuildError("DIALOGUE-163", $"Required spawn parent was not found while spawning character '{resolved.RuntimeKey}'. tag='{_tag}'"));
                return;
            }

            var localPos = entry.SpawnLocalPosition.GetOrDefault(context, Vector3.zero);
            var identity = RuntimeIdentityData.CreateDefault(parent, resolved.RuntimeKey, _preset.Character.RuntimeIdentityCategory);
            identity.Kind = LifetimeScopeKind.Runtime;
            identity.TimeScaleBehavior = _activeScope.Identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;

            var spawnParams = SpawnParams.ForRuntime(
                runtimeTemplate,
                localPos,
                Quaternion.identity,
                Vector3.one,
                identity,
                parent,
                _activeScope,
                worldSpace: false,
                allowPooling: true);

            var resolver = await _runtimeSpawner.SpawnAsync(spawnParams, ct);
            if (resolver == null)
            {
                LogError(BuildError("DIALOGUE-166", $"Character runtime spawn failed for '{resolved.RuntimeKey}'. tag='{_tag}'"));
                return;
            }

            var record = new CharacterRuntimeRecord
            {
                CharacterId = resolved.RuntimeKey,
                CharacterDataId = resolved.CharacterDataId,
                Resolver = resolver,
                IsOwnedByDialogue = true,
                KeepAliveAfterEnd = resolved.Definition?.PersistentRuntime ?? false,
            };

            ResolveSpawnedIdentity(record, resolver);
            _characterRecords[resolved.RuntimeKey] = record;

            if (resolved.CharacterDataId > 0 && _characterDataBase != null)
            {
                var bindScope = record.Scope ?? _activeScope;
                if (bindScope != null)
                    _characterDataBase.TryBindRuntime(resolved.CharacterDataId, bindScope, resolver);
            }
        }

        void ResolveSpawnedIdentity(CharacterRuntimeRecord record, IObjectResolver resolver)
        {
            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                record.RuntimeScope = runtimeScope;
                record.Root = runtimeScope.transform;
                record.Scope = runtimeScope;
                return;
            }

            if (resolver.TryResolve<IScopeNode>(out var scope) && scope != null)
            {
                record.Scope = scope;
                record.Root = scope.Identity?.SelfTransform;
            }
        }

        async UniTask ReleaseAllSpawnedCharactersAsync(CancellationToken ct)
        {
            if (_characterRecords.Count == 0)
                return;

            foreach (var pair in _characterRecords)
            {
                ct.ThrowIfCancellationRequested();

                var record = pair.Value;
                if (record == null)
                    continue;

                if (!record.IsOwnedByDialogue || record.KeepAliveAfterEnd)
                    continue;

                if (record.CharacterDataId > 0)
                    _characterDataBase?.TryReleaseRuntime(record.CharacterDataId);

                await ReleaseCharacterRecordAsync(record);
            }

            _characterRecords.Clear();
        }

        static async UniTask ReleaseCharacterRecordAsync(CharacterRuntimeRecord? record)
        {
            if (record == null || record.Resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            if (record.Resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                if (runtimeScope.Resolver != null &&
                    runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                    pool != null)
                {
                    pool.Release(runtimeScope);
                    return;
                }

                UnityEngine.Object.Destroy(runtimeScope.gameObject);
                return;
            }

            if (record.Root != null)
                UnityEngine.Object.Destroy(record.Root.gameObject);
        }

        Vector3 ResolveRootTargetPosition(DialogueRootPosition rootPosition, IDynamicContext context)
        {
            if (TryResolveLayoutBounds(out var boundsRect))
            {
                var margin = Mathf.Max(0f, _preset.Layout.RootMargin.GetOrDefault(context, 0f));
                switch (rootPosition)
                {
                    case DialogueRootPosition.Top:
                        return new Vector3(boundsRect.center.x, boundsRect.yMax + margin, 0f);

                    case DialogueRootPosition.Center:
                        return new Vector3(boundsRect.center.x, boundsRect.center.y, 0f);

                    case DialogueRootPosition.Bottom:
                        return new Vector3(boundsRect.center.x, boundsRect.yMin - margin, 0f);
                }
            }

            return GetCurrentLayoutFallbackPosition();
        }

        bool TryResolveLayoutBounds(out Rect rect)
        {
            if (_visualBoundsOutput != null && _visualBoundsOutput.HasBounds)
            {
                rect = _visualBoundsOutput.LocalRect;
                return true;
            }

            if (_ownerTransform is RectTransform rootRect)
            {
                rect = rootRect.rect;
                return rect.width > 0f || rect.height > 0f;
            }

            rect = default;
            return false;
        }

        Vector3 GetCurrentLayoutFallbackPosition()
        {
            if (_ownerTransform != null)
                return _ownerTransform.localPosition;

            return Vector3.zero;
        }

        async UniTask<bool> MoveByTransformChannelAsync(string channelTag, Vector3 target, IDynamicContext context, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(channelTag))
                return false;

            if (_activeScope == null)
                return false;

            if (_transformHub == null && !TryResolveFromScopeOrAncestors(_activeScope, out _transformHub))
                return false;

            if (_transformHub == null)
            {
                LogError(BuildError("DIALOGUE-170", $"Required ITransformAnimationHubService was not found while moving layout channel '{channelTag}'. tag='{_tag}'"));
                return false;
            }

            if (!_transformHub.TryGetPlayer(channelTag, out var player) || player == null)
            {
                LogError(BuildError("DIALOGUE-171", $"Required transform channel '{channelTag}' was not found. tag='{_tag}'"));
                return false;
            }

            var duration = Mathf.Max(0f, _preset.Layout.MoveDurationSeconds.GetOrDefault(context, 0f));
            var step = new TransformAnimationPresetStep
            {
                operation = _environmentKind == TransformGridEnvironmentKind.ScreenUI
                    ? TransformAnimationOperation.AnchoredPosition
                    : TransformAnimationOperation.LocalPosition,
                duration = DynamicValueExtensions.FromLiteral(duration),
                ease = _preset.Layout.MoveEase,
                relative = false,
                fireAndForget = false,
            };

            await player.PlayStepAsync(target, step);
            return true;
        }

        IVarStore BuildCommandVars(DialogueRootPosition rootPosition)
        {
            var vars = new VarStore();
            _vars.MergeInto(vars, overwrite: true);

            vars.TrySetVariant(VarIds.GameLib.UI.DialogueChannel.Character.Element.Anchor, DynamicVariant.FromInt((int)_activeCharacterAnchor));
            vars.TrySetVariant(VarIds.GameLib.UI.DialogueChannel.Character.Element.Name, DynamicVariant.FromString(_activeCharacterName ?? string.Empty));

            if (_activeCharacterIconAnimPreset != null)
                vars.TrySetManagedRef(VarIds.GameLib.UI.DialogueChannel.Character.Element.IconAnimPreset, _activeCharacterIconAnimPreset);
            else
                vars.TryUnset(VarIds.GameLib.UI.DialogueChannel.Character.Element.IconAnimPreset);

            vars.TrySetVariant(VarIds.GameLib.UI.DialogueChannel.DialogueLayout.RootPositionType, DynamicVariant.FromInt((int)rootPosition));
            return vars;
        }

        async UniTask RunHookCommandsAsync(CommandListData? commands, string hookName, CancellationToken ct, IVarStore? vars = null)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (_activeScope == null || _commandRunner == null)
            {
                if (_activeScope != null)
                    LogError(BuildError("DIALOGUE-130", $"Required ICommandRunner was not found while executing hook '{hookName}'. tag='{_tag}'"));
                return;
            }

            var ctx = new CommandContext(
                _activeScope,
                vars ?? _vars,
                _commandRunner,
                _activeScope,
                CommandRunOptions.Default);

            CommandRunResult result;
            try
            {
                result = await _commandRunner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueChannel] Hook execution exception. tag='{_tag}' hook={hookName} message={ex.Message}");
                return;
            }

            if (result.Status == CommandRunStatus.Error)
            {
                Debug.LogWarning($"[DialogueChannel] Hook execution failed. tag='{_tag}' hook={hookName} reason={result.FailureKind} message={result.Message}");
            }
        }

        void RefreshModalRegistration()
        {
            if (_modalHub == null)
            {
                if (_preset.Input.AutoPushModalLayer && _isVisible && _isActive)
                    LogError(BuildError("DIALOGUE-140", $"Required IModalStackChannelHubService was not found for modal registration. tag='{_tag}' modalLayerKey='{_preset.Input.ModalLayerKey}'"));
                return;
            }

            var shouldRegister = _preset.Input.AutoPushModalLayer && _isVisible && _isActive;
            if (shouldRegister)
            {
                if (_modalRegistered)
                    return;

                if (_activeScope?.Resolver == null)
                {
                    LogError(BuildError("DIALOGUE-141", $"Required scope resolver was not found for modal registration. tag='{_tag}' modalLayerKey='{_preset.Input.ModalLayerKey}'"));
                    return;
                }

                if (!_activeScope.Resolver.TryResolve<IUIModalRoot>(out var root) || root == null)
                {
                    LogError(BuildError("DIALOGUE-142", $"Required IUIModalRoot was not found for modal registration. tag='{_tag}' modalLayerKey='{_preset.Input.ModalLayerKey}'"));
                    return;
                }

                _modalRoot = root;
                _modalHub.PushModal(_preset.Input.ModalLayerKey, root, _preset.Input.ModalOptions);
                _modalRegistered = true;
                return;
            }

            ReleaseModalRegistration();
        }

        void ReleaseModalRegistration()
        {
            if (!_modalRegistered || _modalHub == null || _modalRoot == null)
            {
                _modalRegistered = false;
                _modalRoot = null;
                return;
            }

            _modalHub.PopModal(_preset.Input.ModalLayerKey, _modalRoot);
            _modalRegistered = false;
            _modalRoot = null;
        }

        bool CanAcceptDialogueInput()
        {
            if (!_preset.Input.EnableInput)
                return false;
            if (!_isInputEnabled)
                return false;
            if (!_isVisible)
                return false;
            if (!_isActive)
                return false;
            if (_isChoiceInputLock)
                return false;
            return true;
        }

        void CancelActiveMessage(string reason)
        {
            var session = _activeMessageSession;
            if (session == null || session.Completed)
                return;

            session.Completed = true;
            session.Completion.TrySetResult(DialogueAdvanceResult.FromCanceled(reason));
            _activeMessageSession = null;
            _activeTypewriterPlayers.Clear();
            _typewriterState = DialogueTypewriterState.Idle;
            PublishSnapshot(force: true);
        }

        DialogueChannelSnapshot BuildSnapshot()
        {
            return new DialogueChannelSnapshot(
                _tag,
                _isVisible,
                _isActive,
                _isInputEnabled,
                _dialogueCount,
                _typewriterState,
                _choiceState,
                _activeCharacterAnchor);
        }

        void PublishSnapshot(bool force)
        {
            var snapshot = BuildSnapshot();
            if (!force && SnapshotEquals(snapshot, _lastSnapshot))
                return;

            _lastSnapshot = snapshot;
            OnStateChanged?.Invoke(snapshot);
        }

        static bool SnapshotEquals(in DialogueChannelSnapshot a, in DialogueChannelSnapshot b)
        {
            return string.Equals(a.Tag, b.Tag, StringComparison.Ordinal)
                   && a.IsVisible == b.IsVisible
                   && a.IsActive == b.IsActive
                   && a.IsInputEnabled == b.IsInputEnabled
                   && a.DialogueCount == b.DialogueCount
                   && a.TypewriterState == b.TypewriterState
                   && a.ChoiceState == b.ChoiceState
                   && a.ActiveCharacterAnchor == b.ActiveCharacterAnchor;
        }

        void Trace(string message)
        {
            if (!_enableDebugLog)
                return;

            Debug.Log($"[DialogueChannel] {message}");
        }

        static string BuildError(string code, string message)
        {
            return $"[{code}] {message}";
        }

        void LogError(string message)
        {
            Debug.LogError($"[DialogueChannel] {message}");
        }

        static bool TryResolveFromScopeOrAncestors<T>(IScopeNode? scope, out T? value) where T : class
        {
            value = null;
            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<T>(out var resolved) && resolved != null)
                {
                    value = resolved;
                    return true;
                }
            }

            return false;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}
