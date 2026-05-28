#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class SpawnRuntimeGridExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SpawnRuntimeGrid;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SpawnRuntimeGridCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SpawnRuntimeGridCommandData is required.");

            if (!typed.Template.TryGet(ctx, out var templatePreset) || templatePreset == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Template preset is null.");

            var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(templatePreset);
            if (runtimeTemplate == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Template preset could not resolve runtime template.");

            if (!SceneKernelSpawnBindingHub.TryResolveSpawnRouteHandler(typed.SpawnerKind, typed.SpawnerTag, out var routeHandler) || routeHandler == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"SceneKernel spawn route handler not found. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");

            IScopeNode? lifetimeScopeParent = null;
            if (typed.OverrideLifetimeScopeParent)
            {
                var (parentResolved, error) = await ActorScopeResolver.ResolveAsync(typed.LifetimeScopeParent, ctx, ct);
                if (parentResolved == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"LifetimeScopeParent resolve failed: {error}");

                lifetimeScopeParent = parentResolved;
            }

            Transform? transformParent = null;
            if (typed.TransformParentPolicy == SpawnTransformParentPolicy.UseTransform)
            {
                transformParent = typed.TransformParent;
            }
            else if (typed.TransformParentPolicy == SpawnTransformParentPolicy.ActorSource)
            {
                transformParent = await ResolveTransformParentFromActorSourceAsync(typed.TransformParentActorSource, ctx, ct);
            }

            var dynamicVars = ctx.Vars ?? NullVarStore.Instance;
            CommandResolveContext CreateDynamicContext() => new(
                ctx.Scope!,
                dynamicVars,
                ctx.CommandRootScope,
                ctx.Resolver,
                NullCommandCatalog.Instance,
                NullCommandKeyResolver.Instance,
                NullCommandResolveLogger.Instance,
                allowRuntimeKeyFallback: ctx.Options.AllowRuntimeKeyFallback,
                runtimeContext: ctx);

            var dynCtxForGrid = CreateDynamicContext();
            var targetCount = Mathf.Max(1, typed.Count.Resolve(dynCtxForGrid));
            var columns = Mathf.Max(1, typed.Columns.Resolve(dynCtxForGrid));
            var configuredRows = Mathf.Max(0, typed.Rows.Resolve(dynCtxForGrid));
            var rows = configuredRows > 0 ? configuredRows : Mathf.Max(1, Mathf.CeilToInt(targetCount / (float)columns));
            var spacing = typed.Spacing.Resolve(dynCtxForGrid);
            var itemSize = ResolveItemSize(runtimeTemplate, typed, dynCtxForGrid);

            var onSpawnedCommon = typed.OnSpawnedCommonCommands;
            var runCommon = typed.RunCommandsOnSpawned && onSpawnedCommon != null && onSpawnedCommon.Count > 0;
            var runConditional = typed.RunCommandsOnSpawned && typed.RunConditionalCommands;
            var counterVarId = ResolveVarId(typed.CounterVar, VarIds.GameLib.Base.CommandVar.i);

            var gridLinkEnabled = typed.EnabledGridBlackboardLink;
            var gridLinkScope = gridLinkEnabled
                ? ActorSourceFastResolver.Resolve(ctx, typed.GridBlackboardActorSource, ctx.Actor ?? ctx.Scope) ?? (ctx.Actor ?? ctx.Scope)
                : null;
            var gridLink = TryResolveGridBlackboard(gridLinkScope, out var gridBlackboard, out var resolvedGridScope) ? gridBlackboard : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[SpawnRuntimeGridExecutor] Start GridLinkEnabled={gridLinkEnabled} RequestedScope={DescribeScope(gridLinkScope)} ResolvedScope={DescribeScope(resolvedGridScope)} LinkFound={(gridLink != null)} Count={targetCount} Cols={columns} Rows={rows}");
#endif
            if (gridLinkEnabled && gridLink == null)
            {
                Debug.LogError($"[SpawnRuntimeGridExecutor] GridBlackboard link is enabled but no IGridBlackboardService was found. RequestedScope={DescribeScope(gridLinkScope)} SpawnerTag={typed.SpawnerTag}");
            }
            else if (gridLinkEnabled && resolvedGridScope != null && !ReferenceEquals(resolvedGridScope, gridLinkScope))
            {
                Debug.LogWarning($"[SpawnRuntimeGridExecutor] GridBlackboardService resolved from parent scope. RequestedScope={DescribeScope(gridLinkScope)} ResolvedScope={DescribeScope(resolvedGridScope)}");
            }
            var gridSpawnedScopeVarId = typed.WriteSpawnedScopeRefToGrid ? ResolveVarId(typed.GridSpawnedScopeVar, 0) : 0;
            var gridLinkVarIds = ResolveGridLinkVarIds(typed.GridLinkVarKeys);
            var explicitGridKeyVarId = typed.UseGridKeyFilter ? ResolveVarId(typed.GridKey, 0) : 0;
            if (typed.UseGridKeyFilter && explicitGridKeyVarId <= 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Grid key filter is enabled but GridKey is not configured.");

            var gridLinkBaseRow = 0;
            var inferredGridKeyVarId = 0;
            if (TryInferGridLinkHint(typed, dynCtxForGrid, out var inferredRow, out var inferredFilterVarId))
            {
                gridLinkBaseRow = Mathf.Max(0, inferredRow);
                inferredGridKeyVarId = inferredFilterVarId;
            }

            var gridLinkFilterVarId = explicitGridKeyVarId > 0
                ? explicitGridKeyVarId
                : inferredGridKeyVarId;

            var linkedCellValues = new List<GridBlackboardCellSnapshot>(16);

            for (int i = 0; i < targetCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                dynamicVars.TrySetVariant(counterVarId, DynamicVariant.FromInt(i));
                var dynCtx = CreateDynamicContext();

                var grid = ResolveGridCoord(i, columns, rows, typed.FillOrder);
                var gridOffset = ResolveGridOffset(grid.x, grid.y, columns, rows, itemSize, spacing, typed.HorizontalAlign, typed.VerticalAlign, typed.HorizontalDirection, typed.VerticalDirection);
                var basePos = typed.Position.Resolve(dynCtx);
                var offset = typed.Offset.Resolve(dynCtx);
                var spawnPos = basePos + offset + new Vector3(gridOffset.x, gridOffset.y, 0f);

                var rotation = Quaternion.Euler(typed.RotationEuler.Resolve(dynCtx));
                var scale = typed.Scale.HasSource ? typed.Scale.Resolve(dynCtx) : Vector3.one;

                var spawnParams = SpawnParams.ForRuntime(
                    runtimeTemplate,
                    spawnPos,
                    rotation,
                    scale,
                    identity: null,
                    transformParent: transformParent,
                    lifetimeScopeParent: lifetimeScopeParent,
                    worldSpace: typed.WorldSpace,
                    allowPooling: typed.AllowPooling);

                object? spawnedObject = await routeHandler.SpawnAsync(spawnParams, ct);
                if (spawnedObject != null && spawnedObject is not IRuntimeResolver)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"SceneKernel spawn route returned an unexpected result type. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");

                var spawnedResolver = spawnedObject as IRuntimeResolver;
                if (spawnedResolver == null)
                    continue;

                if (!spawnedResolver.TryResolve<IScopeNode>(out var spawnedScope) || spawnedScope == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Spawned container does not expose IScopeNode.");

                EnsureScopeBuiltIfNeeded(spawnedScope);

                if (gridLink != null)
                {
                    var rowOffset = typed.GridLinkRowOffset.Resolve(dynCtx);
                    var columnOffset = typed.GridLinkColumnOffset.Resolve(dynCtx);
                    var linkRow = Mathf.Max(0, gridLinkBaseRow + grid.y + rowOffset);
                    var linkColumn = Mathf.Max(0, grid.x + columnOffset);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[SpawnRuntimeGridExecutor] LinkCell spawnIndex={i} base=({grid.y},{grid.x}) offset=({rowOffset},{columnOffset}) targetCell=({linkRow},{linkColumn})");
#endif

                    WriteGridLink(
                        gridLink,
                        dynamicVars,
                        linkRow,
                        linkColumn,
                        typed.WriteSpawnedScopeRefToGrid,
                        gridSpawnedScopeVarId,
                        spawnedScope,
                        gridLinkVarIds);

                    CollectGridCellValues(gridLink, linkRow, linkColumn, gridLinkFilterVarId, linkedCellValues);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[SpawnRuntimeGridExecutor] CollectedCellValues spawnIndex={i} cell=({linkRow},{linkColumn}) count={linkedCellValues.Count}");
                    for (int v = 0; v < linkedCellValues.Count; v++)
                    {
                        var c = linkedCellValues[v];
                        //Debug.Log($"[SpawnRuntimeGridExecutor]   CellValue[{v}] varId={c.VarId} key={VarIdResolver.TryGetIdToStable(c.VarId) ?? "(none)"} kind={c.Value.Kind} value={c.Value}");
                    }
#endif
                    if (!ApplyLinkedCellValuesToSpawnedBlackboard(spawnedScope, linkedCellValues, overwrite: true))
                    {
                        throw new CommandExecutionException(
                            CommandRunFailureKind.ResolveFailed,
                            $"Spawned scope blackboard write failed. spawned={DescribeScope(spawnedScope)} row={linkRow} col={linkColumn} count={linkedCellValues.Count}");
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[SpawnRuntimeGridExecutor] BlackboardWriteSuccess spawnIndex={i} spawned={DescribeScope(spawnedScope)} count={linkedCellValues.Count}");
#endif
                }
                else
                {
                    linkedCellValues.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (gridLinkEnabled)
                        Debug.LogWarning($"[SpawnRuntimeGridExecutor] GridLinkEnabled but link service is null at spawnIndex={i}. RequestedScope={DescribeScope(gridLinkScope)}");
#endif
                }

                if (typed.WriteSpawnedScopeToContext)
                {
                    if (!CommandLtsSlotUtility.IsContextSlot(typed.SpawnedScopeSlot))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"SpawnedScopeSlot must be a context slot. slot={typed.SpawnedScopeSlot}");

                    ctx.SetScope(typed.SpawnedScopeSlot, spawnedScope);
                }

                if (!typed.RunCommandsOnSpawned)
                {
                    await DelayIfNeeded(typed, dynCtx, i, targetCount, ct);
                    continue;
                }

                if (!TryResolveRunner(spawnedScope, out var runner) || runner == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "Spawned scope has no ICommandRunner.");

                var vars = ResolveVars(typed.VarsPolicy, ctx, spawnedScope);
                vars = MergeLinkedCellValuesIntoCommandVars(vars, linkedCellValues);
                vars.TrySetVariant(counterVarId, DynamicVariant.FromInt(i));

                var spawnedOptions = ctx.Options.WithSuppressCancelLog(true);
                var spawnedCtx = new CommandContext(
                    spawnedScope,
                    vars,
                    runner,
                    actor: spawnedScope,
                    options: spawnedOptions,
                    commandRootScope: ctx.CommandRootScope,
                    rootActor: ctx.RootActor,
                    callerActor: ctx.Actor,
                    sourceContext: ctx);

                if (typed.WriteSpawnerToContext)
                {
                    if (!CommandLtsSlotUtility.IsContextSlot(typed.SpawnerContextSlot))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"SpawnerContextSlot must be a context slot. slot={typed.SpawnerContextSlot}");

                    var spawnerScope = ctx.Actor ?? ctx.Scope;
                    spawnedCtx.SetScope(typed.SpawnerContextSlot, spawnerScope);
                }

                if (typed.AwaitOnSpawnedCommands)
                {
                    if (runCommon)
                    {
                        if (await RunListOrBreak(runner, onSpawnedCommon!, spawnedCtx, ct, "OnSpawned Common"))
                            break;
                    }

                    if (runConditional)
                    {
                        var cond = typed.SpawnCondition.EvaluateBool(spawnedCtx);
                        var selected = cond ? typed.OnSpawnedWhenTrueCommands : typed.OnSpawnedWhenFalseCommands;
                        if (selected != null && selected.Count > 0)
                        {
                            if (await RunListOrBreak(runner, selected, spawnedCtx, ct, cond ? "OnSpawned True" : "OnSpawned False"))
                                break;
                        }
                    }
                }
                else
                {
                    RunOnSpawnedInBackground(runner, onSpawnedCommon, typed, spawnedCtx, ct, typed.SpawnerTag);
                }

                await DelayIfNeeded(typed, dynCtx, i, targetCount, ct);
            }
        }

        static async UniTask DelayIfNeeded(SpawnRuntimeGridCommandData typed, CommandResolveContext dynCtx, int index, int count, CancellationToken ct)
        {
            if (!typed.DelayBetweenSpawns.HasSource || index >= count - 1)
                return;

            var delay = typed.DelayBetweenSpawns.Resolve(dynCtx);
            var delaySeconds = Mathf.Max(0f, delay);
            if (delaySeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
        }

        static (int x, int y) ResolveGridCoord(int index, int columns, int rows, SpawnGridFillOrder fillOrder)
        {
            if (fillOrder == SpawnGridFillOrder.ColumnMajor)
            {
                var y = index % rows;
                var x = index / rows;
                return (x, y);
            }

            return (index % columns, index / columns);
        }

        static Vector2 ResolveGridOffset(
            int x,
            int y,
            int columns,
            int rows,
            Vector2 itemSize,
            Vector2 spacing,
            SpawnGridHorizontalAlign horizontalAlign,
            SpawnGridVerticalAlign verticalAlign,
            SpawnGridAxisDirection horizontalDirection,
            SpawnGridAxisDirection verticalDirection)
        {
            var stepX = Mathf.Max(0f, itemSize.x) + spacing.x;
            var stepY = Mathf.Max(0f, itemSize.y) + spacing.y;

            var totalWidth = columns > 0 ? Mathf.Max(0f, columns - 1) * stepX : 0f;
            var totalHeight = rows > 0 ? Mathf.Max(0f, rows - 1) * stepY : 0f;

            var alignX = horizontalAlign switch
            {
                SpawnGridHorizontalAlign.Left => 0f,
                SpawnGridHorizontalAlign.Center => -totalWidth * 0.5f,
                SpawnGridHorizontalAlign.Right => -totalWidth,
                _ => 0f,
            };

            var alignY = verticalAlign switch
            {
                SpawnGridVerticalAlign.Top => 0f,
                SpawnGridVerticalAlign.Center => totalHeight * 0.5f,
                SpawnGridVerticalAlign.Bottom => totalHeight,
                _ => 0f,
            };

            var dirX = horizontalDirection == SpawnGridAxisDirection.Positive ? 1f : -1f;
            var dirY = verticalDirection == SpawnGridAxisDirection.Positive ? 1f : -1f;

            var localX = alignX + (x * stepX * dirX);
            var localY = alignY + (y * stepY * dirY);
            return new Vector2(localX, localY);
        }

        static async UniTask<bool> RunListOrBreak(ICommandRunner runner, CommandListData list, CommandContext ctx, CancellationToken ct, string phase)
        {
            var result = await runner.ExecuteListAsync(list, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Break)
                return true;
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
            {
                var msg = $"{phase} command list failed. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                throw new CommandExecutionException(result.FailureKind, msg);
            }

            return false;
        }

        static Vector2 ResolveItemSize(BaseRuntimeTemplateSO runtimeTemplate, SpawnRuntimeGridCommandData typed, CommandResolveContext dynCtx)
        {
            var prefab = runtimeTemplate.Prefab;
            if (prefab == null)
                return typed.FallbackItemSize.Resolve(dynCtx);

            if (TryGetVisualBoundsSize(prefab.transform, out var visualSize))
                return visualSize;

            if (TryGetRectSize(prefab.transform, out var rectSize))
                return rectSize;

            if (TryGetColliderSize(prefab.transform, out var colliderSize))
                return colliderSize;

            var fallback = typed.FallbackItemSize.Resolve(dynCtx);
            Debug.LogWarning($"[SpawnRuntimeGridExecutor] Could not resolve item size from VisualBounds/RectTransform/Collider2D. Using fallback={fallback} Template={runtimeTemplate.name}");
            return fallback;
        }

        static bool TryGetVisualBoundsSize(Transform root, out Vector2 size)
        {
            size = Vector2.zero;
            if (root == null)
                return false;

            var output = root.GetComponentInChildren<IVisualBoundsOutput>(true);
            if (output == null || !output.HasBounds)
                return false;

            var local = output.LocalSize;
            if (local.x > 0f && local.y > 0f)
            {
                size = local;
                return true;
            }

            var world = output.WorldSize;
            if (world.x > 0f && world.y > 0f)
            {
                size = new Vector2(world.x, world.y);
                return true;
            }

            return false;
        }

        static bool TryGetRectSize(Transform root, out Vector2 size)
        {
            size = Vector2.zero;
            if (root == null)
                return false;

            var rect = root.GetComponentInChildren<RectTransform>(true);
            if (rect == null)
                return false;

            var rectSize = rect.rect.size;
            if (rectSize.x <= 0f || rectSize.y <= 0f)
                return false;

            size = rectSize;
            return true;
        }

        static bool TryGetColliderSize(Transform root, out Vector2 size)
        {
            size = Vector2.zero;
            if (root == null)
                return false;

            var col = root.GetComponentInChildren<Collider2D>(true);
            if (col == null)
                return false;

            var bounds = col.bounds.size;
            if (bounds.x <= 0f || bounds.y <= 0f)
                return false;

            size = new Vector2(bounds.x, bounds.y);
            return true;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
            }
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
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

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            if (scope.Identity != null)
                return $"{scope.Identity.Id}:{scope.Identity.Kind}";

            return scope.GetType().Name;
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode targetScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = targetScope?.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return ctx.Vars ?? NullVarStore.Instance;
        }

        static int ResolveVarId(VarKeyRef key, int fallback)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;
            return key.VarId > 0 ? key.VarId : fallback;
        }

        static List<int> ResolveGridLinkVarIds(List<VarKeyRef>? keys)
        {
            var result = new List<int>();
            if (keys == null || keys.Count == 0)
                return result;

            for (int i = 0; i < keys.Count; i++)
            {
                var id = ResolveVarId(keys[i], 0);
                if (id <= 0 || result.Contains(id))
                    continue;

                result.Add(id);
            }

            return result;
        }

        static bool TryInferGridLinkHint(
            SpawnRuntimeGridCommandData typed,
            IDynamicContext context,
            out int baseRow,
            out int filterVarId)
        {
            baseRow = 0;
            filterVarId = 0;

            if (typed == null || context == null)
                return false;

            if (TryReadGridLinkHint(typed.Columns, context, out baseRow, out filterVarId))
                return true;

            if (TryReadGridLinkHint(typed.Count, context, out baseRow, out filterVarId))
                return true;

            return false;
        }

        static bool TryReadGridLinkHint(
            DynamicValue<int> source,
            IDynamicContext context,
            out int baseRow,
            out int filterVarId)
        {
            baseRow = 0;
            filterVarId = 0;

            if (source.TryGetSource<OtherGridBlackboardColumnCountSource>(out var other) &&
                other != null &&
                other.TryGetGridLinkHint(context, out baseRow, out filterVarId))
            {
                return true;
            }

            if (source.TryGetSource<SelfGridBlackboardColumnCountSource>(out var self) &&
                self != null &&
                self.TryGetGridLinkHint(context, out baseRow, out filterVarId))
            {
                return true;
            }

            return false;
        }

        static void WriteGridLink(
            IGridBlackboardService grid,
            IVarStore vars,
            int row,
            int column,
            bool writeSpawnedScopeRef,
            int spawnedScopeVarId,
            IScopeNode spawnedScope,
            List<int> linkVarIds)
        {
            if (grid == null || row < 0 || column < 0)
                return;

            if (writeSpawnedScopeRef && spawnedScopeVarId > 0)
            {
                var scopeValue = DynamicVariant.FromManagedRef(spawnedScope);
                if (!grid.SetOrExpandVariant(spawnedScopeVarId, row, column, in scopeValue))
                {
                    Debug.LogError($"[SpawnRuntimeGridExecutor] Failed to write spawned scope ref to grid. row={row} col={column} varId={spawnedScopeVarId}");
                }
            }

            if (vars == null || linkVarIds == null || linkVarIds.Count == 0)
                return;

            for (int i = 0; i < linkVarIds.Count; i++)
            {
                var varId = linkVarIds[i];
                if (varId == 0)
                    continue;

                if (!TryReadVarAsVariant(vars, varId, out var value))
                {
                    Debug.LogError($"[SpawnRuntimeGridExecutor] Grid link source var not found in command vars. row={row} col={column} varId={varId} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"}");
                    continue;
                }

                if (!grid.SetOrExpandVariant(varId, row, column, in value))
                {
                    Debug.LogError($"[SpawnRuntimeGridExecutor] Failed to write linked var to grid. row={row} col={column} varId={varId} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"}");
                }
            }
        }

        static bool ApplyLinkedCellValuesToSpawnedBlackboard(
            IScopeNode spawnedScope,
            List<GridBlackboardCellSnapshot> values,
            bool overwrite)
        {
            if (spawnedScope?.Resolver == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SpawnRuntimeGridExecutor] Spawned scope resolver is null while writing linked cell values.");
#endif
                return false;
            }

            if (!spawnedScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard?.LocalVars == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SpawnRuntimeGridExecutor] Spawned blackboard not found. spawned={DescribeScope(spawnedScope)}");
#endif
                return false;
            }

            if (values == null)
                return false;

            if (values.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SpawnRuntimeGridExecutor] No linked values to write. spawned={DescribeScope(spawnedScope)}");
#endif
                return true;
            }

            var local = blackboard.LocalVars;
            for (int i = 0; i < values.Count; i++)
            {
                var cell = values[i];

                var varId = cell.VarId;
                if (varId == 0)
                    continue;

                if (!overwrite && local.Contains(varId))
                    continue;

                if (cell.Value.Kind == ValueKind.ManagedRef)
                {
                    var managed = cell.Value.AsManagedRef;
                    if (managed == null)
                        continue;

                    if (!local.TrySetManagedRef(varId, managed))
                    {
                        Debug.LogError($"[SpawnRuntimeGridExecutor] Failed to write managed ref to spawned blackboard. varId={varId} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"}");
                        return false;
                    }

                    //#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //                    Debug.Log($"[SpawnRuntimeGridExecutor] Wrote managed ref to spawned blackboard. varId={varId} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"} type={managed.GetType().Name}");
                    //#endif

                    continue;
                }

                if (!local.TrySetVariant(varId, cell.Value))
                {
                    Debug.LogError($"[SpawnRuntimeGridExecutor] Failed to write variant to spawned blackboard. varId={varId} kind={cell.Value.Kind} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"}");
                    return false;
                }

                //#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //                Debug.Log($"[SpawnRuntimeGridExecutor] Wrote variant to spawned blackboard. varId={varId} key={VarIdResolver.TryGetIdToStable(varId) ?? "(none)"} kind={cell.Value.Kind} value={cell.Value}");
                //#endif
            }

            return true;
        }

        static void CollectGridCellValues(
            IGridBlackboardService grid,
            int row,
            int column,
            int gridKeyVarId,
            List<GridBlackboardCellSnapshot> destination)
        {
            destination?.Clear();
            if (grid == null || destination == null)
                return;

            if (!grid.TryCollectCell(row, column, destination) || destination.Count == 0)
                return;

            if (gridKeyVarId <= 0)
                return;

            var hasGridKey = false;
            for (int i = 0; i < destination.Count; i++)
            {
                var cell = destination[i];
                if (cell.VarId != gridKeyVarId || cell.Value.Kind == ValueKind.Null)
                    continue;

                if (!cell.Value.TryGet<bool>(out var enabled) || enabled)
                {
                    hasGridKey = true;
                    break;
                }
            }

            if (!hasGridKey)
                destination.Clear();
        }

        static IVarStore MergeLinkedCellValuesIntoCommandVars(IVarStore baseVars, List<GridBlackboardCellSnapshot> linkedCellValues)
        {
            if (linkedCellValues == null || linkedCellValues.Count == 0)
                return baseVars ?? NullVarStore.Instance;

            var merged = new VarStore();
            (baseVars ?? NullVarStore.Instance).MergeInto(merged, overwrite: true);

            for (int i = 0; i < linkedCellValues.Count; i++)
            {
                var cell = linkedCellValues[i];
                if (cell.VarId == 0)
                    continue;

                if (cell.Value.Kind == ValueKind.ManagedRef)
                {
                    var managed = cell.Value.AsManagedRef;
                    if (managed != null)
                        merged.TrySetManagedRef(cell.VarId, managed);
                    continue;
                }

                merged.TrySetVariant(cell.VarId, cell.Value);
            }

            return merged;
        }

        static bool TryReadVarAsVariant(IVarStore vars, int varId, out DynamicVariant value)
        {
            value = DynamicVariant.Null;
            if (vars == null || varId == 0)
                return false;

            if (vars.GetVarKind(varId) == ValueKind.ManagedRef)
            {
                if (!vars.TryGetManagedRef(varId, out var managed) || managed == null)
                    return false;

                value = DynamicVariant.FromManagedRef(managed);
                return true;
            }

            return vars.TryGetVariant(varId, out value);
        }

        static async UniTask<Transform?> ResolveTransformParentFromActorSourceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Transform parent actor resolve failed: {error}");

            var transform = GetTransformFromScope(actorScope);
            if (transform == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Resolved actor scope does not expose a Transform.");

            return transform;
        }

        static Transform? GetTransformFromScope(IScopeNode scope)
        {
            var id = scope?.Identity;
            if (id != null && id.SelfTransform != null)
                return id.SelfTransform;

            return null;
        }

        static void RunOnSpawnedInBackground(
            ICommandRunner runner,
            CommandListData? common,
            SpawnRuntimeGridCommandData typed,
            CommandContext ctx,
            CancellationToken ct,
            string spawnerTag)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    var options = ctx.Options.WithSuppressCancelLog(true);

                    if (common != null && common.Count > 0)
                    {
                        var commonResult = await runner.ExecuteListAsync(common, ctx, ct, options);
                        if (commonResult.Status == CommandRunStatus.Error || commonResult.FailureCount > 0)
                        {
                            Debug.LogError($"[SpawnRuntimeGridExecutor] OnSpawned common command list failed (background). SpawnerTag={spawnerTag} FailureCount={commonResult.FailureCount} ErrorIndex={commonResult.ErrorIndex} Message={commonResult.Message}");
                            return;
                        }
                    }

                    if (!typed.RunConditionalCommands)
                        return;

                    var condition = typed.SpawnCondition.EvaluateBool(ctx);
                    var selected = condition ? typed.OnSpawnedWhenTrueCommands : typed.OnSpawnedWhenFalseCommands;
                    if (selected == null || selected.Count == 0)
                        return;

                    var condResult = await runner.ExecuteListAsync(selected, ctx, ct, options);
                    if (condResult.Status == CommandRunStatus.Error || condResult.FailureCount > 0)
                    {
                        Debug.LogError($"[SpawnRuntimeGridExecutor] OnSpawned conditional command list failed (background). SpawnerTag={spawnerTag} FailureCount={condResult.FailureCount} ErrorIndex={condResult.ErrorIndex} Message={condResult.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception($"[SpawnRuntimeGridExecutor] OnSpawned command list exception (background). SpawnerTag={spawnerTag}", ex));
                }
            });
        }
    }
}
