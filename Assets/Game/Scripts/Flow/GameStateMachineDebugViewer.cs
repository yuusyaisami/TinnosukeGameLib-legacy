#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Actions
{
    [Serializable]
    public sealed class GameStateMachineDebugViewer
    {
        const int MaxLogCount = 64;

        IGameStateMachineService? _service;
        IGameStateMachineSettings? _settings;
        GameState _lastObservedState;
        bool _hasObservedState;
        readonly List<string> _stateChangeLogs = new();

        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _service != null;

        [ShowInInspector, ReadOnly, LabelText("Current State")]
        public string CurrentState
        {
            get
            {
                AutoRefresh();
                return _service != null ? _service.GetCurrentState().ToString() : "(none)";
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Initial State")]
        public string InitialState
        {
            get
            {
                AutoRefresh();
                return _settings != null ? _settings.InitialState.ToString() : "(none)";
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Execute Initial Commands")]
        public bool ExecuteInitialCommandsOnAcquire
        {
            get
            {
                AutoRefresh();
                return _settings != null && _settings.ExecuteInitialStateCommandsOnAcquire;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Initial Delay Frames")]
        public int InitialDelayFramesOnAcquire
        {
            get
            {
                AutoRefresh();
                return _settings?.InitialStateCommandDelayFramesOnAcquire ?? 0;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("State Command Count")]
        public int StateCommandCount
        {
            get
            {
                AutoRefresh();
                return _settings?.StateCommands?.Length ?? 0;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("State Change Logs")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, ShowIndexLabels = true, ShowPaging = false)]
        public List<string> StateChangeLogs
        {
            get
            {
                AutoRefresh();
                return _stateChangeLogs;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("State Commands")]
        [MultiLineProperty]
        public string StateCommandSummary
        {
            get
            {
                AutoRefresh();
                var entries = _settings?.StateCommands;
                if (entries == null || entries.Length == 0)
                    return "(none)";

                var sb = new StringBuilder();
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry == null)
                        continue;

                    var startCount = entry.startCommands?.Count ?? 0;
                    var endCount = entry.endCommands?.Count ?? 0;
                    sb.Append(entry.key)
                      .Append("  Start=")
                      .Append(startCount)
                      .Append("  End=")
                      .Append(endCount);
                    if (i < entries.Length - 1)
                        sb.AppendLine();
                }

                return sb.Length == 0 ? "(none)" : sb.ToString();
            }
        }

        public void Bind(IGameStateMachineService service, IGameStateMachineSettings settings)
        {
            _service = service;
            _settings = settings;

            _stateChangeLogs.Clear();
            _hasObservedState = false;
            AutoRefresh();
        }

        void AutoRefresh()
        {
            if (_service == null)
                return;

            var current = _service.GetCurrentState();
            if (!_hasObservedState)
            {
                _lastObservedState = current;
                _hasObservedState = true;
                AppendLog($"[Init f={Time.frameCount}] {current}");
                return;
            }

            if (_lastObservedState == current)
                return;

            AppendLog($"[Change f={Time.frameCount}] {_lastObservedState} -> {current}");
            _lastObservedState = current;
        }

        void AppendLog(string message)
        {
            if (_stateChangeLogs.Count >= MaxLogCount)
                _stateChangeLogs.RemoveAt(0);

            _stateChangeLogs.Add(message);
        }
    }
}
