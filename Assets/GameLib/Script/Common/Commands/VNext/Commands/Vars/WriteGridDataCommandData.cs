#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum WriteGridDataTargetMode
    {
        Grid = 10,
        Row = 20,
        Column = 30,
    }

    public enum WriteGridDataGridOperation
    {
        ClearAll = 10,
        CopyAllToGrid = 20,
    }

    public enum WriteGridDataRowOperation
    {
        Ensure = 10,
        Insert = 20,
        Append = 30,
        Delete = 40,
        Clear = 50,
        CopyToRow = 60,
    }

    public enum WriteGridDataColumnOperation
    {
        Ensure = 10,
        Insert = 20,
        Append = 30,
        Delete = 40,
        Clear = 50,
        CopyToColumn = 60,
        SetElement = 70,
        RemoveElement = 80,
        AddNumeric = 90,
        MultiplyNumeric = 100,
    }

    [Serializable]
    public sealed class WriteGridDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteGridData;

        public string DebugData
        {
            get
            {
                var gridIdSuffix = GridId != 0 ? $",GridId={DescribeGridId(GridId)}" : string.Empty;
                return TargetMode switch
                {
                    WriteGridDataTargetMode.Grid => $"Grid:{GridOperation}{gridIdSuffix}",
                    WriteGridDataTargetMode.Row => $"Row:{RowOperation}{gridIdSuffix}",
                    WriteGridDataTargetMode.Column => ColumnOperation == WriteGridDataColumnOperation.Append && SetElementAfterAppend
                        ? $"Column:Append+SetElement[{ElementValues?.Entries?.Count ?? 0}]{gridIdSuffix}"
                        : $"Column:{ColumnOperation}{gridIdSuffix}",
                    _ => "WriteGridData",
                };
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(GridActorSource)")]
        public ActorSource GridActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Mode")]
        [EnumToggleButtons]
        [LabelText("Target Mode")]
        public WriteGridDataTargetMode TargetMode = WriteGridDataTargetMode.Column;

        [BoxGroup("Grid")]
        [ShowIf(nameof(IsGridMode))]
        [EnumToggleButtons]
        [LabelText("Operation")]
        public WriteGridDataGridOperation GridOperation = WriteGridDataGridOperation.ClearAll;

        [BoxGroup("Grid")]
        [ShowIf(nameof(ShowGridDestination))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(DestinationGridActorSource)")]
        public ActorSource DestinationGridActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Grid")]
        [ShowIf(nameof(ShowGridCopyOptions))]
        [LabelText("Clear Destination Before Copy")]
        public bool ClearDestinationBeforeCopy = true;

        [BoxGroup("Grid")]
        [ShowIf(nameof(ShowGridCopyOptions))]
        [LabelText("Overwrite Existing On Copy")]
        public bool OverwriteOnCopy = true;

        [BoxGroup("Row")]
        [ShowIf(nameof(IsRowMode))]
        [EnumToggleButtons]
        [LabelText("Operation")]
        public WriteGridDataRowOperation RowOperation = WriteGridDataRowOperation.Ensure;

        [BoxGroup("Row")]
        [ShowIf(nameof(ShowRowIndex))]
        [LabelText("Row Index")]
        public DynamicValue<int> RowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Row Copy")]
        [ShowIf(nameof(ShowRowCopyDestination))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(DestinationRowGridActorSource)")]
        public ActorSource DestinationRowGridActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Row Copy")]
        [ShowIf(nameof(ShowRowCopyDestination))]
        [LabelText("Destination Row Index")]
        public DynamicValue<int> DestinationRowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Row Copy")]
        [ShowIf(nameof(ShowRowCopyOptions))]
        [LabelText("Clear Destination Row Before Copy")]
        public bool ClearDestinationRowBeforeCopy = true;

        [BoxGroup("Row Copy")]
        [ShowIf(nameof(ShowRowCopyOptions))]
        [LabelText("Overwrite Existing On Copy")]
        public bool OverwriteRowCopy = true;

        [BoxGroup("Column")]
        [ShowIf(nameof(IsColumnMode))]
        [EnumToggleButtons]
        [LabelText("Operation")]
        public WriteGridDataColumnOperation ColumnOperation = WriteGridDataColumnOperation.SetElement;

        [BoxGroup("Column")]
        [ShowIf(nameof(ShowColumnAppendSetElementOption))]
        [LabelText("Set Element After Append")]
        public bool SetElementAfterAppend = false;

        [BoxGroup("Column")]
        [ShowIf(nameof(ShowGridId))]
        [LabelText("Grid Id")]
        [Tooltip("各セルへ書き込む識別 VarId です。0 の場合は Grid Id を書き込みません。")]
        [VarIdDropdown]
        public int GridId;

        [BoxGroup("Column")]
        [ShowIf(nameof(ShowElementValues))]
        [LabelText("Cell Vars")]
        [InlineProperty]
        public VarStorePayload ElementValues = new();

        [BoxGroup("Column")]
        [ShowIf(nameof(IsColumnMode))]
        [LabelText("Row Index")]
        public DynamicValue<int> ColumnRowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Column")]
        [ShowIf(nameof(ShowColumnIndex))]
        [LabelText("Column Index")]
        public DynamicValue<int> ColumnIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Column Copy")]
        [ShowIf(nameof(ShowColumnCopyDestination))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(DestinationColumnGridActorSource)")]
        public ActorSource DestinationColumnGridActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Column Copy")]
        [ShowIf(nameof(ShowColumnCopyDestination))]
        [LabelText("Destination Row Index")]
        public DynamicValue<int> DestinationColumnRowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Column Copy")]
        [ShowIf(nameof(ShowColumnCopyDestination))]
        [LabelText("Destination Column Index")]
        public DynamicValue<int> DestinationColumnIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Column Copy")]
        [ShowIf(nameof(ShowColumnCopyOptions))]
        [LabelText("Clear Destination Column Before Copy")]
        public bool ClearDestinationColumnBeforeCopy = true;

        [BoxGroup("Column Copy")]
        [ShowIf(nameof(ShowColumnCopyOptions))]
        [LabelText("Overwrite Existing On Copy")]
        public bool OverwriteColumnCopy = true;

        [BoxGroup("Column Element")]
        [ShowIf(nameof(ShowElementKey))]
        [LabelText("Element Var Key")]
        public VarKeyRef ElementKey;

        [BoxGroup("Column Element")]
        [ShowIf(nameof(ShowElementSetFlags))]
        [LabelText("Overwrite Existing")]
        public bool OverwriteElement = true;

        [BoxGroup("Column Element")]
        [ShowIf(nameof(ShowElementSetFlags))]
        [LabelText("Upsert (Create If Missing)")]
        public bool UpsertElement = true;

        [BoxGroup("Column Element")]
        [ShowIf(nameof(ShowNumericFields))]
        [LabelText("Numeric Value")]
        public DynamicValue<float> NumericValue = DynamicValue<float>.FromSource(new LiteralFloatSource(0f));

        [BoxGroup("Column Element")]
        [ShowIf(nameof(ShowNumericFields))]
        [LabelText("Create Element If Missing")]
        public bool CreateElementIfMissing = true;

        bool IsGridMode() => TargetMode == WriteGridDataTargetMode.Grid;
        bool IsRowMode() => TargetMode == WriteGridDataTargetMode.Row;
        bool IsColumnMode() => TargetMode == WriteGridDataTargetMode.Column;

        bool ShowGridDestination() => IsGridMode() && GridOperation == WriteGridDataGridOperation.CopyAllToGrid;
        bool ShowGridCopyOptions() => ShowGridDestination();

        bool ShowRowIndex() => IsRowMode() && RowOperation != WriteGridDataRowOperation.Append;
        bool ShowRowCopyDestination() => IsRowMode() && RowOperation == WriteGridDataRowOperation.CopyToRow;
        bool ShowRowCopyOptions() => ShowRowCopyDestination();

        bool ShowColumnIndex() => IsColumnMode() && ColumnOperation != WriteGridDataColumnOperation.Append;
        bool ShowColumnCopyDestination() => IsColumnMode() && ColumnOperation == WriteGridDataColumnOperation.CopyToColumn;
        bool ShowColumnCopyOptions() => ShowColumnCopyDestination();
        bool ShowColumnAppendSetElementOption() => IsColumnMode() && ColumnOperation == WriteGridDataColumnOperation.Append;
        bool ShowGridId() => IsColumnMode() && (ColumnOperation == WriteGridDataColumnOperation.Append || ColumnOperation == WriteGridDataColumnOperation.SetElement || ColumnOperation == WriteGridDataColumnOperation.AddNumeric || ColumnOperation == WriteGridDataColumnOperation.MultiplyNumeric);
        bool ShowElementValues() => IsColumnMode() && (ColumnOperation == WriteGridDataColumnOperation.SetElement || (ColumnOperation == WriteGridDataColumnOperation.Append && SetElementAfterAppend));

        bool ShowElementKey()
        {
            if (!IsColumnMode())
                return false;

            return ColumnOperation == WriteGridDataColumnOperation.RemoveElement
                || ColumnOperation == WriteGridDataColumnOperation.AddNumeric
                || ColumnOperation == WriteGridDataColumnOperation.MultiplyNumeric;
        }

        bool ShowElementSetFlags() => ShowElementValues();

        bool ShowNumericFields()
        {
            if (!IsColumnMode())
                return false;

            return ColumnOperation == WriteGridDataColumnOperation.AddNumeric
                || ColumnOperation == WriteGridDataColumnOperation.MultiplyNumeric;
        }

        static string DescribeGridId(int gridId)
            => VarIdResolver.TryGetIdToStable(gridId) ?? $"varId={gridId}";
    }
}