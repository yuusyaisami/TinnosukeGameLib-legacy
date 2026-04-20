#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.MaterialFx;
using Game.Spawn;
using Game.Vars.Generated;
using Game.Visual;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Background
{
    public sealed class BackgroundSystemService :
        IBackgroundSystem,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler,
        IScopeLateTickHandler
    {
        sealed class BackgroundLayerRuntime
        {
            public readonly BackgroundLayerDefinition Definition;
            public readonly Dictionary<BackgroundTileCoord, BackgroundElementHandle> Elements = new();
            public readonly HashSet<BackgroundTileCoord> PendingSpawn = new();
            public readonly HashSet<BackgroundTileCoord> PendingRemove = new();
            public Vector2 Offset;
            public Vector2 ScrollSpeed;
            public bool Paused;
            public bool Enabled = true;

            public BackgroundLayerRuntime(BackgroundLayerDefinition definition)
            {
                Definition = definition;
                Offset = definition.InitialOffset;
                ScrollSpeed = definition.ScrollSpeed;
                Paused = false;
                Enabled = true;
            }
        }

        readonly struct BackgroundSpawnRequest
        {
            public readonly int LayerIndex;
            public readonly BackgroundTileCoord Coord;

            public BackgroundSpawnRequest(int layerIndex, BackgroundTileCoord coord)
            {
                LayerIndex = layerIndex;
                Coord = coord;
            }
        }

        readonly BackgroundSystemConfig _config;
        readonly ISceneSpawnerRegistry _registry;
        readonly IScopeNode _owner;
        readonly BackgroundViewProviderService _viewProvider;
        readonly List<BackgroundLayerRuntime> _layers = new();
        readonly Queue<BackgroundSpawnRequest> _spawnQueue = new();
        readonly Queue<BackgroundSpawnRequest> _removeQueue = new();
        readonly List<BackgroundTileCoord> _removeBuffer = new();
        readonly VarStore _localVars = new();

        IBlackboardService? _blackboard;
        ICommandRunner? _runner;
        CancellationTokenSource? _cts;
        bool _active;
        bool _processing;
        bool _commandRunning;
        bool _dirty = true;
        float _elapsed;
        float _commandElapsed;

        public BackgroundSystemService(
            BackgroundSystemConfig config,
            ISceneSpawnerRegistry registry,
            IScopeNode owner)
        {
            _config = config;
            _registry = registry;
            _owner = owner;
            _viewProvider = new BackgroundViewProviderService(config);

            InitializeLayers();
        }

        public int LayerCount => _layers.Count;

        public bool TryGetLayerState(int index, out BackgroundLayerState state)
        {
            if (index < 0 || index >= _layers.Count)
            {
                state = default;
                return false;
            }

            var layer = _layers[index];
            state = new BackgroundLayerState(index, layer.Definition.Name, layer.Offset, layer.ScrollSpeed);
            return true;
        }

        public void SetLayerOffset(int index, Vector2 offset)
        {
            if (index < 0 || index >= _layers.Count)
                return;
            _layers[index].Offset = offset;
            _dirty = true;
        }

        public void AddLayerOffset(int index, Vector2 delta)
        {
            if (index < 0 || index >= _layers.Count)
                return;
            _layers[index].Offset += delta;
            _dirty = true;
        }

        public void SetLayerScrollSpeed(int index, Vector2 speed)
        {
            if (index < 0 || index >= _layers.Count)
                return;
            _layers[index].ScrollSpeed = speed;
            _dirty = true;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _active = true;
            _dirty = true;
            _elapsed = 0f;
            _commandElapsed = 0f;

            _blackboard = ResolveBlackboard(scope);
            _runner = ResolveRunner(scope);

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            ResetLayerState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _active = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _blackboard = null;
            _runner = null;

            ClearQueues();
            ReleaseAllAsync().Forget();
            ResetLayerState();
        }

        public void Tick()
        {
            if (!_config.RunInLateUpdate)
                TickInternal();
        }

        public void LateTick()
        {
            if (_config.RunInLateUpdate)
                TickInternal();
        }

        void TickInternal()
        {
            if (!_active)
                return;

            var interval = _config.UpdateIntervalSeconds;
            float delta;
            if (interval > 0f)
            {
                _elapsed += Time.deltaTime;
                if (!_dirty && _elapsed < interval)
                    return;
                delta = _elapsed;
                _elapsed = 0f;
                _dirty = false;
            }
            else
            {
                delta = Time.deltaTime;
                _dirty = false;
            }

            UpdateInternal(delta);
        }

        void UpdateInternal(float delta)
        {
            if (_layers.Count == 0)
                return;

            var viewState = _viewProvider.GetViewState();
            if (viewState.ViewSize == Vector2.zero)
                return;
            if (viewState.ViewSize == Vector2.zero)
                return;

            UpdateLayerOffsets(delta);

            var spawnBudget = Mathf.Max(0, _config.MaxSpawnPerFrame);
            var removeBudget = Mathf.Max(0, _config.MaxRemovePerFrame);

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (!layer.Enabled)
                    continue;
                UpdateLayer(i, layer, viewState, delta, ref spawnBudget, ref removeBudget);
            }

            if (!_processing && (_spawnQueue.Count > 0 || _removeQueue.Count > 0))
                ProcessQueuesAsync().Forget();

            UpdateCommandsAsync(viewState, delta).Forget();
        }

        void UpdateLayerOffsets(float delta)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Paused || !layer.Enabled)
                    continue;
                if (layer.ScrollSpeed == Vector2.zero)
                    continue;
                layer.Offset += layer.ScrollSpeed * delta;
            }
        }

        void UpdateLayer(
            int layerIndex,
            BackgroundLayerRuntime layer,
            in BackgroundViewState viewState,
            float delta,
            ref int spawnBudget,
            ref int removeBudget)
        {
            var def = layer.Definition;
            if (!def.TryResolveRuntimeTemplate(CreateDynamicContext(), out _))
                return;

            var tileSize = def.TileSize;
            if (tileSize.x <= 0f || tileSize.y <= 0f)
                return;

            var viewRect = ResolveLayerViewRect(viewState, def);
            var parallaxOffset = ResolveParallaxOffset(viewState.ViewCenter, def.Parallax);
            var layerOffset = layer.Offset;
            var tileSpaceRect = ShiftRect(viewRect, -(parallaxOffset + layerOffset));

            var moveMargin = ResolveMoveMarginTiles(tileSize, layer.ScrollSpeed, delta);
            var margin = _config.ViewMarginTiles + _config.PreloadOutsideViewTiles + def.ExtraMarginTiles + moveMargin;
            var required = CollectRequiredTiles(tileSpaceRect, tileSize, margin);

            UpdateExistingTiles(layerIndex, layer, viewState, tileSize, parallaxOffset, layerOffset, delta);

            if (spawnBudget > 0)
                EnqueueSpawns(layerIndex, layer, required, ref spawnBudget);
            if (removeBudget > 0)
                EnqueueRemovals(layerIndex, layer, required, ref removeBudget);
        }

        void UpdateExistingTiles(
            int layerIndex,
            BackgroundLayerRuntime layer,
            in BackgroundViewState viewState,
            Vector2 tileSize,
            Vector2 parallaxOffset,
            Vector2 layerOffset,
            float delta)
        {
            if (layer.Elements.Count == 0)
                return;

            var def = layer.Definition;
            var time = Time.time;
            var deltaTime = delta;

            foreach (var kv in layer.Elements)
            {
                var handle = kv.Value;
                if (handle.Transform == null)
                    continue;

                var tileRect = ResolveTileRect(kv.Key, tileSize);
                var tilePos = ResolveTilePosition(tileRect, def.SpawnPivot, parallaxOffset, layerOffset, def.ZOffset);

                ApplyTransform(handle, tilePos, def, viewState);

                var adapter = handle.Adapter;
                if (adapter != null)
                {
                    var context = new BackgroundElementContext(
                        _config.Space,
                        _config.Mode,
                        layerIndex,
                        def.Name,
                        kv.Key,
                        tileRect,
                        tileSize,
                        def.TilePadding,
                        def.Parallax,
                        layerOffset,
                        viewState.ViewRect,
                        viewState.ViewCenter,
                        tilePos,
                        def.SortingOrder,
                        def.ZOffset,
                        time,
                        deltaTime);
                    adapter.Apply(context);
                }
            }
        }

        void EnqueueSpawns(
            int layerIndex,
            BackgroundLayerRuntime layer,
            HashSet<BackgroundTileCoord> required,
            ref int spawnBudget)
        {
            if (spawnBudget <= 0)
                return;

            foreach (var coord in required)
            {
                if (spawnBudget <= 0)
                    break;

                if (layer.Elements.ContainsKey(coord) || layer.PendingSpawn.Contains(coord))
                    continue;

                layer.PendingSpawn.Add(coord);
                _spawnQueue.Enqueue(new BackgroundSpawnRequest(layerIndex, coord));
                spawnBudget--;
            }
        }

        void EnqueueRemovals(
            int layerIndex,
            BackgroundLayerRuntime layer,
            HashSet<BackgroundTileCoord> required,
            ref int removeBudget)
        {
            if (removeBudget <= 0 || layer.Elements.Count == 0)
                return;

            _removeBuffer.Clear();
            foreach (var kv in layer.Elements)
            {
                if (removeBudget <= 0)
                    break;

                var coord = kv.Key;
                if (required.Contains(coord) || layer.PendingRemove.Contains(coord))
                    continue;

                _removeBuffer.Add(coord);
                removeBudget--;
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                var coord = _removeBuffer[i];
                layer.PendingRemove.Add(coord);
                _removeQueue.Enqueue(new BackgroundSpawnRequest(layerIndex, coord));
            }
        }

        async UniTaskVoid ProcessQueuesAsync()
        {
            _processing = true;
            try
            {
                var ct = _cts != null ? _cts.Token : CancellationToken.None;

                while (_spawnQueue.Count > 0)
                {
                    var req = _spawnQueue.Dequeue();
                    await SpawnTileAsync(req.LayerIndex, req.Coord, ct);
                    if (req.LayerIndex >= 0 && req.LayerIndex < _layers.Count)
                    {
                        _layers[req.LayerIndex].PendingSpawn.Remove(req.Coord);
                    }
                }

                while (_removeQueue.Count > 0)
                {
                    var req = _removeQueue.Dequeue();
                    await RemoveTileAsync(req.LayerIndex, req.Coord, ct);
                    if (req.LayerIndex >= 0 && req.LayerIndex < _layers.Count)
                    {
                        _layers[req.LayerIndex].PendingRemove.Remove(req.Coord);
                    }
                }
            }
            finally
            {
                _processing = false;
            }
        }

        async UniTask SpawnTileAsync(int layerIndex, BackgroundTileCoord coord, CancellationToken ct)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count)
                return;

            var layer = _layers[layerIndex];
            if (layer.Elements.ContainsKey(coord))
                return;

            var def = layer.Definition;
            if (!def.TryResolveRuntimeTemplate(CreateDynamicContext(), out var runtimeTemplate) || runtimeTemplate == null)
                return;

            var spawner = _registry.TryGet<IAsyncSpawnerService>(def.SpawnerKind, def.SpawnerTag);
            if (spawner == null)
            {
                Debug.LogError($"[BackgroundSystem] Spawner not found. kind={def.SpawnerKind} tag={def.SpawnerTag}");
                return;
            }

            try
            {
                await UniTask.SwitchToMainThread();
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var viewState = _viewProvider.GetViewState();
            var tileSize = def.TileSize;
            var parallaxOffset = ResolveParallaxOffset(viewState.ViewCenter, def.Parallax);
            var tileRect = ResolveTileRect(coord, tileSize);
            var tilePos = ResolveTilePosition(tileRect, def.SpawnPivot, parallaxOffset, layer.Offset, def.ZOffset);

            var parent = def.ParentOverride != null
                ? def.ParentOverride
                : (_config.Space == BackgroundSpace.UI ? _config.UiRoot : _config.WorldRoot);

            var worldSpace = _config.Space == BackgroundSpace.World;
            var allowPooling = runtimeTemplate.UsePooling;

            var p = SpawnParams.ForRuntime(
                runtimeTemplate,
                tilePos,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: parent,
                lifetimeScopeParent: _owner,
                worldSpace: worldSpace,
                allowPooling: allowPooling);

            IRuntimeResolver? resolver = null;
            try
            {
                resolver = await spawner.SpawnAsync(p, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackgroundSystem] SpawnAsync failed: {ex.Message}");
                return;
            }

            if (resolver == null)
                return;

            ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out var runtimeScope, out var baseScope);
            var tr = root != null ? root.transform : null;
            RectTransform? rectTransform = null;
            if (tr != null)
                rectTransform = tr as RectTransform;

            IBackgroundElementAdapter? adapter = null;
            if (resolver.TryResolve<IBackgroundElementAdapter>(out var resolvedAdapter))
                adapter = resolvedAdapter;

            var handle = new BackgroundElementHandle(coord, tr, rectTransform, scopeNode, runtimeScope, baseScope, root, resolver, adapter);
            layer.Elements[coord] = handle;

            ApplyTransform(handle, tilePos, def, viewState);

            var context = new BackgroundElementContext(
                _config.Space,
                _config.Mode,
                layerIndex,
                def.Name,
                coord,
                tileRect,
                tileSize,
                def.TilePadding,
                def.Parallax,
                layer.Offset,
                viewState.ViewRect,
                viewState.ViewCenter,
                tilePos,
                def.SortingOrder,
                def.ZOffset,
                Time.time,
                Time.deltaTime);

            var vars = _blackboard != null ? _blackboard.LocalVars : _localVars;
            WriteBlackboard(viewState, layer, layerIndex, context.DeltaTime, vars);
            WriteSpawnBlackboard(context, layer, handle, vars);
            RunSpawnCommandsInBackground(layer, vars, handle.ScopeNode, ct);

            if (adapter != null)
                adapter.Initialize(context);
        }

        void RunSpawnCommandsInBackground(
            BackgroundLayerRuntime layer,
            IVarStore vars,
            IScopeNode? scopeNode,
            CancellationToken ct)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await ExecuteSpawnCommandsAsync(layer, vars, scopeNode, ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackgroundSystem] Spawn commands failed: {ex.Message}");
                }
            });
        }

        async UniTask RemoveTileAsync(int layerIndex, BackgroundTileCoord coord, CancellationToken ct)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count)
                return;

            var layer = _layers[layerIndex];
            if (!layer.Elements.TryGetValue(coord, out var handle))
                return;

            layer.Elements.Remove(coord);

            try
            {
                await ReleaseHandleAsync(handle, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackgroundSystem] Release failed: {ex.Message}");
            }
        }

        async UniTaskVoid ReleaseAllAsync()
        {
            var ct = _cts != null ? _cts.Token : CancellationToken.None;
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Elements.Count == 0)
                    continue;

                var handles = new List<BackgroundElementHandle>(layer.Elements.Values);
                layer.Elements.Clear();
                layer.PendingSpawn.Clear();
                layer.PendingRemove.Clear();

                for (int h = 0; h < handles.Count; h++)
                {
                    try
                    {
                        await ReleaseHandleAsync(handles[h], ct);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BackgroundSystem] Release failed: {ex.Message}");
                    }
                }
            }
        }

        static async UniTask ReleaseHandleAsync(BackgroundElementHandle handle, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            if (handle.RuntimeScope != null)
            {
                try
                {
                    if (handle.RuntimeScope.Resolver != null &&
                        handle.RuntimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(handle.RuntimeScope);
                        return;
                    }
                }
                catch (Exception)
                {
                }

                if (handle.Root != null)
                {
                    UnityEngine.Object.Destroy(handle.Root);
                }
                else
                {
                    UnityEngine.Object.Destroy(handle.RuntimeScope.gameObject);
                }
                return;
            }

            if (handle.BaseScope != null)
            {
                await handle.BaseScope.DespawnAsync(ct);
                return;
            }

            if (handle.Root != null)
            {
                UnityEngine.Object.Destroy(handle.Root);
            }
        }

        static void ExtractSpawnedInfo(
            IRuntimeResolver? resolver,
            out GameObject? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.gameObject;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr.gameObject;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (scopeNode == null && root != null)
            {
                var comps = root.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] is IScopeNode node)
                    {
                        scopeNode = node;
                        break;
                    }
                }
            }

            baseScope = scopeNode as BaseLifetimeScope;
        }

        void ApplyTransform(BackgroundElementHandle handle, Vector3 tilePos, BackgroundLayerDefinition def, in BackgroundViewState viewState)
        {
            if (_config.Space == BackgroundSpace.UI)
            {
                if (handle.RectTransform != null)
                {
                    handle.RectTransform.anchoredPosition = new Vector2(tilePos.x, tilePos.y);
                    var lp = handle.RectTransform.localPosition;
                    handle.RectTransform.localPosition = new Vector3(lp.x, lp.y, tilePos.z);
                }
                else if (handle.Transform != null)
                {
                    handle.Transform.localPosition = tilePos;
                }
                return;
            }

            if (handle.Transform != null)
            {
                handle.Transform.position = tilePos;
            }
        }

        Rect ResolveLayerViewRect(in BackgroundViewState viewState, BackgroundLayerDefinition def)
        {
            if (viewState.ViewSize == Vector2.zero)
                return Rect.zero;

            if (_config.Space == BackgroundSpace.UI)
                return viewState.ViewRect;

            if (_config.Mode == BackgroundMode.Fixed)
            {
                var size = viewState.ViewSize;
                return new Rect(-size.x * 0.5f, -size.y * 0.5f, size.x, size.y);
            }

            return viewState.ViewRect;
        }

        IDynamicContext CreateDynamicContext()
        {
            return new SimpleDynamicContext(_localVars, _owner);
        }

        static Vector2 ResolveParallaxOffset(Vector2 viewCenter, Vector2 parallax)
        {
            var x = viewCenter.x * (1f - parallax.x);
            var y = viewCenter.y * (1f - parallax.y);
            return new Vector2(x, y);
        }

        static Vector2Int ResolveMoveMarginTiles(Vector2 tileSize, Vector2 scrollSpeed, float delta)
        {
            var mx = tileSize.x > 0f ? Mathf.CeilToInt(Mathf.Abs(scrollSpeed.x) * delta / tileSize.x) : 0;
            var my = tileSize.y > 0f ? Mathf.CeilToInt(Mathf.Abs(scrollSpeed.y) * delta / tileSize.y) : 0;
            return new Vector2Int(mx, my);
        }

        static HashSet<BackgroundTileCoord> CollectRequiredTiles(Rect tileSpaceRect, Vector2 tileSize, Vector2Int margin)
        {
            var required = new HashSet<BackgroundTileCoord>();

            var minX = Mathf.FloorToInt(tileSpaceRect.xMin / tileSize.x) - margin.x;
            var maxX = Mathf.FloorToInt(tileSpaceRect.xMax / tileSize.x) + margin.x;
            var minY = Mathf.FloorToInt(tileSpaceRect.yMin / tileSize.y) - margin.y;
            var maxY = Mathf.FloorToInt(tileSpaceRect.yMax / tileSize.y) + margin.y;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    required.Add(new BackgroundTileCoord(x, y));
                }
            }

            return required;
        }

        static Rect ResolveTileRect(BackgroundTileCoord coord, Vector2 tileSize)
        {
            var min = new Vector2(coord.X * tileSize.x, coord.Y * tileSize.y);
            var rect = new Rect(min, tileSize);
            return rect;
        }

        static Vector3 ResolveTilePosition(Rect tileRect, BackgroundSpawnPivot pivot, Vector2 parallaxOffset, Vector2 layerOffset, float zOffset)
        {
            var anchor = pivot == BackgroundSpawnPivot.TileOrigin ? tileRect.min : tileRect.center;
            var pos = new Vector2(anchor.x, anchor.y) + parallaxOffset + layerOffset;
            return new Vector3(pos.x, pos.y, zOffset);
        }

        static Rect ShiftRect(Rect rect, Vector2 delta)
        {
            return new Rect(rect.position + delta, rect.size);
        }

        async UniTaskVoid UpdateCommandsAsync(BackgroundViewState viewState, float delta)
        {
            if (_commandRunning)
                return;

            var runner = _runner;
            if (runner == null)
                return;

            var updateList = _config.UpdateCommands;
            var conditionalList = _config.ConditionalCommands;
            if ((updateList == null || updateList.Count == 0) && (conditionalList == null || conditionalList.Count == 0))
                return;

            var interval = _config.CommandIntervalSeconds;
            _commandElapsed += delta;
            if (interval > 0f && _commandElapsed < interval)
                return;
            _commandElapsed = 0f;

            _commandRunning = true;
            try
            {
                var vars = _blackboard != null ? _blackboard.LocalVars : _localVars;
                var ctx = new CommandContext(_owner, vars, runner, actor: _owner, options: CommandRunOptions.Default);
                var dynContext = new SimpleDynamicContext(vars, _owner);
                var ct = _cts != null ? _cts.Token : CancellationToken.None;

                for (int i = 0; i < _layers.Count; i++)
                {
                    var layer = _layers[i];
                    WriteBlackboard(viewState, layer, i, delta, vars);

                    if (updateList != null && updateList.Count > 0)
                        await ExecuteCommandListAsync(updateList, ctx, ct);

                    if (conditionalList == null || conditionalList.Count == 0)
                        continue;

                    for (int c = 0; c < conditionalList.Count; c++)
                    {
                        var entry = conditionalList[c];
                        if (entry == null)
                            continue;

                        if (!entry.Condition.TryGet(dynContext, out var ok) || !ok)
                            continue;

                        await ExecuteCommandListAsync(entry.Commands, ctx, ct);
                    }
                }
            }
            finally
            {
                _commandRunning = false;
            }
        }

        static async UniTask ExecuteCommandListAsync(CommandListData? list, CommandContext context, CancellationToken ct)
        {
            if (list == null || list.Count == 0)
                return;

            try
            {
                await context.Runner.ExecuteListAsync(list, context, ct, CommandRunOptions.Default);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackgroundSystem] Command execution failed: {ex.Message}");
            }
        }

        void WriteBlackboard(
            in BackgroundViewState viewState,
            BackgroundLayerRuntime layer,
            int layerIndex,
            float deltaTime,
            IVarStore vars)
        {
            var time = Time.time;

            var def = layer.Definition;
            TrySetVar(vars, VarIds.GameLib.Background.Camera.x, DynamicVariant.FromFloat(viewState.ViewCenter.x));
            TrySetVar(vars, VarIds.GameLib.Background.Camera.y, DynamicVariant.FromFloat(viewState.ViewCenter.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.w, DynamicVariant.FromFloat(viewState.ViewSize.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.h, DynamicVariant.FromFloat(viewState.ViewSize.y));

            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.index, DynamicVariant.FromInt(layerIndex));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.name, DynamicVariant.FromString(def.Name));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Parallax.x, DynamicVariant.FromFloat(def.Parallax.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Parallax.y, DynamicVariant.FromFloat(def.Parallax.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Offset.x, DynamicVariant.FromFloat(layer.Offset.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Offset.y, DynamicVariant.FromFloat(layer.Offset.y));

            TrySetVar(vars, VarIds.GameLib.Background.Spawn.time, DynamicVariant.FromFloat(time));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.deltaTime, DynamicVariant.FromFloat(deltaTime));
        }

        void WriteSpawnBlackboard(
            in BackgroundElementContext context,
            BackgroundLayerRuntime layer,
            BackgroundElementHandle handle,
            IVarStore vars)
        {
            var def = layer.Definition;

            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.index, DynamicVariant.FromInt(context.LayerIndex));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.name, DynamicVariant.FromString(context.LayerName));

            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.x, DynamicVariant.FromInt(context.TileCoord.X));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.y, DynamicVariant.FromInt(context.TileCoord.Y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Rect.x, DynamicVariant.FromFloat(context.TileRect.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Rect.y, DynamicVariant.FromFloat(context.TileRect.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Rect.w, DynamicVariant.FromFloat(context.TileRect.width));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Rect.h, DynamicVariant.FromFloat(context.TileRect.height));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Size.x, DynamicVariant.FromFloat(context.TileSize.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Size.y, DynamicVariant.FromFloat(context.TileSize.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Padding.x, DynamicVariant.FromFloat(context.TilePadding.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Tile.Padding.y, DynamicVariant.FromFloat(context.TilePadding.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Parallax.x, DynamicVariant.FromFloat(context.Parallax.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Parallax.y, DynamicVariant.FromFloat(context.Parallax.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Offset.x, DynamicVariant.FromFloat(context.LayerOffset.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Offset.y, DynamicVariant.FromFloat(context.LayerOffset.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.x, DynamicVariant.FromFloat(context.ViewRect.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.y, DynamicVariant.FromFloat(context.ViewRect.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.w, DynamicVariant.FromFloat(context.ViewRect.width));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Rect.h, DynamicVariant.FromFloat(context.ViewRect.height));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Center.x, DynamicVariant.FromFloat(context.ViewCenter.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.View.Center.y, DynamicVariant.FromFloat(context.ViewCenter.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.World.Pos.x, DynamicVariant.FromFloat(context.WorldPosition.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.World.Pos.y, DynamicVariant.FromFloat(context.WorldPosition.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.World.Pos.z, DynamicVariant.FromFloat(context.WorldPosition.z));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Sorting.order, DynamicVariant.FromInt(context.SortingOrder));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.zOffset, DynamicVariant.FromFloat(context.ZOffset));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.time, DynamicVariant.FromFloat(context.Time));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.deltaTime, DynamicVariant.FromFloat(context.DeltaTime));

            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.TileSize.x, DynamicVariant.FromFloat(def.TileSize.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.TileSize.y, DynamicVariant.FromFloat(def.TileSize.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.ScrollSpeed.x, DynamicVariant.FromFloat(layer.ScrollSpeed.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.ScrollSpeed.y, DynamicVariant.FromFloat(layer.ScrollSpeed.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.InitialOffset.x, DynamicVariant.FromFloat(def.InitialOffset.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.InitialOffset.y, DynamicVariant.FromFloat(def.InitialOffset.y));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.spawnPivot, DynamicVariant.FromInt((int)def.SpawnPivot));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Spawner.kind, DynamicVariant.FromInt((int)def.SpawnerKind));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.Spawner.tag, DynamicVariant.FromString(def.SpawnerTag));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.ExtraMarginTiles.x, DynamicVariant.FromInt(def.ExtraMarginTiles.x));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Layer.ExtraMarginTiles.y, DynamicVariant.FromInt(def.ExtraMarginTiles.y));

            var root = handle.Root;
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Root.name, DynamicVariant.FromString(root != null ? root.name : string.Empty));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Root.instanceId, DynamicVariant.FromInt(root != null ? root.GetInstanceID() : 0));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Root.Scope.runtimeLTS, DynamicVariant.FromInt(handle.RuntimeScope != null ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Root.Scope.baseLTS, DynamicVariant.FromInt(handle.BaseScope != null ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Background.Spawn.Root.Has.adapter, DynamicVariant.FromInt(handle.Adapter != null ? 1 : 0));
        }

        async UniTask ExecuteSpawnCommandsAsync(
            BackgroundLayerRuntime layer,
            IVarStore vars,
            IScopeNode? scopeNode,
            CancellationToken ct)
        {
            var runner = _runner;
            if (runner == null)
                return;

            var commands = layer.Definition.SpawnCommands;
            var conditional = layer.Definition.SpawnConditionalCommands;
            if ((commands == null || commands.Count == 0) &&
                (conditional == null || conditional.Count == 0))
                return;

            var execScope = scopeNode ?? _owner;
            var context = new CommandContext(execScope, vars, runner, actor: execScope, options: CommandRunOptions.Default);
            var dynContext = new SimpleDynamicContext(vars, execScope);

            await ExecuteCommandListAsync(commands, context, ct);

            if (conditional == null || conditional.Count == 0)
                return;

            for (int i = 0; i < conditional.Count; i++)
            {
                var entry = conditional[i];
                if (entry == null)
                    continue;

                if (!entry.Condition.TryGet(dynContext, out var ok) || !ok)
                    continue;

                await ExecuteCommandListAsync(entry.Commands, context, ct);
            }
        }

        void TrySetVar(IVarStore vars, int varId, DynamicVariant value)
        {
            if (varId <= 0)
                return;

            if (_blackboard != null)
                _blackboard.TryLocalSetVariant(varId, value);
            else
                vars.TrySetVariant(varId, value);
        }

        static IBlackboardService? ResolveBlackboard(IScopeNode scope)
        {
            if (scope?.Resolver == null)
                return null;

            return scope.Resolver.TryResolve<IBlackboardService>(out var bb) ? bb : null;
        }

        static ICommandRunner? ResolveRunner(IScopeNode scope)
        {
            if (scope?.Resolver == null)
                return null;

            return scope.Resolver.TryResolve<ICommandRunner>(out var runner) ? runner : null;
        }

        void InitializeLayers()
        {
            _layers.Clear();
            if (_config.Layers == null)
                return;

            for (int i = 0; i < _config.Layers.Count; i++)
            {
                var def = _config.Layers[i];
                if (def == null)
                    continue;

                _layers.Add(new BackgroundLayerRuntime(def));
            }
        }

        void ResetLayerState()
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                layer.Offset = layer.Definition.InitialOffset;
                layer.ScrollSpeed = layer.Definition.ScrollSpeed;
                layer.Paused = false;
                layer.Enabled = true;
            }
        }

        void ClearQueues()
        {
            _spawnQueue.Clear();
            _removeQueue.Clear();
            for (int i = 0; i < _layers.Count; i++)
            {
                _layers[i].PendingSpawn.Clear();
                _layers[i].PendingRemove.Clear();
            }
        }

        // ─── New IBackgroundSystem methods ─────────────────────────────

        public bool TryGetLayerIndexByName(string name, out int index)
        {
            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < _layers.Count; i++)
                {
                    if (string.Equals(_layers[i].Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        public void SetLayerPaused(int index, bool paused)
        {
            if (index < 0 || index >= _layers.Count)
                return;
            _layers[index].Paused = paused;
        }

        public void SetLayerEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= _layers.Count)
                return;

            var layer = _layers[index];
            if (layer.Enabled == enabled)
                return;

            layer.Enabled = enabled;

            // 無効化時はすべての要素を非表示にする
            foreach (var kv in layer.Elements)
            {
                var root = kv.Value.Root;
                if (root != null)
                    root.SetActive(enabled);
            }

            _dirty = true;
        }

        public async UniTask ExecuteOnLayerElementsAsync(int index, CommandListData commands, IVarStore vars, CancellationToken ct)
        {
            if (index < 0 || index >= _layers.Count)
                return;
            if (commands == null || commands.Count == 0)
                return;

            var runner = _runner;
            if (runner == null)
                return;

            var layer = _layers[index];
            foreach (var kv in layer.Elements)
            {
                var handle = kv.Value;
                var execScope = handle.ScopeNode ?? _owner;
                var execRunner = ResolveRunner(execScope) ?? runner;

                var ctx = new CommandContext(execScope, vars, execRunner, actor: execScope, options: CommandRunOptions.Default);

                try
                {
                    await execRunner.ExecuteListAsync(commands, ctx, ct, CommandRunOptions.Default);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackgroundSystem] ExecuteOnLayerElements failed: {ex.Message}");
                }
            }
        }

        public void SetLayerMaterialFx(int index, VisualTargetSelector selector, IReadOnlyList<MaterialFxPresetEntry> entries, bool clearMissingKeys, int basePriority)
        {
            if (index < 0 || index >= _layers.Count)
                return;

            var layer = _layers[index];
            foreach (var kv in layer.Elements)
            {
                var handle = kv.Value;
                if (handle.Resolver == null)
                    continue;

                if (!handle.Resolver.TryResolve<IVisualSystem>(out var visual) || visual == null)
                    continue;

                visual.SetState(selector, entries, clearMissingKeys: clearMissingKeys, basePriority: basePriority);
            }
        }
    }
}
