#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StateMachine
{
    [Serializable]
    public sealed class StateAnimationDebugViewer
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

        [ShowInInspector, ReadOnly, LabelText("Profile")]
        public string ProfileName
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.ProfileName) ? "(none)" : _snapshot.ProfileName;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Current State")]
        public string CurrentState
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.CurrentState) ? "(none)" : _snapshot.CurrentState;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Current Layer")]
        public string CurrentLayer
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.CurrentLayer) ? "(none)" : _snapshot.CurrentLayer;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Machine Revision")]
        public uint MachineRevision
        {
            get
            {
                AutoRefresh();
                return _snapshot.MachineRevision;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Evaluation")]
        [MultiLineProperty]
        public string EvaluationSummary
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.EvaluationSummary) ? "(none)" : _snapshot.EvaluationSummary;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Active Rule")]
        public string ActiveRule
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.ActiveRuleHeader) ? "(none)" : _snapshot.ActiveRuleHeader;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Active Channel")]
        public string ActiveChannel
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.ActiveRuleChannelTag) ? "(none)" : _snapshot.ActiveRuleChannelTag;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Active Priority")]
        public int ActivePriority
        {
            get
            {
                AutoRefresh();
                return _snapshot.ActiveRulePriority;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Has Player")]
        public bool HasCurrentPlayer
        {
            get
            {
                AutoRefresh();
                return _snapshot.HasCurrentPlayer;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Has FlipX")]
        public bool HasFlipX
        {
            get
            {
                AutoRefresh();
                return _snapshot.HasFlipX;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("FlipX")]
        public bool LastFlipX
        {
            get
            {
                AutoRefresh();
                return _snapshot.LastFlipX;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Flip Pending")]
        public bool PendingFlipXActive
        {
            get
            {
                AutoRefresh();
                return _snapshot.PendingFlipXActive;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Pending Target")]
        public bool PendingFlipXValue
        {
            get
            {
                AutoRefresh();
                return _snapshot.PendingFlipXValue;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Pending Elapsed (s)")]
        public float PendingFlipXElapsedSeconds
        {
            get
            {
                AutoRefresh();
                return _snapshot.PendingFlipXElapsedSeconds;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Rule Apply FlipX")]
        public bool ActiveRuleApplyFlipX
        {
            get
            {
                AutoRefresh();
                return _snapshot.ActiveRuleApplyFlipX;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("FlipX True Option")]
        public string ActiveRuleFlipXTrueOptionValue
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.ActiveRuleFlipXTrueOptionValue)
                    ? "(empty)"
                    : _snapshot.ActiveRuleFlipXTrueOptionValue;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Flip Decision")]
        [MultiLineProperty]
        public string FlipDecisionDetail
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.FlipDecisionDetail)
                    ? "(none)"
                    : _snapshot.FlipDecisionDetail;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Flip Configured")]
        public string FlipConfiguredOptionValue
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.FlipConfiguredOptionValue)
                    ? "(empty)"
                    : _snapshot.FlipConfiguredOptionValue;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Flip Derived Key")]
        public string FlipDerivedOptionKey
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.FlipDerivedOptionKey)
                    ? "(empty)"
                    : _snapshot.FlipDerivedOptionKey;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Resolved By Derived")]
        public string FlipResolvedByDerivedKey
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.FlipResolvedByDerivedKey)
                    ? "(empty)"
                    : _snapshot.FlipResolvedByDerivedKey;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Resolved By Configured")]
        public string FlipResolvedByConfiguredKey
        {
            get
            {
                AutoRefresh();
                return string.IsNullOrEmpty(_snapshot.FlipResolvedByConfiguredKey)
                    ? "(empty)"
                    : _snapshot.FlipResolvedByConfiguredKey;
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

        IStateAnimationTelemetry? _telemetry;
        StateAnimationTelemetrySnapshot _snapshot;
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<RuleRow> _rows = new();

        public void Bind(IStateAnimationTelemetry telemetry)
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

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
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

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void ApplySnapshot(in StateAnimationTelemetrySnapshot snapshot)
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
                var rule = rules[i];
                _rows.Add(new RuleRow
                {
                    Rule = rule.RuleHeader,
                    Matched = rule.Matched,
                    Reason = rule.Reason,
                    Priority = rule.Priority,
                    State = rule.StateKey,
                    Layer = rule.LayerKey,
                    Channel = rule.ChannelTag,
                    ApplyFlipX = rule.ApplyFlipX,
                    FlipXTrueOption = string.IsNullOrEmpty(rule.FlipXTrueOptionValue) ? "(empty)" : rule.FlipXTrueOptionValue,
                });
            }
        }

        [Serializable]
        public sealed class RuleRow
        {
            [TableColumnWidth(150)] public string Rule = string.Empty;
            [TableColumnWidth(60)] public bool Matched;
            [TableColumnWidth(220)] public string Reason = string.Empty;
            [TableColumnWidth(70)] public int Priority;
            [TableColumnWidth(120)] public string State = string.Empty;
            [TableColumnWidth(120)] public string Layer = string.Empty;
            [TableColumnWidth(120)] public string Channel = string.Empty;
            [TableColumnWidth(70)] public bool ApplyFlipX;
            [TableColumnWidth(150)] public string FlipXTrueOption = string.Empty;
        }
    }
}
