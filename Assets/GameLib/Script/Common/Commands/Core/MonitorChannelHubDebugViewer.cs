using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.Commands
{
    /// <summary>
    /// Runtime telemetry viewer for MonitorChannelHub. Designed for Odin Inspector.
    /// </summary>
    [Serializable]
    public sealed class MonitorChannelHubDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion => _telemetry?.TelemetryVersion ?? -1;

        [ShowInInspector, ReadOnly, LabelText("Evaluation Mode")]
        public MonitorEvaluationMode EvaluationMode => _snapshotEvaluationMode;

        [ShowInInspector, ReadOnly, LabelText("Default Behavior")]
        public ExecutionBehavior DefaultExecutionBehavior => _snapshotDefaultBehavior;

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<RuleRow> Rules => GetRules();

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<VariableRow> Variables => GetVariables();

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<RunningRow> Running => GetRunning();

        IMonitorChannelHubTelemetry _telemetry;
        int _lastVersion = -1;
        MonitorEvaluationMode _snapshotEvaluationMode;
        ExecutionBehavior _snapshotDefaultBehavior;

        readonly List<RuleRow> _rules = new();
        readonly List<VariableRow> _variables = new();
        readonly List<RunningRow> _running = new();

        public void Bind(IMonitorChannelHubTelemetry telemetry)
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

        List<RuleRow> GetRules()
        {
            AutoRefresh();
            return _rules;
        }

        List<VariableRow> GetVariables()
        {
            AutoRefresh();
            return _variables;
        }

        List<RunningRow> GetRunning()
        {
            AutoRefresh();
            return _running;
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

        void ApplySnapshot(MonitorHubSnapshot snapshot)
        {
            _lastVersion = snapshot.Version;
            _snapshotEvaluationMode = snapshot.EvaluationMode;
            _snapshotDefaultBehavior = snapshot.DefaultExecutionBehavior;

            _rules.Clear();
            if (snapshot.Rules != null)
            {
                for (int i = 0; i < snapshot.Rules.Count; i++)
                {
                    var rule = snapshot.Rules[i];
                    _rules.Add(new RuleRow
                    {
                        Rule = rule.RuleName,
                        IsTrue = rule.IsTrue,
                        Behavior = rule.Behavior,
                        Condition = rule.Condition,
                        Keys = rule.DependentKeys != null && rule.DependentKeys.Count > 0
                            ? string.Join(", ", rule.DependentKeys)
                            : string.Empty,
                    });
                }
            }

            _variables.Clear();
            if (snapshot.Variables != null)
            {
                for (int i = 0; i < snapshot.Variables.Count; i++)
                {
                    var variable = snapshot.Variables[i];
                    _variables.Add(new VariableRow
                    {
                        Key = variable.Key,
                        Type = variable.Type,
                        Value = variable.Value,
                        Version = variable.Version,
                    });
                }
            }

            _running.Clear();
            if (snapshot.RunningEntries != null)
            {
                for (int i = 0; i < snapshot.RunningEntries.Count; i++)
                {
                    var running = snapshot.RunningEntries[i];
                    _running.Add(new RunningRow
                    {
                        Rule = running.RuleName,
                        Phase = running.Phase,
                        Completed = running.Completed,
                    });
                }
            }
        }

        [Serializable]
        public sealed class RuleRow
        {
            [TableColumnWidth(160)] public string Rule;
            [TableColumnWidth(60)] public bool IsTrue;
            [TableColumnWidth(120)] public ExecutionBehavior Behavior;
            [MultiLineProperty(2)] public string Condition;
            [TableColumnWidth(200)] public string Keys;
        }

        [Serializable]
        public sealed class VariableRow
        {
            [TableColumnWidth(180)] public string Key;
            [TableColumnWidth(100)] public string Type;
            [MultiLineProperty(2)] public string Value;
            [TableColumnWidth(70)] public int Version;
        }

        [Serializable]
        public sealed class RunningRow
        {
            [TableColumnWidth(160)] public string Rule;
            [TableColumnWidth(80)] public string Phase;
            [TableColumnWidth(70)] public bool Completed;
        }
    }
}
