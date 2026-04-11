#nullable enable

using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using UnityEngine;
using VContainer;

namespace Game.Conversation
{
    public interface IConversationRuntimeSession
    {
        string Tag { get; }
        bool IsActive { get; }
        ConversationFlowPreset FlowPreset { get; }
        ActorSource DialogueChannelSource { get; }
        string DialogueChannelTag { get; }
        int CurrentNodeId { get; }
        int LastCompletedNodeId { get; }
        int TurnCount { get; }
        int LastSelectedIndex { get; }
        int ChoiceCount { get; }
        IReadOnlyList<ConversationChoiceHistoryEntry> ChoiceHistory { get; }
        ConversationSessionSnapshot Snapshot { get; }

        bool TryGetCurrentNode(out ConversationNodePresetBase? node);
        bool TryGetNode(int nodeId, out ConversationNodePresetBase? node);
        bool TrySetCurrentNode(int nodeId);
        bool TryResolveDialogueTag(ConversationCharacterSlot slot, out string dialogueTag);

        void IncrementTurn();
        void MarkNodeCompleted(int nodeId);
        void RecordChoice(int nodeId, int selectedIndex, long timestampUtcTicks);
        void End(ConversationSessionEndKind endKind, string message);
    }

    public interface IConversationChannelHubService
    {
        int DefinitionCount { get; }
        bool Contains(string tag);
        bool Unregister(string tag);

        bool IsActive(string tag);
        bool TryGetSession(string tag, out IConversationRuntimeSession? session);
        bool TryGetSnapshot(string tag, out ConversationSessionSnapshot snapshot);

        bool TryStartSession(string tag, ConversationFlowPreset preset, out IConversationRuntimeSession? session, out string message);
        bool TryEndSession(string tag, ConversationSessionEndKind endKind, string message, out ConversationSessionSnapshot snapshot);
    }

    public sealed class ConversationChannelHubService :
        IConversationChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly ConversationChannelHubMB _mb;
        readonly Dictionary<string, ConversationChannelDefinition> _definitions = new(StringComparer.Ordinal);
        readonly Dictionary<string, ConversationRuntimeSession> _sessions = new(StringComparer.Ordinal);

        string _activeSessionTag = string.Empty;
        bool _isAcquired;

        public int DefinitionCount => _definitions.Count;

        public ConversationChannelHubService(IScopeNode owner, ConversationChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _isAcquired = true;
            RebuildDefinitions();
            ReleaseAllSessions();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            ReleaseAllSessions();
            _definitions.Clear();
            _activeSessionTag = string.Empty;
            _isAcquired = false;
        }

        public bool Contains(string tag)
        {
            return _definitions.ContainsKey(ConversationTagUtility.Normalize(tag));
        }

        public bool Unregister(string tag)
        {
            var normalized = ConversationTagUtility.Normalize(tag);

            if (string.Equals(_activeSessionTag, normalized, StringComparison.Ordinal))
                return false;

            var removed = _definitions.Remove(normalized);
            return removed;
        }

        public bool IsActive(string tag)
        {
            var normalized = ConversationTagUtility.Normalize(tag);
            return !string.IsNullOrEmpty(_activeSessionTag)
                && string.Equals(_activeSessionTag, normalized, StringComparison.Ordinal)
                && _sessions.TryGetValue(normalized, out var session)
                && session != null
                && session.IsActive;
        }

        public bool TryGetSession(string tag, out IConversationRuntimeSession? session)
        {
            session = null;
            var normalized = ConversationTagUtility.Normalize(tag);
            if (!_sessions.TryGetValue(normalized, out var runtime) || runtime == null)
                return false;

            session = runtime;
            return true;
        }

        public bool TryGetSnapshot(string tag, out ConversationSessionSnapshot snapshot)
        {
            if (TryGetSession(tag, out var session) && session != null)
            {
                snapshot = session.Snapshot;
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryStartSession(string tag, ConversationFlowPreset preset, out IConversationRuntimeSession? session, out string message)
        {
            session = null;
            message = string.Empty;

            if (!_isAcquired)
            {
                message = "[CONV-100] Conversation hub is not acquired.";
                return false;
            }

            if (preset == null)
            {
                message = "[CONV-101] Conversation preset is null.";
                return false;
            }

            var normalized = ConversationTagUtility.Normalize(tag);
            if (!string.IsNullOrEmpty(_activeSessionTag))
            {
                message = $"[CONV-102] Another conversation is already active. active='{_activeSessionTag}'";
                return false;
            }

            var dialogueChannelSource = new ActorSource { Kind = ActorSourceKind.Current };
            var dialogueChannelTag = "default";
            if (_definitions.TryGetValue(normalized, out var definition) && definition != null)
            {
                dialogueChannelSource = definition.DialogueChannelSource;
                dialogueChannelTag = definition.DialogueChannelTag;
            }

            var runtime = new ConversationRuntimeSession(
                normalized,
                preset.CreateRuntimeCopy(),
                dialogueChannelSource,
                dialogueChannelTag);
            if (!runtime.IsActive)
            {
                message = $"[CONV-103] Conversation preset is invalid. tag='{normalized}'";
                return false;
            }

            _sessions[normalized] = runtime;
            _activeSessionTag = normalized;

            session = runtime;
            return true;
        }

        public bool TryEndSession(string tag, ConversationSessionEndKind endKind, string message, out ConversationSessionSnapshot snapshot)
        {
            var normalized = ConversationTagUtility.Normalize(tag);
            if (!_sessions.TryGetValue(normalized, out var session) || session == null)
            {
                snapshot = default;
                return false;
            }

            session.End(endKind, message);
            if (string.Equals(_activeSessionTag, normalized, StringComparison.Ordinal))
                _activeSessionTag = string.Empty;

            snapshot = session.Snapshot;
            return true;
        }

        void RebuildDefinitions()
        {
            _definitions.Clear();

            var channels = _mb.Channels;
            for (var i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                if (channel == null)
                    continue;

                var normalized = ConversationTagUtility.Normalize(channel.ChannelTag);
                if (_definitions.ContainsKey(normalized))
                {
                    Debug.LogWarning($"[ConversationChannelHub] Duplicate channel tag was skipped. tag='{normalized}'");
                    continue;
                }

                _definitions.Add(normalized, channel.CreateRuntimeCopy());
            }
        }

        void ReleaseAllSessions()
        {
            foreach (var pair in _sessions)
            {
                var runtime = pair.Value;
                runtime?.End(ConversationSessionEndKind.Forced, "[CONV-199] Scope released.");
            }

            _sessions.Clear();
            _activeSessionTag = string.Empty;
        }
    }

    sealed class ConversationRuntimeSession : IConversationRuntimeSession
    {
        readonly Dictionary<int, ConversationNodePresetBase> _nodesById = new();
        readonly List<ConversationChoiceHistoryEntry> _choiceHistory = new();

        bool _isActive;
        int _currentNodeId;
        int _lastCompletedNodeId;
        int _turnCount;
        int _lastSelectedIndex = -1;
        ConversationSessionEndKind _endKind = ConversationSessionEndKind.None;
        string _endMessage = string.Empty;

        public string Tag { get; }
        public bool IsActive => _isActive;
        public ConversationFlowPreset FlowPreset { get; }
        public ActorSource DialogueChannelSource { get; }
        public string DialogueChannelTag { get; }
        public int CurrentNodeId => _currentNodeId;
        public int LastCompletedNodeId => _lastCompletedNodeId;
        public int TurnCount => _turnCount;
        public int LastSelectedIndex => _lastSelectedIndex;
        public int ChoiceCount => _choiceHistory.Count;
        public IReadOnlyList<ConversationChoiceHistoryEntry> ChoiceHistory => _choiceHistory;

        public ConversationSessionSnapshot Snapshot => new(
            Tag,
            _isActive,
            _currentNodeId,
            _lastCompletedNodeId,
            _turnCount,
            _lastSelectedIndex,
            _choiceHistory.Count,
            _endKind,
            _endMessage);

        public ConversationRuntimeSession(string tag, ConversationFlowPreset flowPreset, ActorSource dialogueChannelSource, string dialogueChannelTag)
        {
            Tag = ConversationTagUtility.Normalize(tag);
            FlowPreset = flowPreset ?? new ConversationFlowPreset();
            DialogueChannelSource = dialogueChannelSource;
            DialogueChannelTag = ConversationTagUtility.Normalize(dialogueChannelTag);

            if (FlowPreset.Nodes != null)
            {
                for (var i = 0; i < FlowPreset.Nodes.Count; i++)
                {
                    var node = FlowPreset.Nodes[i];
                    if (node == null)
                        continue;
                    if (_nodesById.ContainsKey(node.NodeId))
                        continue;
                    _nodesById.Add(node.NodeId, node);
                }
            }

            _currentNodeId = FlowPreset.EntryNodeId;
            _isActive = _nodesById.ContainsKey(_currentNodeId);
            if (!_isActive)
            {
                _endKind = ConversationSessionEndKind.Failed;
                _endMessage = $"[CONV-110] Entry node was not found. entry={FlowPreset.EntryNodeId}";
            }
        }

        public bool TryGetCurrentNode(out ConversationNodePresetBase? node)
        {
            return TryGetNode(_currentNodeId, out node);
        }

        public bool TryGetNode(int nodeId, out ConversationNodePresetBase? node)
        {
            node = null;
            if (!_nodesById.TryGetValue(nodeId, out var found) || found == null)
                return false;

            node = found;
            return true;
        }

        public bool TrySetCurrentNode(int nodeId)
        {
            if (!_nodesById.ContainsKey(nodeId))
                return false;

            _currentNodeId = nodeId;
            return true;
        }

        public bool TryResolveDialogueTag(ConversationCharacterSlot slot, out string dialogueTag)
        {
            var resolvedTag = FlowPreset.DialogueRouting?.ResolveTag(slot);
            dialogueTag = string.IsNullOrWhiteSpace(resolvedTag) ? DialogueChannelTag : resolvedTag;
            return !string.IsNullOrEmpty(dialogueTag);
        }

        public void IncrementTurn()
        {
            _turnCount++;
        }

        public void MarkNodeCompleted(int nodeId)
        {
            _lastCompletedNodeId = nodeId;
        }

        public void RecordChoice(int nodeId, int selectedIndex, long timestampUtcTicks)
        {
            _lastSelectedIndex = selectedIndex;
            _choiceHistory.Add(new ConversationChoiceHistoryEntry(nodeId, selectedIndex, timestampUtcTicks));
        }

        public void End(ConversationSessionEndKind endKind, string message)
        {
            _isActive = false;
            _endKind = endKind;
            _endMessage = message ?? string.Empty;
        }
    }
}
