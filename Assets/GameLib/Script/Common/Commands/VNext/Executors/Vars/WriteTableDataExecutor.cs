#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WriteTableDataExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WriteTableData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not WriteTableDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteTableDataCommandData is required.");

            if (typed.TableVarId == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Table Var Id is required.");

            var targetScope = ResolveTargetScope(ctx, typed.TableActorSource);
            var tableStore = ResolveTargetVarStore(targetScope);
            IDynamicContext dynCtx = ctx;

            switch (typed.Operation)
            {
                case WriteTableDataOperation.CreateRow:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    EnsureSuccess(
                        tableStore.TryEnsureTableRow(typed.TableVarId, rowIndex),
                        $"CreateRow failed. table={typed.TableVarId} row={rowIndex}");
                    break;
                }

                case WriteTableDataOperation.InsertRow:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    EnsureSuccess(
                        tableStore.TryInsertTableRow(typed.TableVarId, rowIndex),
                        $"InsertRow failed. table={typed.TableVarId} row={rowIndex}");
                    break;
                }

                case WriteTableDataOperation.DeleteRow:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    EnsureSuccess(
                        tableStore.TryRemoveTableRow(typed.TableVarId, rowIndex),
                        $"DeleteRow failed. table={typed.TableVarId} row={rowIndex}");
                    break;
                }

                case WriteTableDataOperation.AppendCell:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    EnsureSuccess(
                        tableStore.TryAppendTableCell(typed.TableVarId, rowIndex, out _),
                        $"AppendCell failed. table={typed.TableVarId} row={rowIndex}");
                    break;
                }

                case WriteTableDataOperation.InsertCell:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    var columnIndex = ResolveNonNegativeIndex(typed.ColumnIndex, dynCtx, "column index");
                    EnsureSuccess(
                        tableStore.TryInsertTableCell(typed.TableVarId, rowIndex, columnIndex),
                        $"InsertCell failed. table={typed.TableVarId} row={rowIndex} column={columnIndex}");
                    break;
                }

                case WriteTableDataOperation.DeleteCell:
                {
                    var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
                    var columnIndex = ResolveNonNegativeIndex(typed.ColumnIndex, dynCtx, "column index");
                    EnsureSuccess(
                        tableStore.TryRemoveTableCell(typed.TableVarId, rowIndex, columnIndex),
                        $"DeleteCell failed. table={typed.TableVarId} row={rowIndex} column={columnIndex}");
                    break;
                }

                case WriteTableDataOperation.WriteVarToCell:
                    ExecuteWriteVarToCell(typed, tableStore, dynCtx);
                    break;

                case WriteTableDataOperation.MergeEntriesToCell:
                    ExecuteMergeEntriesToCell(typed, tableStore, dynCtx);
                    break;

                case WriteTableDataOperation.ClearTable:
                    EnsureSuccess(
                        tableStore.TryClearTable(typed.TableVarId),
                        $"ClearTable failed. table={typed.TableVarId}");
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void ExecuteWriteVarToCell(WriteTableDataCommandData typed, IVarStore tableStore, IDynamicContext dynCtx)
        {
            var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
            var columnIndex = ResolveNonNegativeIndex(typed.ColumnIndex, dynCtx, "column index");
            var targetVarId = ResolveVarId(typed.TargetKey);
            if (targetVarId == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteVarToCell requires Target Var.");

            EnsureSuccess(
                tableStore.TryGetOrEnsureTableCellStore(typed.TableVarId, rowIndex, columnIndex, autoCreateRow: true, out var cellStore),
                $"WriteVarToCell target cell does not exist. table={typed.TableVarId} row={rowIndex} column={columnIndex}");

            if (typed.StoreMode == VarStoreWriteMode.DeferredDynamic)
            {
                if (!typed.Value.HasSource)
                {
                    EnsureSuccess(
                        cellStore.TryUnset(targetVarId),
                        $"WriteVarToCell deferred unset failed. table={typed.TableVarId} row={rowIndex} column={columnIndex} varId={targetVarId}");
                    return;
                }

                var deferred = new DeferredDynamicVarValue(typed.Value, typed.ValueKind, targetVarId, "WriteTableData.WriteVarToCell");
                EnsureSuccess(
                    cellStore.TrySetManagedRef(targetVarId, deferred),
                    $"WriteVarToCell deferred write failed. table={typed.TableVarId} row={rowIndex} column={columnIndex} varId={targetVarId}");
                return;
            }

            var entry = new VarStorePayload.Entry
            {
                VarId = targetVarId,
                Kind = typed.ValueKind,
                StoreMode = VarStoreWriteMode.Immediate,
                Value = typed.Value,
            };

            if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, dynCtx, out var value))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"WriteVarToCell value conversion failed. varId={targetVarId} kind={typed.ValueKind}");

            if (value.Kind == ValueKind.Null)
            {
                EnsureSuccess(
                    cellStore.TryUnset(targetVarId),
                    $"WriteVarToCell unset failed. table={typed.TableVarId} row={rowIndex} column={columnIndex} varId={targetVarId}");
                return;
            }

            if (value.Kind == ValueKind.ManagedRef)
            {
                EnsureSuccess(
                    value.AsManagedRef != null && cellStore.TrySetManagedRef(targetVarId, value.AsManagedRef),
                    $"WriteVarToCell managed ref write failed. table={typed.TableVarId} row={rowIndex} column={columnIndex} varId={targetVarId}");
                return;
            }

            EnsureSuccess(
                cellStore.TrySetVariant(targetVarId, in value),
                $"WriteVarToCell write failed. table={typed.TableVarId} row={rowIndex} column={columnIndex} varId={targetVarId}");
        }

        static void ExecuteMergeEntriesToCell(WriteTableDataCommandData typed, IVarStore tableStore, IDynamicContext dynCtx)
        {
            var rowIndex = ResolveNonNegativeIndex(typed.RowIndex, dynCtx, "row index");
            var columnIndex = ResolveNonNegativeIndex(typed.ColumnIndex, dynCtx, "column index");

            EnsureSuccess(
                tableStore.TryGetOrEnsureTableCellStore(typed.TableVarId, rowIndex, columnIndex, autoCreateRow: true, out var cellStore),
                $"MergeEntriesToCell target cell does not exist. table={typed.TableVarId} row={rowIndex} column={columnIndex}");

            typed.MergePayload?.ApplyTo(cellStore, dynCtx, typed.OverwriteOnMerge);
        }

        static IScopeNode ResolveTargetScope(CommandContext ctx, ActorSource actorSource)
        {
            var origin = ctx.Actor ?? ctx.Scope;
            var resolved = ActorSourceFastResolver.Resolve(ctx, actorSource, origin);
            return resolved ?? origin;
        }

        static IVarStore ResolveTargetVarStore(IScopeNode targetScope)
        {
            var resolver = targetScope?.Resolver;
            if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                return vars;

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "IVarStore is missing on target scope.");
        }

        static int ResolveNonNegativeIndex(DynamicValue<int> source, IDynamicContext context, string label)
        {
            var index = source.GetOrDefault(context, 0);
            if (index < 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"{label} must be >= 0, but was {index}.");

            return index;
        }

        static int ResolveVarId(in VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolvedVarId))
                return resolvedVarId;

            return 0;
        }

        static void EnsureSuccess(bool success, string message)
        {
            if (!success)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, message);
        }
    }
}
