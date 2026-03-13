#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.AI
{
    /// <summary>
    /// AI State のランタイムテレメトリビューア（Odin Inspector 用）
    /// </summary>
    [Serializable]
    public sealed class AIStateDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion => _telemetry?.TelemetryVersion ?? -1;

        [ShowInInspector, ReadOnly, LabelText("Active Clip")]
        public string? ActiveClip => _snapshotActiveClip;

        [ShowInInspector, ReadOnly, LabelText("Stack Depth")]
        public int StackDepth => _snapshotStackDepth;

        [ShowInInspector, ReadOnly, LabelText("Is Blocked")]
        public bool IsBlocked => _snapshotIsBlocked;

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Clip Stack")]
        public List<ClipStackRow> Stack => GetStack();

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Recent Transitions")]
        public List<TransitionRow> Transitions => GetTransitions();

        IAIStateTelemetry? _telemetry;
        int _lastVersion = -1;
        string? _snapshotActiveClip;
        int _snapshotStackDepth;
        bool _snapshotIsBlocked;

        readonly List<ClipStackRow> _stack = new();
        readonly List<TransitionRow> _transitions = new();

        /// <summary>
        /// テレメトリソースをバインド
        /// </summary>
        public void Bind(IAIStateTelemetry? telemetry)
        {
            _telemetry = telemetry;
            _lastVersion = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            if (_telemetry == null)
                return;

            ApplySnapshot(_telemetry.GetSnapshot());
        }

        List<ClipStackRow> GetStack()
        {
            AutoRefresh();
            return _stack;
        }

        List<TransitionRow> GetTransitions()
        {
            AutoRefresh();
            return _transitions;
        }

        void AutoRefresh()
        {
            if (_telemetry == null)
                return;

            var version = _telemetry.TelemetryVersion;
            if (version == _lastVersion)
                return;

            ApplySnapshot(_telemetry.GetSnapshot());
        }

        void ApplySnapshot(AIStateSnapshot snapshot)
        {
            _lastVersion = snapshot.Version;
            _snapshotActiveClip = snapshot.ActiveClipKey;
            _snapshotStackDepth = snapshot.StackDepth;
            _snapshotIsBlocked = snapshot.IsBlocked;

            _stack.Clear();
            if (snapshot.Stack != null)
            {
                for (int i = 0; i < snapshot.Stack.Count; i++)
                {
                    var entry = snapshot.Stack[i];
                    _stack.Add(new ClipStackRow
                    {
                        Index = entry.Index,
                        StableKey = entry.StableKey,
                        Priority = entry.Priority,
                        IsTop = entry.IsTop,
                        PopRequested = entry.HasPopRequest
                    });
                }
            }

            _transitions.Clear();
            if (snapshot.RecentTransitions != null)
            {
                for (int i = 0; i < snapshot.RecentTransitions.Count; i++)
                {
                    var entry = snapshot.RecentTransitions[i];
                    _transitions.Add(new TransitionRow
                    {
                        Frame = entry.Frame,
                        Description = entry.Description
                    });
                }
            }
        }

        /// <summary>
        /// Clip スタック表示行
        /// </summary>
        [Serializable]
        public sealed class ClipStackRow
        {
            [TableColumnWidth(50)] public int Index;
            [TableColumnWidth(200)] public string StableKey = "";
            [TableColumnWidth(70)] public int Priority;
            [TableColumnWidth(60)] public bool IsTop;
            [TableColumnWidth(100)] public bool PopRequested;
        }

        /// <summary>
        /// 遷移履歴表示行
        /// </summary>
        [Serializable]
        public sealed class TransitionRow
        {
            [TableColumnWidth(80)] public int Frame;
            [MultiLineProperty(2)] public string Description = "";
        }
    }
}
