#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WriteGridDataExecutor : ICommandExecutor
    {
        readonly List<GridBlackboardCellSnapshot> _snapshotBuffer = new(64);

        public int CommandId => CommandIds.WriteGridData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not WriteGridDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteGridDataCommandData is required.");

            var origin = ctx.Actor ?? ctx.Scope;
            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.GridActorSource, origin) ?? origin;
            if (!TryResolveGridBlackboard(targetScope, out var targetGrid, out _) || targetGrid == null)
                return UniTask.CompletedTask;

            // Keep CommandContext so ContextSlot/CommandRoot actor sources can resolve during payload evaluation.
            IDynamicContext dynCtx = ctx;

            switch (typed.TargetMode)
            {
                case WriteGridDataTargetMode.Grid:
                    ExecuteGridOperation(typed, ctx, targetScope, targetGrid);
                    break;

                case WriteGridDataTargetMode.Row:
                    ExecuteRowOperation(typed, ctx, dynCtx, targetScope, targetGrid);
                    break;

                case WriteGridDataTargetMode.Column:
                    ExecuteColumnOperation(typed, ctx, dynCtx, targetScope, targetGrid);
                    break;
            }

            return UniTask.CompletedTask;
        }

        void ExecuteGridOperation(
            WriteGridDataCommandData typed,
            CommandContext ctx,
            IScopeNode? targetScope,
            IGridBlackboardService targetGrid)
        {
            switch (typed.GridOperation)
            {
                case WriteGridDataGridOperation.ClearAll:
                    targetGrid.ClearAll();
                    break;

                case WriteGridDataGridOperation.CopyAllToGrid:
                    if (!TryResolveGridFromActorSource(ctx, typed.DestinationGridActorSource, targetScope, out var destinationGrid) ||
                        destinationGrid == null)
                    {
                        return;
                    }

                    CopyAllCells(targetGrid, destinationGrid, typed.ClearDestinationBeforeCopy, typed.OverwriteOnCopy, typed.GridId);
                    break;
            }

        }

        void ExecuteRowOperation(
            WriteGridDataCommandData typed,
            CommandContext ctx,
            IDynamicContext dynCtx,
            IScopeNode? targetScope,
            IGridBlackboardService targetGrid)
        {
            switch (typed.RowOperation)
            {
                case WriteGridDataRowOperation.Append:
                    if (targetGrid.AppendRow(out var appendedRow))
                        ApplyGridIdToRow(targetGrid, appendedRow, typed.GridId);
                    return;
            }

            if (!TryResolveNonNegativeIndex(typed.RowIndex, dynCtx, out var rowIndex))
                return;

            switch (typed.RowOperation)
            {
                case WriteGridDataRowOperation.Ensure:
                    targetGrid.EnsureRow(rowIndex);
                    ApplyGridIdToRow(targetGrid, rowIndex, typed.GridId);
                    break;

                case WriteGridDataRowOperation.Insert:
                    targetGrid.InsertRow(rowIndex);
                    ApplyGridIdToRow(targetGrid, rowIndex, typed.GridId);
                    break;

                case WriteGridDataRowOperation.Delete:
                    targetGrid.RemoveRow(rowIndex);
                    break;

                case WriteGridDataRowOperation.Clear:
                    switch (typed.RowClearMode)
                    {
                        case WriteGridDataRowClearMode.ClearCellVars:
                            targetGrid.ClearRow(rowIndex);
                            break;

                        default:
                            ClearRowColumns(targetGrid, rowIndex);
                            break;
                    }
                    break;

                case WriteGridDataRowOperation.CopyToRow:
                    if (!TryResolveNonNegativeIndex(typed.DestinationRowIndex, dynCtx, out var destinationRowIndex))
                        return;

                    if (!TryResolveGridFromActorSource(ctx, typed.DestinationRowGridActorSource, targetScope, out var destinationGrid) ||
                        destinationGrid == null)
                    {
                        return;
                    }

                    CopyRow(
                        targetGrid,
                        rowIndex,
                        destinationGrid,
                        destinationRowIndex,
                        typed.ClearDestinationRowBeforeCopy,
                        typed.OverwriteRowCopy,
                        typed.GridId);
                    break;
            }
        }

        void ExecuteColumnOperation(
            WriteGridDataCommandData typed,
            CommandContext ctx,
            IDynamicContext dynCtx,
            IScopeNode? targetScope,
            IGridBlackboardService targetGrid)
        {
            if (!TryResolveNonNegativeIndex(typed.ColumnRowIndex, dynCtx, out var rowIndex))
                return;

            switch (typed.ColumnOperation)
            {
                case WriteGridDataColumnOperation.Append:
                    if (!targetGrid.AppendColumn(rowIndex, out var appendedColumn))
                        return;

                    GridBlackboardWriteUtility.ApplyCellValues(
                        targetGrid,
                        rowIndex,
                        appendedColumn,
                        typed.SetElementAfterAppend ? typed.ElementValues : null,
                        dynCtx,
                        typed.OverwriteElement,
                        typed.UpsertElement,
                        typed.GridId);

                    return;
            }

            if (!TryResolveNonNegativeIndex(typed.ColumnIndex, dynCtx, out var columnIndex))
                return;

            switch (typed.ColumnOperation)
            {
                case WriteGridDataColumnOperation.Ensure:
                    targetGrid.EnsureColumn(rowIndex, columnIndex);
                    ApplyGridIdToCell(targetGrid, rowIndex, columnIndex, typed.GridId);
                    break;

                case WriteGridDataColumnOperation.Insert:
                    targetGrid.InsertColumn(rowIndex, columnIndex);
                    ApplyGridIdToCell(targetGrid, rowIndex, columnIndex, typed.GridId);
                    break;

                case WriteGridDataColumnOperation.Delete:
                    targetGrid.RemoveColumn(rowIndex, columnIndex);
                    break;

                case WriteGridDataColumnOperation.Clear:
                    targetGrid.ClearColumn(rowIndex, columnIndex);
                    break;

                case WriteGridDataColumnOperation.CopyToColumn:
                    if (!TryResolveNonNegativeIndex(typed.DestinationColumnRowIndex, dynCtx, out var destinationRowIndex) ||
                        !TryResolveNonNegativeIndex(typed.DestinationColumnIndex, dynCtx, out var destinationColumnIndex))
                    {
                        return;
                    }

                    if (!TryResolveGridFromActorSource(ctx, typed.DestinationColumnGridActorSource, targetScope, out var destinationGrid) ||
                        destinationGrid == null)
                    {
                        return;
                    }

                    CopyCell(
                        targetGrid,
                        rowIndex,
                        columnIndex,
                        destinationGrid,
                        destinationRowIndex,
                        destinationColumnIndex,
                        typed.ClearDestinationColumnBeforeCopy,
                        typed.OverwriteColumnCopy,
                        typed.GridId);
                    break;

                case WriteGridDataColumnOperation.SetElement:
                    GridBlackboardWriteUtility.ApplyCellValues(
                        targetGrid,
                        rowIndex,
                        columnIndex,
                        typed.ElementValues,
                        dynCtx,
                        typed.OverwriteElement,
                        typed.UpsertElement,
                        typed.GridId);
                    break;

                case WriteGridDataColumnOperation.RemoveElement:
                    {
                        var varId = ResolveVarId(typed.ElementKey);
                        if (varId == 0)
                            return;

                        targetGrid.TryUnsetVariant(varId, rowIndex, columnIndex);
                        ApplyGridIdToCell(targetGrid, rowIndex, columnIndex, typed.GridId);
                        break;
                    }

                case WriteGridDataColumnOperation.AddNumeric:
                    {
                        var varId = ResolveVarId(typed.ElementKey);
                        if (varId == 0)
                            return;

                        var operand = typed.NumericValue.GetOrDefault(dynCtx, 0f);
                        ApplyNumericElement(
                            targetGrid,
                            rowIndex,
                            columnIndex,
                            varId,
                            operand,
                            isMultiply: false,
                            typed.CreateElementIfMissing,
                            typed.GridId);
                        break;
                    }

                case WriteGridDataColumnOperation.MultiplyNumeric:
                    {
                        var varId = ResolveVarId(typed.ElementKey);
                        if (varId == 0)
                            return;

                        var operand = typed.NumericValue.GetOrDefault(dynCtx, 1f);
                        ApplyNumericElement(
                            targetGrid,
                            rowIndex,
                            columnIndex,
                            varId,
                            operand,
                            isMultiply: true,
                            typed.CreateElementIfMissing,
                            typed.GridId);
                        break;
                    }
            }
        }

        void CopyAllCells(
            IGridBlackboardService source,
            IGridBlackboardService destination,
            bool clearDestinationBeforeCopy,
            bool overwrite,
            int gridIdVarId)
        {
            _snapshotBuffer.Clear();
            source.TryCollectAllCells(_snapshotBuffer);

            if (clearDestinationBeforeCopy)
                destination.ClearAll();

            for (int i = 0; i < _snapshotBuffer.Count; i++)
            {
                var cell = _snapshotBuffer[i];
                if (gridIdVarId != 0)
                {
                    var gridIdValue = DynamicVariant.FromBool(true);
                    GridBlackboardWriteUtility.TryWriteGridId(destination, cell.Row, cell.Column, gridIdVarId, in gridIdValue);
                }

                if (!overwrite && destination.TryGetVariant(cell.VarId, cell.Row, cell.Column, out _))
                    continue;

                destination.SetOrExpandVariant(cell.VarId, cell.Row, cell.Column, in cell.Value);
            }
        }

        void CopyRow(
            IGridBlackboardService source,
            int sourceRow,
            IGridBlackboardService destination,
            int destinationRow,
            bool clearDestinationBeforeCopy,
            bool overwrite,
            int gridIdVarId)
        {
            _snapshotBuffer.Clear();
            source.TryCollectRow(sourceRow, _snapshotBuffer);

            destination.EnsureRow(destinationRow);
            if (clearDestinationBeforeCopy)
                destination.ClearRow(destinationRow);

            for (int i = 0; i < _snapshotBuffer.Count; i++)
            {
                var cell = _snapshotBuffer[i];
                if (gridIdVarId != 0)
                {
                    var gridIdValue = DynamicVariant.FromBool(true);
                    GridBlackboardWriteUtility.TryWriteGridId(destination, destinationRow, cell.Column, gridIdVarId, in gridIdValue);
                }

                if (!overwrite && destination.TryGetVariant(cell.VarId, destinationRow, cell.Column, out _))
                    continue;

                destination.SetOrExpandVariant(cell.VarId, destinationRow, cell.Column, in cell.Value);
            }
        }

        void CopyCell(
            IGridBlackboardService source,
            int sourceRow,
            int sourceColumn,
            IGridBlackboardService destination,
            int destinationRow,
            int destinationColumn,
            bool clearDestinationBeforeCopy,
            bool overwrite,
            int gridIdVarId)
        {
            _snapshotBuffer.Clear();
            source.TryCollectCell(sourceRow, sourceColumn, _snapshotBuffer);

            destination.EnsureColumn(destinationRow, destinationColumn);
            if (clearDestinationBeforeCopy)
                destination.ClearColumn(destinationRow, destinationColumn);

            for (int i = 0; i < _snapshotBuffer.Count; i++)
            {
                var cell = _snapshotBuffer[i];
                if (gridIdVarId != 0)
                {
                    var gridIdValue = DynamicVariant.FromBool(true);
                    GridBlackboardWriteUtility.TryWriteGridId(destination, destinationRow, destinationColumn, gridIdVarId, in gridIdValue);
                }

                if (!overwrite && destination.TryGetVariant(cell.VarId, destinationRow, destinationColumn, out _))
                    continue;

                destination.SetOrExpandVariant(cell.VarId, destinationRow, destinationColumn, in cell.Value);
            }
        }

        static void ApplyNumericElement(
            IGridBlackboardService grid,
            int row,
            int column,
            int varId,
            float operand,
            bool isMultiply,
            bool createIfMissing,
            int gridIdVarId)
        {
            if (varId == 0)
                return;

            if (gridIdVarId != 0)
            {
                var gridIdValue = DynamicVariant.FromBool(true);
                GridBlackboardWriteUtility.TryWriteGridId(grid, row, column, gridIdVarId, in gridIdValue);
            }

            if (!grid.TryGetVariant(varId, row, column, out var current))
            {
                if (!createIfMissing)
                    return;

                var initial = DynamicVariant.FromFloat(operand);
                grid.SetOrExpandVariant(varId, row, column, in initial);
                return;
            }

            var currentValue = ResolveNumeric(current, isMultiply ? 1f : 0f);
            var next = isMultiply ? currentValue * operand : currentValue + operand;

            if (current.Kind == ValueKind.Int && Mathf.Abs(next - Mathf.Round(next)) <= 0.0001f)
            {
                var intValue = DynamicVariant.FromInt(Mathf.RoundToInt(next));
                grid.SetOrExpandVariant(varId, row, column, in intValue);
                return;
            }

            var floatValue = DynamicVariant.FromFloat(next);
            grid.SetOrExpandVariant(varId, row, column, in floatValue);
        }

        static float ResolveNumeric(in DynamicVariant value, float fallback)
        {
            if (value.TryGet<float>(out var floatValue))
                return floatValue;

            if (value.TryGet<int>(out var intValue))
                return intValue;

            if (value.TryGet<bool>(out var boolValue))
                return boolValue ? 1f : 0f;

            return fallback;
        }

        static bool TryResolveNonNegativeIndex(DynamicValue<int> source, IDynamicContext dynCtx, out int index)
        {
            index = source.GetOrDefault(dynCtx, 0);
            return index >= 0;
        }

        static void ClearRowColumns(IGridBlackboardService grid, int row)
        {
            if (!grid.TryGetColumnCount(row, out var columnCount) || columnCount <= 0)
                return;

            for (var column = columnCount - 1; column >= 0; column--)
                grid.RemoveColumn(row, column);
        }

        static void ApplyGridIdToRow(IGridBlackboardService grid, int row, int gridIdVarId)
        {
            if (gridIdVarId == 0 || !grid.TryGetColumnCount(row, out var columnCount))
                return;

            for (var column = 0; column < columnCount; column++)
                ApplyGridIdToCell(grid, row, column, gridIdVarId);
        }

        static void ApplyGridIdToCell(IGridBlackboardService grid, int row, int column, int gridIdVarId)
        {
            if (gridIdVarId == 0)
                return;

            var gridIdValue = DynamicVariant.FromBool(true);
            GridBlackboardWriteUtility.TryWriteGridId(grid, row, column, gridIdVarId, in gridIdValue);
        }

        static int ResolveVarId(in VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolvedVarId))
                return resolvedVarId;

            return 0;
        }

        static bool TryResolveGridFromActorSource(
            CommandContext ctx,
            ActorSource actorSource,
            IScopeNode? fallbackOrigin,
            out IGridBlackboardService? grid)
        {
            grid = null;

            var origin = fallbackOrigin ?? ctx.Actor ?? ctx.Scope;
            var scope = ActorSourceFastResolver.Resolve(ctx, actorSource, origin) ?? origin;
            return TryResolveGridBlackboard(scope, out grid, out _);
        }

        static bool TryResolveGridBlackboard(IScopeNode? scope, out IGridBlackboardService? grid, out IScopeNode? resolvedScope)
        {
            grid = null;
            resolvedScope = null;

            for (var node = scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IGridBlackboardService>(out var resolved) && resolved != null)
                {
                    grid = resolved;
                    resolvedScope = node;
                    return true;
                }
            }

            return false;
        }
    }
}
