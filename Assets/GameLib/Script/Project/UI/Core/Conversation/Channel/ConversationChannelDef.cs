#nullable enable

using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Dialogue;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Conversation
{
    public enum ConversationCharacterSlot
    {
        None = 0,
        FarLeft = 10,
        Left = 20,
        MidLeft = 30,
        Center = 40,
        MidRight = 50,
        Right = 60,
        FarRight = 70,
    }

    public enum ConversationNodeJointKind
    {
        Default = 10,
        Choice = 20,
        IfTrue = 30,
        IfFalse = 40,
        SwitchCase = 50,
        SwitchDefault = 60,
    }

    public enum ConversationMessageHookMergeMode
    {
        Append = 10,
        Override = 20,
    }

    public enum ConversationSessionEndKind
    {
        None = 0,
        Completed = 10,
        Failed = 20,
        Canceled = 30,
        Forced = 40,
    }

    [Serializable]
    public sealed class ConversationChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("ConversationChannelHub 内で参照する識別タグです。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Dialogue Link")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Dialogue Channel\", _dialogueChannelSource)")]
        [Tooltip("リンク先の IDialogueService を持つ scope です。")]
        [SerializeField]
        ActorSource _dialogueChannelSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Dialogue Link")]
        [LabelText("Dialogue Channel Tag")]
        [Tooltip("リンク時に使用する DialogueChannel の tag です。")]
        [SerializeField]
        string _dialogueChannelTag = "default";

        public string ChannelTag => ConversationTagUtility.Normalize(_channelTag);
        public ActorSource DialogueChannelSource => _dialogueChannelSource;
        public string DialogueChannelTag => DialogueTagUtility.Normalize(_dialogueChannelTag);

        public ConversationChannelDefinition CreateRuntimeCopy()
        {
            return new ConversationChannelDefinition
            {
                _channelTag = _channelTag,
                _dialogueChannelSource = _dialogueChannelSource,
                _dialogueChannelTag = _dialogueChannelTag,
            };
        }
    }

    [Serializable]
    public sealed class ConversationFlowPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Flow")]
        [LabelText("Entry Node Id")]
        [Tooltip("会話開始時に最初に実行するノード ID です。")]
        [MinValue(1)]
        [SerializeField]
        int _entryNodeId = 1;

        [BoxGroup("Flow")]
        [LabelText("Max Node Steps")]
        [Tooltip("1 回の run で実行できる最大ノード数です。ループ暴走を防ぎます。")]
        [MinValue(1)]
        [SerializeField]
        int _maxNodeSteps = 1024;

        [BoxGroup("Flow")]
        [LabelText("Nodes")]
        [Tooltip("会話ノード定義です。NodeId は flow 内で一意にします。")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true, CustomAddFunction = nameof(AddNodeInternal), ListElementLabelName = nameof(ConversationNodePresetBase.ListLabel))]
        [SerializeReference]
        List<ConversationNodePresetBase> _nodes = new() { new ConversationStartNodePreset() };

        [BoxGroup("Flow")]
        [LabelText("Settings")]
        [InlineProperty]
        [SerializeField]
        ConversationFlowSettingsPreset _settings = new();

        [BoxGroup("Flow")]
        [LabelText("Dialogue Routing Overrides")]
        [Tooltip("必要なら slot ごとの Dialogue Channel Tag を上書きします。未設定なら Conversation Channel の Dialogue Channel Tag を使います。")]
        [InlineProperty]
        [SerializeField]
        ConversationDialogueRoutingPreset _dialogueRouting = new();

        [BoxGroup("Flow")]
        [LabelText("Hooks")]
        [InlineProperty]
        [SerializeField]
        ConversationFlowHookPreset _hooks = new();

        [BoxGroup("Flow")]
        [LabelText("Graph State")]
        [InlineProperty]
        [SerializeField]
        ConversationFlowGraphStatePreset _graphState = new();

        public int EntryNodeId => _entryNodeId;
        public int MaxNodeSteps => Mathf.Max(1, _maxNodeSteps);
        public IReadOnlyList<ConversationNodePresetBase> Nodes => _nodes;
        public List<ConversationNodePresetBase> EditorNodes => _nodes;
        public ConversationFlowSettingsPreset Settings => _settings;
        public ConversationDialogueRoutingPreset DialogueRouting => _dialogueRouting;
        public ConversationFlowHookPreset Hooks => _hooks;
        public ConversationFlowGraphStatePreset GraphState => _graphState;

        public string ListLabel => $"Nodes={_nodes?.Count ?? 0}";

        public void SetEntryNodeId(int nodeId)
        {
            _entryNodeId = Mathf.Max(1, nodeId);
        }

        public void AddNodeInternal()
        {
            AddNode(new ConversationMessageNodePreset());
        }

        public ConversationFlowPreset CreateRuntimeCopy()
        {
            var copy = new ConversationFlowPreset
            {
                _entryNodeId = _entryNodeId,
                _maxNodeSteps = _maxNodeSteps,
                _nodes = new List<ConversationNodePresetBase>(),
                _settings = _settings?.CreateRuntimeCopy() ?? new ConversationFlowSettingsPreset(),
                _dialogueRouting = _dialogueRouting?.CreateRuntimeCopy() ?? new ConversationDialogueRoutingPreset(),
                _hooks = _hooks?.CreateRuntimeCopy() ?? new ConversationFlowHookPreset(),
                _graphState = _graphState?.CreateRuntimeCopy() ?? new ConversationFlowGraphStatePreset(),
            };

            if (_nodes != null)
            {
                for (var i = 0; i < _nodes.Count; i++)
                {
                    var node = _nodes[i];
                    if (node == null)
                        continue;
                    copy._nodes.Add(node.CreateRuntimeCopy());
                }
            }

            copy.ValidateAndFixDuplicateNodeIds(false);
            copy.EnsureStartNode();
            copy.RebuildPreviousLinks();
            return copy;
        }

        public bool TryGetNode(int nodeId, out ConversationNodePresetBase? node)
        {
            node = null;
            if (_nodes == null)
                return false;

            for (var i = 0; i < _nodes.Count; i++)
            {
                var candidate = _nodes[i];
                if (candidate == null || candidate.NodeId != nodeId)
                    continue;

                node = candidate;
                return true;
            }

            return false;
        }

        public int GenerateNextNodeId()
        {
            var maxId = 0;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node == null)
                    continue;

                if (node.NodeId > maxId)
                    maxId = node.NodeId;
            }

            return maxId + 1;
        }

        public void AddNode(ConversationNodePresetBase node)
        {
            if (node == null)
                return;

            ValidateAndFixDuplicateNodeIds(false);
            EnsureStartNode();

            if (node is ConversationStartNodePreset)
            {
                for (var i = 0; i < _nodes.Count; i++)
                {
                    if (_nodes[i] is ConversationStartNodePreset)
                        return;
                }
            }

            _nodes.Add(node);
            ValidateAndFixDuplicateNodeIds(false);
        }

        public bool RemoveNode(int nodeId)
        {
            if (nodeId <= 0)
                return false;

            for (var i = _nodes.Count - 1; i >= 0; i--)
            {
                var node = _nodes[i];
                if (node == null || node.NodeId != nodeId)
                    continue;

                _nodes.RemoveAt(i);
                _graphState.RemoveNodeView(nodeId);

                ValidateAndFixDuplicateNodeIds(false);
                EnsureStartNode();
                return true;
            }

            return false;
        }

        public bool ReplaceNode(int nodeId, ConversationNodePresetBase replacement)
        {
            if (nodeId <= 0 || replacement == null)
                return false;

            for (var i = 0; i < _nodes.Count; i++)
            {
                var current = _nodes[i];
                if (current == null || current.NodeId != nodeId)
                    continue;

                replacement.SetNodeId(nodeId);
                _nodes[i] = replacement;
                ValidateAndFixDuplicateNodeIds(false);
                EnsureStartNode();
                return true;
            }

            return false;
        }

        public ConversationNodeGraphViewPreset GetOrCreateNodeView(int nodeId)
        {
            return _graphState.GetOrCreateNodeView(nodeId);
        }

        public void RebuildPreviousLinks()
        {
            ValidateAndFixDuplicateNodeIds(false);
            var startNodeId = EnsureStartNode().NodeId;

            if (_nodes == null)
                return;

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node == null)
                    continue;

                if (node is ConversationChoiceNodePreset choiceNode)
                    choiceNode.SyncChoiceJointsWithEntries();
                else if (node is ConversationSwitchNodePreset switchNode)
                    switchNode.SyncSwitchJointsWithCases();

                node.ClearPreviousLinks();
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                var source = _nodes[i];
                if (source == null)
                    continue;

                var joints = source.NextNodeJoints;
                for (var j = 0; j < joints.Count; j++)
                {
                    var joint = joints[j];
                    if (joint == null)
                        continue;

                    var candidates = joint.NextNodeCandidates;
                    var removedStartCandidate = false;
                    for (var c = 0; c < candidates.Count; c++)
                    {
                        var candidateNodeId = candidates[c];
                        if (candidateNodeId == startNodeId)
                        {
                            removedStartCandidate = true;
                            continue;
                        }

                        if (!TryGetNode(candidateNodeId, out var target) || target == null)
                            continue;

                        target.AddPreviousCandidate(source.NodeId);
                    }

                    if (removedStartCandidate)
                    {
                        joint.RemoveNextCandidate(startNodeId);
                        Debug.LogWarning($"[ConversationFlow] Removed invalid candidate link {source.NodeId} -> START({startNodeId}). Start node cannot have incoming links.");
                    }

                    var selected = joint.SelectedNextNodeId;
                    if (selected <= 0)
                        continue;

                    if (selected == startNodeId)
                    {
                        joint.SetSelectedNextNodeId(0);
                        joint.RemoveNextCandidate(startNodeId);
                        Debug.LogWarning($"[ConversationFlow] Cleared invalid selected link {source.NodeId} -> START({startNodeId}). Start node cannot have incoming links.");
                        continue;
                    }

                    if (!TryGetNode(selected, out var selectedTarget) || selectedTarget == null)
                        continue;

                    selectedTarget.AddPreviousCandidate(source.NodeId);
                    selectedTarget.SelectPreviousNode(source.NodeId);
                }
            }
        }

        public void ValidateAndFixDuplicateNodeIds(bool logErrors)
        {
            if (_nodes == null || _nodes.Count == 0)
                return;

            var used = new HashSet<int>();
            var maxId = 0;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node == null)
                    continue;

                if (node.NodeId > maxId)
                    maxId = node.NodeId;
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node == null)
                    continue;

                var currentId = Mathf.Max(1, node.NodeId);
                if (!used.Contains(currentId))
                {
                    used.Add(currentId);
                    node.MarkCurrentNodeIdAsValid();
                    continue;
                }

                var reverted = Mathf.Max(1, node.LastValidNodeId);
                if (used.Contains(reverted))
                {
                    reverted = maxId + 1;
                    maxId = reverted;
                }

                if (logErrors)
                {
                    Debug.LogError($"[ConversationFlow] Duplicate NodeId detected ({currentId}). Reverted node to {reverted}.");
                }

                node.SetNodeIdWithoutUpdatingLastValid(reverted);
                used.Add(reverted);
                node.MarkCurrentNodeIdAsValid();
            }
        }

        public ConversationStartNodePreset EnsureStartNode()
        {
            if (_nodes == null)
                _nodes = new List<ConversationNodePresetBase>();

            ConversationStartNodePreset? start = null;
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] is not ConversationStartNodePreset candidate)
                    continue;

                if (start == null)
                {
                    start = candidate;
                    continue;
                }

                _graphState.RemoveNodeView(candidate.NodeId);
                _nodes.RemoveAt(i);
                i--;
            }

            if (start == null)
            {
                start = new ConversationStartNodePreset();
                start.SetNodeId(GenerateNextNodeId());
                _nodes.Insert(0, start);
            }

            _entryNodeId = start.NodeId;
            return start;
        }
    }

    [Serializable]
    public sealed class ConversationFlowSettingsPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Message Defaults")]
        [LabelText("Message Detail")]
        [InlineProperty]
        [SerializeField]
        ConversationMessageDetailSettingsPreset _defaultMessageDetailSettings = new();

        [BoxGroup("Message Defaults")]
        [LabelText("Message Hooks")]
        [InlineProperty]
        [SerializeField]
        ConversationMessageNodeHookPreset _defaultMessageHooks = new();

        public ConversationMessageDetailSettingsPreset DefaultMessageDetailSettings => _defaultMessageDetailSettings;
        public ConversationMessageNodeHookPreset DefaultMessageHooks => _defaultMessageHooks;

        public ConversationFlowSettingsPreset CreateRuntimeCopy()
        {
            return new ConversationFlowSettingsPreset
            {
                _defaultMessageDetailSettings = _defaultMessageDetailSettings?.CreateRuntimeCopy() ?? new ConversationMessageDetailSettingsPreset(),
                _defaultMessageHooks = _defaultMessageHooks?.CreateRuntimeCopy() ?? new ConversationMessageNodeHookPreset(),
            };
        }
    }

    [Serializable]
    public sealed class ConversationMessageDetailSettingsPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Detail")]
        [LabelText("Use Preset Play Mode")]
        [SerializeField]
        bool _usePresetPlayMode = true;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesExplicitPlayMode))]
        [LabelText("Play Mode")]
        [SerializeField]
        TextPlayMode _playMode = TextPlayMode.Typewriter;

        [BoxGroup("Detail")]
        [LabelText("Text Settings")]
        [SerializeField]
        SetTextSettings _textSettings = SetTextSettings.Default;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesTypewriterOptions))]
        [LabelText("Wait Typewriter Complete")]
        [SerializeField]
        bool _waitForTypewriterComplete = true;

        [BoxGroup("Detail")]
        [LabelText("Await Input")]
        [SerializeField]
        bool _awaitInput = true;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesTypewriterOptions))]
        [LabelText("Allow Typewriter Skip")]
        [SerializeField]
        bool _allowTypewriterSkipByInput = true;

        [BoxGroup("Detail")]
        [LabelText("Use Auto Advance Delay")]
        [SerializeField]
        bool _useAutoAdvanceDelay;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesAutoDelay))]
        [LabelText("Auto Advance Delay")]
        [SerializeField]
        DynamicValue<float> _autoAdvanceDelaySeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Detail")]
        [LabelText("Auto Setup If Hidden")]
        [SerializeField]
        bool _autoSetupIfHidden = true;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesAutoSetup))]
        [LabelText("Auto Setup")]
        [InlineProperty]
        [SerializeField]
        DialogueSetupRequest _autoSetup = new();

        public bool UsePresetPlayMode => _usePresetPlayMode;
        public TextPlayMode PlayMode => _playMode;
        public SetTextSettings TextSettings => _textSettings;
        public bool WaitForTypewriterComplete => _waitForTypewriterComplete;
        public bool AwaitInput => _awaitInput;
        public bool AllowTypewriterSkipByInput => _allowTypewriterSkipByInput;
        public bool UseAutoAdvanceDelay => _useAutoAdvanceDelay;
        public DynamicValue<float> AutoAdvanceDelaySeconds => _autoAdvanceDelaySeconds;
        public bool AutoSetupIfHidden => _autoSetupIfHidden;
        public DialogueSetupRequest AutoSetup => _autoSetup;

        bool UsesExplicitPlayMode => !_usePresetPlayMode;
        bool UsesTypewriterOptions => _usePresetPlayMode || _playMode == TextPlayMode.Typewriter;
        bool UsesAutoDelay => _useAutoAdvanceDelay;
        bool UsesAutoSetup => _autoSetupIfHidden;

        public void ApplyTo(DialogueMessageRequest request)
        {
            if (request == null)
                return;

            request.UsePresetPlayMode = _usePresetPlayMode;
            request.PlayMode = _playMode;
            request.TextSettings = _textSettings;
            request.WaitForTypewriterComplete = _waitForTypewriterComplete;
            request.AwaitInput = _awaitInput;
            request.AllowTypewriterSkipByInput = _allowTypewriterSkipByInput;
            request.UseAutoAdvanceDelay = _useAutoAdvanceDelay;
            request.AutoAdvanceDelaySeconds = _autoAdvanceDelaySeconds;
            request.AutoSetupIfHidden = _autoSetupIfHidden;
            request.AutoSetup = _autoSetup?.CreateRuntimeCopy() ?? new DialogueSetupRequest();
        }

        public ConversationMessageDetailSettingsPreset CreateRuntimeCopy()
        {
            return new ConversationMessageDetailSettingsPreset
            {
                _usePresetPlayMode = _usePresetPlayMode,
                _playMode = _playMode,
                _textSettings = _textSettings,
                _waitForTypewriterComplete = _waitForTypewriterComplete,
                _awaitInput = _awaitInput,
                _allowTypewriterSkipByInput = _allowTypewriterSkipByInput,
                _useAutoAdvanceDelay = _useAutoAdvanceDelay,
                _autoAdvanceDelaySeconds = _autoAdvanceDelaySeconds,
                _autoSetupIfHidden = _autoSetupIfHidden,
                _autoSetup = _autoSetup?.CreateRuntimeCopy() ?? new DialogueSetupRequest(),
            };
        }
    }

    [Serializable]
    public sealed class ConversationMessageNodeHookPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Hooks")]
        [LabelText("On Before Message")]
        [CommandListFunctionName("Conversation.Message.OnBefore")]
        [SerializeField]
        CommandListData _onBeforeMessageCommands = new();

        [BoxGroup("Hooks")]
        [LabelText("On After Message")]
        [CommandListFunctionName("Conversation.Message.OnAfter")]
        [SerializeField]
        CommandListData _onAfterMessageCommands = new();

        public CommandListData OnBeforeMessageCommands => _onBeforeMessageCommands;
        public CommandListData OnAfterMessageCommands => _onAfterMessageCommands;

        public ConversationMessageNodeHookPreset CreateRuntimeCopy()
        {
            return new ConversationMessageNodeHookPreset
            {
                _onBeforeMessageCommands = ConversationCloneUtility.CloneCommandList(_onBeforeMessageCommands),
                _onAfterMessageCommands = ConversationCloneUtility.CloneCommandList(_onAfterMessageCommands),
            };
        }
    }

    [Serializable]
    public sealed class ConversationDialogueRoutingPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Routing")]
        [LabelText("Far Left")]
        [SerializeField]
        string _farLeftTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Left")]
        [SerializeField]
        string _leftTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Mid Left")]
        [SerializeField]
        string _midLeftTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Center")]
        [SerializeField]
        string _centerTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Mid Right")]
        [SerializeField]
        string _midRightTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Right")]
        [SerializeField]
        string _rightTag = string.Empty;

        [BoxGroup("Routing")]
        [LabelText("Far Right")]
        [SerializeField]
        string _farRightTag = string.Empty;

        public ConversationDialogueRoutingPreset CreateRuntimeCopy()
        {
            return new ConversationDialogueRoutingPreset
            {
                _farLeftTag = _farLeftTag,
                _leftTag = _leftTag,
                _midLeftTag = _midLeftTag,
                _centerTag = _centerTag,
                _midRightTag = _midRightTag,
                _rightTag = _rightTag,
                _farRightTag = _farRightTag,
            };
        }

        public string? ResolveTag(ConversationCharacterSlot slot)
        {
            return slot switch
            {
                ConversationCharacterSlot.FarLeft => ConversationTagUtility.NormalizeNullable(_farLeftTag),
                ConversationCharacterSlot.Left => ConversationTagUtility.NormalizeNullable(_leftTag),
                ConversationCharacterSlot.MidLeft => ConversationTagUtility.NormalizeNullable(_midLeftTag),
                ConversationCharacterSlot.Center => ConversationTagUtility.NormalizeNullable(_centerTag),
                ConversationCharacterSlot.MidRight => ConversationTagUtility.NormalizeNullable(_midRightTag),
                ConversationCharacterSlot.Right => ConversationTagUtility.NormalizeNullable(_rightTag),
                ConversationCharacterSlot.FarRight => ConversationTagUtility.NormalizeNullable(_farRightTag),
                _ => ConversationTagUtility.NormalizeNullable(_centerTag),
            };
        }
    }

    [Serializable]
    public sealed class ConversationFlowHookPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Hooks")]
        [LabelText("On Started")]
        [CommandListFunctionName("Conversation.Flow.OnStarted")]
        [SerializeField]
        CommandListData _onStartedCommands = new();

        [BoxGroup("Hooks")]
        [LabelText("On Completed")]
        [CommandListFunctionName("Conversation.Flow.OnCompleted")]
        [SerializeField]
        CommandListData _onCompletedCommands = new();

        [BoxGroup("Hooks")]
        [LabelText("On Failed")]
        [CommandListFunctionName("Conversation.Flow.OnFailed")]
        [SerializeField]
        CommandListData _onFailedCommands = new();

        [BoxGroup("Hooks")]
        [LabelText("On Canceled")]
        [CommandListFunctionName("Conversation.Flow.OnCanceled")]
        [SerializeField]
        CommandListData _onCanceledCommands = new();

        public CommandListData OnStartedCommands => _onStartedCommands;
        public CommandListData OnCompletedCommands => _onCompletedCommands;
        public CommandListData OnFailedCommands => _onFailedCommands;
        public CommandListData OnCanceledCommands => _onCanceledCommands;

        public ConversationFlowHookPreset CreateRuntimeCopy()
        {
            return new ConversationFlowHookPreset
            {
                _onStartedCommands = ConversationCloneUtility.CloneCommandList(_onStartedCommands),
                _onCompletedCommands = ConversationCloneUtility.CloneCommandList(_onCompletedCommands),
                _onFailedCommands = ConversationCloneUtility.CloneCommandList(_onFailedCommands),
                _onCanceledCommands = ConversationCloneUtility.CloneCommandList(_onCanceledCommands),
            };
        }
    }

    public interface IConversationNodePreset : IDynamicManagedRefValue
    {
        int NodeId { get; }
        ConversationCharacterSlot Slot { get; }
        string DialogueTagOverride { get; }
        string DebugViewText { get; }
        IReadOnlyList<int> PreviousNodeCandidates { get; }
        IReadOnlyList<int> SelectedPreviousNodeIds { get; }
        IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints { get; }
        CommandListData OnEnterCommands { get; }
        CommandListData OnExitCommands { get; }
        ConversationNodePresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public abstract class ConversationNodePresetBase : IConversationNodePreset
    {
        [BoxGroup("Node")]
        [LabelText("Node Id")]
        [MinValue(1)]
        [SerializeField]
        int _nodeId = 1;

        [SerializeField]
        [HideInInspector]
        int _lastValidNodeId = 1;

        [BoxGroup("Node")]
        [ShowIf(nameof(UsesSpeakerSlot))]
        [LabelText("Speaker Slot")]
        [Tooltip("Conversation slot -> Dialogue tag ルーティングで使用する話者スロットです。")]
        [SerializeField]
        ConversationCharacterSlot _slot = ConversationCharacterSlot.Center;

        [BoxGroup("Node")]
        [ShowIf(nameof(UsesDialogueTagOverride))]
        [LabelText("Dialogue Tag Override")]
        [Tooltip("空でない場合、slot ルーティングより優先してこの Dialogue tag を使います。")]
        [SerializeField]
        string _dialogueTagOverride = string.Empty;

        [BoxGroup("Node")]
        [LabelText("Debug View Text")]
        [ReadOnly]
        [ShowInInspector]
        string _debugViewTextCached = string.Empty;

        [BoxGroup("Links")]
        [LabelText("Prev Candidates")]
        [HideInInspector]
        [SerializeField]
        List<int> _previousNodeCandidates = new();

        [BoxGroup("Links")]
        [LabelText("Prev Selected")]
        [HideInInspector]
        [SerializeField]
        List<int> _selectedPreviousNodeIds = new();

        [BoxGroup("Hooks")]
        [LabelText("On Enter")]
        [InlineProperty]
        [CommandListFunctionName("Conversation.Node.OnEnter")]
        [SerializeField]
        CommandListData _onEnterCommands = new();

        [BoxGroup("Hooks")]
        [LabelText("On Exit")]
        [InlineProperty]
        [CommandListFunctionName("Conversation.Node.OnExit")]
        [SerializeField]
        CommandListData _onExitCommands = new();

        public int NodeId => _nodeId;
        public int LastValidNodeId => Mathf.Max(1, _lastValidNodeId);
        public ConversationCharacterSlot Slot => _slot;
        public string DialogueTagOverride => ConversationTagUtility.NormalizeNullable(_dialogueTagOverride) ?? string.Empty;
        public string DebugViewText => BuildDebugViewText();
        public IReadOnlyList<int> PreviousNodeCandidates => _previousNodeCandidates;
        public IReadOnlyList<int> SelectedPreviousNodeIds => _selectedPreviousNodeIds;
        public CommandListData OnEnterCommands => _onEnterCommands;
        public CommandListData OnExitCommands => _onExitCommands;

        public abstract IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints { get; }
        public abstract ConversationNodePresetBase CreateRuntimeCopy();
        public virtual bool IsStartNode => false;
        public virtual bool UsesSpeakerSlot => true;
        public virtual bool UsesDialogueTagOverride => true;
        public virtual bool SupportsDynamicJoints => false;
        public virtual ConversationNodeJointPreset? AddDynamicJoint() => null;
        public virtual bool RemoveJoint(ConversationNodeJointPreset joint) => false;
        public string ListLabel => $"{NodeId}: {DebugViewText}";

        protected abstract string BuildDebugViewText();

        protected void CopyCommonTo(ConversationNodePresetBase target)
        {
            target._nodeId = _nodeId;
            target._lastValidNodeId = _lastValidNodeId;
            target._slot = _slot;
            target._dialogueTagOverride = _dialogueTagOverride;
            target._debugViewTextCached = _debugViewTextCached;
            target._previousNodeCandidates = new List<int>(_previousNodeCandidates);
            target._selectedPreviousNodeIds = new List<int>(_selectedPreviousNodeIds);
            target._onEnterCommands = ConversationCloneUtility.CloneCommandList(_onEnterCommands);
            target._onExitCommands = ConversationCloneUtility.CloneCommandList(_onExitCommands);
        }

        public void ClearPreviousLinks()
        {
            _previousNodeCandidates.Clear();
            _selectedPreviousNodeIds.Clear();
        }

        public void SetNodeId(int nodeId)
        {
            _nodeId = Mathf.Max(1, nodeId);
            _lastValidNodeId = _nodeId;
        }

        public void SetNodeIdWithoutUpdatingLastValid(int nodeId)
        {
            _nodeId = Mathf.Max(1, nodeId);
        }

        public void MarkCurrentNodeIdAsValid()
        {
            _nodeId = Mathf.Max(1, _nodeId);
            _lastValidNodeId = _nodeId;
        }

        public void CopySharedFieldsFrom(ConversationNodePresetBase source)
        {
            if (source == null)
                return;

            _slot = source._slot;
            _dialogueTagOverride = source._dialogueTagOverride;
            _lastValidNodeId = source._lastValidNodeId;
            _previousNodeCandidates = new List<int>(source._previousNodeCandidates);
            _selectedPreviousNodeIds = new List<int>(source._selectedPreviousNodeIds);
            _onEnterCommands = ConversationCloneUtility.CloneCommandList(source._onEnterCommands);
            _onExitCommands = ConversationCloneUtility.CloneCommandList(source._onExitCommands);
            _debugViewTextCached = source._debugViewTextCached;
        }

        public void AddPreviousCandidate(int nodeId)
        {
            if (nodeId <= 0 || _previousNodeCandidates.Contains(nodeId))
                return;

            _previousNodeCandidates.Add(nodeId);
        }

        public void SelectPreviousNode(int nodeId)
        {
            if (nodeId <= 0)
                return;

            if (!_previousNodeCandidates.Contains(nodeId))
                _previousNodeCandidates.Add(nodeId);

            if (_selectedPreviousNodeIds.Contains(nodeId))
                return;

            _selectedPreviousNodeIds.Add(nodeId);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _nodeId = Mathf.Max(1, _nodeId);
            if (_lastValidNodeId <= 0)
                _lastValidNodeId = _nodeId;

            _debugViewTextCached = BuildDebugViewText();
        }
#endif
    }

    [Serializable]
    public sealed class ConversationNodeJointPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Joint")]
        [LabelText("Joint Kind")]
        [SerializeField]
        ConversationNodeJointKind _jointKind = ConversationNodeJointKind.Default;

        [BoxGroup("Joint")]
        [LabelText("Joint Name")]
        [SerializeField]
        string _jointName = "Next";

        [BoxGroup("Joint")]
        [LabelText("Debug Text")]
        [SerializeField]
        string _debugText = string.Empty;

        [BoxGroup("Joint")]
        [LabelText("Branch Key")]
        [Tooltip("Choice/Switch 判定で使うキーです。")]
        [SerializeField]
        int _branchKey;

        [BoxGroup("Joint")]
        [LabelText("Next Candidates")]
        [SerializeField]
        List<int> _nextNodeCandidates = new();

        [BoxGroup("Joint")]
        [LabelText("Selected Next Node Id")]
        [SerializeField]
        int _selectedNextNodeId;

        [BoxGroup("Joint")]
        [LabelText("On Matched")]
        [CommandListFunctionName("Conversation.Node.Joint.OnMatched")]
        [SerializeField]
        CommandListData _onMatchedCommands = new();

        public ConversationNodeJointKind JointKind => _jointKind;
        public string JointName => string.IsNullOrWhiteSpace(_jointName) ? "Joint" : _jointName.Trim();
        public string DebugText => _debugText ?? string.Empty;
        public int BranchKey => _branchKey;
        public IReadOnlyList<int> NextNodeCandidates => _nextNodeCandidates;
        public int SelectedNextNodeId => _selectedNextNodeId;
        public CommandListData OnMatchedCommands => _onMatchedCommands;

        public void SetSelectedNextNodeId(int nextNodeId)
        {
            _selectedNextNodeId = nextNodeId;
            if (_selectedNextNodeId > 0 && !_nextNodeCandidates.Contains(_selectedNextNodeId))
                _nextNodeCandidates.Add(_selectedNextNodeId);
        }

        public void AddNextCandidate(int nodeId)
        {
            if (nodeId <= 0 || _nextNodeCandidates.Contains(nodeId))
                return;

            _nextNodeCandidates.Add(nodeId);
        }

        public void RemoveNextCandidate(int nodeId)
        {
            if (nodeId <= 0)
                return;

            _nextNodeCandidates.Remove(nodeId);
            if (_selectedNextNodeId == nodeId)
                _selectedNextNodeId = 0;
        }

        public void SetJointName(string jointName)
        {
            _jointName = jointName ?? string.Empty;
        }

        public void SetJointKind(ConversationNodeJointKind jointKind)
        {
            _jointKind = jointKind;
        }

        public void SetBranchKey(int branchKey)
        {
            _branchKey = Mathf.Max(0, branchKey);
        }

        public void SetDebugText(string debugText)
        {
            _debugText = debugText ?? string.Empty;
        }

        public ConversationNodeJointPreset CreateRuntimeCopy()
        {
            return new ConversationNodeJointPreset
            {
                _jointKind = _jointKind,
                _jointName = _jointName,
                _debugText = _debugText,
                _branchKey = _branchKey,
                _nextNodeCandidates = new List<int>(_nextNodeCandidates),
                _selectedNextNodeId = _selectedNextNodeId,
                _onMatchedCommands = ConversationCloneUtility.CloneCommandList(_onMatchedCommands),
            };
        }

        public static ConversationNodeJointPreset CreateDefault(string jointName, ConversationNodeJointKind kind, int branchKey = 0)
        {
            return new ConversationNodeJointPreset
            {
                _jointKind = kind,
                _jointName = jointName,
                _branchKey = branchKey,
            };
        }
    }

    [Serializable]
    public sealed class ConversationStartNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Links")]
        [LabelText("Next Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _nextNodeJoints =
            new() { ConversationNodeJointPreset.CreateDefault("Start", ConversationNodeJointKind.Default) };

        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _nextNodeJoints;
        public override bool IsStartNode => true;

        protected override string BuildDebugViewText()
        {
            return "Start";
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationStartNodePreset
            {
                _nextNodeJoints = ConversationNodeJointUtility.CloneJointList(_nextNodeJoints),
            };

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationMessageNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Message")]
        [LabelText("Character Data Id")]
        [Tooltip("発話者の CharacterDataBase ID です。0 のときは話者未指定になります。")]
        [ValueDropdown(nameof(GetCharacterDataIdDropdownItems))]
        [MinValue(0)]
        [SerializeField]
        int _characterDataId;

        [BoxGroup("Message")]
        [LabelText("Body Text")]
        [InlineProperty]
        [SerializeField]
        DynamicValue<string> _bodyText = DynamicValueExtensions.FromLiteral(string.Empty);

        [BoxGroup("Character Modules")]
        [ShowIf(nameof(UsesCharacterBinding))]
        [LabelText("Expression Key")]
        [Tooltip("Expression module があるキャラクターで使用する表情キーです。")]
        [ValueDropdown(nameof(GetExpressionKeyDropdownItems))]
        [SerializeField]
        string _expressionKey = string.Empty;

        [BoxGroup("Character Modules")]
        [ShowIf(nameof(UsesCharacterBinding))]
        [LabelText("Use Default Image Fallback")]
        [SerializeField]
        bool _useDefaultImageFallback = true;

        [BoxGroup("Detail")]
        [LabelText("Override Detail Settings")]
        [SerializeField]
        bool _overrideDetailSettings;

        [BoxGroup("Detail")]
        [ShowIf(nameof(UsesDetailSettingsOverride))]
        [LabelText("Detail Settings")]
        [InlineProperty]
        [SerializeField]
        ConversationMessageDetailSettingsPreset _detailSettingsOverride = new();

        [BoxGroup("Message Hooks")]
        [LabelText("Override Message Hooks")]
        [SerializeField]
        bool _overrideMessageHooks;

        [BoxGroup("Message Hooks")]
        [ShowIf(nameof(UsesMessageHooksOverride))]
        [LabelText("Hook Merge Mode")]
        [SerializeField]
        ConversationMessageHookMergeMode _messageHookMergeMode = ConversationMessageHookMergeMode.Append;

        [BoxGroup("Message Hooks")]
        [ShowIf(nameof(UsesMessageHooksOverride))]
        [LabelText("Hooks")]
        [InlineProperty]
        [SerializeField]
        ConversationMessageNodeHookPreset _messageHooksOverride = new();

        [BoxGroup("Links")]
        [LabelText("Next Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _nextNodeJoints = new() { ConversationNodeJointPreset.CreateDefault("Next", ConversationNodeJointKind.Default) };

        public int CharacterDataId => _characterDataId;
        public DynamicValue<string> BodyText => _bodyText;
        public string ExpressionKey => _expressionKey ?? string.Empty;
        public bool UseDefaultImageFallback => _useDefaultImageFallback;
        public bool OverrideDetailSettings => _overrideDetailSettings;
        public ConversationMessageDetailSettingsPreset DetailSettingsOverride => _detailSettingsOverride;
        public bool OverrideMessageHooks => _overrideMessageHooks;
        public ConversationMessageHookMergeMode MessageHookMergeMode => _messageHookMergeMode;
        public ConversationMessageNodeHookPreset MessageHooksOverride => _messageHooksOverride;
        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _nextNodeJoints;

        bool UsesCharacterBinding => _characterDataId > 0;
        bool UsesDetailSettingsOverride => _overrideDetailSettings;
        bool UsesMessageHooksOverride => _overrideMessageHooks;

        IEnumerable<ValueDropdownItem<int>> GetCharacterDataIdDropdownItems()
        {
            var used = new HashSet<int>();
            yield return new ValueDropdownItem<int>("<None>", 0);

            var definitions = Resources.FindObjectsOfTypeAll<CharacterDataBaseMB>();
            if (definitions != null)
            {
                for (var i = 0; i < definitions.Length; i++)
                {
                    var database = definitions[i];
                    if (database == null)
                        continue;

                    var items = database.Definitions;
                    for (var d = 0; d < items.Count; d++)
                    {
                        var candidate = items[d];
                        if (candidate == null || candidate.CharacterId <= 0)
                            continue;

                        if (!used.Add(candidate.CharacterId))
                            continue;

                        yield return new ValueDropdownItem<int>(BuildCharacterDefinitionLabel(candidate), candidate.CharacterId);
                    }
                }
            }

            if (_characterDataId > 0 && !used.Contains(_characterDataId))
                yield return new ValueDropdownItem<int>($"{_characterDataId}: (Missing Definition)", _characterDataId);
        }

        IEnumerable<ValueDropdownItem<string>> GetExpressionKeyDropdownItems()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            yield return new ValueDropdownItem<string>("<None>", string.Empty);

            if (_characterDataId <= 0)
                yield break;

            if (!TryResolveCharacterDefinition(_characterDataId, out var definition) || definition == null)
                yield break;

            if (!definition.TryGetModule<CharacterExpressionModulePreset>(out var expressionModule) || expressionModule == null)
                yield break;

            var entries = expressionModule.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var key = string.IsNullOrWhiteSpace(entry.Key) ? string.Empty : entry.Key.Trim();
                if (string.IsNullOrEmpty(key) || !used.Add(key))
                    continue;

                var debugText = string.IsNullOrWhiteSpace(entry.DebugText) ? string.Empty : entry.DebugText.Trim();
                var label = string.IsNullOrEmpty(debugText)
                    ? key
                    : $"{debugText} ({key})";

                yield return new ValueDropdownItem<string>(label, key);
            }

            if (!string.IsNullOrWhiteSpace(_expressionKey) && !used.Contains(_expressionKey))
                yield return new ValueDropdownItem<string>($"{_expressionKey} (Missing Expression)", _expressionKey);
        }

        static string BuildCharacterDefinitionLabel(CharacterDataBaseDefinition definition)
        {
            var stableKey = string.IsNullOrWhiteSpace(definition.StableKey)
                ? string.Empty
                : definition.StableKey.Trim();
            var displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? string.Empty
                : definition.DisplayName.Trim();

            if (!string.IsNullOrEmpty(stableKey) && !string.IsNullOrEmpty(displayName))
                return $"{definition.CharacterId}: {stableKey} ({displayName})";

            if (!string.IsNullOrEmpty(stableKey))
                return $"{definition.CharacterId}: {stableKey}";

            if (!string.IsNullOrEmpty(displayName))
                return $"{definition.CharacterId}: {displayName}";

            return $"{definition.CharacterId}: Character";
        }

        static bool TryResolveCharacterDefinition(int characterDataId, out CharacterDataBaseDefinition? definition)
        {
            definition = null;
            if (characterDataId <= 0)
                return false;

            var databases = Resources.FindObjectsOfTypeAll<CharacterDataBaseMB>();
            if (databases == null || databases.Length == 0)
                return false;

            for (var i = 0; i < databases.Length; i++)
            {
                var db = databases[i];
                if (db == null)
                    continue;

                var entries = db.Definitions;
                for (var d = 0; d < entries.Count; d++)
                {
                    var candidate = entries[d];
                    if (candidate == null)
                        continue;

                    if (candidate.CharacterId != characterDataId)
                        continue;

                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        protected override string BuildDebugViewText()
        {
            if (_characterDataId <= 0)
                return "Chara: (none)";

            var speaker = ResolveSpeakerLabel();
            return string.IsNullOrWhiteSpace(speaker)
                ? "Chara: (unresolved)"
                : $"Chara: {speaker}";
        }

        string ResolveSpeakerLabel()
        {
            if (_characterDataId <= 0)
                return string.Empty;

            if (!TryResolveCharacterDefinition(_characterDataId, out var definition) || definition == null)
                return string.Empty;

            var displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? string.Empty
                : definition.DisplayName.Trim();
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            var stableKey = string.IsNullOrWhiteSpace(definition.StableKey)
                ? string.Empty
                : definition.StableKey.Trim();
            if (!string.IsNullOrEmpty(stableKey))
                return stableKey;

            return string.Empty;
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationMessageNodePreset
            {
                _characterDataId = _characterDataId,
                _bodyText = _bodyText,
                _expressionKey = _expressionKey,
                _useDefaultImageFallback = _useDefaultImageFallback,
                _overrideDetailSettings = _overrideDetailSettings,
                _detailSettingsOverride = _detailSettingsOverride?.CreateRuntimeCopy() ?? new ConversationMessageDetailSettingsPreset(),
                _overrideMessageHooks = _overrideMessageHooks,
                _messageHookMergeMode = _messageHookMergeMode,
                _messageHooksOverride = _messageHooksOverride?.CreateRuntimeCopy() ?? new ConversationMessageNodeHookPreset(),
                _nextNodeJoints = ConversationNodeJointUtility.CloneJointList(_nextNodeJoints),
            };

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationChoiceNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Choice")]
        [LabelText("Choice Request")]
        [InlineProperty]
        [SerializeField]
        DialogueChoiceRequest _choiceRequest = new();

        [BoxGroup("Choice")]
        [LabelText("Write SelectedIndex To Vars")]
        [SerializeField]
        bool _writeSelectedIndexToVars = true;

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesSelectedVar))]
        [LabelText("SelectedIndex Var")]
        [SerializeField]
        VarKeyRef _selectedIndexVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Choice")]
        [LabelText("Choice Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _choiceJoints =
            new() { ConversationNodeJointPreset.CreateDefault("Choice 0", ConversationNodeJointKind.Choice, 0) };

        [BoxGroup("Choice Hooks")]
        [LabelText("On Selected")]
        [CommandListFunctionName("Conversation.Node.Choice.OnSelected")]
        [SerializeField]
        CommandListData _onChoiceSelectedCommands = new();

        [BoxGroup("Choice Hooks")]
        [LabelText("On Canceled")]
        [CommandListFunctionName("Conversation.Node.Choice.OnCanceled")]
        [SerializeField]
        CommandListData _onChoiceCanceledCommands = new();

        [BoxGroup("Choice Hooks")]
        [LabelText("On Timeout")]
        [CommandListFunctionName("Conversation.Node.Choice.OnTimeout")]
        [SerializeField]
        CommandListData _onChoiceTimeoutCommands = new();

        [BoxGroup("Choice Hooks")]
        [LabelText("On Replaced")]
        [CommandListFunctionName("Conversation.Node.Choice.OnReplaced")]
        [SerializeField]
        CommandListData _onChoiceReplacedCommands = new();

        public DialogueChoiceRequest ChoiceRequest => _choiceRequest;
        public bool WriteSelectedIndexToVars => _writeSelectedIndexToVars;
        public VarKeyRef SelectedIndexVar => _selectedIndexVar;
        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _choiceJoints;
        public CommandListData OnChoiceSelectedCommands => _onChoiceSelectedCommands;
        public CommandListData OnChoiceCanceledCommands => _onChoiceCanceledCommands;
        public CommandListData OnChoiceTimeoutCommands => _onChoiceTimeoutCommands;
        public CommandListData OnChoiceReplacedCommands => _onChoiceReplacedCommands;

        bool UsesSelectedVar => _writeSelectedIndexToVars;

        protected override string BuildDebugViewText()
        {
            var count = _choiceRequest?.GridChoiceRequest?.Entries?.Count ?? 0;
            return $"Choice ({count} entries)";
        }

        public void SyncChoiceJointsWithEntries()
        {
            _choiceJoints ??= new List<ConversationNodeJointPreset>();
            var entries = _choiceRequest?.GridChoiceRequest?.Entries;
            var entryCount = entries?.Count ?? 0;

            var existingByIndex = new Dictionary<int, ConversationNodeJointPreset>();
            for (var i = 0; i < _choiceJoints.Count; i++)
            {
                var joint = _choiceJoints[i];
                if (joint == null || joint.JointKind != ConversationNodeJointKind.Choice)
                    continue;

                var branchIndex = Mathf.Max(0, joint.BranchKey);
                if (existingByIndex.ContainsKey(branchIndex))
                    continue;

                existingByIndex.Add(branchIndex, joint);
            }

            var synchronized = new List<ConversationNodeJointPreset>(entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                if (!existingByIndex.TryGetValue(i, out var joint) || joint == null)
                    joint = ConversationNodeJointPreset.CreateDefault(BuildChoiceJointName(entries, i), ConversationNodeJointKind.Choice, i);

                joint.SetJointKind(ConversationNodeJointKind.Choice);
                joint.SetBranchKey(i);
                joint.SetJointName(BuildChoiceJointName(entries, i));
                synchronized.Add(joint);
            }

            _choiceJoints = synchronized;
        }

        static string BuildChoiceJointName(List<GridObjectChoiceEntry>? entries, int index)
        {
            var fallback = $"Choice {index}";
            if (entries == null || index < 0 || index >= entries.Count)
                return fallback;

            var entry = entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.DisplayName))
                return fallback;

            return entry.DisplayName.Trim();
        }

        public bool TryResolveChoiceJoint(int selectedIndex, out ConversationNodeJointPreset? joint)
        {
            SyncChoiceJointsWithEntries();
            joint = null;
            for (var i = 0; i < _choiceJoints.Count; i++)
            {
                var candidate = _choiceJoints[i];
                if (candidate == null)
                    continue;
                if (candidate.JointKind != ConversationNodeJointKind.Choice)
                    continue;
                if (candidate.BranchKey != selectedIndex)
                    continue;

                joint = candidate;
                return true;
            }

            return false;
        }

        public override bool SupportsDynamicJoints => false;

        public override ConversationNodeJointPreset? AddDynamicJoint()
        {
            return null;
        }

        public override bool RemoveJoint(ConversationNodeJointPreset joint)
        {
            return false;
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationChoiceNodePreset
            {
                _choiceRequest = _choiceRequest?.CreateRuntimeCopy() ?? new DialogueChoiceRequest(),
                _writeSelectedIndexToVars = _writeSelectedIndexToVars,
                _selectedIndexVar = _selectedIndexVar,
                _choiceJoints = ConversationNodeJointUtility.CloneJointList(_choiceJoints),
                _onChoiceSelectedCommands = ConversationCloneUtility.CloneCommandList(_onChoiceSelectedCommands),
                _onChoiceCanceledCommands = ConversationCloneUtility.CloneCommandList(_onChoiceCanceledCommands),
                _onChoiceTimeoutCommands = ConversationCloneUtility.CloneCommandList(_onChoiceTimeoutCommands),
                _onChoiceReplacedCommands = ConversationCloneUtility.CloneCommandList(_onChoiceReplacedCommands),
            };

            copy.SyncChoiceJointsWithEntries();
            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationIfNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("If")]
        [LabelText("Condition")]
        [SerializeField]
        DynamicValue<bool> _conditionValue = DynamicValueExtensions.FromLiteral(false);

        [BoxGroup("Links")]
        [LabelText("Next Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _nextNodeJoints =
            new()
            {
                ConversationNodeJointPreset.CreateDefault("True", ConversationNodeJointKind.IfTrue, 1),
                ConversationNodeJointPreset.CreateDefault("False", ConversationNodeJointKind.IfFalse, 0),
            };

        public DynamicValue<bool> ConditionValue => _conditionValue;
        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _nextNodeJoints;
        public override bool UsesSpeakerSlot => false;
        public override bool UsesDialogueTagOverride => false;

        protected override string BuildDebugViewText()
        {
            return "If";
        }

        public bool TryResolveConditionJoint(bool condition, out ConversationNodeJointPreset? joint)
        {
            joint = null;
            var expectedKind = condition ? ConversationNodeJointKind.IfTrue : ConversationNodeJointKind.IfFalse;
            for (var i = 0; i < _nextNodeJoints.Count; i++)
            {
                var candidate = _nextNodeJoints[i];
                if (candidate == null || candidate.JointKind != expectedKind)
                    continue;

                joint = candidate;
                return true;
            }

            return false;
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationIfNodePreset
            {
                _conditionValue = _conditionValue,
                _nextNodeJoints = ConversationNodeJointUtility.CloneJointList(_nextNodeJoints),
            };

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationSwitchCasePreset : IDynamicManagedRefValue
    {
        public string ListLabel
        {
            get
            {
                var caseValue = _matchMode switch
                {
                    SwitchCaseMatchMode.Exact => CommandDebugDataHelper.GetDynamicDebugData(_caseValue),
                    SwitchCaseMatchMode.Compare => $"{_compareOp} {CommandDebugDataHelper.GetDynamicDebugData(_compareTarget)}",
                    SwitchCaseMatchMode.Condition => CommandDebugDataHelper.GetDynamicDebugData(_conditionValue),
                    _ => "<unknown>",
                };
                return $"Mode={_matchMode} Case={caseValue}";
            }
        }

        [LabelText("Match Mode")]
        [EnumToggleButtons]
        [SerializeField]
        SwitchCaseMatchMode _matchMode = SwitchCaseMatchMode.Exact;

        [LabelText("Case Value")]
        [ShowIf(nameof(IsExactMode))]
        [SerializeField]
        DynamicValue _caseValue;

        [LabelText("Compare Operator")]
        [ShowIf(nameof(IsCompareMode))]
        [SerializeField]
        SwitchNumericCompareOp _compareOp = SwitchNumericCompareOp.GreaterOrEqual;

        [LabelText("Compare Target")]
        [ShowIf(nameof(IsCompareMode))]
        [SerializeField]
        DynamicValue _compareTarget;

        [LabelText("Condition")]
        [ShowIf(nameof(IsConditionMode))]
        [SerializeField]
        DynamicValue<bool> _conditionValue = DynamicValueExtensions.FromLiteral(false);

        public SwitchCaseMatchMode MatchMode => _matchMode;
        public DynamicValue CaseValue => _caseValue;
        public SwitchNumericCompareOp CompareOp => _compareOp;
        public DynamicValue CompareTarget => _compareTarget;
        public DynamicValue<bool> ConditionValue => _conditionValue;

        bool IsExactMode() => _matchMode == SwitchCaseMatchMode.Exact;
        bool IsCompareMode() => _matchMode == SwitchCaseMatchMode.Compare;
        bool IsConditionMode() => _matchMode == SwitchCaseMatchMode.Condition;

        public bool IsMatched(DynamicVariant switchValue, IDynamicContext context)
        {
            return _matchMode switch
            {
                SwitchCaseMatchMode.Exact => switchValue.Equals(_caseValue.Evaluate(context)),
                SwitchCaseMatchMode.Compare => EvaluateNumericComparison(switchValue, _compareTarget.Evaluate(context), _compareOp),
                SwitchCaseMatchMode.Condition => _conditionValue.GetOrDefault(context, false),
                _ => false,
            };
        }

        public string BuildJointName(int index)
        {
            var fallback = $"Case {index}";
            return _matchMode switch
            {
                SwitchCaseMatchMode.Exact => $"{fallback}: {CommandDebugDataHelper.GetDynamicDebugData(_caseValue)}",
                SwitchCaseMatchMode.Compare => $"{fallback}: {_compareOp} {CommandDebugDataHelper.GetDynamicDebugData(_compareTarget)}",
                SwitchCaseMatchMode.Condition => fallback,
                _ => fallback,
            };
        }

        public ConversationSwitchCasePreset CreateRuntimeCopy()
        {
            return new ConversationSwitchCasePreset
            {
                _matchMode = _matchMode,
                _caseValue = _caseValue,
                _compareOp = _compareOp,
                _compareTarget = _compareTarget,
                _conditionValue = _conditionValue,
            };
        }

        static bool EvaluateNumericComparison(DynamicVariant left, DynamicVariant right, SwitchNumericCompareOp op)
        {
            if (op == SwitchNumericCompareOp.Equal)
                return left.Equals(right);
            if (op == SwitchNumericCompareOp.NotEqual)
                return !left.Equals(right);

            if (!TryGetNumeric(left, out var l) || !TryGetNumeric(right, out var r))
                return false;

            return op switch
            {
                SwitchNumericCompareOp.LessThan => l < r,
                SwitchNumericCompareOp.LessOrEqual => l <= r,
                SwitchNumericCompareOp.GreaterThan => l > r,
                SwitchNumericCompareOp.GreaterOrEqual => l >= r,
                _ => false,
            };
        }

        static bool TryGetNumeric(DynamicVariant value, out double number)
        {
            switch (value.Kind)
            {
                case ValueKind.Bool:
                    number = value.AsBool ? 1d : 0d;
                    return true;
                case ValueKind.Int:
                    number = value.AsInt;
                    return true;
                case ValueKind.Float:
                    number = value.AsFloat;
                    return true;
                default:
                    number = 0d;
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class ConversationJumpNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Links")]
        [LabelText("Next Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _nextNodeJoints =
            new() { ConversationNodeJointPreset.CreateDefault("Jump", ConversationNodeJointKind.Default) };

        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _nextNodeJoints;

        protected override string BuildDebugViewText()
        {
            return "Jump";
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationJumpNodePreset
            {
                _nextNodeJoints = ConversationNodeJointUtility.CloneJointList(_nextNodeJoints),
            };

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationSwitchNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Switch")]
        [LabelText("Switch Value")]
        [SerializeField]
        DynamicValue _switchValue;

        [BoxGroup("Switch")]
        [LabelText("Evaluate Order")]
        [EnumToggleButtons]
        [SerializeField]
        SwitchEvaluateOrder _evaluateOrder = SwitchEvaluateOrder.TopToBottom;

        [BoxGroup("Switch")]
        [LabelText("Cases")]
        [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = nameof(ConversationSwitchCasePreset.ListLabel))]
        [SerializeField]
        List<ConversationSwitchCasePreset> _cases = new() { new ConversationSwitchCasePreset() };

        [BoxGroup("Links")]
        [LabelText("Switch Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _switchJoints =
            new()
            {
                ConversationNodeJointPreset.CreateDefault("Case 0", ConversationNodeJointKind.SwitchCase, 0),
                ConversationNodeJointPreset.CreateDefault("Default", ConversationNodeJointKind.SwitchDefault),
            };

        public DynamicValue SwitchValue => _switchValue;
        public SwitchEvaluateOrder EvaluateOrder => _evaluateOrder;
        public IReadOnlyList<ConversationSwitchCasePreset> Cases => _cases;
        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _switchJoints;
        public override bool UsesSpeakerSlot => false;
        public override bool UsesDialogueTagOverride => false;

        protected override string BuildDebugViewText()
        {
            return $"Switch ({_cases?.Count ?? 0} cases)";
        }

        public void SyncSwitchJointsWithCases()
        {
            _cases ??= new List<ConversationSwitchCasePreset>();
            _switchJoints ??= new List<ConversationNodeJointPreset>();

            var caseJointsByIndex = new Dictionary<int, ConversationNodeJointPreset>();
            ConversationNodeJointPreset? defaultJoint = null;
            for (var i = 0; i < _switchJoints.Count; i++)
            {
                var candidate = _switchJoints[i];
                if (candidate == null)
                    continue;

                if (candidate.JointKind == ConversationNodeJointKind.SwitchDefault)
                {
                    defaultJoint ??= candidate;
                    continue;
                }

                if (candidate.JointKind != ConversationNodeJointKind.SwitchCase)
                    continue;

                var branchIndex = Mathf.Max(0, candidate.BranchKey);
                if (!caseJointsByIndex.ContainsKey(branchIndex))
                    caseJointsByIndex.Add(branchIndex, candidate);
            }

            var synchronized = new List<ConversationNodeJointPreset>(_cases.Count + 1);
            for (var i = 0; i < _cases.Count; i++)
            {
                _cases[i] ??= new ConversationSwitchCasePreset();
                if (!caseJointsByIndex.TryGetValue(i, out var caseJoint) || caseJoint == null)
                    caseJoint = ConversationNodeJointPreset.CreateDefault($"Case {i}", ConversationNodeJointKind.SwitchCase, i);

                caseJoint.SetJointKind(ConversationNodeJointKind.SwitchCase);
                caseJoint.SetBranchKey(i);
                caseJoint.SetJointName(_cases[i].BuildJointName(i));
                synchronized.Add(caseJoint);
            }

            defaultJoint ??= ConversationNodeJointPreset.CreateDefault("Default", ConversationNodeJointKind.SwitchDefault);
            defaultJoint.SetJointKind(ConversationNodeJointKind.SwitchDefault);
            defaultJoint.SetJointName("Default");
            synchronized.Add(defaultJoint);

            _switchJoints = synchronized;
        }

        public bool TryResolveSwitchJoint(IDynamicContext context, out ConversationNodeJointPreset? joint)
        {
            SyncSwitchJointsWithCases();
            joint = null;
            var switchValue = _switchValue.Evaluate(context);

            var startIndex = 0;
            var endIndex = _cases.Count;
            var step = 1;
            if (_evaluateOrder == SwitchEvaluateOrder.BottomToTop)
            {
                startIndex = _cases.Count - 1;
                endIndex = -1;
                step = -1;
            }

            for (var i = startIndex; i != endIndex; i += step)
            {
                var entry = _cases[i];
                if (entry == null)
                    continue;

                if (!entry.IsMatched(switchValue, context))
                    continue;

                if (TryGetSwitchCaseJoint(i, out joint))
                    return true;
            }

            return TryGetDefaultSwitchJoint(out joint);
        }

        public bool TryResolveSwitchJoint(int key, out ConversationNodeJointPreset? joint)
        {
            SyncSwitchJointsWithCases();
            joint = null;

            for (var i = 0; i < _switchJoints.Count; i++)
            {
                var candidate = _switchJoints[i];
                if (candidate == null)
                    continue;
                if (candidate.JointKind != ConversationNodeJointKind.SwitchCase)
                    continue;
                if (candidate.BranchKey != key)
                    continue;

                joint = candidate;
                return true;
            }

            return TryGetDefaultSwitchJoint(out joint);
        }

        bool TryGetSwitchCaseJoint(int caseIndex, out ConversationNodeJointPreset? joint)
        {
            joint = null;
            for (var i = 0; i < _switchJoints.Count; i++)
            {
                var candidate = _switchJoints[i];
                if (candidate == null)
                    continue;
                if (candidate.JointKind != ConversationNodeJointKind.SwitchCase)
                    continue;
                if (candidate.BranchKey != caseIndex)
                    continue;

                joint = candidate;
                return true;
            }

            return false;
        }

        bool TryGetDefaultSwitchJoint(out ConversationNodeJointPreset? joint)
        {
            joint = null;
            for (var i = 0; i < _switchJoints.Count; i++)
            {
                var candidate = _switchJoints[i];
                if (candidate == null)
                    continue;
                if (candidate.JointKind != ConversationNodeJointKind.SwitchDefault)
                    continue;

                joint = candidate;
                return true;
            }

            return false;
        }

        public override bool SupportsDynamicJoints => true;

        public override ConversationNodeJointPreset? AddDynamicJoint()
        {
            _cases ??= new List<ConversationSwitchCasePreset>();
            _cases.Add(new ConversationSwitchCasePreset());
            SyncSwitchJointsWithCases();

            var createdIndex = _cases.Count - 1;
            return TryGetSwitchCaseJoint(createdIndex, out var joint) ? joint : null;
        }

        public override bool RemoveJoint(ConversationNodeJointPreset joint)
        {
            if (joint == null)
                return false;

            if (joint.JointKind == ConversationNodeJointKind.SwitchDefault)
                return false;

            var caseIndex = Mathf.Max(0, joint.BranchKey);
            if (caseIndex < 0 || caseIndex >= _cases.Count)
                return false;

            _cases.RemoveAt(caseIndex);
            SyncSwitchJointsWithCases();
            return true;
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            _cases ??= new List<ConversationSwitchCasePreset>();
            var copy = new ConversationSwitchNodePreset
            {
                _switchValue = _switchValue,
                _evaluateOrder = _evaluateOrder,
                _cases = new List<ConversationSwitchCasePreset>(),
                _switchJoints = ConversationNodeJointUtility.CloneJointList(_switchJoints),
            };

            for (var i = 0; i < _cases.Count; i++)
            {
                var entry = _cases[i];
                if (entry == null)
                    continue;

                copy._cases.Add(entry.CreateRuntimeCopy());
            }

            copy.SyncSwitchJointsWithCases();

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationCommandOnlyNodePreset : ConversationNodePresetBase
    {
        [BoxGroup("Links")]
        [LabelText("Next Joints")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [HideInInspector]
        [SerializeField]
        List<ConversationNodeJointPreset> _nextNodeJoints =
            new() { ConversationNodeJointPreset.CreateDefault("Next", ConversationNodeJointKind.Default) };

        public override IReadOnlyList<ConversationNodeJointPreset> NextNodeJoints => _nextNodeJoints;
        public override bool UsesSpeakerSlot => false;
        public override bool UsesDialogueTagOverride => false;

        protected override string BuildDebugViewText()
        {
            return "Command";
        }

        public override ConversationNodePresetBase CreateRuntimeCopy()
        {
            var copy = new ConversationCommandOnlyNodePreset
            {
                _nextNodeJoints = ConversationNodeJointUtility.CloneJointList(_nextNodeJoints),
            };

            CopyCommonTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationFlowGraphStatePreset : IDynamicManagedRefValue
    {
        [BoxGroup("Graph")]
        [LabelText("Zoom")]
        [MinValue(0.1f)]
        [SerializeField]
        float _zoom = 1f;

        [BoxGroup("Graph")]
        [LabelText("Pan")]
        [SerializeField]
        Vector2 _pan = Vector2.zero;

        [BoxGroup("Graph")]
        [LabelText("Selected Node Id")]
        [SerializeField]
        int _selectedNodeId;

        [BoxGroup("Graph")]
        [LabelText("Node Views")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeField]
        List<ConversationNodeGraphViewPreset> _nodeViews = new();

        public float Zoom => Mathf.Max(0.1f, _zoom);
        public Vector2 Pan => _pan;
        public int SelectedNodeId => _selectedNodeId;
        public IReadOnlyList<ConversationNodeGraphViewPreset> NodeViews => _nodeViews;

        public void SetZoom(float zoom)
        {
            _zoom = Mathf.Max(0.1f, zoom);
        }

        public void SetPan(Vector2 pan)
        {
            _pan = pan;
        }

        public void SetSelectedNodeId(int nodeId)
        {
            _selectedNodeId = nodeId;
        }

        public ConversationNodeGraphViewPreset GetOrCreateNodeView(int nodeId)
        {
            for (var i = 0; i < _nodeViews.Count; i++)
            {
                var nodeView = _nodeViews[i];
                if (nodeView == null || nodeView.NodeId != nodeId)
                    continue;

                return nodeView;
            }

            var created = new ConversationNodeGraphViewPreset();
            created.SetNodeId(nodeId);
            _nodeViews.Add(created);
            return created;
        }

        public void RemoveNodeView(int nodeId)
        {
            for (var i = _nodeViews.Count - 1; i >= 0; i--)
            {
                var nodeView = _nodeViews[i];
                if (nodeView == null || nodeView.NodeId != nodeId)
                    continue;

                _nodeViews.RemoveAt(i);
            }

            if (_selectedNodeId == nodeId)
                _selectedNodeId = 0;
        }

        public ConversationFlowGraphStatePreset CreateRuntimeCopy()
        {
            var copy = new ConversationFlowGraphStatePreset
            {
                _zoom = _zoom,
                _pan = _pan,
                _selectedNodeId = _selectedNodeId,
                _nodeViews = new List<ConversationNodeGraphViewPreset>(),
            };

            for (var i = 0; i < _nodeViews.Count; i++)
            {
                var view = _nodeViews[i];
                if (view == null)
                    continue;

                copy._nodeViews.Add(view.CreateRuntimeCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class ConversationNodeGraphViewPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Node View")]
        [LabelText("Node Id")]
        [SerializeField]
        int _nodeId;

        [BoxGroup("Node View")]
        [LabelText("Position")]
        [SerializeField]
        Vector2 _position = Vector2.zero;

        [BoxGroup("Node View")]
        [LabelText("Size")]
        [SerializeField]
        Vector2 _size = new(320f, 84f);

        [BoxGroup("Node View")]
        [LabelText("Expanded")]
        [SerializeField]
        bool _isExpanded = true;

        public int NodeId => _nodeId;
        public Vector2 Position => _position;
        public Vector2 Size => _size;
        public bool IsExpanded => _isExpanded;

        public void SetNodeId(int nodeId)
        {
            _nodeId = Mathf.Max(1, nodeId);
        }

        public void SetPosition(Vector2 position)
        {
            _position = position;
        }

        public void SetSize(Vector2 size)
        {
            var width = Mathf.Max(120f, size.x);
            var height = Mathf.Max(48f, size.y);
            _size = new Vector2(width, height);
        }

        public void SetExpanded(bool isExpanded)
        {
            _isExpanded = isExpanded;
        }

        public ConversationNodeGraphViewPreset CreateRuntimeCopy()
        {
            return new ConversationNodeGraphViewPreset
            {
                _nodeId = _nodeId,
                _position = _position,
                _size = _size,
                _isExpanded = _isExpanded,
            };
        }
    }

    public readonly struct ConversationChoiceHistoryEntry
    {
        public int NodeId { get; }
        public int SelectedIndex { get; }
        public long TimestampUtcTicks { get; }

        public ConversationChoiceHistoryEntry(int nodeId, int selectedIndex, long timestampUtcTicks)
        {
            NodeId = nodeId;
            SelectedIndex = selectedIndex;
            TimestampUtcTicks = timestampUtcTicks;
        }
    }

    public readonly struct ConversationSessionSnapshot
    {
        public string Tag { get; }
        public bool IsActive { get; }
        public int CurrentNodeId { get; }
        public int LastCompletedNodeId { get; }
        public int TurnCount { get; }
        public int LastSelectedIndex { get; }
        public int ChoiceCount { get; }
        public ConversationSessionEndKind EndKind { get; }
        public string EndMessage { get; }

        public ConversationSessionSnapshot(
            string tag,
            bool isActive,
            int currentNodeId,
            int lastCompletedNodeId,
            int turnCount,
            int lastSelectedIndex,
            int choiceCount,
            ConversationSessionEndKind endKind,
            string endMessage)
        {
            Tag = ConversationTagUtility.Normalize(tag);
            IsActive = isActive;
            CurrentNodeId = currentNodeId;
            LastCompletedNodeId = lastCompletedNodeId;
            TurnCount = turnCount;
            LastSelectedIndex = lastSelectedIndex;
            ChoiceCount = choiceCount;
            EndKind = endKind;
            EndMessage = endMessage ?? string.Empty;
        }
    }

    static class ConversationNodeJointUtility
    {
        public static List<ConversationNodeJointPreset> CloneJointList(List<ConversationNodeJointPreset>? source)
        {
            var clone = new List<ConversationNodeJointPreset>();
            if (source == null)
                return clone;

            for (var i = 0; i < source.Count; i++)
            {
                var joint = source[i];
                if (joint == null)
                    continue;

                clone.Add(joint.CreateRuntimeCopy());
            }

            return clone;
        }

        public static int ResolveFirstConnectedNodeId(IReadOnlyList<ConversationNodeJointPreset> joints)
        {
            for (var i = 0; i < joints.Count; i++)
            {
                var joint = joints[i];
                if (joint == null)
                    continue;

                if (joint.SelectedNextNodeId > 0)
                    return joint.SelectedNextNodeId;
            }

            return 0;
        }
    }

    internal static class ConversationTagUtility
    {
        public static string Normalize(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }

        public static string? NormalizeNullable(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;
            return tag.Trim();
        }
    }

    static class ConversationCloneUtility
    {
        public static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }
}
