#nullable enable
using Game.Common;
using Game.Vars.Generated;
using VContainer;

namespace Game.Channel
{
    internal sealed class GridObjectChannelPayloadBuilder
    {
        readonly string _tag;

        public GridObjectChannelPayloadBuilder(string tag)
        {
            _tag = tag;
        }

        public VarStore BuildPayload(GridObjectChannelRuntimeState state, GridObjectChannelResolvedItem item)
        {
            var payload = new VarStore(initialCapacity: 32);
            ApplyItemVars(payload, state, item);
            ApplyCellValues(payload, item.CellValues);
            return payload;
        }

        public IVarStore BuildCommandVars(VarStore payload)
        {
            return BlackboardPayloadProjectionUtility.ProjectCommandVars(payload);
        }

        void ApplyItemVars(IVarStore vars, GridObjectChannelRuntimeState state, GridObjectChannelResolvedItem item)
        {
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.channelTag, DynamicVariant.FromString(_tag));
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.listIndex, DynamicVariant.FromInt(item.ListIndex));
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.row, DynamicVariant.FromInt(item.Row));
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.column, DynamicVariant.FromInt(item.Column));
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.sourceRow, DynamicVariant.FromInt(item.SourceRow));
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.sourceColumn, DynamicVariant.FromInt(item.SourceColumn));

            if (state.ActiveChoiceEntries == null || item.ListIndex < 0 || item.ListIndex >= state.ActiveChoiceEntries.Count)
                return;

            var choiceEntry = state.ActiveChoiceEntries[item.ListIndex];
            var displayName = choiceEntry?.DisplayName ?? string.Empty;
            GridObjectChannelRuntimeUtility.WriteVariant(vars, VarIds.GameLib.UI.DialogueChannel.Choice.DisplayName, DynamicVariant.FromString(displayName));
        }

        static void ApplyCellValues(IVarStore vars, System.Collections.Generic.List<GridBlackboardCellSnapshot>? values)
        {
            if (vars == null || values == null)
                return;

            for (var i = 0; i < values.Count; i++)
            {
                var cell = values[i];
                if (cell.VarId == 0)
                    continue;

                if (cell.Value.Kind == ValueKind.ManagedRef)
                {
                    var managed = cell.Value.AsManagedRef;
                    if (managed != null)
                        vars.TrySetManagedRef(cell.VarId, managed);

                    continue;
                }

                vars.TrySetVariant(cell.VarId, cell.Value);
            }
        }
    }
}
