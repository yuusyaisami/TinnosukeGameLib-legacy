#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using VContainer.Unity;

namespace Game.Chunk
{
    public sealed class ChunkStreamerService :
        IChunkStreamer,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly ChunkStreamerConfig _config;
        readonly IChunkFactory _factory;
        readonly IChunkContentPlanner _planner;
        readonly IChunkViewProvider _viewProvider;
        readonly IChunkBiomeService? _biomeService;

        readonly Dictionary<ChunkCoord, ChunkHandle> _chunks = new();
        readonly HashSet<ChunkCoord> _pendingSpawn = new();
        readonly HashSet<ChunkCoord> _pendingRemove = new();
        readonly Queue<ChunkCoord> _spawnQueue = new();
        readonly Queue<ChunkCoord> _removeQueue = new();

        CancellationTokenSource? _cts;
        float _elapsed;
        bool _active;
        bool _processing;

        public ChunkStreamerService(
            ChunkStreamerConfig config,
            IChunkFactory factory,
            IChunkContentPlanner planner,
            IChunkViewProvider viewProvider,
            IChunkBiomeService? biomeService = null)
        {
            _config = config;
            _factory = factory;
            _planner = planner;
            _viewProvider = viewProvider;
            _biomeService = biomeService;
        }

        public bool TryGetChunk(ChunkCoord coord, out ChunkHandle handle)
        {
            return _chunks.TryGetValue(coord, out handle!);
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _active = true;
            _elapsed = 0f;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _active = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_chunks.Count > 0)
            {
                var handles = new List<ChunkHandle>(_chunks.Values);
                _chunks.Clear();
                ReleaseAllAsync(handles).Forget();
            }
            else
            {
                _chunks.Clear();
            }
            _pendingSpawn.Clear();
            _pendingRemove.Clear();
            _spawnQueue.Clear();
            _removeQueue.Clear();
        }

        public void Tick()
        {
            if (!_active)
                return;

            var interval = _config.Settings.UpdateIntervalSeconds;
            if (interval > 0f)
            {
                _elapsed += Time.deltaTime;
                if (_elapsed < interval)
                    return;
                _elapsed = 0f;
            }

            var viewRect = _viewProvider.GetViewRect();
            var required = CollectRequiredChunks(viewRect);

            EnqueueSpawns(required);
            EnqueueRemovals(required, viewRect);

            if (!_processing && (_spawnQueue.Count > 0 || _removeQueue.Count > 0))
                ProcessQueuesAsync().Forget();
        }

        HashSet<ChunkCoord> CollectRequiredChunks(Rect viewRect)
        {
            var required = new HashSet<ChunkCoord>();
            var origin = _config.OriginSettings;
            var chunkSize = _config.Settings.ChunkSizeCells;
            var margin = _config.Settings.ViewMarginChunks;

            var cellMin = ChunkCoordUtility.WorldToCell(viewRect.min, origin);
            var cellMax = ChunkCoordUtility.WorldToCell(viewRect.max, origin);

            var minCoord = ChunkCoordUtility.CellToChunkCoord(cellMin, origin, chunkSize);
            var maxCoord = ChunkCoordUtility.CellToChunkCoord(cellMax, origin, chunkSize);

            var minX = Mathf.Min(minCoord.X, maxCoord.X) - margin;
            var maxX = Mathf.Max(minCoord.X, maxCoord.X) + margin;
            var minY = Mathf.Min(minCoord.Y, maxCoord.Y) - margin;
            var maxY = Mathf.Max(minCoord.Y, maxCoord.Y) + margin;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    required.Add(new ChunkCoord(x, y));
                }
            }

            return required;
        }

        void EnqueueSpawns(HashSet<ChunkCoord> required)
        {
            var limit = _config.Settings.MaxChunksPerFrame;
            var added = 0;

            foreach (var coord in required)
            {
                if (_chunks.ContainsKey(coord) || _pendingSpawn.Contains(coord))
                    continue;
                if (added >= limit)
                    break;

                _pendingSpawn.Add(coord);
                _spawnQueue.Enqueue(coord);
                added++;
            }
        }

        void EnqueueRemovals(HashSet<ChunkCoord> required, Rect viewRect)
        {
            var limit = _config.Settings.MaxChunksPerFrame;
            var removed = 0;

            foreach (var kv in _chunks)
            {
                var coord = kv.Key;
                if (required.Contains(coord) || _pendingRemove.Contains(coord))
                    continue;

                if (!ShouldRemoveChunk(kv.Value, viewRect))
                    continue;

                if (removed >= limit)
                    break;

                _pendingRemove.Add(coord);
                _removeQueue.Enqueue(coord);
                removed++;
            }
        }

        bool ShouldRemoveChunk(ChunkHandle handle, Rect viewRect)
        {
            var bounds = handle.WorldBounds;
            var boundsRect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            if (boundsRect.Overlaps(viewRect))
                return false;

            var removeDistance = _config.Settings.RemoveDistance;
            if (removeDistance <= 0f)
                return true;

            if (_config.TargetTransform == null)
                return true;

            var targetPos = _viewProvider.GetTargetPosition();
            var center = boundsRect.center;
            var sqr = (targetPos - center).sqrMagnitude;
            return sqr >= removeDistance * removeDistance;
        }

        async UniTaskVoid ProcessQueuesAsync()
        {
            _processing = true;
            try
            {
                var ct = _cts != null ? _cts.Token : CancellationToken.None;

                while (_spawnQueue.Count > 0)
                {
                    var coord = _spawnQueue.Dequeue();
                    await SpawnChunkAsync(coord, ct);
                    _pendingSpawn.Remove(coord);
                }

                while (_removeQueue.Count > 0)
                {
                    var coord = _removeQueue.Dequeue();
                    await RemoveChunkAsync(coord, ct);
                    _pendingRemove.Remove(coord);
                }
            }
            finally
            {
                _processing = false;
            }
        }

        async UniTask SpawnChunkAsync(ChunkCoord coord, CancellationToken ct)
        {
            if (_chunks.ContainsKey(coord))
                return;

            var cellRect = ChunkCoordUtility.CalcChunkCellRect(coord, _config.OriginSettings, _config.Settings.ChunkSizeCells);
            var bounds = ChunkCoordUtility.CalcChunkWorldBounds(cellRect, _config.OriginSettings);
            var context = new ChunkContext(coord, cellRect, bounds, bounds.center, _config.Settings, _config.OriginSettings);

            var plan = _planner.BuildPlan(context);
            var handle = await _factory.SpawnAsync(context, plan, ct);
            if (handle == null)
                return;

            _chunks[coord] = handle;
        }

        async UniTask RemoveChunkAsync(ChunkCoord coord, CancellationToken ct)
        {
            if (!_chunks.TryGetValue(coord, out var handle))
                return;

            _chunks.Remove(coord);
            _biomeService?.Forget(coord);

            await _factory.ReleaseAsync(handle, ct);
        }

        async UniTaskVoid ReleaseAllAsync(List<ChunkHandle> handles)
        {
            var ct = _cts != null ? _cts.Token : CancellationToken.None;
            for (int i = 0; i < handles.Count; i++)
            {
                await _factory.ReleaseAsync(handles[i], ct);
            }
        }
    }
}
