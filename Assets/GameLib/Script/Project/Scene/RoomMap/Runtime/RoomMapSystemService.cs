#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.RoomMap
{
    public sealed class RoomMapSystemService :
        IRoomMapSystemService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IRoomMapBuilder _builder;
        readonly IRoomMapVisualizer _visualizer;
        readonly ICommandRunner _runner;
        readonly IRoomMapSystemOptions _options;

        readonly SemaphoreSlim _mutex = new(1, 1);

        CancellationTokenSource? _buildCts;
        RoomMapInstance? _instance;
        RoomMapProfileSO? _lastProfile;

        public bool HasMap => _instance != null;

        public RoomMapSystemService(
            IScopeNode owner,
            IRoomMapBuilder builder,
            IRoomMapVisualizer visualizer,
            IRoomMapSystemOptions options,
            ICommandRunner runner)
        {
            _owner = owner;
            _builder = builder;
            _visualizer = visualizer;
            _runner = runner;
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // no-op (v0.1)
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            CancelBuild();
            // Avoid async work here; scope may be tearing down.
            _instance = null;
            _lastProfile = null;
        }

        public async UniTask BuildAsync(RoomMapProfileSO profile, IScopeNode? lifetimeScopeParent = null, float? visualDelayOverrideSeconds = null, CancellationToken ct = default)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            await _mutex.WaitAsync(ct);
            try
            {
                if (_instance != null && _lastProfile == profile)
                {
                    if (_instance.HasLiveObjects())
                        return;
                }

                var ownerScope = _owner;
                CancelBuild();

                _buildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var buildCt = _buildCts.Token;

                ValidateProfileFailFast(profile);

                await ClearInternalAsync();

                // onBegin (owner scope)
                await ExecuteHookAsync(profile.OnBegin, ownerScope, buildCt);

                try
                {
                    var spawnScope = lifetimeScopeParent ?? ownerScope;
                    _instance = await _builder.BuildAsync(profile, _options.RoomMapParentTransform, spawnScope, buildCt);
                    _lastProfile = profile;

                    await _visualizer.ApplyAsync(_instance, profile, profile.Visual, _runner, visualDelayOverrideSeconds, buildCt);

                    await ExecuteHookAsync(profile.OnCompleted, ownerScope, buildCt);
                }
                catch (OperationCanceledException)
                {
                    var externalCanceled = ct.IsCancellationRequested;
                    var internalCanceled = _buildCts != null && _buildCts.IsCancellationRequested;
                    var ownerId = _owner?.Identity?.Id ?? "(no id)";
                    Debug.LogError($"[RoomMapSystemService] BuildAsync canceled. profile={profile.name} owner={ownerId} externalCanceled={externalCanceled} internalCanceled={internalCanceled}");
                    await ClearInternalAsync();
                    throw;
                }
                catch (Exception ex)
                {
                    LogRoomMapException("BuildAsync", ex);
                    if (profile.FailurePolicy == RoomMapFailurePolicy.FailFast)
                        await ClearInternalAsync();
                    throw;
                }
                finally
                {
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask ApplyVisualAsync(RoomMapTileVisualSO visual, CancellationToken ct = default)
        {
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));

            await _mutex.WaitAsync(ct);
            try
            {
                if (_instance == null)
                    return;

                var profile = _lastProfile;
                if (profile == null)
                    throw new InvalidOperationException("No last profile for current map.");

                await _visualizer.ApplyAsync(_instance, profile, visual, _runner, null, ct);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask ClearAsync(CancellationToken ct = default)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                await ClearInternalAsync();
                _lastProfile = null;
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask RemoveRectAsync(RectInt rect, CancellationToken ct = default)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                if (_instance == null)
                    return;

                await UniTask.SwitchToMainThread();

                // Do actual release with async loop to keep try/catch per cell.
                for (int layerIndex = 0; layerIndex < _instance.LayerCount; layerIndex++)
                {
                    var layerWidth = _instance.GetLayerWidth(layerIndex);
                    var layerHeight = _instance.GetLayerHeight(layerIndex);

                    var xMin = Mathf.Clamp(rect.xMin, 0, layerWidth);
                    var xMax = Mathf.Clamp(rect.xMax, 0, layerWidth);
                    var yMin = Mathf.Clamp(rect.yMin, 0, layerHeight);
                    var yMax = Mathf.Clamp(rect.yMax, 0, layerHeight);

                    for (int y = yMin; y < yMax; y++)
                    {
                        for (int x = xMin; x < xMax; x++)
                        {
                            ct.ThrowIfCancellationRequested();

                            if (!_instance.TryGet(layerIndex, x, y, out var cell))
                                continue;

                            await ReleaseCellSafeAsync(cell);
                            _instance.Set(layerIndex, x, y, cell.AsEmpty());
                        }
                    }
                }

                // Dynamic entries: remove those whose Cell falls inside rect.
                for (int i = _instance.DynamicCount - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();

                    var entry = _instance.DynamicRecords[i];
                    var c = entry.Cell;
                    if (c.x < rect.xMin || c.x >= rect.xMax || c.y < rect.yMin || c.y >= rect.yMax)
                        continue;

                    await ReleaseDynamicSafeAsync(entry);
                    _instance.RemoveDynamicAtSwapBack(i);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        void CancelBuild()
        {
            if (_buildCts == null)
                return;

            try
            {
                _buildCts.Cancel();
            }
            catch (Exception ex)
            {
                LogRoomMapException("CancelBuild", ex);
            }

            _buildCts.Dispose();
            _buildCts = null;
        }

        static void ValidateProfileFailFast(RoomMapProfileSO profile)
        {
            var layout = profile.Layout;
            var def = profile.Definition;
            var visual = profile.Visual;

            if (layout == null || def == null || visual == null)
                throw new InvalidOperationException("RoomMapProfileSO refs are null.");

            if (layout.LayerCount <= 0)
                throw new InvalidOperationException("RoomMapLayoutSO has no layers.");

            for (int layerIndex = 0; layerIndex < layout.LayerCount; layerIndex++)
            {
                if (!layout.TryGetLayer(layerIndex, out var layer) || layer == null)
                    throw new InvalidOperationException($"RoomMapLayoutSO layer missing at index {layerIndex}.");

                var cells = layer.CellsUnsafe;
                var width = layer.Width;
                var height = layer.Height;

                var expectedCellsLen = Math.Max(1, width * height);
                if (cells == null || cells.Length != expectedCellsLen)
                    throw new InvalidOperationException($"Layout cells length mismatch. layer={layerIndex} cells.Length={cells?.Length ?? 0}, expected={expectedCellsLen}");

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var idx = y * width + x;
                        var tileId = idx >= 0 && idx < cells.Length ? cells[idx] : 0;
                        if (tileId < 0)
                            throw new InvalidOperationException($"Invalid tileId={tileId} at ({x},{y}) layer={layerIndex}.");
                    }
                }
            }
        }

        async UniTask ExecuteHookAsync(CommandListData list, IScopeNode scope, CancellationToken ct)
        {
            if (list == null || list.Count == 0)
                return;

            var ctx = new CommandContext(scope, NullVarStore.Instance, _runner, actor: scope, options: CommandRunOptions.Default);
            await _runner.ExecuteListAsync(list, ctx, ct, CommandRunOptions.Default);
        }

        async UniTask ClearInternalAsync()
        {
            // Ignore cancellation and finish cleanup.
            var inst = _instance;
            _instance = null;

            if (inst == null)
                return;

            for (int layerIndex = 0; layerIndex < inst.LayerCount; layerIndex++)
            {
                var cells = inst.GetRawCellsUnsafe(layerIndex);
                for (int i = 0; i < cells.Length; i++)
                {
                    await ReleaseCellSafeAsync(cells[i]);
                    cells[i] = cells[i].AsEmpty();
                }
            }

            var dyn = inst.DynamicRecords;
            for (int i = 0; i < dyn.Count; i++)
                await ReleaseDynamicSafeAsync(dyn[i]);

            inst.ClearAllToEmpty();
        }

        static async UniTask ReleaseCellSafeAsync(RoomMapInstance.CellRecord cell)
        {
            if (cell.Resolver == null)
                return;

            try
            {
                await cell.Lifetime.ReleaseAsync(CancellationToken.None, ex => LogRoomMapException("ReleaseCellSafeAsync - pool release", ex));
            }
            catch (Exception ex)
            {
                LogRoomMapException("ReleaseCellSafeAsync", ex);
            }
        }

        static async UniTask ReleaseDynamicSafeAsync(RoomMapInstance.DynamicRecord cell)
        {
            if (cell.Resolver == null)
                return;

            try
            {
                await cell.Lifetime.ReleaseAsync(CancellationToken.None, ex => LogRoomMapException("ReleaseDynamicSafeAsync - pool release", ex));
            }
            catch (Exception ex)
            {
                LogRoomMapException("ReleaseDynamicSafeAsync", ex);
            }
        }

        static void LogRoomMapException(string context, Exception ex)
        {
            if (ex == null)
                return;

            Debug.LogWarning($"[RoomMapSystemService] {context}: {ex.Message}");
            Debug.LogException(ex);
        }
    }
}
