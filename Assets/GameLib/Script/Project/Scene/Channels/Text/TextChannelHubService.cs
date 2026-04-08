// Game.Channel.TextChannelHubService.cs

#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.MaterialFx;
using Game.Scalar;
using Game.Times;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VNext = Game.Commands.VNext;

namespace Game.Channel
{
    public interface ITextChannelHubService : IChannelHubService
    {
        IReadOnlyList<ITextChannelPlayer> Players { get; }

        ITextChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out ITextChannelPlayer player);

        bool RegisterDynamicBinding(in TextDynamicBindingRegisterRequest request);
        bool UnregisterDynamicBinding(string tag);
        void UnregisterAllDynamicBindings();
        bool HasDynamicBinding(string tag);
    }

    /// <summary>
    /// Text チャネルの Hub。
    /// - tag -> def/player 管理
    /// - dynamic text binding 管理（event + poll / poll-only）
    /// - lifecycle は IScopeAcquireHandler / IScopeReleaseHandler に寄せる
    /// </summary>
    public sealed class TextChannelHubService :
        ITextChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IDisposable
    {
        const string DefaultTag = "default";

        sealed class TextDynamicBindingState : IDisposable
        {
            public readonly string Tag;
            public readonly ITextChannelPlayer Player;
            public readonly DynamicValue<string> Source;
            public readonly TextDynamicBindingPlayMode PlayMode;
            public readonly SetTextSettings CounterSettings;
            public readonly IVarStore SnapshotVars;
            public readonly IVarStore WorkingVars;
            public readonly VNext.CommandContext EvaluationContext;
            public readonly IScopeNode OwnerActor;
            public readonly int PollIntervalFrames;

            public TextDynamicBindingWatchMode WatchMode;
            public string LastText;
            public bool Dirty;
            public bool RequiresFullRefresh;
            public int NextPollFrame;

            public readonly HashSet<int> DependentVarIds = new();
            public readonly HashSet<int> TrackedVarIds = new();
            public readonly HashSet<int> PendingRefreshVarIds = new();
            public readonly Dictionary<int, string> StableKeyByVarId = new();

            public int DirectVarStoreVarId;
            public int DirectBlackboardVarId;
            public bool DirectBlackboardIsGlobal;
            public ScalarKey DirectScalarKey;
            public bool UseDirectScalarSubscription;
            public bool IsRichTextRefService;

            public IVarStore? OwnerVarStore;
            public IBlackboardService? OwnerBlackboard;
            public IBaseScalarService? OwnerScalar;

            public Action<int>? OwnerVarChangedHandler;
            public IVarStore? ObservedOwnerVarStore;
            public Action<int>? OwnerBlackboardChangedHandler;
            public IVarStore? ObservedOwnerBlackboardVarStore;
            public IDisposable? DirectScalarSubscription;

            public TextDynamicBindingState(
                string tag,
                ITextChannelPlayer player,
                DynamicValue<string> source,
                TextDynamicBindingPlayMode playMode,
                in SetTextSettings counterSettings,
                IVarStore snapshotVars,
                IVarStore workingVars,
                VNext.CommandContext evaluationContext,
                IScopeNode ownerActor,
                int pollIntervalFrames)
            {
                Tag = tag;
                Player = player;
                Source = source;
                PlayMode = playMode;
                CounterSettings = counterSettings;
                SnapshotVars = snapshotVars;
                WorkingVars = workingVars;
                EvaluationContext = evaluationContext;
                OwnerActor = ownerActor;
                PollIntervalFrames = Mathf.Max(1, pollIntervalFrames);

                WatchMode = TextDynamicBindingWatchMode.EventAndPoll;
                LastText = player.Target != null ? player.Target.text ?? string.Empty : string.Empty;
                Dirty = true;
                RequiresFullRefresh = true;
                NextPollFrame = Time.frameCount + PollIntervalFrames;

                DirectVarStoreVarId = 0;
                DirectBlackboardVarId = 0;
                DirectBlackboardIsGlobal = false;
                DirectScalarKey = default;
                UseDirectScalarSubscription = false;
                IsRichTextRefService = false;
            }

            public void Dispose()
            {
                if (ObservedOwnerVarStore != null && OwnerVarChangedHandler != null)
                {
                    try { ObservedOwnerVarStore.OnVarChanged -= OwnerVarChangedHandler; }
                    catch { }
                }
                ObservedOwnerVarStore = null;
                OwnerVarChangedHandler = null;

                if (ObservedOwnerBlackboardVarStore != null && OwnerBlackboardChangedHandler != null)
                {
                    try { ObservedOwnerBlackboardVarStore.OnVarChanged -= OwnerBlackboardChangedHandler; }
                    catch { }
                }
                ObservedOwnerBlackboardVarStore = null;
                OwnerBlackboardChangedHandler = null;

                try { DirectScalarSubscription?.Dispose(); }
                catch { }
                DirectScalarSubscription = null;
            }
        }

        readonly Dictionary<string, ITextChannelPlayer> _players = new(StringComparer.Ordinal);
        readonly Dictionary<string, TextChannelDef> _defsByTag = new(StringComparer.Ordinal);
        readonly List<ITextChannelPlayer> _playerList = new();
        readonly List<TextChannelDef> _defOrder = new();

        readonly List<ChannelDefBase> _defsSnapshot = new();
        bool _defsDirty = true;

        readonly Dictionary<string, TextDynamicBindingState> _bindingsByTag = new(StringComparer.Ordinal);
        readonly List<TextDynamicBindingState> _bindingList = new();
        readonly HashSet<string> _bindingWarnKeys = new(StringComparer.Ordinal);

        readonly IScopeNode _ownerScope;
        readonly IMaterialFxServiceFactory? _materialFxFactory;
        readonly ILTSIdentityService? _identity;

        bool _isAcquired;
        bool _disposed;

        public IReadOnlyList<ITextChannelPlayer> Players => _playerList;
        public IScopeNode OwnerScope => _ownerScope;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    for (int i = 0; i < _defOrder.Count; i++)
                        _defsSnapshot.Add(_defOrder[i]);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public TextChannelHubService(
            TextChannelDef[] channelDefs,
            IScopeNode ownerScope,
            IMaterialFxServiceFactory? materialFxFactory = null,
            ILTSIdentityService? identity = null)
        {
            _ownerScope = ownerScope;
            _materialFxFactory = materialFxFactory;
            _identity = identity;

            if (channelDefs == null)
                return;

            for (int i = 0; i < channelDefs.Length; i++)
                RegisterDefinitionInternal(channelDefs[i], overwrite: false);

            // Build once eagerly so command execution can resolve channels even when acquire timing is delayed.
            BuildPlayersFromDefinitions();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_ownerScope, scope))
                return;

            if (_disposed)
                return;

            if (_isAcquired && !isReset)
                return;

            if (_isAcquired || isReset)
                ReleaseRuntimeState();

            BuildPlayersFromDefinitions();
            _isAcquired = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_ownerScope, scope))
                return;

            ReleaseRuntimeState();
            _isAcquired = false;
        }

        void ITickable.Tick()
        {
            if (_disposed || !_isAcquired)
                return;

            if (_bindingList.Count == 0)
                return;

            var frame = Time.frameCount;
            for (int i = _bindingList.Count - 1; i >= 0; i--)
            {
                var state = _bindingList[i];
                if (state == null)
                    continue;

                if (!IsScopeAlive(state.OwnerActor) || !_players.ContainsKey(state.Tag))
                {
                    RemoveBindingAt(i);
                    continue;
                }

                var pollDue = frame >= state.NextPollFrame;
                if (!state.Dirty && !pollDue)
                    continue;

                if (pollDue)
                    state.RequiresFullRefresh = true;

                RefreshBindingVarsForEvaluation(state);

                if (!TryEvaluateBindingText(state, out var resolvedText))
                {
                    state.Dirty = false;
                    state.PendingRefreshVarIds.Clear();
                    state.RequiresFullRefresh = false;
                    state.NextPollFrame = frame + state.PollIntervalFrames;
                    continue;
                }

                resolvedText = TextChannelTextEvaluationUtility.EvaluateRichTextTemplate(state.EvaluationContext, resolvedText);

                if (!string.Equals(state.LastText, resolvedText, StringComparison.Ordinal))
                {
                    ApplyBindingText(state, resolvedText);
                    state.LastText = resolvedText;
                }

                state.Dirty = false;
                state.PendingRefreshVarIds.Clear();
                state.RequiresFullRefresh = false;
                state.NextPollFrame = frame + state.PollIntervalFrames;
            }
        }

        public ITextChannelPlayer GetPlayer(string tag)
        {
            tag = NormalizeTag(tag);
            EnsurePlayerBuilt(tag);
            if (_players.TryGetValue(tag, out var player))
                return player;

            throw new KeyNotFoundException($"[TextChannelHub] Channel '{tag}' not found.");
        }

        public bool TryGetPlayer(string tag, out ITextChannelPlayer player)
        {
            tag = NormalizeTag(tag);
            EnsurePlayerBuilt(tag);
            return _players.TryGetValue(tag, out player);
        }

        public bool HasDynamicBinding(string tag)
        {
            tag = NormalizeTag(tag);
            return _bindingsByTag.ContainsKey(tag);
        }

        public bool RegisterDynamicBinding(in TextDynamicBindingRegisterRequest request)
        {
            if (_disposed)
                return false;

            if (request.SourceContext == null)
                return false;

            var tag = NormalizeTag(request.ChannelTag);
            EnsurePlayerBuilt(tag);
            if (!_players.TryGetValue(tag, out var player) || player == null)
            {
                WarnBindingOnce($"{tag}:player-missing", $"[TextChannelHub] Dynamic bind rejected: player not found. tag='{tag}'");
                return false;
            }

            if (!request.Source.HasSource)
            {
                WarnBindingOnce($"{tag}:source-missing", $"[TextChannelHub] Dynamic bind rejected: source is empty. tag='{tag}'");
                return false;
            }

            var ownerActor = IsScopeAlive(request.OwnerActor)
                ? request.OwnerActor
                : request.SourceContext.Actor ?? request.SourceContext.Scope;

            if (!IsScopeAlive(ownerActor))
            {
                WarnBindingOnce($"{tag}:owner-invalid", $"[TextChannelHub] Dynamic bind rejected: owner actor is invalid. tag='{tag}'");
                return false;
            }

            UnregisterDynamicBinding(tag);

            if (!TryCreateBindingState(in request, tag, player, ownerActor, out var state, out var rejectReason))
            {
                if (!string.IsNullOrEmpty(rejectReason))
                    WarnBindingOnce($"{tag}:reject:{rejectReason}", $"[TextChannelHub] Dynamic bind rejected. tag='{tag}' reason={rejectReason}");
                return false;
            }

            _bindingsByTag[tag] = state!;
            _bindingList.Add(state!);
            return true;
        }

        public bool UnregisterDynamicBinding(string tag)
        {
            tag = NormalizeTag(tag);
            if (!_bindingsByTag.TryGetValue(tag, out var state) || state == null)
                return false;

            _bindingsByTag.Remove(tag);
            _bindingList.Remove(state);
            state.Dispose();
            return true;
        }

        public void UnregisterAllDynamicBindings()
        {
            for (int i = _bindingList.Count - 1; i >= 0; i--)
            {
                var state = _bindingList[i];
                try { state?.Dispose(); }
                catch { }
            }

            _bindingList.Clear();
            _bindingsByTag.Clear();
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            tag = NormalizeTag(tag);
            if (_defsByTag.TryGetValue(tag, out var textDef))
            {
                def = textDef;
                return true;
            }

            def = default!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not TextChannelDef textDef)
                return false;

            return RegisterDefinitionInternal(textDef, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            tag = NormalizeTag(tag);
            return RemoveChannelInternal(tag, removeDefinition: true);
        }

        bool RegisterDefinitionInternal(TextChannelDef def, bool overwrite)
        {
            if (def == null)
                return false;

            var tag = NormalizeTag(def.Tag);
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (_defsByTag.ContainsKey(tag))
            {
                if (!overwrite)
                    return false;

                _defsByTag[tag] = def;
                ReplaceDefOrder(tag, def);
                _defsDirty = true;

                if (_isAcquired)
                    TryCreateOrReplacePlayer(tag, def, overwriteExisting: true);
                return true;
            }

            _defsByTag[tag] = def;
            _defOrder.Add(def);
            _defsDirty = true;

            if (_isAcquired)
                TryCreateOrReplacePlayer(tag, def, overwriteExisting: false);

            return true;
        }

        bool EnsurePlayerBuilt(string tag)
        {
            if (_disposed)
                return false;

            if (_players.ContainsKey(tag))
                return true;

            if (!_defsByTag.TryGetValue(tag, out var def) || def == null)
                return false;

            return TryCreateOrReplacePlayer(tag, def, overwriteExisting: false);
        }

        void ReplaceDefOrder(string tag, TextChannelDef replacement)
        {
            for (int i = 0; i < _defOrder.Count; i++)
            {
                var existing = _defOrder[i];
                if (existing != null && string.Equals(NormalizeTag(existing.Tag), tag, StringComparison.Ordinal))
                {
                    _defOrder[i] = replacement;
                    return;
                }
            }

            _defOrder.Add(replacement);
        }

        void BuildPlayersFromDefinitions()
        {
            ClearPlayersOnly();

            for (int i = 0; i < _defOrder.Count; i++)
            {
                var def = _defOrder[i];
                if (def == null)
                    continue;

                var tag = NormalizeTag(def.Tag);
                TryCreateOrReplacePlayer(tag, def, overwriteExisting: false);
            }
        }

        bool TryCreateOrReplacePlayer(string tag, TextChannelDef def, bool overwriteExisting)
        {
            if (def == null)
                return false;

            var integrityOwner = _ownerScope.Identity?.SelfTransform ?? (_ownerScope as Component)?.transform;
            if (integrityOwner == null)
                return false;

            def.EnsureIntegrity(integrityOwner);

            var targetText = def.Text;
            if (targetText == null)
                return false;

            if (_players.ContainsKey(tag))
            {
                if (!overwriteExisting)
                    return false;

                RemoveChannelInternal(tag, removeDefinition: false);
            }

            var timeScaleBehavior = _identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;
            var counterSettings = new SetTextSettings
            {
                UseCounter = def.UseCounter,
                CounterEase = def.CounterEase,
                CounterDurationSeconds = def.CounterDurationSeconds,
                CounterUseUnscaledTime = def.CounterUseUnscaledTime,
            };

            var player = new TextChannelPlayer(
                tag,
                targetText,
                def.UseRichTextAnimator,
                def.UseTypewriter,
                def.UseCounter,
                true,
                _ownerScope,
                timeScaleBehavior,
                _materialFxFactory,
                def.MaterialFxPresetEntries,
                counterSettings);

            _players[tag] = player;
            _playerList.Add(player);
            return true;
        }

        bool RemoveChannelInternal(string tag, bool removeDefinition)
        {
            var changed = false;

            if (UnregisterDynamicBinding(tag))
                changed = true;

            if (_players.TryGetValue(tag, out var player) && player != null)
            {
                try { player.Dispose(); }
                catch { }

                _players.Remove(tag);
                _playerList.Remove(player);
                changed = true;
            }

            if (removeDefinition)
            {
                if (_defsByTag.Remove(tag))
                    changed = true;

                for (int i = _defOrder.Count - 1; i >= 0; i--)
                {
                    var def = _defOrder[i];
                    if (def != null && string.Equals(NormalizeTag(def.Tag), tag, StringComparison.Ordinal))
                    {
                        _defOrder.RemoveAt(i);
                        changed = true;
                    }
                }

                if (changed)
                    _defsDirty = true;
            }

            return changed;
        }

        bool TryCreateBindingState(
            in TextDynamicBindingRegisterRequest request,
            string tag,
            ITextChannelPlayer player,
            IScopeNode ownerActor,
            out TextDynamicBindingState? state,
            out string rejectReason)
        {
            state = null;
            rejectReason = string.Empty;

            var snapshotVars = request.SnapshotVars ?? NullVarStore.Instance;
            var workingVars = new VarStore();
            snapshotVars.MergeInto(workingVars, overwrite: true);

            var sourceContext = request.SourceContext;
            var evalContext = new VNext.CommandContext(
                sourceContext.Scope,
                workingVars,
                sourceContext.Runner,
                ownerActor,
                sourceContext.Options,
                sourceContext.CommandRootScope ?? sourceContext.Scope,
                sourceContext.RootActor ?? ownerActor,
                sourceContext.CallerActor ?? ownerActor);
            CopyContextSlots(sourceContext, evalContext);

            var bindingState = new TextDynamicBindingState(
                tag,
                player,
                request.Source,
                request.PlayMode,
                request.CounterSettings,
                snapshotVars,
                workingVars,
                evalContext,
                ownerActor,
                request.PollIntervalFrames);

            if (!ConfigureBindingSourceHints(bindingState, out rejectReason))
            {
                bindingState.Dispose();
                return false;
            }

            TryResolveOwnerServices(bindingState);
            RefreshBindingVarsForEvaluation(bindingState);
            AttachBindingSubscriptions(bindingState);

            state = bindingState;
            return true;
        }

        bool ConfigureBindingSourceHints(TextDynamicBindingState state, out string rejectReason)
        {
            rejectReason = string.Empty;
            bool hasEventCandidate = false;
            bool forcePollOnly = false;

            if (state.Source.TryGetSource<RichTextSource>(out var richTextSource) &&
                richTextSource.SourceMode == RichTextSourceMode.RefService)
            {
                state.IsRichTextRefService = true;
                forcePollOnly = true;
            }

            if (state.Source.TryGetSource<VarStoreSource>(out var varStoreSource))
            {
                var varId = varStoreSource.VarId;
                if (varId != 0)
                {
                    state.DirectVarStoreVarId = varId;
                    state.TrackedVarIds.Add(varId);
                    hasEventCandidate = true;
                }
            }

            if (state.Source.TryGetSource<SelfBlackboardSource>(out var selfBlackboardSource))
            {
                if (selfBlackboardSource.BlackboardVarId != 0)
                {
                    state.DirectBlackboardVarId = selfBlackboardSource.BlackboardVarId;
                    state.TrackedVarIds.Add(selfBlackboardSource.BlackboardVarId);
                }

                if (selfBlackboardSource.ReadScope == BlackboardReadScope.Global)
                {
                    state.DirectBlackboardIsGlobal = true;
                    forcePollOnly = true;
                }
                else if (selfBlackboardSource.BlackboardVarId != 0)
                {
                    hasEventCandidate = true;
                }
            }
            else if (state.Source.TryGetSource<OtherBlackboardSource>(out var otherBlackboardSource))
            {
                if (otherBlackboardSource.BlackboardVarId != 0)
                {
                    state.DirectBlackboardVarId = otherBlackboardSource.BlackboardVarId;
                    state.TrackedVarIds.Add(otherBlackboardSource.BlackboardVarId);
                }

                if (otherBlackboardSource.ReadScope == BlackboardReadScope.Global)
                {
                    state.DirectBlackboardIsGlobal = true;
                    forcePollOnly = true;
                }
                else if (otherBlackboardSource.BlackboardVarId != 0 &&
                         otherBlackboardSource.TargetActor.Kind == VNext.ActorSourceKind.Current)
                {
                    hasEventCandidate = true;
                }
            }

            if (state.Source.TryGetSource<SelfScalarSource>(out var selfScalarSource))
            {
                if (!IsScalarKeyEmpty(selfScalarSource.ScalarKey))
                {
                    state.DirectScalarKey = selfScalarSource.ScalarKey;
                    state.UseDirectScalarSubscription = selfScalarSource.TargetActorSource.Kind == VNext.ActorSourceKind.Current;
                    if (state.UseDirectScalarSubscription)
                        hasEventCandidate = true;
                }
            }
            else if (state.Source.TryGetSource<OtherScalarSource>(out var otherScalarSource))
            {
                if (!IsScalarKeyEmpty(otherScalarSource.ScalarKey))
                {
                    state.DirectScalarKey = otherScalarSource.ScalarKey;
                    state.UseDirectScalarSubscription = otherScalarSource.TargetActorSource.Kind == VNext.ActorSourceKind.Current;
                    if (state.UseDirectScalarSubscription)
                        hasEventCandidate = true;
                }
            }

            var dependentKeys = state.Source.GetDependentKeys();
            if (dependentKeys != null)
            {
                for (int i = 0; i < dependentKeys.Count; i++)
                {
                    var key = dependentKeys[i];
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                        continue;

                    state.DependentVarIds.Add(varId);
                    state.TrackedVarIds.Add(varId);
                    if (!state.StableKeyByVarId.ContainsKey(varId))
                        state.StableKeyByVarId[varId] = key;
                }

                if (state.DependentVarIds.Count > 0)
                    hasEventCandidate = true;
            }

            if (state.TrackedVarIds.Count == 0 && IsLiteralLike(state.Source))
            {
                rejectReason = "literal-source";
                return false;
            }

            foreach (var varId in state.TrackedVarIds)
            {
                if (state.StableKeyByVarId.ContainsKey(varId))
                    continue;

                if (VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
                    state.StableKeyByVarId[varId] = stableKey;
            }

            state.WatchMode = (forcePollOnly || !hasEventCandidate)
                ? TextDynamicBindingWatchMode.PollOnly
                : TextDynamicBindingWatchMode.EventAndPoll;

            return true;
        }

        void AttachBindingSubscriptions(TextDynamicBindingState state)
        {
            if (state.WatchMode != TextDynamicBindingWatchMode.EventAndPoll)
                return;

            TryResolveOwnerServices(state);

            if (state.TrackedVarIds.Count > 0 && state.OwnerVarStore != null)
            {
                state.ObservedOwnerVarStore = state.OwnerVarStore;
                state.OwnerVarChangedHandler = varId => OnOwnerVarChanged(state, varId);
                state.ObservedOwnerVarStore.OnVarChanged += state.OwnerVarChangedHandler;
            }

            if (state.TrackedVarIds.Count > 0 && state.OwnerBlackboard?.LocalVars != null)
            {
                state.ObservedOwnerBlackboardVarStore = state.OwnerBlackboard.LocalVars;
                state.OwnerBlackboardChangedHandler = varId => OnOwnerBlackboardVarChanged(state, varId);
                state.ObservedOwnerBlackboardVarStore.OnVarChanged += state.OwnerBlackboardChangedHandler;
            }

            if (state.UseDirectScalarSubscription &&
                state.OwnerScalar != null &&
                !IsScalarKeyEmpty(state.DirectScalarKey))
            {
                try
                {
                    state.DirectScalarSubscription = state.OwnerScalar.GlobalSubscribe(
                        state.DirectScalarKey,
                        args => OnDirectScalarChanged(state, args));
                }
                catch (Exception ex)
                {
                    WarnBindingOnce(
                        $"{state.Tag}:scalar-subscribe-failed:{state.DirectScalarKey.Name}",
                        $"[TextChannelHub] Scalar subscribe failed. tag='{state.Tag}' key='{state.DirectScalarKey.FormatLabel()}' message={ex.Message}");
                }
            }
        }

        void OnOwnerVarChanged(TextDynamicBindingState state, int varId)
        {
            if (_disposed || state == null)
                return;

            if (varId == 0 || !state.TrackedVarIds.Contains(varId))
                return;

            state.PendingRefreshVarIds.Add(varId);
            state.Dirty = true;
        }

        void OnOwnerBlackboardVarChanged(TextDynamicBindingState state, int varId)
        {
            if (_disposed || state == null)
                return;

            if (varId == 0 || !state.TrackedVarIds.Contains(varId))
                return;

            state.PendingRefreshVarIds.Add(varId);
            state.Dirty = true;
        }

        void OnDirectScalarChanged(TextDynamicBindingState state, ScalarValueChangedArgs args)
        {
            if (_disposed || state == null)
                return;

            state.Dirty = true;

            var name = args.Key.Name;
            if (string.IsNullOrEmpty(name))
                return;

            if (VarIdResolver.TryResolve(name, out var fullVarId) && fullVarId != 0)
                state.PendingRefreshVarIds.Add(fullVarId);

            var leafName = ExtractLeaf(name);
            if (!string.IsNullOrEmpty(leafName) &&
                !string.Equals(leafName, name, StringComparison.Ordinal) &&
                VarIdResolver.TryResolve(leafName, out var leafVarId) &&
                leafVarId != 0)
            {
                state.PendingRefreshVarIds.Add(leafVarId);
            }
        }

        void RefreshBindingVarsForEvaluation(TextDynamicBindingState state)
        {
            if (state.TrackedVarIds.Count == 0)
                return;

            TryResolveOwnerServices(state);

            if (state.RequiresFullRefresh || state.PendingRefreshVarIds.Count == 0)
            {
                foreach (var varId in state.TrackedVarIds)
                    RefreshTrackedVarValue(state, varId);
                return;
            }

            foreach (var varId in state.PendingRefreshVarIds)
                RefreshTrackedVarValue(state, varId);
        }

        void RefreshTrackedVarValue(TextDynamicBindingState state, int varId)
        {
            if (varId == 0)
                return;

            // まず command 時スナップショットを基底として復元する。
            TryCopyVarFromStore(state.SnapshotVars, state.WorkingVars, varId, unsetIfMissing: true);

            if (state.OwnerVarStore != null &&
                TryCopyVarFromStore(state.OwnerVarStore, state.WorkingVars, varId, unsetIfMissing: false))
                return;

            if (state.OwnerBlackboard != null)
            {
                var localVars = state.OwnerBlackboard.LocalVars;
                if (localVars != null &&
                    TryCopyVarFromStore(localVars, state.WorkingVars, varId, unsetIfMissing: false))
                    return;

                if (state.OwnerBlackboard.TryGlobalGetVariant(varId, out var globalVariant))
                {
                    state.WorkingVars.TrySetVariant(varId, globalVariant);
                    return;
                }
            }

            if (state.OwnerScalar == null)
                return;

            if (!TryResolveStableKey(state, varId, out var stableKey))
                return;

            var scalarKey = new ScalarKey(stableKey);
            if (state.OwnerScalar.GlobalTryGet(scalarKey, out var scalarValue) ||
                state.OwnerScalar.LocalTryGet(scalarKey, out scalarValue))
            {
                state.WorkingVars.TrySetVariant(varId, DynamicVariant.FromFloat(scalarValue));
                return;
            }

            var leafKey = ExtractLeaf(stableKey);
            if (string.IsNullOrEmpty(leafKey) || string.Equals(leafKey, stableKey, StringComparison.Ordinal))
                return;

            var leafScalarKey = new ScalarKey(leafKey);
            if (state.OwnerScalar.GlobalTryGet(leafScalarKey, out var leafValue) ||
                state.OwnerScalar.LocalTryGet(leafScalarKey, out leafValue))
            {
                state.WorkingVars.TrySetVariant(varId, DynamicVariant.FromFloat(leafValue));
            }
        }

        bool TryEvaluateBindingText(TextDynamicBindingState state, out string text)
        {
            text = string.Empty;
            try
            {
                text = state.Source.Resolve(state.EvaluationContext) ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                WarnBindingOnce(
                    $"{state.Tag}:evaluate-exception:{state.Source.SourceTypeName}",
                    $"[TextChannelHub] Dynamic bind evaluate failed. tag='{state.Tag}' source={state.Source.SourceTypeName} message={ex.Message}");
                return false;
            }
        }

        static void ApplyBindingText(TextDynamicBindingState state, string text)
        {
            if (state.PlayMode == TextDynamicBindingPlayMode.Counter)
            {
                var settings = state.CounterSettings;
                settings.UseCounter = true;
                state.Player.SetText(text, TextPlayMode.Count, settings);
                return;
            }

            state.Player.SetText(text, TextPlayMode.Instant);
        }

        void TryResolveOwnerServices(TextDynamicBindingState state)
        {
            if (state.OwnerVarStore == null &&
                state.OwnerActor?.Resolver != null &&
                state.OwnerActor.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                state.OwnerVarStore = vars;
            }

            if (state.OwnerBlackboard == null &&
                state.OwnerActor?.Resolver != null &&
                state.OwnerActor.Resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null)
            {
                state.OwnerBlackboard = blackboard;
            }

            if (state.OwnerScalar == null)
                state.OwnerScalar = ResolveScalarService(state.OwnerActor);
        }

        static IBaseScalarService? ResolveScalarService(IScopeNode? origin)
        {
            for (var node = origin; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IBaseScalarService>(out var scalarService) && scalarService != null)
                    return scalarService;
            }

            return null;
        }

        static bool TryCopyVarFromStore(IVarStore source, IVarStore destination, int varId, bool unsetIfMissing)
        {
            if (source == null || destination == null || varId == 0)
                return false;

            if (!source.Contains(varId))
            {
                if (unsetIfMissing)
                    destination.TryUnset(varId);
                return false;
            }

            var kind = source.GetVarKind(varId);
            if (kind == Game.Common.ValueKind.ManagedRef)
            {
                if (source.TryGetManagedRef(varId, out var managed) && managed != null)
                {
                    destination.TrySetManagedRef(varId, managed);
                    return true;
                }

                if (unsetIfMissing)
                    destination.TryUnset(varId);
                return false;
            }

            if (source.TryGetVariant(varId, out var variant))
            {
                if (variant.Kind == Game.Common.ValueKind.Null)
                    destination.TryUnset(varId);
                else
                    destination.TrySetVariant(varId, variant);
                return true;
            }

            if (unsetIfMissing)
                destination.TryUnset(varId);
            return false;
        }

        static bool TryResolveStableKey(TextDynamicBindingState state, int varId, out string stableKey)
        {
            stableKey = string.Empty;
            if (varId == 0)
                return false;

            if (state.StableKeyByVarId.TryGetValue(varId, out stableKey) && !string.IsNullOrEmpty(stableKey))
                return true;

            if (!VarIdResolver.TryGetStableKey(varId, out stableKey) || string.IsNullOrEmpty(stableKey))
                return false;

            state.StableKeyByVarId[varId] = stableKey;
            return true;
        }

        static bool IsLiteralLike(in DynamicValue<string> source)
        {
            if (source.TryGetSource<LiteralStringSource>(out _))
                return true;
            if (source.TryGetSource<LiteralSource>(out _))
                return true;

            return string.Equals(source.SourceTypeName, "Literal", StringComparison.Ordinal);
        }

        static bool IsScalarKeyEmpty(in ScalarKey key)
        {
            return key.Id == 0 && string.IsNullOrEmpty(key.Name);
        }

        static string ExtractLeaf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var index = path.LastIndexOfAny(new[] { '.', '/', '\\' });
            if (index < 0 || index + 1 >= path.Length)
                return path;

            return path[(index + 1)..];
        }

        void ReleaseRuntimeState()
        {
            UnregisterAllDynamicBindings();
            ClearPlayersOnly();
        }

        void ClearPlayersOnly()
        {
            for (int i = _playerList.Count - 1; i >= 0; i--)
            {
                var player = _playerList[i];
                try { player?.Dispose(); }
                catch { }
            }

            _playerList.Clear();
            _players.Clear();
        }

        void RemoveBindingAt(int index)
        {
            if (index < 0 || index >= _bindingList.Count)
                return;

            var state = _bindingList[index];
            _bindingList.RemoveAt(index);
            if (state != null)
            {
                _bindingsByTag.Remove(state.Tag);
                try { state.Dispose(); }
                catch { }
            }
        }

        void WarnBindingOnce(string key, string message)
        {
            if (_bindingWarnKeys.Add(key))
                Debug.LogWarning(message);
        }

        static bool IsScopeAlive(IScopeNode? scope)
        {
            if (scope == null)
                return false;

            if (scope is UnityEngine.Object unityObject && !unityObject)
                return false;

            return true;
        }

        static void CopyContextSlots(VNext.CommandContext source, VNext.CommandContext destination)
        {
            destination.SetScope(VNext.CommandLtsSlot.ContextA, source.GetScope(VNext.CommandLtsSlot.ContextA));
            destination.SetScope(VNext.CommandLtsSlot.ContextB, source.GetScope(VNext.CommandLtsSlot.ContextB));
            destination.SetScope(VNext.CommandLtsSlot.ContextC, source.GetScope(VNext.CommandLtsSlot.ContextC));
            destination.SetScope(VNext.CommandLtsSlot.ContextD, source.GetScope(VNext.CommandLtsSlot.ContextD));
        }

        static string NormalizeTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? DefaultTag : tag;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseRuntimeState();

            _defsByTag.Clear();
            _defOrder.Clear();
            _defsSnapshot.Clear();
            _defsDirty = true;
            _isAcquired = false;
        }
    }
}
