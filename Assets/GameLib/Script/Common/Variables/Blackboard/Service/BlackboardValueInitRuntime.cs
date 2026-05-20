#nullable enable

using System;

namespace Game.Common
{
    public enum BlackboardValueInitPhase
    {
        Create = 10,
        Acquire = 20,
    }

    public sealed class BlackboardLocalValueInitEntryPlan
    {
        public BlackboardLocalValueInitEntryPlan(int varId, DynamicValue value, int order)
        {
            if (varId == 0)
                throw new ArgumentException("Blackboard local value init entries must provide a non-zero var id.", nameof(varId));

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), order, "Blackboard local value init entries must provide a non-negative order.");

            VarId = varId;
            Value = value;
            Order = order;
        }

        public int VarId { get; }

        public DynamicValue Value { get; }

        public int Order { get; }
    }

    public sealed class BlackboardLocalValueInitPlan
    {
        readonly BlackboardLocalValueInitEntryPlan[] entries;

        public BlackboardLocalValueInitPlan(BlackboardValueInitPhase phase, bool overwriteExisting, BlackboardLocalValueInitEntryPlan[] entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            Phase = phase;
            OverwriteExisting = overwriteExisting;
            this.entries = CloneAndSortEntries(entries);
        }

        public BlackboardValueInitPhase Phase { get; }

        public bool OverwriteExisting { get; }

        public ReadOnlySpan<BlackboardLocalValueInitEntryPlan> Entries => entries;

        static BlackboardLocalValueInitEntryPlan[] CloneAndSortEntries(BlackboardLocalValueInitEntryPlan[] source)
        {
            BlackboardLocalValueInitEntryPlan[] clone = new BlackboardLocalValueInitEntryPlan[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Blackboard local value init entries must not contain null items.", nameof(source));
            }

            Array.Sort(clone, static (left, right) =>
            {
                int result = left.Order.CompareTo(right.Order);
                return result != 0 ? result : left.VarId.CompareTo(right.VarId);
            });
            return clone;
        }
    }

    public sealed class BlackboardGridValueInitCellPlan
    {
        readonly VarStorePayload.Entry[] entries;

        public BlackboardGridValueInitCellPlan(int row, int column, int order, VarStorePayload.Entry[]? entries = null)
        {
            if (row < 0)
                throw new ArgumentOutOfRangeException(nameof(row), row, "Blackboard grid init cells must provide a non-negative row.");

            if (column < 0)
                throw new ArgumentOutOfRangeException(nameof(column), column, "Blackboard grid init cells must provide a non-negative column.");

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), order, "Blackboard grid init cells must provide a non-negative order.");

            Row = row;
            Column = column;
            Order = order;
            this.entries = CloneAndSortEntries(entries);
        }

        public int Row { get; }

        public int Column { get; }

        public int Order { get; }

        public ReadOnlySpan<VarStorePayload.Entry> Entries => entries;

        static VarStorePayload.Entry[] CloneAndSortEntries(VarStorePayload.Entry[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<VarStorePayload.Entry>();

            VarStorePayload.Entry[] clone = new VarStorePayload.Entry[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }
    }

    public sealed class BlackboardGridValueInitPlan
    {
        readonly BlackboardGridValueInitCellPlan[] cells;

        public BlackboardGridValueInitPlan(BlackboardValueInitPhase phase, bool overwriteExisting, int gridIdVarId, BlackboardGridValueInitCellPlan[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            Phase = phase;
            OverwriteExisting = overwriteExisting;
            GridIdVarId = gridIdVarId;
            this.cells = CloneAndSortCells(cells);
        }

        public BlackboardValueInitPhase Phase { get; }

        public bool OverwriteExisting { get; }

        public int GridIdVarId { get; }

        public ReadOnlySpan<BlackboardGridValueInitCellPlan> Cells => cells;

        static BlackboardGridValueInitCellPlan[] CloneAndSortCells(BlackboardGridValueInitCellPlan[] source)
        {
            BlackboardGridValueInitCellPlan[] clone = new BlackboardGridValueInitCellPlan[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Blackboard grid init cells must not contain null items.", nameof(source));
            }

            Array.Sort(clone, static (left, right) =>
            {
                int result = left.Order.CompareTo(right.Order);
                if (result != 0)
                    return result;

                result = left.Row.CompareTo(right.Row);
                if (result != 0)
                    return result;

                return left.Column.CompareTo(right.Column);
            });
            return clone;
        }
    }

    public static class BlackboardValueInitRuntime
    {
        public static void ApplyLocalPlan(IBlackboardService blackboard, IScopeNode? owner, BlackboardLocalValueInitPlan? plan, DynamicEvaluationRuntime runtime)
        {
            if (blackboard == null || plan == null)
                return;

            IVarStore vars = blackboard.LocalVars;
            if (vars == null)
                return;

            DynamicEvaluationPhase phase = MapPhase(plan.Phase);
            IDynamicContext baseContext = new SimpleDynamicContext(vars, owner);
            ReadOnlySpan<BlackboardLocalValueInitEntryPlan> entries = plan.Entries;
            for (int index = 0; index < entries.Length; index++)
            {
                BlackboardLocalValueInitEntryPlan entry = entries[index];
                if (!plan.OverwriteExisting && vars.Contains(entry.VarId))
                    continue;

                if (plan.OverwriteExisting && vars.Contains(entry.VarId))
                    vars.TryUnset(entry.VarId);

                DynamicVariant evaluated = EvaluateExplicitInitValue(
                    entry.Value,
                    runtime,
                    baseContext,
                    phase,
                    ComposePositiveId(phase, entry.VarId, entry.Order, 0),
                    ComposePositiveId(phase, entry.VarId, entry.Order, 1),
                    "LocalVar:" + entry.VarId);

                TrySetLocalValue(vars, entry.VarId, in evaluated);
            }
        }

        public static void ApplyGridPlan(IBlackboardService blackboard, IGridBlackboardService gridBlackboard, IScopeNode? owner, BlackboardGridValueInitPlan? plan, DynamicEvaluationRuntime runtime)
        {
            if (blackboard == null || gridBlackboard == null || plan == null)
                return;

            IVarStore vars = blackboard.LocalVars;
            if (vars == null)
                return;

            DynamicEvaluationPhase phase = MapPhase(plan.Phase);
            IDynamicContext baseContext = new SimpleDynamicContext(vars, owner);
            ReadOnlySpan<BlackboardGridValueInitCellPlan> cells = plan.Cells;
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                BlackboardGridValueInitCellPlan cell = cells[cellIndex];
                ReadOnlySpan<VarStorePayload.Entry> entries = cell.Entries;
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    VarStorePayload.Entry entry = entries[entryIndex];
                    if (entry.VarId == 0)
                        continue;

                    DynamicEvaluationContext evaluationContext = CreateEvaluationContext(
                        runtime,
                        baseContext,
                        phase,
                        ComposePositiveId(phase, cell.Row, cell.Column, entry.VarId, entryIndex),
                        ComposePositiveId(phase, cell.Row, cell.Column, entry.VarId, entryIndex, 1),
                        "GridCell:" + cell.Row + ":" + cell.Column + ":VarId=" + entry.VarId);

                    if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, evaluationContext, out DynamicVariant value))
                        continue;

                    GridBlackboardWriteUtility.TryWriteCellValue(gridBlackboard, cell.Row, cell.Column, entry.VarId, in value, plan.OverwriteExisting, upsert: true);
                }

                if (plan.GridIdVarId != 0)
                {
                    DynamicVariant gridIdValue = DynamicVariant.FromBool(true);
                    GridBlackboardWriteUtility.TryWriteGridId(gridBlackboard, cell.Row, cell.Column, plan.GridIdVarId, in gridIdValue);
                }
            }
        }

        static DynamicVariant EvaluateExplicitInitValue(
            DynamicValue value,
            DynamicEvaluationRuntime runtime,
            IDynamicContext baseContext,
            DynamicEvaluationPhase phase,
            int planIdValue,
            int sourceHandleValue,
            string sourceLocation)
        {
            DynamicEvaluationContext context = CreateEvaluationContext(runtime, baseContext, phase, planIdValue, sourceHandleValue, sourceLocation);
            return value.Evaluate(context);
        }

        static DynamicEvaluationContext CreateEvaluationContext(
            DynamicEvaluationRuntime runtime,
            IDynamicContext baseContext,
            DynamicEvaluationPhase phase,
            int planIdValue,
            int sourceHandleValue,
            string sourceLocation)
        {
            DynamicEvaluationPlan evaluationPlan = new DynamicEvaluationPlan
            {
                PlanId = new DynamicEvaluationPlanId(planIdValue),
                RootSource = new DynamicSourceHandle(sourceHandleValue),
                Phase = phase,
                DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                FallbackPolicy = DynamicFallbackPolicy.Forbidden,
                CachePolicy = DynamicCachePolicy.SharedTracked,
                RequirePlan = true,
                SourceLocation = sourceLocation,
            };

            DynamicDependencyStamp dependencyStamp = DynamicDependencyStamp.FromContext(baseContext, sourceVersion: sourceHandleValue);

            return new DynamicEvaluationContext(baseContext, runtime, evaluationPlan, phase, dependencyStamp, requirePlan: true);
        }

        static DynamicEvaluationPhase MapPhase(BlackboardValueInitPhase phase)
        {
            switch (phase)
            {
                case BlackboardValueInitPhase.Create:
                    return DynamicEvaluationPhase.Init;
                case BlackboardValueInitPhase.Acquire:
                    return DynamicEvaluationPhase.Acquire;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported blackboard value init phase.");
            }
        }

        static int ComposePositiveId(DynamicEvaluationPhase phase, int a, int b, int c)
        {
            return ComposePositiveId(phase, a, b, c, 0, 0);
        }

        static int ComposePositiveId(DynamicEvaluationPhase phase, int a, int b, int c, int d)
        {
            return ComposePositiveId(phase, a, b, c, d, 0);
        }

        static int ComposePositiveId(DynamicEvaluationPhase phase, int a, int b, int c, int d, int salt)
        {
            unchecked
            {
                int value = 17;
                value = (value * 31) + (int)phase;
                value = (value * 31) + a;
                value = (value * 31) + b;
                value = (value * 31) + c;
                value = (value * 31) + d;
                value = (value * 31) + salt;
                if (value == int.MinValue)
                    value = int.MaxValue;
                value = Math.Abs(value);
                return value == 0 ? 1 : value;
            }
        }

        static bool TrySetLocalValue(IVarStore vars, int varId, in DynamicVariant value)
        {
            if (value.Kind == ValueKind.Null)
            {
                if (!vars.Contains(varId))
                    return true;

                return vars.TryUnset(varId);
            }

            if (value.Kind == ValueKind.ManagedRef)
                return value.AsManagedRef != null && vars.TrySetManagedRef(varId, value.AsManagedRef);

            return vars.TrySetVariant(varId, value);
        }
    }
}