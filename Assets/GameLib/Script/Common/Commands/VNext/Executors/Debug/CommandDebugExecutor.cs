#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandDebugExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.DebugCommandContext;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandDebugCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandDebugCommandData is required.");

            var output = BuildPayload(typed, ctx);
            Debug.Log(output);
            return UniTask.CompletedTask;
        }

        static string BuildPayload(CommandDebugCommandData data, CommandContext ctx)
        {
            var sb = new StringBuilder();
            var label = string.IsNullOrEmpty(data.Label) ? "CommandDebug" : data.Label;
            sb.AppendLine($"[CommandDebug] {label} (CmdId={data.CommandId})");

            if (!string.IsNullOrEmpty(data.Message))
                sb.AppendLine($"Message: {data.Message}");

            sb.AppendLine($"Scope: {DescribeScope(ctx.Scope)}");
            if (ctx.Actor != null)
                sb.AppendLine($"Actor: {DescribeScope(ctx.Actor)}");

            if (data.LogScopeInfo && ctx.Scope?.Identity != null)
                sb.AppendLine($"Scope Identity: Id={ctx.Scope.Identity.Id} Kind={ctx.Scope.Identity.Kind} Active={ctx.Scope.Identity.IsActive}");

            if (data.LogRunnerInfo)
            {
                sb.AppendLine($"Runner: {ctx.Runner.GetType().Name}");
                sb.AppendLine($"Runner Scope: {DescribeScope(ctx.Runner.Scope)}");
                sb.AppendLine($"Runner Vars Version: {ctx.Vars?.GlobalVersion ?? 0}");
            }

            if (data.LogContextSlots)
                AppendContextSlots(sb, ctx);

            if (data.LogOptions)
            {
                var options = ctx.Options;
                sb.AppendLine($"Options: FailurePolicy={options.FailurePolicy} TracePolicy={options.TracePolicy} MaxDepth={options.MaxTraceDepth} MaxFrames={options.MaxTraceFrames} AllowActorFallback={options.AllowActorFallback} AllowRuntimeKeyFallback={options.AllowRuntimeKeyFallback}");
            }

            if (data.LogVarStore)
            {
                var maxEntries = Math.Max(data.MaxVarEntries, 0);
                AppendVarStore(sb, ctx.Vars, maxEntries);
            }

            var targetScope = ResolveTargetScope(data, ctx, out var resolveStatus);
            sb.AppendLine("Target Scope:");
            sb.AppendLine($"  Source: {DescribeActorSource(data.TargetScope)}");
            sb.AppendLine($"  Resolved: {resolveStatus}");

            if (targetScope != null)
            {
                if (data.LogTargetScopeInfo)
                    AppendScopeInfo(sb, targetScope);

                if (data.LogTargetBlackboard)
                {
                    sb.AppendLine("Target Blackboard:");
                    AppendBlackboard(sb, targetScope, Math.Max(data.MaxBlackboardEntries, 0));
                }

                if (data.LogTargetGridBlackboard)
                {
                    sb.AppendLine("Target Grid Blackboard:");
                    AppendGridBlackboard(sb, targetScope, Math.Max(data.MaxGridBlackboardEntries, 0));
                }

                if (data.LogTargetScalar)
                {
                    sb.AppendLine("Target Scalar:");
                    AppendScalar(sb, targetScope, Math.Max(data.MaxScalarEntries, 0), data.LogScalarSnapshots, Math.Max(data.MaxScalarSnapshotsPerKey, 0));
                }
            }
            else
            {
                if (data.LogTargetScopeInfo)
                    sb.AppendLine("  <unresolved>");

                if (data.LogTargetBlackboard)
                    sb.AppendLine("Target Blackboard: <unresolved scope>");

                if (data.LogTargetScalar)
                    sb.AppendLine("Target Scalar: <unresolved scope>");
            }

            if (data.LogWatches)
            {
                var maxEntries = Math.Max(data.MaxWatchEntries, 0);
                AppendWatches(sb, data.Watches, ctx, maxEntries);
            }

            return sb.ToString();
        }

        static IScopeNode? ResolveTargetScope(CommandDebugCommandData data, CommandContext ctx, out string resolveStatus)
        {
            resolveStatus = "(not resolved)";

            if (ctx == null)
            {
                resolveStatus = "CommandContext is null";
                return null;
            }

            var cache = default(ActorSourceResolveCache);
            var scope = ActorSourceFastResolver.ResolveCached(ctx, data.TargetScope, ref cache);
            if (scope == null)
            {
                resolveStatus = "Target scope not found";
                return null;
            }

            resolveStatus = DescribeScope(scope);
            return scope;
        }

        static string DescribeActorSource(in ActorSource source)
        {
            return source.Kind switch
            {
                ActorSourceKind.ByIdentity => $"ByIdentity ({source.Identity.kind}, Id={source.Identity.id}, Category={source.Identity.category}, Search={source.Identity.searchScope}, ActiveOnly={source.Identity.requireActive})",
                ActorSourceKind.FromUnityObject => source.UnityObject != null ? $"FromUnityObject ({source.UnityObject.name})" : "FromUnityObject (null)",
                ActorSourceKind.Shared => source.Shared == null
                    ? "Shared (null)"
                    : string.IsNullOrWhiteSpace(source.Shared.SharedTag)
                        ? $"Shared (empty, Owner={source.Shared.SharedHubActorSource.Kind})"
                        : $"Shared ({source.Shared.SharedTag}, Owner={source.Shared.SharedHubActorSource.Kind})",
                ActorSourceKind.ContextSlot => $"ContextSlot ({source.ContextSlot})",
                _ => source.Kind.ToString(),
            };
        }

        static void AppendScopeInfo(StringBuilder sb, IScopeNode scope)
        {
            sb.AppendLine("Target Scope Info:");
            sb.AppendLine($"  Type: {scope.GetType().Name}");
            sb.AppendLine($"  Kind: {scope.Kind}");
            sb.AppendLine($"  Identity: {DescribeIdentity(scope.Identity)}");
            sb.AppendLine($"  Visible: {scope.IsVisible}");
            sb.AppendLine($"  Active: {scope.IsActive}");
            sb.AppendLine($"  Parent: {DescribeScope(scope.Parent)}");
            sb.AppendLine($"  Resolver: {DescribeResolver(scope.Resolver)}");

            if (scope is BaseLifetimeScope baseScope)
            {
                sb.AppendLine($"  UseAsGameLogicRoot: {baseScope.UseAsGameLogicRoot}");
            }

            var path = scope.GetPathFromRoot();
            if (path != null && path.Count > 0)
            {
                sb.Append("  Path: ");
                for (int i = 0; i < path.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" > ");
                    sb.Append(DescribeScope(path[i]));
                }
                sb.AppendLine();
            }

            if (scope.Resolver != null)
            {
                var hasBlackboard = scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null;
                var hasGridBlackboard = scope.Resolver.TryResolve<IGridBlackboardService>(out var gridBlackboard) && gridBlackboard != null;
                var hasScalar = scope.Resolver.TryResolve<IBaseScalarService>(out var scalar) && scalar != null;
                var hasTelemetry = scope.Resolver.TryResolve<IScalarTelemetry>(out var telemetry) && telemetry != null;
                sb.AppendLine($"  Services: Blackboard={hasBlackboard} GridBlackboard={hasGridBlackboard} Scalar={hasScalar} ScalarTelemetry={hasTelemetry}");

                if (scope.Resolver.TryResolve<ScopeBindingRegistryService>(out var registry) && registry != null)
                {
                    sb.AppendLine($"  ProfileRegistry: Version={registry.Version} ScopeIdentity={registry.ScopeIdentity} SaveEnabled={registry.IsSaveEnabled} ProfileCount={registry.ProfileCount}");

                    var profileIndex = 0;
                    foreach (var runtime in registry.EnumerateProfiles())
                    {
                        if (runtime == null)
                            continue;

                        sb.AppendLine($"    [{profileIndex}] {runtime.ProfileType?.Name ?? "<unknown>"} Applied={runtime.IsBindingsApplied} RegisteredVersion={runtime.RegisteredVersion} SaveEntries={runtime.SaveEntries?.Count ?? 0}");

                        if (runtime.Profile != null)
                        {
                            var bindingIndex = 0;
                            var bindings = runtime.Profile.EnumerateBindings();
                            foreach (var binding in bindings)
                            {
                                if (binding == null)
                                    continue;

                                sb.AppendLine($"      - [{bindingIndex}] {DescribeProfileBinding(binding)}");
                                bindingIndex++;
                            }

                            if (bindingIndex == 0)
                                sb.AppendLine("      (no bindings)");
                        }

                        profileIndex++;
                    }

                    if (profileIndex == 0)
                        sb.AppendLine("    (no profiles)");
                }
                else
                {
                    sb.AppendLine("  ProfileRegistry: <not found>");
                }
            }
        }

        static void AppendBlackboard(StringBuilder sb, IScopeNode scope, int maxEntries)
        {
            if (scope?.Resolver == null || !scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
            {
                sb.AppendLine("  <blackboard service not found>");
                return;
            }

            var vars = blackboard.LocalVars;
            if (vars == null)
            {
                sb.AppendLine("  <local vars not found>");
                return;
            }

            var count = 0;
            foreach (var varId in vars.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...blackboard entries truncated after {maxEntries} items.");
                    break;
                }

                var kind = vars.GetVarKind(varId);
                var key = VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
                var value = GetVarValueDescription(vars, varId, kind);
                sb.AppendLine($"  varId={varId} key={key} kind={kind} value={value}");
                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no vars)");
        }

        static void AppendScalar(StringBuilder sb, IScopeNode scope, int maxKeys, bool includeSnapshots, int maxSnapshotsPerKey)
        {
            if (scope?.Resolver == null)
            {
                sb.AppendLine("  <resolver not found>");
                return;
            }

            if (!scope.Resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
            {
                sb.AppendLine("  <scalar service not found>");
                return;
            }

            if (!scope.Resolver.TryResolve<IScalarTelemetry>(out var telemetry) || telemetry == null)
            {
                sb.AppendLine("  <scalar telemetry not found>");
                return;
            }

            var count = 0;
            foreach (var key in telemetry.EnumerateKeys())
            {
                if (key.Id == 0 && string.IsNullOrWhiteSpace(key.Name))
                    continue;

                if (maxKeys > 0 && count >= maxKeys)
                {
                    sb.AppendLine($"  ...scalar keys truncated after {maxKeys} items.");
                    break;
                }

                var currentValue = scalar.LocalGet(key);
                sb.AppendLine($"  key={key.FormatLabel()} current={currentValue}");

                if (includeSnapshots)
                {
                    var snapshotCount = 0;
                    foreach (var snapshot in telemetry.Enumerate(key))
                    {
                        if (maxSnapshotsPerKey > 0 && snapshotCount >= maxSnapshotsPerKey)
                        {
                            sb.AppendLine($"    ...snapshots truncated after {maxSnapshotsPerKey} items.");
                            break;
                        }

                        sb.AppendLine($"    {DescribeScalarSnapshot(snapshot)}");
                        snapshotCount++;
                    }

                    if (snapshotCount == 0)
                        sb.AppendLine("    (no snapshots)");
                }

                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no scalar keys)");
        }

        static string DescribeScalarSnapshot(ScalarSnapshot snapshot)
        {
            var kindLabel = snapshot.Kind == ScalarModKind.Add ? "Add" : "Mul";
            var valueText = snapshot.Kind == ScalarModKind.Mul
                ? $"x{snapshot.Value:0.##}"
                : $"{(snapshot.Value >= 0 ? "+" : string.Empty)}{snapshot.Value:0.##}";
            var sourceText = snapshot.Source != null ? $" src={snapshot.Source}" : string.Empty;
            var tagText = string.IsNullOrWhiteSpace(snapshot.Tag) ? string.Empty : $" tag={snapshot.Tag}";
            var layerText = string.IsNullOrWhiteSpace(snapshot.Layer) ? string.Empty : $" layer={snapshot.Layer}";
            var remainText = snapshot.Remain < 0 ? string.Empty : $" remain={snapshot.Remain:0.##}s";
            return $"[{kindLabel}] {valueText}{sourceText}{tagText}{layerText}{remainText}";
        }

        static string DescribeIdentity(ILTSIdentityService? identity)
        {
            if (identity == null)
                return "<null>";

            var category = string.IsNullOrWhiteSpace(identity.Category) ? "(none)" : identity.Category;
            var id = string.IsNullOrWhiteSpace(identity.Id) ? "(none)" : identity.Id;
            return $"Id={id} Kind={identity.Kind} Category={category} Active={identity.IsActive}";
        }

        static string DescribeResolver(IObjectResolver? resolver)
        {
            if (resolver == null)
                return "<null>";

            return resolver.GetType().Name;
        }

        static string DescribeProfileBinding(IProfileValueBinding binding)
        {
            if (binding == null)
                return "<null>";

            var typeName = binding.GetType().Name;
            var scalarKey = binding.ScalarKey.Id != 0 ? binding.ScalarKey.FormatLabel() : "(unbound)";
            var blackboardKey = binding.BlackboardKey != 0 ? VarIdResolver.TryGetIdToStable(binding.BlackboardKey) ?? $"varId={binding.BlackboardKey}" : "(unbound)";

            if (binding is ProfileFloatValue floatBinding)
            {
                return $"{typeName} Value={floatBinding.Value} Scalar={scalarKey} Policy={floatBinding.ScalarPolicyValue} UseEffectMod={floatBinding.UseEffectMod} UseClampMod={floatBinding.UseClampMod} UseLocalBase={floatBinding.UseLocalBase} LocalBase={floatBinding.LocalBaseValue} Blackboard={blackboardKey} BlackboardPolicy={floatBinding.BlackboardPolicyValue}";
            }

            return $"{typeName} Scalar={scalarKey} ScalarPolicy={binding.ScalarPolicy} Blackboard={blackboardKey} BlackboardPolicy={binding.BlackboardPolicy} HasAnyBinding={binding.HasAnyBinding} SaveScalar={binding.ScalarSaveEnabled} SaveBlackboard={binding.BlackboardSaveEnabled}";
        }

        static string DescribeScope(IScopeNode? node)
        {
            if (node == null)
                return "<null>";

            if (node is Component component && component.gameObject != null)
                return component.gameObject.name;

            if (node.Identity != null)
                return $"{node.Identity.Id}:{node.Identity.Kind}";

            return node.GetType().Name;
        }

        static void AppendContextSlots(StringBuilder sb, CommandContext ctx)
        {
            sb.AppendLine("Context Slots:");
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextA);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextB);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextC);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextD);
        }

        static void AppendContextSlot(StringBuilder sb, CommandContext ctx, CommandLtsSlot slot)
        {
            sb.AppendLine($"  {slot}: {DescribeSlotScope(ctx.GetScope(slot))}");
        }

        static string DescribeSlotScope(IScopeNode? node)
        {
            if (node == null)
                return "<null>";

            if (node.Identity != null)
                return $"{node.Identity.Kind}:{node.Identity.Id}";

            return DescribeScope(node);
        }

        static void AppendVarStore(StringBuilder sb, IVarStore? vars, int maxEntries)
        {
            if (vars == null)
            {
                sb.AppendLine("VarStore: <null>");
                return;
            }

            sb.AppendLine($"VarStore Version={vars.GlobalVersion}");
            var count = 0;
            foreach (var varId in vars.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...var entries truncated after {maxEntries} items.");
                    break;
                }

                var kind = vars.GetVarKind(varId);
                var key = VarIdResolver.TryGetStableKey(varId, out var stableKey) ? stableKey : "<runtime>";
                var value = GetVarValueDescription(vars, varId, kind);
                sb.AppendLine($"  varId={varId} key={key} kind={kind} value={value}");
                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no vars)");

            AppendVarStoreTables(sb, vars, maxEntries, count);
        }

        static void AppendVarStoreTables(StringBuilder sb, IVarStore vars, int maxEntries, int usedEntries)
        {
            var totalEntries = usedEntries;
            var truncated = false;
            var hasTables = false;

            foreach (var tableVarId in vars.EnumerateTableVarIds())
            {
                if (tableVarId == 0)
                    continue;

                if (!hasTables)
                {
                    sb.AppendLine("VarStore Tables:");
                    hasTables = true;
                }

                var tableKey = VarIdResolver.TryGetStableKey(tableVarId, out var tableStableKey) ? tableStableKey : "<runtime>";
                if (!vars.TryGetTableRowCount(tableVarId, out var rowCount))
                {
                    sb.AppendLine($"  tableVarId={tableVarId} key={tableKey} rows=<unresolved>");
                    continue;
                }

                sb.AppendLine($"  tableVarId={tableVarId} key={tableKey} rows={rowCount}");
                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    if (!vars.TryGetTableColumnCount(tableVarId, rowIndex, out var columnCount))
                    {
                        sb.AppendLine($"    row={rowIndex} cols=<unresolved>");
                        continue;
                    }

                    sb.AppendLine($"    row={rowIndex} cols={columnCount}");
                    for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    {
                        if (!vars.TryGetTableCellStore(tableVarId, rowIndex, columnIndex, out var cellStore) || cellStore == null)
                        {
                            sb.AppendLine($"      cell[{rowIndex},{columnIndex}] <missing>");
                            continue;
                        }

                        var cellValueCount = 0;
                        foreach (var cellVarId in cellStore.EnumerateVarIds())
                        {
                            if (cellVarId == 0)
                                continue;

                            if (maxEntries > 0 && totalEntries >= maxEntries)
                            {
                                truncated = true;
                                break;
                            }

                            if (cellValueCount == 0)
                                sb.AppendLine($"      cell[{rowIndex},{columnIndex}]");

                            var kind = cellStore.GetVarKind(cellVarId);
                            var cellKey = VarIdResolver.TryGetStableKey(cellVarId, out var cellStableKey) ? cellStableKey : "<runtime>";
                            var value = GetVarValueDescription(cellStore, cellVarId, kind);
                            sb.AppendLine($"        varId={cellVarId} key={cellKey} kind={kind} value={value}");
                            cellValueCount++;
                            totalEntries++;
                        }

                        if (!truncated && cellValueCount == 0)
                            sb.AppendLine($"      cell[{rowIndex},{columnIndex}] (empty)");

                        if (truncated && cellValueCount == 0)
                            sb.AppendLine($"      cell[{rowIndex},{columnIndex}] <truncated>");

                        if (truncated)
                            break;
                    }

                    if (truncated)
                        break;
                }

                if (truncated)
                    break;
            }

            if (truncated)
                sb.AppendLine($"  ...table cell entries truncated after {maxEntries} items.");
        }

        static void AppendWatches(StringBuilder sb, System.Collections.Generic.IReadOnlyList<CommandDebugWatchEntry>? watches, CommandContext ctx, int maxEntries)
        {
            if (watches == null || watches.Count == 0)
            {
                sb.AppendLine("Watches: (none)");
                return;
            }

            if (ctx.Scope == null)
            {
                sb.AppendLine("Watches: (scope is null)");
                return;
            }

            sb.AppendLine("Watches:");
            var count = 0;
            for (int i = 0; i < watches.Count; i++)
            {
                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...watch entries truncated after {maxEntries} items.");
                    break;
                }

                var w = watches[i];
                if (w == null)
                    continue;

                var label = string.IsNullOrWhiteSpace(w.Label) ? $"watch[{i}]" : w.Label.Trim();
                var v = w.Value.Evaluate(ctx);
                var valueText = GetVariantValueDescription(v);
                if (!w.Value.HasSource)
                {
                    valueText += " [WARN: no source]";
                }
                else if (v.IsNull)
                {
                    valueText += " [WARN: null result]";
                }

                if (w.IncludeSourceInfo)
                    sb.AppendLine($"  {label}: {valueText} (Source={w.Value.SourceTypeName}, Data={w.Value.DebugData})");
                else
                    sb.AppendLine($"  {label}: {valueText}");

                count++;
            }
        }

        static void AppendGridBlackboard(StringBuilder sb, IScopeNode scope, int maxEntries)
        {
            if (scope?.Resolver == null || !scope.Resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
            {
                sb.AppendLine("  <grid blackboard service not found>");
                return;
            }

            var hasRowCount = grid.TryGetRowCount(out var rowCount);
            sb.AppendLine($"  Rows={(hasRowCount ? rowCount.ToString() : "<empty>")}");

            var cells = new List<GridBlackboardCellSnapshot>(32);
            if (!grid.TryCollectAllCells(cells) || cells.Count == 0)
            {
                sb.AppendLine("  (no grid cells)");
                return;
            }

            cells.Sort(static (a, b) =>
            {
                var rowCompare = a.Row.CompareTo(b.Row);
                if (rowCompare != 0)
                    return rowCompare;

                var columnCompare = a.Column.CompareTo(b.Column);
                if (columnCompare != 0)
                    return columnCompare;

                return a.VarId.CompareTo(b.VarId);
            });

            var count = 0;
            var currentRow = int.MinValue;
            var currentColumn = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...grid entries truncated after {maxEntries} items.");
                    break;
                }

                var cell = cells[i];
                if (cell.Row != currentRow || cell.Column != currentColumn)
                {
                    currentRow = cell.Row;
                    currentColumn = cell.Column;
                    if (grid.TryGetColumnCount(cell.Row, out var columnCount))
                        sb.AppendLine($"  Cell[{cell.Row},{cell.Column}] ColumnsInRow={columnCount}");
                    else
                        sb.AppendLine($"  Cell[{cell.Row},{cell.Column}]");
                }

                var key = VarIdResolver.TryGetIdToStable(cell.VarId) ?? $"varId={cell.VarId}";
                sb.AppendLine($"    varId={cell.VarId} key={key} value={GetVariantValueDescription(cell.Value)}");
                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no grid cells)");
        }

        static string GetVarValueDescription(IVarStore vars, int varId, ValueKind kind)
        {
            if (kind == ValueKind.ManagedRef)
            {
                if (vars.TryGetManagedRef(varId, out var managedRef) && managedRef != null)
                    return ManagedRefDebugTextFormatter.Format(managedRef);
                return "null";
            }

            if (vars.TryGetVariant(varId, out var variant))
                return GetVariantValueDescription(variant);

            return "<unknown>";
        }

        static string GetVariantValueDescription(in DynamicVariant variant)
        {
            if (variant.Kind == ValueKind.ManagedRef)
                return ManagedRefDebugTextFormatter.Format(variant.AsManagedRef);

            return variant.ToString();
        }
    }
}
