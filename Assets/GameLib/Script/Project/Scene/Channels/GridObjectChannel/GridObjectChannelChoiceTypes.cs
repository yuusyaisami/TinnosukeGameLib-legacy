#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChoiceEntry : IDynamicManagedRefValue
    {
        [BoxGroup("Entry")]
        [LabelText("Display Name")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _displayName = string.Empty;

        [BoxGroup("Commands")]
        [LabelText("On Spawn Commands")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Choice.Entry.OnSpawn")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Selected Commands")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Choice.Entry.OnSelected")]
        CommandListData _selectedCommands = new();

        [BoxGroup("Vars")]
        [LabelText("On Selected Vars")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        VarStorePayload _selectedVars = new();

        public string DisplayName => _displayName ?? string.Empty;
        public CommandListData SpawnCommands => _spawnCommands;
        public CommandListData SelectedCommands => _selectedCommands;
        public VarStorePayload SelectedVars => _selectedVars;

        public GridObjectChoiceEntry CreateRuntimeCopy()
        {
            return new GridObjectChoiceEntry
            {
                _displayName = _displayName,
                _spawnCommands = CloneCommandList(_spawnCommands),
                _selectedCommands = CloneCommandList(_selectedCommands),
                _selectedVars = _selectedVars ?? new VarStorePayload(),
            };
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [Serializable]
    public sealed class GridObjectChoiceWaitOptions : IDynamicManagedRefValue
    {
        [BoxGroup("Wait")]
        [LabelText("Allow Cancel")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _allowCancel = true;

        [BoxGroup("Wait")]
        [LabelText("Use Timeout")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _useTimeout;

        [BoxGroup("Wait")]
        [ShowIf(nameof(_useTimeout))]
        [LabelText("Timeout Seconds")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [MinValue(0f)]
        DynamicValue<float> _timeoutSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Wait")]
        [LabelText("Concurrency Policy")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChoiceConcurrencyPolicy _concurrencyPolicy = GridObjectChoiceConcurrencyPolicy.ErrorIfActive;

        [BoxGroup("Wait")]
        [LabelText("Keep Alive")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _keepAliveAfterCompletion;

        public bool AllowCancel => _allowCancel;
        public bool UseTimeout => _useTimeout;
        public DynamicValue<float> TimeoutSeconds => _timeoutSeconds;
        public GridObjectChoiceConcurrencyPolicy ConcurrencyPolicy => _concurrencyPolicy;
        public bool KeepAliveAfterCompletion => _keepAliveAfterCompletion;

        public float ResolveTimeoutSeconds(IDynamicContext context)
        {
            if (!_useTimeout)
                return 0f;

            return Mathf.Max(0f, _timeoutSeconds.GetOrDefault(context, 0f));
        }

        public GridObjectChoiceWaitOptions CreateRuntimeCopy()
        {
            return new GridObjectChoiceWaitOptions
            {
                _allowCancel = _allowCancel,
                _useTimeout = _useTimeout,
                _timeoutSeconds = _timeoutSeconds,
                _concurrencyPolicy = _concurrencyPolicy,
                _keepAliveAfterCompletion = _keepAliveAfterCompletion,
            };
        }
    }

    [Serializable]
    public sealed class GridObjectChoiceRequest
    {
        [BoxGroup("Choice")]
        [LabelText("Entries")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<GridObjectChoiceEntry> _entries = new();

        [BoxGroup("Choice")]
        [LabelText("Bind Overrides")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelBindRequest _bindRequest = new();

        [BoxGroup("Choice")]
        [LabelText("Wait Options")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChoiceWaitOptions _waitOptions = new();

        public List<GridObjectChoiceEntry> Entries => _entries;
        public GridObjectChannelBindRequest BindRequest => _bindRequest;
        public GridObjectChoiceWaitOptions WaitOptions => _waitOptions;

        public GridObjectChoiceRequest CreateRuntimeCopy()
        {
            var copy = new GridObjectChoiceRequest
            {
                _bindRequest = _bindRequest?.Clone() ?? new GridObjectChannelBindRequest(),
                _waitOptions = _waitOptions?.CreateRuntimeCopy() ?? new GridObjectChoiceWaitOptions(),
            };

            if (_entries != null && _entries.Count > 0)
            {
                for (var i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (entry == null)
                        continue;

                    copy._entries.Add(entry.CreateRuntimeCopy());
                }
            }

            return copy;
        }
    }

    public sealed class GridObjectChoiceSessionResult
    {
        public GridObjectChoiceCompletionKind CompletionKind { get; }
        public int SelectedIndex { get; }
        public ButtonChannelPhase TriggeredPhase { get; }
        public string Message { get; }

        public bool IsSelected => CompletionKind == GridObjectChoiceCompletionKind.Selected;
        public bool IsCanceledLike => CompletionKind == GridObjectChoiceCompletionKind.Canceled || CompletionKind == GridObjectChoiceCompletionKind.Replaced;
        public bool IsSuccess => CompletionKind == GridObjectChoiceCompletionKind.Selected ||
                                 CompletionKind == GridObjectChoiceCompletionKind.Canceled ||
                                 CompletionKind == GridObjectChoiceCompletionKind.Timeout ||
                                 CompletionKind == GridObjectChoiceCompletionKind.Replaced;

        GridObjectChoiceSessionResult(
            GridObjectChoiceCompletionKind completionKind,
            int selectedIndex,
            ButtonChannelPhase triggeredPhase,
            string message)
        {
            CompletionKind = completionKind;
            SelectedIndex = selectedIndex;
            TriggeredPhase = triggeredPhase;
            Message = message ?? string.Empty;
        }

        public static GridObjectChoiceSessionResult Selected(int selectedIndex, ButtonChannelPhase triggeredPhase)
            => new(GridObjectChoiceCompletionKind.Selected, selectedIndex, triggeredPhase, string.Empty);

        public static GridObjectChoiceSessionResult Canceled(string message = "")
            => new(GridObjectChoiceCompletionKind.Canceled, -1, ButtonChannelPhase.Idle, message);

        public static GridObjectChoiceSessionResult Timeout(string message = "")
            => new(GridObjectChoiceCompletionKind.Timeout, -1, ButtonChannelPhase.Idle, message);

        public static GridObjectChoiceSessionResult Replaced(string message = "")
            => new(GridObjectChoiceCompletionKind.Replaced, -1, ButtonChannelPhase.Idle, message);

        public static GridObjectChoiceSessionResult Failed(string message)
            => new(GridObjectChoiceCompletionKind.Failed, -1, ButtonChannelPhase.Idle, message);
    }
}
