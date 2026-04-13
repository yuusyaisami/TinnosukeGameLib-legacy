#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum WriteTableDataOperation
    {
        CreateRow = 10,
        InsertRow = 20,
        DeleteRow = 30,
        AppendCell = 40,
        InsertCell = 50,
        DeleteCell = 60,
        WriteVarToCell = 70,
        MergeEntriesToCell = 80,
        ClearTable = 90,
    }

    [Serializable]
    public sealed class WriteTableDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteTableData;

        public string DebugData
        {
            get
            {
                var table = DescribeTableId(TableVarId);
                return Operation switch
                {
                    WriteTableDataOperation.ClearTable => $"Table:{Operation} {table}",
                    WriteTableDataOperation.CreateRow or WriteTableDataOperation.InsertRow or WriteTableDataOperation.DeleteRow =>
                        $"Table:{Operation} Row={RowIndex.SourceTypeName} {table}",
                    WriteTableDataOperation.AppendCell or WriteTableDataOperation.InsertCell or WriteTableDataOperation.DeleteCell =>
                        $"Table:{Operation} Row={RowIndex.SourceTypeName} Col={ColumnIndex.SourceTypeName} {table}",
                    WriteTableDataOperation.WriteVarToCell =>
                        $"Table:{Operation} Key={TargetKey.StableKey} {table}",
                    WriteTableDataOperation.MergeEntriesToCell =>
                        $"Table:{Operation} Entries={MergePayload?.Entries?.Count ?? 0} {table}",
                    _ => "WriteTableData",
                };
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TableActorSource)")]
        public ActorSource TableActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Table Var Id")]
        [VarIdDropdown]
        public int TableVarId;

        [BoxGroup("Mode")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public WriteTableDataOperation Operation = WriteTableDataOperation.WriteVarToCell;

        [BoxGroup("Index")]
        [ShowIf(nameof(ShowRowIndex))]
        [LabelText("Row Index")]
        public DynamicValue<int> RowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Index")]
        [ShowIf(nameof(ShowColumnIndex))]
        [LabelText("Column Index")]
        public DynamicValue<int> ColumnIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Write")]
        [ShowIf(nameof(ShowWritePayload))]
        [LabelText("Target Var")]
        public VarKeyRef TargetKey;

        [BoxGroup("Write")]
        [ShowIf(nameof(ShowWritePayload))]
        [LabelText("Value Kind")]
        public VarStorePayload.EntryValueKind ValueKind = VarStorePayload.EntryValueKind.Auto;

        [BoxGroup("Write")]
        [ShowIf(nameof(ShowWritePayload))]
        [LabelText("Store Mode")]
        public VarStoreWriteMode StoreMode = VarStoreWriteMode.Immediate;

        [BoxGroup("Write")]
        [ShowIf(nameof(ShowWritePayload))]
        [LabelText("Value")]
        [InlineProperty]
        public DynamicValue Value = DynamicValue.FromSource(new LiteralIntSource(0));

        [BoxGroup("Merge")]
        [ShowIf(nameof(ShowMergePayload))]
        [LabelText("Payload")]
        [InlineProperty]
        public VarStorePayload MergePayload = new();

        [BoxGroup("Merge")]
        [ShowIf(nameof(ShowMergePayload))]
        [LabelText("Overwrite Existing")]
        public bool OverwriteOnMerge = true;

        bool ShowRowIndex()
        {
            return Operation != WriteTableDataOperation.ClearTable;
        }

        bool ShowColumnIndex()
        {
            return Operation == WriteTableDataOperation.InsertCell
                || Operation == WriteTableDataOperation.DeleteCell
                || Operation == WriteTableDataOperation.WriteVarToCell
                || Operation == WriteTableDataOperation.MergeEntriesToCell;
        }

        bool ShowWritePayload()
        {
            return Operation == WriteTableDataOperation.WriteVarToCell;
        }

        bool ShowMergePayload()
        {
            return Operation == WriteTableDataOperation.MergeEntriesToCell;
        }

        static string DescribeTableId(int tableVarId)
            => tableVarId == 0 ? "table=0" : (VarIdResolver.TryGetIdToStable(tableVarId) ?? $"table={tableVarId}");
    }
}
