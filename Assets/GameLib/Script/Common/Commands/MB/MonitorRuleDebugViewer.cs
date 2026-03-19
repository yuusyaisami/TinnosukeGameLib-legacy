#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands
{
    [Serializable]
    public sealed class MonitorRuleDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _telemetry != null;

        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion
        {
            get
            {
                AutoRefresh();
                return _snapshot.Version;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Hub Available")]
        public bool HubAvailable
        {
            get
            {
                AutoRefresh();
                return _snapshot.HubAvailable;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Scope Kind")]
        public string ScopeKind
        {
            get
            {
                AutoRefresh();
                return _snapshot.ScopeKind;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Scope Id")]
        public string ScopeId
        {
            get
            {
                AutoRefresh();
                return _snapshot.ScopeId;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Rule Count")]
        public int RuleCount
        {
            get
            {
                AutoRefresh();
                return _rows.Count;
            }
        }

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<RuleRow> Rules
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        IMonitorRuleTelemetry? _telemetry;
        MonitorRuleTelemetrySnapshot _snapshot;
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<RuleRow> _rows = new();

        public void Bind(IMonitorRuleTelemetry telemetry)
        {
            _telemetry = telemetry;
            _lastVersion = -1;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            if (_telemetry == null)
                return;

            ApplySnapshot(_telemetry.GetSnapshot());
        }

        void AutoRefresh()
        {
            if (_telemetry == null)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            var telemetryVersion = _telemetry.TelemetryVersion;
            if (telemetryVersion == _lastVersion)
            {
                _lastRefreshFrame = frame;
                return;
            }

            ApplySnapshot(_telemetry.GetSnapshot());
        }

        void ApplySnapshot(in MonitorRuleTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot;
            _lastVersion = snapshot.Version;
            _lastRefreshFrame = Time.frameCount;

            _rows.Clear();
            var rules = snapshot.Rules;
            if (rules == null)
                return;

            for (int i = 0; i < rules.Count; i++)
            {
                var src = rules[i];
                _rows.Add(new RuleRow
                {
                    RuleName = src.RuleName,
                    Kind = src.RuleKind.ToString(),
                    RuntimeRegistered = src.RuntimeRegistered,
                    IsTrue = src.RuntimeIsTrue,
                    RunningCount = src.RuntimeRunningCount,
                    RunningPhases = src.RuntimeRunningPhases,
                    Behavior = src.Behavior.ToString(),
                    CancelOnChange = src.CancelRunningOnConditionChange,
                    ExecuteInitialCondition = src.ExecuteInitialCondition,
                    ConditionSource = src.ConditionSourceType,
                    DependentKeys = src.DependentKeys != null && src.DependentKeys.Count > 0
                        ? string.Join(", ", src.DependentKeys)
                        : string.Empty,
                    EventName = src.EventName,
                    EventTarget = src.EventTargetKind.ToString(),
                    ValueSource = src.ValueSource.ToString(),
                    ValueMode = src.ValueChangeMode.ToString(),
                    VarStoreVarId = src.VarStoreVarId,
                    BlackboardVarId = src.BlackboardVarId,
                    BlackboardReadScope = src.BlackboardReadScope.ToString(),
                    ScalarKey = src.ScalarKey,
                    ChangeEpsilon = src.ChangeEpsilon,
                    ExecuteInitialValueChangedEnter = src.ExecuteInitialValueChangedEnter,
                    InitialValueChangedEnterDelaySeconds = src.InitialValueChangedEnterDelaySeconds,
                    EnterCount = src.OnEnterCommandCount,
                    ExitCount = src.OnExitCommandCount,
                    WhileTrueCount = src.WhileTrueCommandCount,
                    WhileTrueInterval = src.WhileTrueIntervalSeconds,
                    WhileFalseCount = src.WhileFalseCommandCount,
                    WhileFalseInterval = src.WhileFalseIntervalSeconds,
                });
            }
        }

        [Serializable]
        public sealed class RuleRow
        {
            [TableColumnWidth(170)] public string RuleName = string.Empty;
            [TableColumnWidth(120)] public string Kind = string.Empty;
            [TableColumnWidth(70)] public bool RuntimeRegistered;
            [TableColumnWidth(56)] public bool IsTrue;
            [TableColumnWidth(70)] public int RunningCount;
            [TableColumnWidth(150)] public string RunningPhases = string.Empty;
            [TableColumnWidth(120)] public string Behavior = string.Empty;
            [TableColumnWidth(70)] public bool CancelOnChange;
            [TableColumnWidth(70)] public bool ExecuteInitialCondition;
            [TableColumnWidth(110)] public string ConditionSource = string.Empty;
            [TableColumnWidth(200)] public string DependentKeys = string.Empty;
            [TableColumnWidth(120)] public string EventName = string.Empty;
            [TableColumnWidth(90)] public string EventTarget = string.Empty;
            [TableColumnWidth(90)] public string ValueSource = string.Empty;
            [TableColumnWidth(90)] public string ValueMode = string.Empty;
            [TableColumnWidth(80)] public int VarStoreVarId;
            [TableColumnWidth(80)] public int BlackboardVarId;
            [TableColumnWidth(110)] public string BlackboardReadScope = string.Empty;
            [TableColumnWidth(140)] public string ScalarKey = string.Empty;
            [TableColumnWidth(80)] public float ChangeEpsilon;
            [TableColumnWidth(70)] public bool ExecuteInitialValueChangedEnter;
            [TableColumnWidth(76)] public float InitialValueChangedEnterDelaySeconds;
            [TableColumnWidth(56)] public int EnterCount;
            [TableColumnWidth(56)] public int ExitCount;
            [TableColumnWidth(66)] public int WhileTrueCount;
            [TableColumnWidth(76)] public float WhileTrueInterval;
            [TableColumnWidth(66)] public int WhileFalseCount;
            [TableColumnWidth(76)] public float WhileFalseInterval;
        }
    }
}
