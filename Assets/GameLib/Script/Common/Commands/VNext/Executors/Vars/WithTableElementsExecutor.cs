#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithTableElementsExecutor : ICommandExecutor
    {
        readonly struct TableElement
        {
            public readonly int Row;
            public readonly int Column;
            public readonly int ColumnCount;

            public TableElement(int row, int column, int columnCount)
            {
                Row = row;
                Column = column;
                ColumnCount = columnCount;
            }
        }

        public int CommandId => CommandIds.WithTableElements;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithTableElementsCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithTableElementsCommandData is required.");

            if (typed.Body == null || typed.Body.Count == 0)
                return;

            var table = typed.TableSource.GetOrDefault(ctx);
            if (table == null)
            {
                if (typed.MissingTablePolicy == CommandFailurePolicy.Skip)
                    return;

                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Table was not found.");
            }

            var targetScope = ResolveTargetScope(ctx, typed.TableActorSource);
            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolveRunner(targetScope, out var runner) || runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ICommandRunner is missing on target scope.");

            var rowCount = table.RowCount;
            var elements = BuildExecutionElements(typed, ctx, targetScope, runner, table, rowCount);
            if (elements.Count == 0)
                return;

            if (typed.AwaitMode == FlowRunAwaitMode.RunInBackground)
            {
                for (var i = 0; i < elements.Count; i++)
                {
                    var runCtx = BuildExecutionContext(typed, ctx, targetScope, runner, table, rowCount, elements[i]);
                    var task = runner.ExecuteListAsync(typed.Body, runCtx, ct, ctx.Options);
                    RunInBackground(task);
                }

                return;
            }

            for (var i = 0; i < elements.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var runCtx = BuildExecutionContext(typed, ctx, targetScope, runner, table, rowCount, elements[i]);
                var result = await runner.ExecuteListAsync(typed.Body, runCtx, ct, ctx.Options);
                if (result.Status == CommandRunStatus.Break)
                    break;
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                    throw new CommandExecutionException(result.FailureKind, result.Message ?? "WithTableElements body failed.");
            }
        }

        static List<TableElement> BuildExecutionElements(
            WithTableElementsCommandData typed,
            CommandContext rootCtx,
            IScopeNode targetScope,
            ICommandRunner runner,
            Table table,
            int rowCount)
        {
            var rows = SelectRows(typed, rootCtx, targetScope, runner, table, rowCount);
            var elements = new List<TableElement>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!table.TryGetColumnCount(row, out var columnCount))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Row was not found while selecting columns. row={row}");

                var columns = SelectColumns(typed, rootCtx, targetScope, runner, table, rowCount, row, columnCount);
                for (var c = 0; c < columns.Count; c++)
                    elements.Add(new TableElement(row, columns[c], columnCount));
            }

            if (typed.TraversalOrder == TableTraversalOrder.ColumnMajor)
            {
                elements.Sort((a, b) =>
                {
                    var columnCompare = a.Column.CompareTo(b.Column);
                    if (columnCompare != 0)
                        return columnCompare;

                    return a.Row.CompareTo(b.Row);
                });
            }

            return elements;
        }

        static List<int> SelectRows(
            WithTableElementsCommandData typed,
            CommandContext rootCtx,
            IScopeNode targetScope,
            ICommandRunner runner,
            Table table,
            int rowCount)
        {
            var rows = new List<int>();
            switch (typed.RowMode)
            {
                case TableSelectorMode.Custom:
                    {
                        var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, rootCtx, "row index");
                        if (rowIndex >= rowCount)
                            throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Custom row index is out of range. row={rowIndex} rowCount={rowCount}");

                        rows.Add(rowIndex);
                        break;
                    }

                case TableSelectorMode.All:
                    for (var row = 0; row < rowCount; row++)
                        rows.Add(row);
                    break;

                case TableSelectorMode.Condition:
                    for (var row = 0; row < rowCount; row++)
                    {
                        if (!table.TryGetColumnCount(row, out var columnCount))
                            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Row was not found while evaluating row condition. row={row}");

                        var rowVars = BuildContextVars(typed, rootCtx, targetScope, runner, rootCtx.Vars, row, -1, rowCount, columnCount, null);
                        var rowCtx = CreateDerivedContext(rootCtx, targetScope, runner, rowVars);
                        if (typed.RowCondition.EvaluateBool(rowCtx))
                            rows.Add(row);
                    }
                    break;
            }

            return rows;
        }

        static List<int> SelectColumns(
            WithTableElementsCommandData typed,
            CommandContext rootCtx,
            IScopeNode targetScope,
            ICommandRunner runner,
            Table table,
            int rowCount,
            int row,
            int columnCount)
        {
            var columns = new List<int>();
            switch (typed.ColumnMode)
            {
                case TableSelectorMode.Custom:
                    {
                        var rowVars = BuildContextVars(typed, rootCtx, targetScope, runner, rootCtx.Vars, row, -1, rowCount, columnCount, null);
                        var rowCtx = CreateDerivedContext(rootCtx, targetScope, runner, rowVars);
                        var columnIndex = ResolveNonNegativeIndex(typed.ColumnIndex, rowCtx, "column index");
                        if (columnIndex >= columnCount)
                            throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Custom column index is out of range. row={row} column={columnIndex} columnCount={columnCount}");

                        columns.Add(columnIndex);
                        break;
                    }

                case TableSelectorMode.All:
                    for (var column = 0; column < columnCount; column++)
                        columns.Add(column);
                    break;

                case TableSelectorMode.Condition:
                    for (var column = 0; column < columnCount; column++)
                    {
                        if (!table.TryGetCellVars(row, column, out var cellVars))
                            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Cell was not found while evaluating column condition. row={row} column={column}");

                        var columnVars = BuildContextVars(typed, rootCtx, targetScope, runner, rootCtx.Vars, row, column, rowCount, columnCount, cellVars);
                        var columnCtx = CreateDerivedContext(rootCtx, targetScope, runner, columnVars);
                        if (typed.ColumnCondition.EvaluateBool(columnCtx))
                            columns.Add(column);
                    }
                    break;
            }

            return columns;
        }

        static CommandContext BuildExecutionContext(
            WithTableElementsCommandData typed,
            CommandContext rootCtx,
            IScopeNode targetScope,
            ICommandRunner runner,
            Table table,
            int rowCount,
            TableElement element)
        {
            if (!table.TryGetCellVars(element.Row, element.Column, out var cellVars))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Cell was not found. row={element.Row} column={element.Column}");

            var merged = BuildContextVars(
                typed,
                rootCtx,
                targetScope,
                runner,
                rootCtx.Vars,
                element.Row,
                element.Column,
                rowCount,
                element.ColumnCount,
                cellVars);

            return CreateDerivedContext(rootCtx, targetScope, runner, merged);
        }

        static VarStore BuildContextVars(
            WithTableElementsCommandData typed,
            CommandContext rootCtx,
            IScopeNode targetScope,
            ICommandRunner runner,
            IVarStore commandVars,
            int rowIndex,
            int columnIndex,
            int rowCount,
            int columnCount,
            VarStoreCellPayload? cellVars)
        {
            var merged = new VarStore(initialCapacity: 16);
            (commandVars ?? NullVarStore.Instance).MergeInto(merged, overwrite: true);

            WriteInt(merged, typed.RowIndexVarId, rowIndex);
            WriteInt(merged, typed.ColumnIndexVarId, columnIndex);
            WriteInt(merged, typed.RowCountVarId, rowCount);
            WriteInt(merged, typed.ColumnCountVarId, columnCount);

            if (cellVars != null)
            {
                // Cell vars must win over the outer command vars for row-specific data.
                var tempContext = CreateDerivedContext(rootCtx, targetScope, runner, merged);
                cellVars.ApplyTo(merged, tempContext, overwrite: true);

                // Keep the table indices authoritative even if a cell reuses the same var ids.
                WriteInt(merged, typed.RowIndexVarId, rowIndex);
                WriteInt(merged, typed.ColumnIndexVarId, columnIndex);
                WriteInt(merged, typed.RowCountVarId, rowCount);
                WriteInt(merged, typed.ColumnCountVarId, columnCount);
            }

            return merged;
        }

        static void WriteInt(IVarStore vars, int varId, int value)
        {
            if (varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromInt(value));
        }

        static CommandContext CreateDerivedContext(CommandContext source, IScopeNode scope, ICommandRunner runner, IVarStore vars)
        {
            return new CommandContext(
                scope,
                vars,
                runner,
                actor: scope,
                options: source.Options,
                commandRootScope: source.CommandRootScope,
                rootActor: source.RootActor,
                callerActor: source.Actor,
                sourceContext: source);
        }

        static IScopeNode ResolveTargetScope(CommandContext ctx, ActorSource actorSource)
        {
            var origin = ctx.Actor ?? ctx.Scope;
            var resolved = ActorSourceFastResolver.Resolve(ctx, actorSource, origin);
            return resolved ?? origin;
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static int ResolveNonNegativeIndex(DynamicValue<int> source, IDynamicContext context, string label)
        {
            var index = source.GetOrDefault(context, 0);
            if (index < 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"{label} must be >= 0, but was {index}.");

            return index;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }

        static void RunInBackground(UniTask<CommandRunResult> task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    Debug.LogError($"[WithTableElementsExecutor] Background execution failed: {ex.Message}");
                }
            });
        }
    }
}
