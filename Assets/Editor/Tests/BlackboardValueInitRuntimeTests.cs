#nullable enable

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    public sealed class BlackboardValueInitRuntimeTests
    {
        [Test]
        public void ApplyLocalPlan_SkipsExistingCreateAndOverwritesOnAcquire()
        {
            var scope = new TestScopeNode(LifetimeScopeKind.Runtime);
            var blackboard = new BlackboardService(scope);
            var runtime = new DynamicEvaluationRuntime();

            Assert.That(blackboard.LocalVars.TrySetVariant(101, DynamicVariant.FromInt(3)), Is.True);

            var skippedChild = new CountingSource(DynamicVariant.FromFloat(5f));
            var appliedChild = new CountingSource(DynamicVariant.FromFloat(7f));
            var overwriteChild = new CountingSource(DynamicVariant.FromFloat(10f));

            var createPlan = new BlackboardLocalValueInitPlan(
                BlackboardValueInitPhase.Create,
                overwriteExisting: false,
                new[]
                {
                    new BlackboardLocalValueInitEntryPlan(101, DynamicValue.FromSource(new NestedSource(DynamicValue<float>.FromSource(skippedChild))), 0),
                    new BlackboardLocalValueInitEntryPlan(102, DynamicValue.FromSource(new NestedSource(DynamicValue<float>.FromSource(appliedChild))), 1),
                });

            BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, scope, createPlan, runtime);

            Assert.That(blackboard.LocalVars.TryGetVariant(101, out DynamicVariant retainedValue), Is.True);
            Assert.That(retainedValue.AsInt, Is.EqualTo(3));
            Assert.That(blackboard.LocalVars.TryGetVariant(102, out DynamicVariant createdValue), Is.True);
            Assert.That(createdValue.AsFloat, Is.EqualTo(8f));
            Assert.That(skippedChild.Evaluations, Is.Zero);
            Assert.That(appliedChild.Evaluations, Is.EqualTo(1));
            Assert.That(runtime.LastDependencies, Has.Count.EqualTo(1));

            var acquirePlan = new BlackboardLocalValueInitPlan(
                BlackboardValueInitPhase.Acquire,
                overwriteExisting: true,
                new[]
                {
                    new BlackboardLocalValueInitEntryPlan(101, DynamicValue.FromSource(new NestedSource(DynamicValue<float>.FromSource(overwriteChild))), 0),
                });

            BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, scope, acquirePlan, runtime);

            Assert.That(blackboard.LocalVars.TryGetVariant(101, out DynamicVariant overwrittenValue), Is.True);
            Assert.That(overwrittenValue.AsFloat, Is.EqualTo(11f));
            Assert.That(overwriteChild.Evaluations, Is.EqualTo(1));
            Assert.That(runtime.LastDependencies, Has.Count.EqualTo(1));
        }

        [Test]
        public void ApplyLocalPlan_RespectsExplicitEntryOrder()
        {
            var scope = new TestScopeNode(LifetimeScopeKind.Runtime);
            var blackboard = new BlackboardService(scope);
            var runtime = new DynamicEvaluationRuntime();

            var plan = new BlackboardLocalValueInitPlan(
                BlackboardValueInitPhase.Create,
                overwriteExisting: false,
                new[]
                {
                    new BlackboardLocalValueInitEntryPlan(20, DynamicValue.FromLiteral(5), 0),
                    new BlackboardLocalValueInitEntryPlan(10, DynamicValue.FromVarId(20), 1),
                });

            BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, scope, plan, runtime);

            Assert.That(blackboard.LocalVars.TryGetVariant(20, out DynamicVariant sourceValue), Is.True);
            Assert.That(sourceValue.AsInt, Is.EqualTo(5));
            Assert.That(blackboard.LocalVars.TryGetVariant(10, out DynamicVariant dependentValue), Is.True);
            Assert.That(dependentValue.AsInt, Is.EqualTo(5));
        }

        [Test]
        public void ApplyGridPlan_WritesEvaluatedCellValuesAndGridId()
        {
            var scope = new TestScopeNode(LifetimeScopeKind.Runtime);
            var blackboard = new BlackboardService(scope);
            var gridBlackboard = new GridBlackboardService();
            var runtime = new DynamicEvaluationRuntime();
            var child = new CountingSource(DynamicVariant.FromFloat(6f));

            var gridPlan = new BlackboardGridValueInitPlan(
                BlackboardValueInitPhase.Create,
                overwriteExisting: false,
                gridIdVarId: 999,
                new[]
                {
                    new BlackboardGridValueInitCellPlan(
                        2,
                        3,
                        0,
                        new[]
                        {
                            new VarStorePayload.Entry
                            {
                                VarId = 401,
                                Kind = VarStorePayload.EntryValueKind.Float,
                                StoreMode = VarStoreWriteMode.Immediate,
                                Value = DynamicValue.FromSource(new NestedSource(DynamicValue<float>.FromSource(child))),
                            },
                        }),
                });

            BlackboardValueInitRuntime.ApplyGridPlan(blackboard, gridBlackboard, scope, gridPlan, runtime);

            Assert.That(gridBlackboard.TryGetVariant(401, 2, 3, out DynamicVariant cellValue), Is.True);
            Assert.That(cellValue.AsFloat, Is.EqualTo(7f));
            Assert.That(gridBlackboard.TryGetVariant(999, 2, 3, out DynamicVariant gridIdValue), Is.True);
            Assert.That(gridIdValue.AsBool, Is.True);
            Assert.That(child.Evaluations, Is.EqualTo(1));
            Assert.That(runtime.LastDependencies, Has.Count.EqualTo(1));
        }

        [Test]
        public void TablePayload_PreservesRowAndCellIdentity()
        {
            var table = new Table
            {
                Revision = 7,
            };

            GetMutableRows(table).Add(new Table.RowPayload
            {
                RowId = 101,
                Revision = 11,
                Cells =
                {
                    new Table.CellPayload
                    {
                        ColumnId = 201,
                        CellId = 301,
                        Revision = 13,
                    },
                },
            });

            Assert.That(table.TryGetRowIdentity(0, out int rowId, out int rowRevision), Is.True);
            Assert.That(rowId, Is.EqualTo(101));
            Assert.That(rowRevision, Is.EqualTo(11));

            Assert.That(table.TryGetCellIdentity(0, 0, out int resolvedRowId, out int columnId, out int cellId, out int cellRevision), Is.True);
            Assert.That(resolvedRowId, Is.EqualTo(101));
            Assert.That(columnId, Is.EqualTo(201));
            Assert.That(cellId, Is.EqualTo(301));
            Assert.That(cellRevision, Is.EqualTo(13));
        }

        [Test]
        public void TablePayload_NormalizesMissingIds_WithoutDependingOnRowOrder()
        {
            var first = new Table();
            GetMutableRows(first).Add(new Table.RowPayload
            {
                Revision = 11,
                Cells =
                {
                    new Table.CellPayload
                    {
                        Revision = 13,
                    },
                },
            });
            GetMutableRows(first).Add(new Table.RowPayload
            {
                Revision = 21,
                Cells =
                {
                    new Table.CellPayload
                    {
                        Revision = 23,
                    },
                },
            });

            first.NormalizeIdentities();

            var second = new Table();
            GetMutableRows(second).Add(new Table.RowPayload
            {
                Revision = 21,
                Cells =
                {
                    new Table.CellPayload
                    {
                        Revision = 23,
                    },
                },
            });
            GetMutableRows(second).Add(new Table.RowPayload
            {
                Revision = 11,
                Cells =
                {
                    new Table.CellPayload
                    {
                        Revision = 13,
                    },
                },
            });

            second.NormalizeIdentities();

            Assert.That(first.TryGetRowIdentity(0, out int firstRowId, out int firstRowRevision), Is.True);
            Assert.That(firstRowRevision, Is.EqualTo(11));
            Assert.That(first.TryGetCellIdentity(0, 0, out _, out _, out int firstCellId, out int firstCellRevision), Is.True);
            Assert.That(firstCellRevision, Is.EqualTo(13));

            Assert.That(second.TryGetRowIdentity(1, out int secondRowId, out int secondRowRevision), Is.True);
            Assert.That(secondRowRevision, Is.EqualTo(11));
            Assert.That(second.TryGetCellIdentity(1, 0, out _, out _, out int secondCellId, out int secondCellRevision), Is.True);
            Assert.That(secondCellRevision, Is.EqualTo(13));

            Assert.That(firstRowId, Is.EqualTo(secondRowId));
            Assert.That(firstCellId, Is.EqualTo(secondCellId));
        }

        static List<Table.RowPayload> GetMutableRows(Table table)
        {
            var field = typeof(Table).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);

            var rows = field!.GetValue(table) as List<Table.RowPayload>;
            Assert.That(rows, Is.Not.Null);
            return rows!;
        }

        [Test]
        public void VarStoreTable_RevisionsBumpOnStructuralAndCellChanges()
        {
            var store = new VarStore();

            Assert.That(store.TryEnsureTableRow(900, 0), Is.True);
            Assert.That(store.GetTableVersion(900), Is.EqualTo(1));
            Assert.That(store.TryGetTableRowVersion(900, 0, out int rowVersionAfterCreate), Is.True);
            Assert.That(rowVersionAfterCreate, Is.EqualTo(0));

            Assert.That(store.TryAppendTableCell(900, 0, out int columnIndex), Is.True);
            Assert.That(columnIndex, Is.EqualTo(0));
            Assert.That(store.GetTableVersion(900), Is.EqualTo(2));
            Assert.That(store.TryGetTableRowVersion(900, 0, out int rowVersionAfterAppend), Is.True);
            Assert.That(rowVersionAfterAppend, Is.EqualTo(1));
            Assert.That(store.TryGetTableCellVersion(900, 0, 0, out int cellVersionBeforeWrite), Is.True);
            Assert.That(cellVersionBeforeWrite, Is.EqualTo(0));

            Assert.That(store.TryGetTableCellStore(900, 0, 0, out IVarStore cellStore), Is.True);
            Assert.That(cellStore.TrySetVariant(401, DynamicVariant.FromInt(3)), Is.True);

            Assert.That(store.GetTableVersion(900), Is.EqualTo(3));
            Assert.That(store.TryGetTableRowVersion(900, 0, out int rowVersionAfterCellWrite), Is.True);
            Assert.That(rowVersionAfterCellWrite, Is.EqualTo(2));
            Assert.That(store.TryGetTableCellVersion(900, 0, 0, out int cellVersionAfterWrite), Is.True);
            Assert.That(cellVersionAfterWrite, Is.EqualTo(1));
        }

        sealed class CountingSource : IDynamicSource
        {
            readonly DynamicVariant value;

            public CountingSource(DynamicVariant value)
            {
                this.value = value;
            }

            public int Evaluations { get; private set; }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                _ = context;
                Evaluations++;
                return value;
            }

            public string SourceTypeName => nameof(CountingSource);

            public string GetDebugData => Evaluations.ToString();
        }

        sealed class NestedSource : IDynamicSource
        {
            readonly DynamicValue<float> child;

            public NestedSource(DynamicValue<float> child)
            {
                this.child = child;
            }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                return DynamicVariant.FromFloat(child.GetOrDefault(context, 0f) + 1f);
            }

            public string SourceTypeName => nameof(NestedSource);

            public string GetDebugData => string.Empty;
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly LifetimeScopeKind kind;

            public TestScopeNode(LifetimeScopeKind kind)
            {
                this.kind = kind;
            }

            public IScopeNode? Parent { get; set; }

            public IScopeIdentityService? Identity { get; set; }

            public LifetimeScopeKind Kind => kind;

            public IRuntimeResolver? Resolver { get; set; }

            public bool IsVisible { get; private set; } = true;

            public bool IsActive { get; private set; } = true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                IsVisible = visible;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                IsActive = active;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                var nodes = new List<IScopeNode>();
                TestScopeNode? current = this;

                while (current != null)
                {
                    nodes.Add(current);
                    current = current.Parent as TestScopeNode;
                }

                nodes.Reverse();
                return nodes;
            }
        }
    }
}
