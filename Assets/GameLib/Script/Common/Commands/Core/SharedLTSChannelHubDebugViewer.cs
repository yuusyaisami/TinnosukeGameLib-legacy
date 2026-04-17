#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.VarStoreKeys;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands
{
    /// <summary>
    /// Runtime debug viewer for SharedLTSChannelHub.
    /// </summary>
    [Serializable]
    public sealed class SharedLTSChannelHubDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion => _telemetry?.TelemetryVersion ?? -1;

        [ShowInInspector, ReadOnly, LabelText("Channel Count")]
        public int ChannelCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.ChannelCount;
            }
        }

        [ShowInInspector]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            DraggableItems = false,
            IsReadOnly = true,
            ShowPaging = true,
            NumberOfItemsPerPage = 4,
            ListElementLabelName = nameof(ChannelRow.Header))]
        [LabelText("Registered Shared LTS Channels")]
        public List<ChannelRow> Channels
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        ISharedLTSChannelHubTelemetry? _telemetry;
        SharedLTSChannelHubSnapshot _snapshot;
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<ChannelRow> _rows = new();

        public void Bind(ISharedLTSChannelHubTelemetry telemetry)
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
            var telemetryVersion = _telemetry.TelemetryVersion;
            if (telemetryVersion != _lastVersion)
            {
                ApplySnapshot(_telemetry.GetTelemetrySnapshot());
                return;
            }

            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void ApplySnapshot(in SharedLTSChannelHubSnapshot snapshot)
        {
            _snapshot = snapshot;
            _lastVersion = snapshot.Version;
            _lastRefreshFrame = Time.frameCount;

            _rows.Clear();
            var channels = snapshot.Channels;
            if (channels == null)
                return;

            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                var row = new ChannelRow
                {
                    Tag = channel.Tag,
                    Status = channel.Status,
                    ScopeLabel = channel.ScopeLabel,
                    ScopePath = channel.ScopePath,
                    ScopeKind = channel.ScopeKind,
                    ScopeId = channel.ScopeId,
                    ScopeCategory = channel.ScopeCategory,
                    Active = channel.IsActive,
                    Visible = channel.IsVisible,
                    BlackboardStatus = channel.BlackboardStatus,
                    LocalVarCount = channel.LocalVarCount,
                    TableCount = channel.TableCount,
                };

                var localVars = channel.LocalVars;
                if (localVars != null)
                {
                    for (int j = 0; j < localVars.Count; j++)
                    {
                        var variable = localVars[j];
                        row.LocalVars.Add(new VariableRow
                        {
                            VarId = variable.VarId,
                            Key = variable.Key,
                            Kind = variable.Kind,
                            Version = variable.Version,
                            Value = variable.Value,
                        });
                    }
                }

                var tables = channel.Tables;
                if (tables != null)
                {
                    for (int j = 0; j < tables.Count; j++)
                    {
                        var table = tables[j];
                        row.Tables.Add(new TableRow
                        {
                            TableVarId = table.TableVarId,
                            Key = table.Key,
                            Version = table.Version,
                            RowCount = table.RowCount,
                            ColumnCount = table.ColumnCount,
                            CellCount = table.CellCount,
                        });
                    }
                }

                _rows.Add(row);
            }
        }

        [Serializable]
        public sealed class ChannelRow
        {
            [TableColumnWidth(160)] public string Tag = string.Empty;
            [TableColumnWidth(100)] public string Status = string.Empty;
            [TableColumnWidth(180)] public string ScopeLabel = string.Empty;
            [TableColumnWidth(160)] public string ScopePath = string.Empty;
            [TableColumnWidth(90)] public string ScopeKind = string.Empty;
            [TableColumnWidth(120)] public string ScopeId = string.Empty;
            [TableColumnWidth(120)] public string ScopeCategory = string.Empty;
            [TableColumnWidth(60)] public bool Active;
            [TableColumnWidth(60)] public bool Visible;
            [TableColumnWidth(100)] public string BlackboardStatus = string.Empty;
            [TableColumnWidth(70)] public int LocalVarCount;
            [TableColumnWidth(70)] public int TableCount;

            [ListDrawerSettings(
                ShowFoldout = true,
                DefaultExpandedState = false,
                DraggableItems = false,
                IsReadOnly = true,
                ShowPaging = true,
                NumberOfItemsPerPage = 8,
                ListElementLabelName = nameof(VariableRow.Header))]
            [LabelText("Local Vars")]
            public List<VariableRow> LocalVars = new();

            [ListDrawerSettings(
                ShowFoldout = true,
                DefaultExpandedState = false,
                DraggableItems = false,
                IsReadOnly = true,
                ShowPaging = true,
                NumberOfItemsPerPage = 6,
                ListElementLabelName = nameof(TableRow.Header))]
            [LabelText("Table Vars")]
            public List<TableRow> Tables = new();

            public string Header => $"{Tag} [{Status}] {ScopeLabel} vars={LocalVarCount} tables={TableCount}";
        }

        [Serializable]
        public sealed class VariableRow
        {
            [TableColumnWidth(80)] public int VarId;
            [TableColumnWidth(180)] public string Key = string.Empty;
            [TableColumnWidth(100)] public string Kind = string.Empty;
            [TableColumnWidth(70)] public int Version;
            [MultiLineProperty(2)] public string Value = string.Empty;

            public string Header => string.IsNullOrWhiteSpace(Key) ? $"varId={VarId}" : $"{Key} ({Kind})";
        }

        [Serializable]
        public sealed class TableRow
        {
            [TableColumnWidth(80)] public int TableVarId;
            [TableColumnWidth(180)] public string Key = string.Empty;
            [TableColumnWidth(70)] public int Version;
            [TableColumnWidth(70)] public int RowCount;
            [TableColumnWidth(70)] public int ColumnCount;
            [TableColumnWidth(70)] public int CellCount;

            public string Header => string.IsNullOrWhiteSpace(Key) ? $"tableVarId={TableVarId}" : $"{Key} ({CellCount} cells)";
        }
    }
}