#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.RoomMap
{
    public interface IRoomMapVisualizer
    {
        UniTask ApplyAsync(RoomMapInstance instance, RoomMapProfileSO profile, RoomMapTileVisualSO visual, ICommandRunner runner, float? delayOverrideSeconds, CancellationToken ct);
    }

    public sealed class RoomMapVisualizer : IRoomMapVisualizer
    {
        public async UniTask ApplyAsync(RoomMapInstance instance, RoomMapProfileSO profile, RoomMapTileVisualSO visual, ICommandRunner runner, float? delayOverrideSeconds, CancellationToken ct)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));

            await UniTask.SwitchToMainThread();

            var roomCenter = CalculateRoomCenter(instance);

            var delayPerCell = Mathf.Max(0f, delayOverrideSeconds ?? profile.DelayPerCellSeconds);

            // Static pass: cell grid
            await ApplyStaticAsync(instance, profile, visual, runner, roomCenter, delayPerCell, ct);

            // Dynamic pass: additional spawned entries
            var dynamicVisual = ReferenceEquals(visual, profile.Visual) ? profile.DynamicVisualOrFallback : visual;
            if (instance.DynamicCount > 0)
                await ApplyDynamicAsync(instance, profile, dynamicVisual, runner, roomCenter, delayPerCell, ct);
        }

        static async UniTask ApplyStaticAsync(RoomMapInstance instance, RoomMapProfileSO profile, RoomMapTileVisualSO visual, ICommandRunner runner, Vector3 roomCenter, float delayPerCell, CancellationToken ct)
        {
            var order = profile.VisualOrder;
            var delay = delayPerCell;
            var policy = profile.FailurePolicy;

            var width = instance.MaxWidth;
            var height = instance.MaxHeight;
            var layerCount = instance.LayerCount;
            var commonCommand = visual.CommonCommand;

            var tileRegistry = RoomMapTileRegistryLocator.GetOrCreate();

            var yieldEvery = 128;
            var processed = 0;
            float totalTime = Time.realtimeSinceStartup;

            for (int i = 0; i < width * height; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (x, y) = IndexToCoord(i, width, height, order);
                var hasAnyCell = false;

                for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                {
                    if (!instance.TryGet(layerIndex, x, y, out var cell))
                        continue;

                    if (cell.TileId == 0)
                        continue;

                    if (!visual.TryGetTileVisual(cell.TileId, out var baseVisual) || baseVisual == null)
                        continue;

                    var resolved = ResolveTileVisual(instance, visual, tileRegistry, baseVisual, cell.TileId, x, y, layerIndex);

                    var scope = cell.ScopeNode;
                    if (scope == null || scope.Resolver == null)
                    {
                        if (policy == RoomMapFailurePolicy.FailFast)
                            throw new InvalidOperationException($"Cell scope missing for tileId={cell.TileId} at ({x},{y}).");
                        continue;
                    }

                    if (scope.Resolver.TryResolve<IBlackboardService>(out var bbSvc) && bbSvc != null)
                    {
                        bbSvc.TryGlobalSetVariant(VarIds.GameLib.RoomMap.roomCenterPos, DynamicVariant.FromVector3(roomCenter));
                    }

                    var vars = new VarStore(initialCapacity: 7);
                    vars.TrySetVariant(VarIds.GameLib.RoomMap.tileId, DynamicVariant.FromInt(cell.TileId));
                    vars.TrySetVariant(VarIds.GameLib.RoomMap.x, DynamicVariant.FromInt(x));
                    vars.TrySetVariant(VarIds.GameLib.RoomMap.y, DynamicVariant.FromInt(y));
                    vars.TrySetVariant(VarIds.GameLib.RoomMap.worldPos, DynamicVariant.FromVector3(cell.WorldPos));
                    vars.TrySetVariant(VarIds.GameLib.RoomMap.roomCenterPos, DynamicVariant.FromVector3(roomCenter));

                    if (resolved.Preset != null)
                        vars.TrySetManagedRef(VarIds.GameLib.RoomMap.animPreset, resolved.Preset);

                    var ctx = new CommandContext(scope, vars, runner, actor: scope, options: CommandRunOptions.Default);

                    try
                    {
                        if (commonCommand != null && commonCommand.Count > 0)
                            await runner.ExecuteListAsync(commonCommand, ctx, ct, CommandRunOptions.Default);

                        if (resolved.FirstCommand != null && resolved.FirstCommand.Count > 0)
                            await runner.ExecuteListAsync(resolved.FirstCommand, ctx, ct, CommandRunOptions.Default);

                        if (resolved.SecondCommand != null && resolved.SecondCommand.Count > 0)
                            await runner.ExecuteListAsync(resolved.SecondCommand, ctx, ct, CommandRunOptions.Default);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        if (policy == RoomMapFailurePolicy.FailFast)
                            throw;
                    }

                    hasAnyCell = true;
                }

                if (hasAnyCell && delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true, cancellationToken: ct);

                if (hasAnyCell)
                    processed++;
                if (processed > 0 && processed % yieldEvery == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            float elapsedTime = Time.realtimeSinceStartup - totalTime;
            float averageTimePerCell = processed > 0 ? elapsedTime / processed : 0f;
            float theoreticalWaitTime = delay * processed;
            _ = averageTimePerCell;
            _ = theoreticalWaitTime;
        }

        static async UniTask ApplyDynamicAsync(RoomMapInstance instance, RoomMapProfileSO profile, RoomMapTileVisualSO visual, ICommandRunner runner, Vector3 roomCenter, float delayPerCell, CancellationToken ct)
        {

            var delay = delayPerCell;
            var policy = profile.FailurePolicy;
            var commonCommand = visual.CommonCommand;

            var yieldEvery = 64;
            var processed = 0;

            var list = instance.DynamicRecords;
            for (int i = 0; i < list.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = list[i];
                if (entry.TileId == 0)
                    continue;

                if (!visual.TryGetTileVisual(entry.TileId, out var baseVisual) || baseVisual == null)
                    continue;

                // Dynamic: no AutoTile in v2.
                var resolved = ResolvedTileVisual.FromBase(baseVisual);

                var scope = entry.ScopeNode;
                if (scope == null || scope.Resolver == null)
                {
                    if (policy == RoomMapFailurePolicy.FailFast)
                        throw new InvalidOperationException($"Dynamic entry scope missing for tileId={entry.TileId} at cell=({entry.Cell.x},{entry.Cell.y}).");
                    continue;
                }

                var vars = new VarStore(initialCapacity: 7);
                vars.TrySetVariant(VarIds.GameLib.RoomMap.tileId, DynamicVariant.FromInt(entry.TileId));
                vars.TrySetVariant(VarIds.GameLib.RoomMap.x, DynamicVariant.FromInt(entry.Cell.x));
                vars.TrySetVariant(VarIds.GameLib.RoomMap.y, DynamicVariant.FromInt(entry.Cell.y));
                vars.TrySetVariant(VarIds.GameLib.RoomMap.worldPos, DynamicVariant.FromVector3(entry.WorldPos));
                vars.TrySetVariant(VarIds.GameLib.RoomMap.roomCenterPos, DynamicVariant.FromVector3(roomCenter));

                if (resolved.Preset != null)
                    vars.TrySetManagedRef(VarIds.GameLib.RoomMap.animPreset, resolved.Preset);

                var ctx = new CommandContext(scope, vars, runner, actor: scope, options: CommandRunOptions.Default);

                try
                {
                    if (commonCommand != null && commonCommand.Count > 0)
                        await runner.ExecuteListAsync(commonCommand, ctx, ct, CommandRunOptions.Default);

                    if (resolved.FirstCommand != null && resolved.FirstCommand.Count > 0)
                        await runner.ExecuteListAsync(resolved.FirstCommand, ctx, ct, CommandRunOptions.Default);

                    if (resolved.SecondCommand != null && resolved.SecondCommand.Count > 0)
                        await runner.ExecuteListAsync(resolved.SecondCommand, ctx, ct, CommandRunOptions.Default);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    if (policy == RoomMapFailurePolicy.FailFast)
                        throw;
                }

                if (delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true, cancellationToken: ct);

                processed++;
                if (processed % yieldEvery == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }


        }

        static Vector3 CalculateRoomCenter(RoomMapInstance instance)
        {
            if (instance == null)
                return Vector3.zero;

            var sum = Vector3.zero;
            var count = 0;

            for (int layerIndex = 0; layerIndex < instance.LayerCount; layerIndex++)
            {
                var cells = instance.GetRawCellsUnsafe(layerIndex);
                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    if (cell.TileId == 0)
                        continue;

                    sum += cell.WorldPos;
                    count++;
                }
            }

            var dynamic = instance.DynamicRecords;
            for (int i = 0; i < dynamic.Count; i++)
            {
                var entry = dynamic[i];
                if (entry.TileId == 0)
                    continue;

                sum += entry.WorldPos;
                count++;
            }

            return count == 0 ? Vector3.zero : sum / count;
        }

        static (int x, int y) IndexToCoord(int i, int width, int height, RoomMapVisualOrder order)
        {
            // Use (x,y) generation based on the chosen order.
            switch (order)
            {
                case RoomMapVisualOrder.RowMajor_TopLeft:
                    {
                        var y = i / width;
                        var x = i - y * width;
                        return (x, y);
                    }
                case RoomMapVisualOrder.RowMajor_TopRight:
                    {
                        var y = i / width;
                        var x = i - y * width;
                        return (width - 1 - x, y);
                    }
                case RoomMapVisualOrder.RowMajor_BottomLeft:
                    {
                        var y = i / width;
                        var x = i - y * width;
                        return (x, height - 1 - y);
                    }
                case RoomMapVisualOrder.RowMajor_BottomRight:
                    {
                        var y = i / width;
                        var x = i - y * width;
                        return (width - 1 - x, height - 1 - y);
                    }
                case RoomMapVisualOrder.Diagonal_TopLeft:
                    {
                        // Enumerate diagonals by sum s=x+y
                        return DiagonalIndexToCoord(i, width, height, topLeft: true);
                    }
                case RoomMapVisualOrder.Diagonal_BottomLeft:
                    {
                        // Enumerate diagonals by sum s=(height-1-y)+x
                        return DiagonalIndexToCoord(i, width, height, topLeft: false);
                    }
                default:
                    {
                        var y = i / width;
                        var x = i - y * width;
                        return (x, y);
                    }
            }
        }

        static (int x, int y) DiagonalIndexToCoord(int i, int width, int height, bool topLeft)
        {
            // Convert linear i into diagonal traversal.
            // This is O(n) per lookup if naive; implement incremental mapping.
            // Since v0.1 sizes are small (<=4096 cells) this is acceptable.
            var count = 0;
            if (topLeft)
            {
                for (int s = 0; s <= (width - 1) + (height - 1); s++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var x = s - y;
                        if (x < 0 || x >= width)
                            continue;

                        if (count == i)
                            return (x, y);
                        count++;
                    }
                }
            }
            else
            {
                for (int s = 0; s <= (width - 1) + (height - 1); s++)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        var x = s - (height - 1 - y);
                        if (x < 0 || x >= width)
                            continue;

                        if (count == i)
                            return (x, y);
                        count++;
                    }
                }
            }

            var fallbackY = i / width;
            var fallbackX = i - fallbackY * width;
            return (fallbackX, fallbackY);
        }

        readonly struct ResolvedTileVisual
        {
            public readonly Game.Channel.AnimationSpritePreset? Preset;
            public readonly CommandListData? FirstCommand;
            public readonly CommandListData? SecondCommand;

            public ResolvedTileVisual(Game.Channel.AnimationSpritePreset? preset, CommandListData? firstCommand, CommandListData? secondCommand)
            {
                Preset = preset;
                FirstCommand = firstCommand;
                SecondCommand = secondCommand;
            }

            public static ResolvedTileVisual FromBase(RoomTileVisualData baseVisual)
            {
                var preset = baseVisual != null && baseVisual.OverridePreset ? baseVisual.DefaultPreset : null;
                return new ResolvedTileVisual(preset, baseVisual?.TileCommand, null);
            }
        }

        static ResolvedTileVisual ResolveTileVisual(
            RoomMapInstance instance,
            RoomMapTileVisualSO visual,
            RoomMapTileRegistry tileRegistry,
            RoomTileVisualData baseVisual,
            int centerTileId,
            int x,
            int y,
            int layerIndex)
        {
            if (baseVisual == null)
                return default;

            var resolvedPreset = baseVisual.OverridePreset ? baseVisual.DefaultPreset : null;
            CommandListData? first = baseVisual.TileCommand;
            CommandListData? second = null;

            var ruleSet = visual.AutoTileRuleSet;
            if (ruleSet != null)
            {
                if (RoomMapAutoTileEvaluator.TryFindBestRule(ruleSet, instance, tileRegistry, layerIndex, x, y, centerTileId, out var winner) && winner != null)
                {
                    var ov = winner.ResultOverride;
                    if (ov != null)
                    {
                        if (ov.OverridePreset)
                            resolvedPreset = ov.DefaultPreset;

                        if (ov.OverrideCommand)
                        {
                            if (ov.AppendCommand)
                            {
                                // base then override
                                first = baseVisual.TileCommand;
                                second = ov.TileCommand;
                            }
                            else
                            {
                                first = ov.TileCommand;
                                second = null;
                            }
                        }
                    }
                }
            }

            return new ResolvedTileVisual(resolvedPreset, first, second);
        }
    }
}
