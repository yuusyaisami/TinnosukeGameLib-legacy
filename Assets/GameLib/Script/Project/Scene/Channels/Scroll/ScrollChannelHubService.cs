#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.TransformSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    public sealed class ScrollChannelHubService :
        IScrollChannelHubService,
        ITickable,
        ILateTickable,
        ITickPhase,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        const string LogPrefix = "[ScrollChannelHub]";

        readonly Dictionary<string, ScrollChannelDefinition> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, ScrollChannelRuntimePlayer> _playersByTag = new(StringComparer.Ordinal);
        readonly List<ScrollChannelRuntimePlayer> _players = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();

        readonly Camera? _worldCamera;
        readonly bool _useCameraView;
        readonly Vector2 _manualViewSize;
        readonly Vector2Int _viewMarginTiles;
        readonly bool _runInLateUpdate;
        readonly bool _forceTickInRuntime;

        IScopeNode? _owner;
        IVarStore _vars = NullVarStore.Instance;
        ISceneSpawnerRegistry? _registry;
        ICommandRunner? _runner;
        CancellationTokenSource? _cts;

        bool _active;
        bool _defsDirty = true;
        bool _processing;

        public TickPhase Phase => _runInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var def in _defsByTag.Values)
                        _defsSnapshot.Add(def);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public ScrollChannelHubService(
            ScrollChannelDefinition[] definitions,
            Camera? worldCamera,
            bool useCameraView,
            Vector2 manualViewSize,
            Vector2Int viewMarginTiles,
            bool runInLateUpdate,
            bool forceTickInRuntime)
        {
            _worldCamera = worldCamera;
            _useCameraView = useCameraView;
            _manualViewSize = manualViewSize;
            _viewMarginTiles = new Vector2Int(Mathf.Max(0, viewMarginTiles.x), Mathf.Max(0, viewMarginTiles.y));
            _runInLateUpdate = runInLateUpdate;
            _forceTickInRuntime = forceTickInRuntime;

            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                RegisterChannelInternal(definitions[i], overwrite: false);
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_defsByTag.TryGetValue(tag, out var hit) && hit != null)
            {
                def = hit;
                return true;
            }

            def = null!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not ScrollChannelDefinition typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (!_defsByTag.Remove(tag))
                return false;

            if (_playersByTag.TryGetValue(tag, out var player) && player != null)
            {
                _players.Remove(player);
                _ = ReleasePlayerAsync(player, CancellationToken.None);
            }

            _playersByTag.Remove(tag);
            _defsDirty = true;
            return true;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _owner = scope;
            _vars = ResolveVars(scope);
            _registry = ResolveRegistry(scope);
            _runner = ResolveRunner(scope);

            _active = true;
            _processing = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                player.Offset = Vector2.zero;
                player.LoggedMissingTemplate = false;
                player.LoggedMissingSpawner = false;
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _active = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            var ct = CancellationToken.None;
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                _ = ReleasePlayerAsync(player, ct);
            }
        }

        public void Tick()
        {
            if (_runInLateUpdate && !_forceTickInRuntime)
                return;

            TickInternal();
        }

        public void LateTick()
        {
            if (_forceTickInRuntime)
                return;
            if (!_runInLateUpdate)
                return;

            TickInternal();
        }

        void TickInternal()
        {
            if (!_active)
                return;

            if (!TryGetViewRect(out var viewRect))
                return;

            var owner = _owner;
            if (owner == null)
                return;

            var dt = Time.deltaTime;
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                var def = player.Definition;
                if (!def.Enabled)
                    continue;

                player.Offset += def.ScrollSpeed * dt;
                var dynCtx = new SimpleDynamicContext(_vars, owner);
                var origin = def.Origin.GetOrDefault(dynCtx, Vector3.zero);
                UpdatePlayerTiles(player, viewRect, origin);
            }

            if (!_processing)
            {
                var ct = _cts != null ? _cts.Token : CancellationToken.None;
                _processing = true;
                UniTask.Void(async () => await ProcessQueuesAsync(ct));
            }
        }

        void UpdatePlayerTiles(ScrollChannelRuntimePlayer player, Rect viewRect, Vector3 origin)
        {
            var def = player.Definition;
            var tileSize = def.TileSize;
            if (tileSize.x <= 0f || tileSize.y <= 0f)
                return;

            var dir = ResolveScrollDirection(def.ScrollSpeed);
            var step = ResolveTileStep(tileSize, dir);
            var scrollDistance = ResolveScrollDistance(player.Offset, dir);
            CollectRequiredTiles(player.RequiredTiles, viewRect, def, origin, dir, step, scrollDistance, _viewMarginTiles);

            foreach (var kv in player.Tiles)
            {
                var pos = ResolveTilePosition(player, kv.Key, origin, dir, step, scrollDistance);
                ApplyTransform(kv.Value, pos);
            }

            foreach (var coord in player.RequiredTiles)
            {
                if (player.Tiles.ContainsKey(coord) || player.PendingSpawn.Contains(coord))
                    continue;
                player.PendingSpawn.Add(coord);
            }

            player.RemoveBuffer.Clear();
            foreach (var kv in player.Tiles)
            {
                if (player.RequiredTiles.Contains(kv.Key))
                    continue;
                if (player.PendingRemove.Contains(kv.Key))
                    continue;
                player.RemoveBuffer.Add(kv.Key);
            }

            for (int i = 0; i < player.RemoveBuffer.Count; i++)
                player.PendingRemove.Add(player.RemoveBuffer[i]);
        }

        async UniTask ProcessQueuesAsync(CancellationToken ct)
        {
            try
            {
                for (int p = 0; p < _players.Count; p++)
                {
                    var player = _players[p];
                    if (!player.Definition.Enabled)
                        continue;

                    if (player.PendingRemove.Count > 0)
                    {
                        var removeList = new List<ScrollTileCoord>(player.PendingRemove);
                        player.PendingRemove.Clear();
                        for (int i = 0; i < removeList.Count; i++)
                            await RemoveTileAsync(player, removeList[i], ct);
                    }

                    if (player.PendingSpawn.Count > 0)
                    {
                        var spawnList = new List<ScrollTileCoord>(player.PendingSpawn);
                        player.PendingSpawn.Clear();
                        for (int i = 0; i < spawnList.Count; i++)
                            await SpawnTileAsync(player, spawnList[i], ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _processing = false;
            }
        }

        async UniTask SpawnTileAsync(ScrollChannelRuntimePlayer player, ScrollTileCoord coord, CancellationToken ct)
        {
            if (!_active)
                return;

            if (player.Tiles.ContainsKey(coord))
                return;

            var owner = _owner;
            if (owner == null)
                return;

            var dynCtx = new SimpleDynamicContext(_vars, owner);
            if (!player.Definition.RuntimeTemplatePreset.TryGet(dynCtx, out var preset) || preset == null)
            {
                if (!player.LoggedMissingTemplate)
                {
                    player.LoggedMissingTemplate = true;
                    Debug.LogError($"{LogPrefix} RuntimeTemplatePreset is invalid. Tag={player.Definition.Tag}");
                }
                return;
            }

            var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            if (runtimeTemplate == null)
            {
                if (!player.LoggedMissingTemplate)
                {
                    player.LoggedMissingTemplate = true;
                    Debug.LogError($"{LogPrefix} RuntimeTemplate resolve failed. Tag={player.Definition.Tag}");
                }
                return;
            }

            player.LoggedMissingTemplate = false;

            if (!TryResolveSpawner(player.Definition, out var spawner))
            {
                if (!player.LoggedMissingSpawner)
                {
                    player.LoggedMissingSpawner = true;
                    Debug.LogError($"{LogPrefix} Spawner not found. Tag={player.Definition.Tag}");
                }
                return;
            }

            player.LoggedMissingSpawner = false;

            var origin = player.Definition.Origin.GetOrDefault(dynCtx, Vector3.zero);
            var dir = ResolveScrollDirection(player.Definition.ScrollSpeed);
            var step = ResolveTileStep(player.Definition.TileSize, dir);
            var scrollDistance = ResolveScrollDistance(player.Offset, dir);
            var position = ResolveTilePosition(player, coord, origin, dir, step, scrollDistance);
            var spawnParams = SpawnParams.ForRuntime(
                runtimeTemplate,
                position,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: player.Definition.TransformParent,
                lifetimeScopeParent: null,
                worldSpace: true,
                allowPooling: player.Definition.AllowPooling);

            IObjectResolver? resolver = null;
            try
            {
                resolver = await spawner.SpawnAsync(spawnParams, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Spawn failed: {ex.Message}");
                Debug.LogException(ex);
                return;
            }

            if (resolver == null)
                return;

            ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out var runtimeScope, out var baseScope);
            var tr = root != null ? root.transform : null;
            RectTransform? rectTransform = null;
            if (tr != null)
                rectTransform = tr as RectTransform;

            var handle = new ScrollTileHandle(coord, tr, rectTransform, scopeNode, runtimeScope, baseScope, root, resolver);
            player.Tiles[coord] = handle;

            UniTask.Void(async () => await RunOnSpawnedCommandsAsync(player, handle, ct));
        }

        async UniTask RemoveTileAsync(ScrollChannelRuntimePlayer player, ScrollTileCoord coord, CancellationToken ct)
        {
            if (!player.Tiles.TryGetValue(coord, out var handle) || handle == null)
                return;

            player.Tiles.Remove(coord);

            try
            {
                await ReleaseHandleAsync(handle, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Remove tile failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        async UniTask ReleasePlayerAsync(ScrollChannelRuntimePlayer player, CancellationToken ct)
        {
            var handles = new List<ScrollTileHandle>(player.Tiles.Values);
            player.Tiles.Clear();
            player.PendingSpawn.Clear();
            player.PendingRemove.Clear();
            player.RequiredTiles.Clear();

            for (int i = 0; i < handles.Count; i++)
            {
                try
                {
                    await ReleaseHandleAsync(handles[i], ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogPrefix} Release tile failed: {ex.Message}");
                    Debug.LogException(ex);
                }
            }
        }

        async UniTask RunOnSpawnedCommandsAsync(ScrollChannelRuntimePlayer player, ScrollTileHandle handle, CancellationToken ct)
        {
            var commands = player.Definition.OnSpawnedCommands;
            if (commands == null || commands.Count == 0)
                return;

            var scope = handle.ScopeNode;
            if (scope == null)
                return;

            EnsureScopeBuiltIfNeeded(scope);

            var runner = _runner;
            if (runner == null)
                return;

            var vars = ResolveVars(player.Definition.VarsPolicy, _vars, scope);
            var options = CommandRunOptions.Default.WithSuppressCancelLog(true);
            var context = new CommandContext(scope, vars, runner, actor: scope, options);

            try
            {
                _ = await runner.ExecuteListAsync(commands, context, ct, options);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} OnSpawned command failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        bool RegisterChannelInternal(ScrollChannelDefinition? def, bool overwrite)
        {
            if (def == null)
                return false;

            if (string.IsNullOrWhiteSpace(def.Tag))
                return false;

            if (_defsByTag.ContainsKey(def.Tag))
            {
                if (!overwrite)
                    return false;

                _defsByTag.Remove(def.Tag);
                if (_playersByTag.TryGetValue(def.Tag, out var oldPlayer) && oldPlayer != null)
                {
                    _players.Remove(oldPlayer);
                    _ = ReleasePlayerAsync(oldPlayer, CancellationToken.None);
                }
                _playersByTag.Remove(def.Tag);
            }

            _defsByTag[def.Tag] = def;
            var player = new ScrollChannelRuntimePlayer(def);
            _playersByTag[def.Tag] = player;
            _players.Add(player);
            _defsDirty = true;
            return true;
        }

        bool TryResolveSpawner(ScrollChannelDefinition def, out IAsyncSpawnerService spawner)
        {
            spawner = null!;

            if (def.SpawnerKind != SpawnerKind.RuntimeEntity && def.SpawnerKind != SpawnerKind.RuntimeUIElement)
            {
                Debug.LogError($"{LogPrefix} SpawnerKind must be RuntimeEntity or RuntimeUIElement. Current={def.SpawnerKind}");
                return false;
            }

            var registry = _registry ?? ResolveRegistry(_owner);
            if (registry == null)
                return false;

            _registry = registry;

            var allowTagFallback = string.IsNullOrEmpty(def.SpawnerTag);
            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                registry,
                def.SpawnerKind,
                def.SpawnerTag,
                allowTagFallback,
                allowRuntimeUiFallback: true);

            if (resolved.Spawner == null)
                return false;

            spawner = resolved.Spawner;
            return true;
        }

        bool TryGetViewRect(out Rect viewRect)
        {
            var cam = _worldCamera != null ? _worldCamera : Camera.main;
            if (_useCameraView && cam != null && cam.orthographic)
            {
                var height = cam.orthographicSize * 2f;
                var width = height * cam.aspect;
                var center = (Vector2)cam.transform.position;
                viewRect = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
                return true;
            }

            var centerPos = _owner?.Identity?.SelfTransform != null
                ? (Vector2)_owner.Identity.SelfTransform.position
                : Vector2.zero;

            var size = _manualViewSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                viewRect = default;
                return false;
            }

            viewRect = new Rect(centerPos.x - size.x * 0.5f, centerPos.y - size.y * 0.5f, size.x, size.y);
            return true;
        }

        Vector3 ResolveTilePosition(ScrollChannelRuntimePlayer player, ScrollTileCoord coord)
        {
            var def = player.Definition;
            var dir = ResolveScrollDirection(def.ScrollSpeed);
            var step = ResolveTileStep(def.TileSize, dir);
            var scrollDistance = ResolveScrollDistance(player.Offset, dir);
            var owner = _owner;
            var origin = owner != null
                ? def.Origin.GetOrDefault(new SimpleDynamicContext(_vars, owner), Vector3.zero)
                : Vector3.zero;
            return ResolveTilePosition(player, coord, origin, dir, step, scrollDistance);
        }

        static Vector2 ResolveScrollDirection(Vector2 speed)
        {
            if (speed.sqrMagnitude <= 0.000001f)
                return Vector2.right;

            return speed.normalized;
        }

        static float ResolveTileStep(Vector2 tileSize, Vector2 dir)
        {
            var step = Mathf.Abs(dir.x) * Mathf.Max(0.0001f, tileSize.x) + Mathf.Abs(dir.y) * Mathf.Max(0.0001f, tileSize.y);
            return Mathf.Max(0.0001f, step);
        }

        static float ResolveScrollDistance(Vector2 offset, Vector2 dir)
            => Vector2.Dot(offset, dir);

        static Vector3 ResolveTilePosition(ScrollChannelRuntimePlayer player, ScrollTileCoord coord, Vector3 origin, Vector2 dir, float step, float scrollDistance)
        {
            var origin2 = new Vector2(origin.x, origin.y);
            var distance = (coord.X * step) + scrollDistance;
            var xy = origin2 + (dir * distance);
            return new Vector3(xy.x, xy.y, origin.z);
        }

        static void ApplyTransform(ScrollTileHandle handle, Vector3 worldPosition)
        {
            if (handle.Transform == null)
                return;

            handle.Transform.position = worldPosition;
        }

        static void CollectRequiredTiles(
            HashSet<ScrollTileCoord> target,
            Rect viewRect,
            ScrollChannelDefinition def,
            Vector3 origin,
            Vector2 dir,
            float step,
            float scrollDistance,
            Vector2Int viewMarginTiles)
        {
            target.Clear();

            var minProj = float.PositiveInfinity;
            var maxProj = float.NegativeInfinity;

            var p0 = new Vector2(viewRect.xMin, viewRect.yMin);
            var p1 = new Vector2(viewRect.xMin, viewRect.yMax);
            var p2 = new Vector2(viewRect.xMax, viewRect.yMin);
            var p3 = new Vector2(viewRect.xMax, viewRect.yMax);

            UpdateProjectionRange(p0, dir, ref minProj, ref maxProj);
            UpdateProjectionRange(p1, dir, ref minProj, ref maxProj);
            UpdateProjectionRange(p2, dir, ref minProj, ref maxProj);
            UpdateProjectionRange(p3, dir, ref minProj, ref maxProj);

            var originProj = Vector2.Dot(new Vector2(origin.x, origin.y), dir) + scrollDistance;

            var baseMargin = Mathf.CeilToInt(
                Mathf.Abs(dir.x) * (viewMarginTiles.x + def.ExtraMarginTiles.x) +
                Mathf.Abs(dir.y) * (viewMarginTiles.y + def.ExtraMarginTiles.y));

            var minIndex = Mathf.FloorToInt((minProj - originProj) / step) - baseMargin;
            var maxIndex = Mathf.CeilToInt((maxProj - originProj) / step) + baseMargin;

            for (int i = minIndex; i <= maxIndex; i++)
            {
                target.Add(new ScrollTileCoord(i, 0));
            }
        }

        static void UpdateProjectionRange(Vector2 point, Vector2 dir, ref float minProj, ref float maxProj)
        {
            var projection = Vector2.Dot(point, dir);
            if (projection < minProj)
                minProj = projection;
            if (projection > maxProj)
                maxProj = projection;
        }

        static void ExtractSpawnedInfo(
            IObjectResolver resolver,
            out GameObject? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            resolver.TryResolve(out runtimeScope);
            resolver.TryResolve(out baseScope);
            resolver.TryResolve(out scopeNode);

            if (runtimeScope != null)
                root = runtimeScope.gameObject;
            else if (baseScope != null)
                root = baseScope.gameObject;
            else if (scopeNode?.Identity?.SelfTransform != null)
                root = scopeNode.Identity.SelfTransform.gameObject;
        }

        static async UniTask ReleaseHandleAsync(ScrollTileHandle handle, CancellationToken ct)
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
                try
                {
                    await handle.BaseScope.DespawnAsync(ct);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                }
            }

            if (handle.Root != null)
            {
                UnityEngine.Object.Destroy(handle.Root);
            }
            else if (handle.Transform != null)
            {
                UnityEngine.Object.Destroy(handle.Transform.gameObject);
            }
        }

        static IVarStore ResolveVars(IScopeNode? scope)
        {
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                return vars;
            return NullVarStore.Instance;
        }

        static ISceneSpawnerRegistry? ResolveRegistry(IScopeNode? scope)
        {
            var resolver = scope?.Resolver;
            if (resolver == null)
                return null;

            return resolver.TryResolve<ISceneSpawnerRegistry>(out var registry) ? registry : null;
        }

        static ICommandRunner? ResolveRunner(IScopeNode? scope)
        {
            var resolver = scope?.Resolver;
            if (resolver == null)
                return null;

            return resolver.TryResolve<ICommandRunner>(out var runner) ? runner : null;
        }

        static IVarStore ResolveVars(VarsPolicy policy, IVarStore inheritVars, IScopeNode targetScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = targetScope.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return inheritVars ?? NullVarStore.Instance;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }
    }
}
