#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Common
{
    public readonly struct GridBlackboardCellSnapshot
    {
        public readonly int Row;
        public readonly int Column;
        public readonly int VarId;
        public readonly DynamicVariant Value;

        public GridBlackboardCellSnapshot(int row, int column, int varId, in DynamicVariant value)
        {
            Row = row;
            Column = column;
            VarId = varId;
            Value = value;
        }

        public GridBlackboardCellSnapshot(int row, int column, in DynamicVariant value)
        {
            Row = row;
            Column = column;
            VarId = 0;
            Value = value;
        }
    }

    public interface IGridBlackboardService
    {
        bool TryGetVariant(int varId, int row, int column, out DynamicVariant value);
        bool TrySetVariant(int varId, int row, int column, in DynamicVariant value);
        bool SetOrExpandVariant(int varId, int row, int column, in DynamicVariant value);

        bool TryGetCellVariant(int row, int column, bool useVarFilter, int varIdFilter, out DynamicVariant value, out int resolvedVarId);
        bool TryGetFirstVariant(int row, int column, out DynamicVariant value, out int resolvedVarId);

        bool TryGetRowCount(out int rowCount);
        bool TryGetColumnCount(int row, out int columnCount);
        bool TryGetRowCount(int varId, out int rowCount);
        bool TryGetColumnCount(int varId, int row, out int columnCount);

        bool EnsureRow(int row);
        bool InsertRow(int row);
        bool AppendRow(out int appendedRow);
        bool RemoveRow(int row);
        bool ClearRow(int row);

        bool EnsureColumn(int row, int column);
        bool InsertColumn(int row, int column);
        bool AppendColumn(int row, out int appendedColumn);
        bool RemoveColumn(int row, int column);
        bool ClearColumn(int row, int column);

        bool TryUnsetVariant(int varId, int row, int column);

        bool TryCollectRow(int row, List<GridBlackboardCellSnapshot> destination);
        bool TryCollectCell(int row, int column, List<GridBlackboardCellSnapshot> destination);

        bool TryCollectAllCells(List<GridBlackboardCellSnapshot> destination);
        bool TryCollectCells(int varId, List<GridBlackboardCellSnapshot> destination);
        void ClearVar(int varId);
        void ClearAll();
    }

    public sealed class GridBlackboardService : IGridBlackboardService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly List<List<List<CellVarEntry>>> _rows = new();

        struct CellVarEntry
        {
            public int VarId;
            public DynamicVariant Value;
        }

        public bool TryGetVariant(int varId, int row, int column, out DynamicVariant value)
        {
            if (varId == 0 || !TryGetCell(row, column, out var vars) || vars == null)
            {
                value = default;
                return false;
            }

            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].VarId != varId)
                    continue;

                value = vars[i].Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool TrySetVariant(int varId, int row, int column, in DynamicVariant value)
            => TrySetVariantInternal(varId, row, column, value, allowExpandCell: false, allowInsertVar: false);

        public bool SetOrExpandVariant(int varId, int row, int column, in DynamicVariant value)
            => TrySetVariantInternal(varId, row, column, value, allowExpandCell: true, allowInsertVar: true);

        public bool TryGetCellVariant(int row, int column, bool useVarFilter, int varIdFilter, out DynamicVariant value, out int resolvedVarId)
        {
            value = default;
            resolvedVarId = 0;

            if (!TryGetCell(row, column, out var vars) || vars == null || vars.Count == 0)
                return false;

            if (useVarFilter)
            {
                if (varIdFilter == 0)
                    return false;

                for (int i = 0; i < vars.Count; i++)
                {
                    if (vars[i].VarId != varIdFilter)
                        continue;

                    value = vars[i].Value;
                    resolvedVarId = vars[i].VarId;
                    return true;
                }

                return false;
            }

            var first = vars[0];
            value = first.Value;
            resolvedVarId = first.VarId;
            return true;
        }

        public bool TryGetFirstVariant(int row, int column, out DynamicVariant value, out int resolvedVarId)
            => TryGetCellVariant(row, column, useVarFilter: false, varIdFilter: 0, out value, out resolvedVarId);

        public bool TryGetRowCount(out int rowCount)
        {
            rowCount = _rows.Count;
            return rowCount > 0;
        }

        public bool TryGetColumnCount(int row, out int columnCount)
        {
            if (row < 0 || row >= _rows.Count)
            {
                columnCount = 0;
                return false;
            }

            var columns = _rows[row];
            columnCount = columns?.Count ?? 0;
            return columnCount > 0;
        }

        public bool TryGetRowCount(int varId, out int rowCount)
        {
            if (varId == 0)
            {
                return TryGetRowCount(out rowCount);
            }

            var maxRow = -1;
            for (int row = 0; row < _rows.Count; row++)
            {
                var columns = _rows[row];
                if (columns == null)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var vars = columns[column];
                    if (vars == null)
                        continue;

                    for (int i = 0; i < vars.Count; i++)
                    {
                        if (vars[i].VarId == varId)
                        {
                            maxRow = row;
                            break;
                        }
                    }

                    if (maxRow == row)
                        break;
                }
            }

            rowCount = maxRow + 1;
            return rowCount > 0;
        }

        public bool TryGetColumnCount(int varId, int row, out int columnCount)
        {
            if (varId == 0)
                return TryGetColumnCount(row, out columnCount);

            if (row < 0 || row >= _rows.Count)
            {
                columnCount = 0;
                return false;
            }

            var columns = _rows[row];
            if (columns == null)
            {
                columnCount = 0;
                return false;
            }

            var maxColumn = -1;
            for (int column = 0; column < columns.Count; column++)
            {
                var vars = columns[column];
                if (vars == null)
                    continue;

                for (int i = 0; i < vars.Count; i++)
                {
                    if (vars[i].VarId == varId)
                    {
                        maxColumn = column;
                        break;
                    }
                }
            }

            columnCount = maxColumn + 1;
            return columnCount > 0;
        }

        public bool EnsureRow(int row)
        {
            if (row < 0)
                return false;

            while (row >= _rows.Count)
                _rows.Add(new List<List<CellVarEntry>>());

            if (_rows[row] == null)
                _rows[row] = new List<List<CellVarEntry>>();

            return true;
        }

        public bool InsertRow(int row)
        {
            if (row < 0 || row > _rows.Count)
                return false;

            _rows.Insert(row, new List<List<CellVarEntry>>());
            return true;
        }

        public bool AppendRow(out int appendedRow)
        {
            appendedRow = _rows.Count;
            _rows.Add(new List<List<CellVarEntry>>());
            return true;
        }

        public bool RemoveRow(int row)
        {
            if (row < 0 || row >= _rows.Count)
                return false;

            _rows.RemoveAt(row);
            return true;
        }

        public bool ClearRow(int row)
        {
            if (row < 0 || row >= _rows.Count)
                return false;

            var columns = _rows[row];
            if (columns == null)
                return false;

            for (int column = 0; column < columns.Count; column++)
            {
                var vars = columns[column];
                vars?.Clear();
            }

            return true;
        }

        public bool EnsureColumn(int row, int column)
        {
            if (row < 0 || column < 0)
                return false;

            if (!EnsureRow(row))
                return false;

            var columns = _rows[row];
            if (columns == null)
            {
                columns = new List<List<CellVarEntry>>();
                _rows[row] = columns;
            }

            while (column >= columns.Count)
                columns.Add(new List<CellVarEntry>());

            if (columns[column] == null)
                columns[column] = new List<CellVarEntry>();

            return true;
        }

        public bool InsertColumn(int row, int column)
        {
            if (row < 0 || column < 0)
                return false;

            if (!EnsureRow(row))
                return false;

            var columns = _rows[row];
            if (columns == null)
            {
                columns = new List<List<CellVarEntry>>();
                _rows[row] = columns;
            }

            if (column > columns.Count)
                return false;

            columns.Insert(column, new List<CellVarEntry>());
            return true;
        }

        public bool AppendColumn(int row, out int appendedColumn)
        {
            appendedColumn = -1;
            if (row < 0)
                return false;

            if (!EnsureRow(row))
                return false;

            var columns = _rows[row];
            if (columns == null)
            {
                columns = new List<List<CellVarEntry>>();
                _rows[row] = columns;
            }

            columns.Add(new List<CellVarEntry>());
            appendedColumn = columns.Count - 1;
            return true;
        }

        public bool RemoveColumn(int row, int column)
        {
            if (row < 0 || column < 0 || row >= _rows.Count)
                return false;

            var columns = _rows[row];
            if (columns == null || column >= columns.Count)
                return false;

            columns.RemoveAt(column);
            return true;
        }

        public bool ClearColumn(int row, int column)
        {
            if (!TryGetCell(row, column, out var vars) || vars == null)
                return false;

            vars.Clear();
            return true;
        }

        public bool TryUnsetVariant(int varId, int row, int column)
        {
            if (varId == 0 || !TryGetCell(row, column, out var vars) || vars == null)
                return false;

            var removed = false;
            for (int i = vars.Count - 1; i >= 0; i--)
            {
                if (vars[i].VarId != varId)
                    continue;

                vars.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        public bool TryCollectRow(int row, List<GridBlackboardCellSnapshot> destination)
        {
            if (destination == null || row < 0 || row >= _rows.Count)
                return false;

            var columns = _rows[row];
            if (columns == null)
                return false;

            var originalCount = destination.Count;
            for (int column = 0; column < columns.Count; column++)
            {
                var vars = columns[column];
                if (vars == null)
                    continue;

                for (int i = 0; i < vars.Count; i++)
                    destination.Add(new GridBlackboardCellSnapshot(row, column, vars[i].VarId, vars[i].Value));
            }

            return destination.Count > originalCount;
        }

        public bool TryCollectCell(int row, int column, List<GridBlackboardCellSnapshot> destination)
        {
            if (destination == null || !TryGetCell(row, column, out var vars) || vars == null)
                return false;

            var originalCount = destination.Count;
            for (int i = 0; i < vars.Count; i++)
                destination.Add(new GridBlackboardCellSnapshot(row, column, vars[i].VarId, vars[i].Value));

            return destination.Count > originalCount;
        }

        public bool TryCollectAllCells(List<GridBlackboardCellSnapshot> destination)
        {
            if (destination == null)
                return false;

            var originalCount = destination.Count;

            for (int row = 0; row < _rows.Count; row++)
            {
                var columns = _rows[row];
                if (columns == null)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var vars = columns[column];
                    if (vars == null)
                        continue;

                    for (int i = 0; i < vars.Count; i++)
                    {
                        destination.Add(new GridBlackboardCellSnapshot(row, column, vars[i].VarId, vars[i].Value));
                    }
                }
            }

            return destination.Count > originalCount;
        }

        public bool TryCollectCells(int varId, List<GridBlackboardCellSnapshot> destination)
        {
            if (destination == null || varId == 0)
                return false;

            var originalCount = destination.Count;

            for (int row = 0; row < _rows.Count; row++)
            {
                var columns = _rows[row];
                if (columns == null)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var vars = columns[column];
                    if (vars == null)
                        continue;

                    for (int i = 0; i < vars.Count; i++)
                    {
                        if (vars[i].VarId != varId)
                            continue;

                        destination.Add(new GridBlackboardCellSnapshot(row, column, varId, vars[i].Value));
                    }
                }
            }

            return destination.Count > originalCount;
        }

        public void ClearVar(int varId)
        {
            if (varId == 0)
                return;

            for (int row = 0; row < _rows.Count; row++)
            {
                var columns = _rows[row];
                if (columns == null)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var vars = columns[column];
                    if (vars == null)
                        continue;

                    for (int i = vars.Count - 1; i >= 0; i--)
                    {
                        if (vars[i].VarId == varId)
                            vars.RemoveAt(i);
                    }
                }
            }
        }

        public void ClearAll()
        {
            _rows.Clear();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            ClearAll();
        }

        bool TryGetCell(int row, int column, out List<CellVarEntry>? vars)
        {
            if (row < 0 || column < 0 || row >= _rows.Count)
            {
                vars = null;
                return false;
            }

            var columns = _rows[row];
            if (columns == null || column >= columns.Count)
            {
                vars = null;
                return false;
            }

            vars = columns[column];
            return true;
        }

        bool TrySetVariantInternal(int varId, int row, int column, in DynamicVariant value, bool allowExpandCell, bool allowInsertVar)
        {
            if (varId == 0 || row < 0 || column < 0)
                return false;

            if (!allowExpandCell && row >= _rows.Count)
                return false;

            while (allowExpandCell && row >= _rows.Count)
                _rows.Add(new List<List<CellVarEntry>>());

            var columns = _rows[row];
            if (columns == null)
            {
                if (!allowExpandCell)
                    return false;

                columns = new List<List<CellVarEntry>>();
                _rows[row] = columns;
            }

            if (!allowExpandCell && column >= columns.Count)
                return false;

            while (allowExpandCell && column >= columns.Count)
                columns.Add(new List<CellVarEntry>());

            var vars = columns[column];
            if (vars == null)
            {
                if (!allowExpandCell)
                    return false;

                vars = new List<CellVarEntry>();
                columns[column] = vars;
            }

            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].VarId != varId)
                    continue;

                vars[i] = new CellVarEntry { VarId = varId, Value = value };
                return true;
            }

            if (!allowInsertVar)
                return false;

            vars.Add(new CellVarEntry { VarId = varId, Value = value });
            return true;
        }
    }
}