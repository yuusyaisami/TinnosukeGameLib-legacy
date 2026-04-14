#nullable enable

using System;
using Game.Common;
using Game.Vars.Generated;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum TableSelectorMode
    {
        Custom = 10,
        All = 20,
        Condition = 30,
    }

    public enum TableTraversalOrder
    {
        RowMajor = 10,
        ColumnMajor = 20,
    }

    [Serializable]
    public sealed class WithTableElementsCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithTableElements;

        public string DebugData
        {
            get
            {
                var bodyCount = Body?.Count ?? 0;
                var table = DescribeTableSource(TableSource);
                return $"Table.WithElements Row={RowMode} Col={ColumnMode} Order={TraversalOrder} Await={AwaitMode} Missing={MissingTablePolicy} Body={bodyCount} {table}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TableActorSource)")]
        public ActorSource TableActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Table")]
        public DynamicValue<Table> TableSource = DynamicValue<Table>.FromSource(new LiteralTableSource());

        [BoxGroup("Select")]
        [LabelText("Row Mode")]
        [EnumToggleButtons]
        public TableSelectorMode RowMode = TableSelectorMode.All;

        [BoxGroup("Select")]
        [ShowIf(nameof(ShowRowIndex))]
        [LabelText("Row Index")]
        public DynamicValue<int> RowIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Select")]
        [ShowIf(nameof(ShowRowCondition))]
        [LabelText("Row Condition")]
        public DynamicValue<bool> RowCondition = DynamicValue<bool>.FromSource(new LiteralBoolSource(true));

        [BoxGroup("Select")]
        [LabelText("Column Mode")]
        [EnumToggleButtons]
        public TableSelectorMode ColumnMode = TableSelectorMode.All;

        [BoxGroup("Select")]
        [ShowIf(nameof(ShowColumnIndex))]
        [LabelText("Column Index")]
        public DynamicValue<int> ColumnIndex = DynamicValue<int>.FromSource(new LiteralIntSource(0));

        [BoxGroup("Select")]
        [ShowIf(nameof(ShowColumnCondition))]
        [LabelText("Column Condition")]
        public DynamicValue<bool> ColumnCondition = DynamicValue<bool>.FromSource(new LiteralBoolSource(true));

        [BoxGroup("Select")]
        [LabelText("Traversal Order")]
        [EnumToggleButtons]
        public TableTraversalOrder TraversalOrder = TableTraversalOrder.RowMajor;

        [BoxGroup("Execution")]
        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [BoxGroup("Execution")]
        [LabelText("Missing Table Policy")]
        [EnumToggleButtons]
        public CommandFailurePolicy MissingTablePolicy = CommandFailurePolicy.FailFast;

        [BoxGroup("Context")]
        [LabelText("Row Index Var")]
        [VarIdDropdown]
        public int RowIndexVarId = VarIds.GameLib.Channel.GridObjectChannel.Item.row;

        [BoxGroup("Context")]
        [LabelText("Column Index Var")]
        [VarIdDropdown]
        public int ColumnIndexVarId = VarIds.GameLib.Channel.GridObjectChannel.Item.column;

        [BoxGroup("Context")]
        [LabelText("Row Count Var")]
        [VarIdDropdown]
        public int RowCountVarId = VarIds.GameLib.Channel.GridObjectChannel.Item.sourceRow;

        [BoxGroup("Context")]
        [LabelText("Column Count Var")]
        [VarIdDropdown]
        public int ColumnCountVarId = VarIds.GameLib.Channel.GridObjectChannel.Item.sourceColumn;

        [BoxGroup("Body")]
        [LabelText("Body")]
        [CommandListFunctionName("Table.WithElements")]
        public CommandListData Body = new();

        bool ShowRowIndex() => RowMode == TableSelectorMode.Custom;
        bool ShowRowCondition() => RowMode == TableSelectorMode.Condition;
        bool ShowColumnIndex() => ColumnMode == TableSelectorMode.Custom;
        bool ShowColumnCondition() => ColumnMode == TableSelectorMode.Condition;

        static string DescribeTableSource(DynamicValue<Table> tableSource)
        {
            if (!tableSource.HasSource)
                return "table=None";

            var sourceType = tableSource.SourceTypeName;
            var debugData = tableSource.SourceDebugData;
            if (string.IsNullOrEmpty(debugData))
                return $"table={sourceType}";

            return $"table={sourceType}:{debugData}";
        }
    }
}
