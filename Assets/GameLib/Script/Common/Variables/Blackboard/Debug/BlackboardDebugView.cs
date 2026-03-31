#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    [Serializable]
    public sealed class BlackboardDebugView
    {
        [FoldoutGroup("Query Inputs")]
        [LabelText("Stable Key / Alias")]
        [Tooltip("Specify the stable key, alias, or numeric ID to inspect via TryGlobalGetVariant.")]
        public string queryKey = string.Empty;

        [FoldoutGroup("Query Inputs")]
        [LabelText("Var Id Override")]
        [Tooltip("Numerical varId that takes precedence over the key/alias when non-zero.")]
        [VarIdDropdown]
        public int queryVarId;

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Input Used")]
        public string LastQueryInput { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Queried Id")]
        public int LastQueriedVarId { get; private set; }

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Resolved Key")]
        public string LastResolvedKey { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Kind")]
        public string LastQueryKind { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Value")]
        public string LastQueryValue { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Status")]
        public string LastQueryStatus { get; private set; } = "(idle)";

        [FoldoutGroup("Local Store")]
        [ShowInInspector, ReadOnly, LabelText("Registered Vars")]
        public List<BlackboardVarRow> LocalVars => BuildLocalRows();

        [FoldoutGroup("Grid Store")]
        [ShowInInspector, ReadOnly, LabelText("Status")]
        public string GridStatus
        {
            get
            {
                BuildGridRows();
                return _gridStatus;
            }
        }

        [FoldoutGroup("Grid Store")]
        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Rows")]
        public List<GridBlackboardRow> GridRows
        {
            get
            {
                BuildGridRows();
                return _gridRows;
            }
        }

        [NonSerialized]
        IBlackboardService? _blackboard;
        [NonSerialized]
        IGridBlackboardService? _gridBlackboard;
        readonly List<GridBlackboardRow> _gridRows = new();
        string _gridStatus = "(idle)";

#if UNITY_EDITOR
        [NonSerialized]
        UnityEngine.MonoBehaviour? _ownerForEditor;
        static double _lastRepaintTime = 0.0;
        const double RepaintIntervalSeconds = 0.1; // throttle frequency
#endif

        public void Initialize(IBlackboardService blackboard)
        {
            Initialize(blackboard, null, null);
        }

        /// <summary>
        /// Editor-only initialize that accepts owner MonoBehaviour to allow forcing Inspector repaints.
        /// </summary>
        public void Initialize(IBlackboardService blackboard, UnityEngine.MonoBehaviour owner)
        {
            Initialize(blackboard, null, owner);
        }

        public void Initialize(IBlackboardService blackboard, IGridBlackboardService? gridBlackboard, UnityEngine.MonoBehaviour? owner)
        {
            _blackboard = blackboard;
            _gridBlackboard = gridBlackboard;
            _gridRows.Clear();
            _gridStatus = "(idle)";
            LastQueryStatus = "(idle)";
            LastQueryValue = "(none)";
            LastQueryKind = "(none)";
            LastResolvedKey = "(none)";
            LastQueriedVarId = 0;
            LastQueryInput = "(none)";
#if UNITY_EDITOR
            _ownerForEditor = owner;
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
        }

        public void Dispose()
        {
            _blackboard = null;
            _gridBlackboard = null;
            _gridRows.Clear();
            _gridStatus = "(idle)";
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            _ownerForEditor = null;
#endif
        }

#if UNITY_EDITOR
        void OnEditorUpdate()
        {
            // Throttle to avoid excessive editor CPU usage
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintIntervalSeconds)
                return;

            _lastRepaintTime = now;

            if (_blackboard == null || _ownerForEditor == null)
                return;

            if (UnityEditor.Selection.activeObject != _ownerForEditor)
                return;

            UnityEditor.EditorUtility.SetDirty(_ownerForEditor);
        }
#endif

        [FoldoutGroup("Query Inputs")]
        [Button(ButtonSizes.Medium)]
        public void InspectGlobalVar()
        {
            if (_blackboard == null)
            {
                LastQueryStatus = "Blackboard not ready";
                return;
            }

            var trimmedKey = string.IsNullOrWhiteSpace(queryKey) ? string.Empty : queryKey.Trim();
            LastQueryInput = string.IsNullOrEmpty(trimmedKey) ? "(none)" : trimmedKey;

            var resolvedId = ResolveVarId(trimmedKey);
            LastQueriedVarId = resolvedId;

            if (resolvedId == 0)
            {
                LastResolvedKey = "(none)";
                LastQueryKind = "(none)";
                LastQueryValue = "(none)";
                LastQueryStatus = "Specify a valid varId or stable key.";
                return;
            }

            LastResolvedKey = VarIdResolver.TryGetIdToStable(resolvedId) ?? LastQueryInput;

            if (_blackboard.TryGlobalGetVariant(resolvedId, out var variant))
            {
                LastQueryKind = variant.Kind.ToString();
                LastQueryValue = variant.ToString();
                LastQueryStatus = "Found via TryGlobalGetVariant";
                return;
            }

            LastQueryKind = "(none)";
            LastQueryValue = "(missing)";
            LastQueryStatus = "Variable not set in this scope hierarchy.";
        }

        List<BlackboardVarRow> BuildLocalRows()
        {
            if (_blackboard?.LocalVars == null)
                return new List<BlackboardVarRow>();

            var rows = new List<BlackboardVarRow>();
            foreach (var varId in _blackboard.LocalVars.EnumerateVarIds())
            {
                var kind = _blackboard.LocalVars.GetVarKind(varId);
                var version = _blackboard.LocalVars.GetVarVersion(varId);
                var keyName = VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
                string valueText;

                if (kind == ValueKind.ManagedRef)
                {
                    if (_blackboard.LocalVars.TryGetManagedRef(varId, out var managed))
                    {
                        valueText = managed?.ToString() ?? "(null)";
                    }
                    else
                    {
                        valueText = "(null)";
                    }
                }
                else if (_blackboard.LocalVars.TryGetVariant(varId, out var variant))
                {
                    valueText = variant.ToString();
                }
                else
                {
                    valueText = "(unavailable)";
                }

                rows.Add(new BlackboardVarRow(varId, keyName, kind.ToString(), version, valueText));
            }

            return rows;
        }

        List<GridBlackboardRow> BuildGridRows()
        {
            _gridRows.Clear();

            if (_gridBlackboard == null)
            {
                _gridStatus = "Grid blackboard not ready";
                return _gridRows;
            }

            if (!_gridBlackboard.TryGetRowCount(out var rowCount) || rowCount <= 0)
            {
                _gridStatus = "(empty)";
                return _gridRows;
            }

            var totalColumns = 0;
            var totalVars = 0;
            var cellBuffer = new List<GridBlackboardCellSnapshot>(16);

            for (int row = 0; row < rowCount; row++)
            {
                var rowRow = new GridBlackboardRow
                {
                    RowIndex = row,
                    ColumnCount = 0,
                };

                if (_gridBlackboard.TryGetColumnCount(row, out var columnCount) && columnCount > 0)
                {
                    rowRow.ColumnCount = columnCount;

                    for (int column = 0; column < columnCount; column++)
                    {
                        var columnRow = new GridBlackboardColumn
                        {
                            ColumnIndex = column,
                            VarCount = 0,
                        };

                        cellBuffer.Clear();
                        if (_gridBlackboard.TryCollectCell(row, column, cellBuffer))
                        {
                            columnRow.VarCount = cellBuffer.Count;
                            totalVars += cellBuffer.Count;

                            for (int i = 0; i < cellBuffer.Count; i++)
                            {
                                var cell = cellBuffer[i];
                                var keyName = VarIdResolver.TryGetIdToStable(cell.VarId) ?? $"varId={cell.VarId}";
                                columnRow.Vars.Add(new GridBlackboardVarRow(cell.VarId, keyName, cell.Value.Kind.ToString(), cell.Value.ToString()));
                            }
                        }

                        rowRow.Columns.Add(columnRow);
                        totalColumns++;
                    }
                }

                _gridRows.Add(rowRow);
            }

            _gridStatus = $"Rows={rowCount}, Columns={totalColumns}, Vars={totalVars}";
            return _gridRows;
        }

        // ------------------------------------------------------------
        // Write (test) inputs - allow inspector-driven test writes to this blackboard
        // ------------------------------------------------------------

        [FoldoutGroup("Query Inputs")]
        [LabelText("Write Entries")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = false)]
        [SerializeField]
        List<BlackboardWriteEntry> writeEntries = new();

        [FoldoutGroup("Query Inputs")]
        [Button(ButtonSizes.Medium)]
        public void ApplyAllWrites()
        {
            if (_blackboard == null)
            {
                LastQueryStatus = "Blackboard not ready";
                return;
            }

            for (int i = 0; i < writeEntries.Count; i++)
            {
                ApplyWrite(i);
            }
        }

        void ApplyWrite(int index)
        {
            if (index < 0 || index >= writeEntries.Count)
                return;

            var blackboard = _blackboard;
            if (blackboard == null)
            {
                writeEntries[index].LastStatus = "Blackboard not ready";
                return;
            }

            var entry = writeEntries[index];
            var trimmedKey = string.IsNullOrWhiteSpace(entry.queryKey) ? string.Empty : entry.queryKey.Trim();
            var resolvedId = ResolveVarId(trimmedKey, entry.queryVarId);
            entry.LastStatus = "(idle)"; // reset

            if (resolvedId == 0)
            {
                entry.LastStatus = "Invalid varId / key";
                return;
            }

            var value = entry.value.Evaluate(EmptyDynamicContext.Instance);
            var fallback = entry.fallback;

            try
            {
                var ok = blackboard.TryGlobalSetVariant(resolvedId, in value, fallback);
                entry.LastStatus = ok ? "Written" : "Not written (fallback/exists?)";
            }
            catch (System.Exception ex)
            {
                entry.LastStatus = $"Error: {ex.Message}";
            }
        }

        int ResolveVarId(string trimmedKey, int overrideVarId = 0)
        {
            if (overrideVarId != 0)
                return overrideVarId;

            if (string.IsNullOrEmpty(trimmedKey))
                return 0;

            if (int.TryParse(trimmedKey, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed != 0)
                return parsed;

            if (VarIdResolver.TryResolve(trimmedKey, out var resolved) && resolved != 0)
                return resolved;

            return 0;
        }

        [Serializable]
        public sealed class BlackboardWriteEntry
        {
            [LabelText("Stable Key / Alias")]
            [Tooltip("Stable key, alias, or numeric ID to write to. VarId override takes precedence when non-zero.")]
            public string queryKey = string.Empty;

            [LabelText("Var Id Override")]
            [VarIdDropdown]
            public int queryVarId;

            [LabelText("Value")]
            [SerializeField]
            public LiteralSource value = new();

            [LabelText("Fallback")]
            public GlobalBlackboardSetFallback fallback = GlobalBlackboardSetFallback.CreateGameLogicRoot;

            [ShowInInspector, ReadOnly, LabelText("Status")]
            public string LastStatus { get; internal set; } = "(idle)";
        }

        int ResolveVarId(string trimmedKey)
        {
            if (queryVarId != 0)
                return queryVarId;

            if (string.IsNullOrEmpty(trimmedKey))
                return 0;

            if (int.TryParse(trimmedKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed != 0)
                return parsed;

            if (VarIdResolver.TryResolve(trimmedKey, out var resolved) && resolved != 0)
                return resolved;

            return 0;
        }

        [Serializable]
        public readonly struct BlackboardVarRow
        {
            [ShowInInspector, ReadOnly, LabelText("Id")]
            public readonly int VarId;

            [ShowInInspector, ReadOnly, LabelText("Key")]
            public readonly string KeyName;

            [ShowInInspector, ReadOnly, LabelText("Kind")]
            public readonly string KindName;

            [ShowInInspector, ReadOnly, LabelText("Write Version")]
            public readonly int Version;

            [ShowInInspector, ReadOnly, LabelText("Value")]
            public readonly string Value;

            public BlackboardVarRow(int varId, string keyName, string kindName, int version, string value)
            {
                VarId = varId;
                KeyName = keyName;
                KindName = kindName;
                Version = version;
                Value = value;
            }
        }

        [Serializable]
        public sealed class GridBlackboardRow
        {
            [TableColumnWidth(60)] public int RowIndex;
            [TableColumnWidth(80)] public int ColumnCount;

            [TableList(IsReadOnly = true, AlwaysExpanded = true)]
            public List<GridBlackboardColumn> Columns = new();
        }

        [Serializable]
        public sealed class GridBlackboardColumn
        {
            [TableColumnWidth(60)] public int ColumnIndex;
            [TableColumnWidth(80)] public int VarCount;

            [TableList(IsReadOnly = true, AlwaysExpanded = true)]
            public List<GridBlackboardVarRow> Vars = new();
        }

        [Serializable]
        public sealed class GridBlackboardVarRow
        {
            [TableColumnWidth(80)] public int VarId;
            [TableColumnWidth(160)] public string KeyName = string.Empty;
            [TableColumnWidth(120)] public string KindName = string.Empty;
            [TableColumnWidth(240)] public string Value = string.Empty;

            public GridBlackboardVarRow(int varId, string keyName, string kindName, string value)
            {
                VarId = varId;
                KeyName = keyName;
                KindName = kindName;
                Value = value;
            }
        }
    }
}
