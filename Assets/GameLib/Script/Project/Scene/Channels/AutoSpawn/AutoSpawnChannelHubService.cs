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
    public sealed class AutoSpawnChannelHubService :
        IAutoSpawnChannelHubService,
        ITickable,
        ILateTickable,
        ITickPhase,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        const string LogPrefix = "[AutoSpawnChannelHub]";

        readonly Dictionary<string, AutoSpawnChannelDefinition> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, AutoSpawnChannelRuntimePlayer> _playersByTag = new(StringComparer.Ordinal);
        readonly List<AutoSpawnChannelRuntimePlayer> _players = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();

        readonly bool _runInLateUpdate;
        readonly bool _forceTickInRuntime;
        readonly IAreaChannelHubService? _areaHub;

        IScopeNode? _owner;
        IVarStore _vars = NullVarStore.Instance;
        ISceneSpawnerRegistry? _registry;
        CancellationTokenSource? _cts;
        bool _defsDirty = true;
        bool _active;
        bool _loggedMissingRegistry;
        bool _loggedMissingAreaHub;

        public TickPhase Phase => _runInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var item in _defsByTag.Values)
                        _defsSnapshot.Add(item);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public AutoSpawnChannelHubService(
            AutoSpawnChannelDefinition[] definitions,
            IAreaChannelHubService? areaHub,
            bool runInLateUpdate,
            bool forceTickInRuntime)
        {
            _areaHub = areaHub;
            _runInLateUpdate = runInLateUpdate;
            _forceTickInRuntime = forceTickInRuntime;

            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                RegisterChannelInternal(definitions[i], overwrite: false);
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            tag = NormalizeTag(tag);

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
            if (def is not AutoSpawnChannelDefinition typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            tag = NormalizeTag(tag);

            if (!_defsByTag.Remove(tag))
                return false;

            if (_playersByTag.TryGetValue(tag, out var player) && player != null)
            {
                RequestReleaseManagedSpawns(player);
                _players.Remove(player);
            }

            _playersByTag.Remove(tag);
            _defsDirty = true;
            return true;
        }

        public bool MutateChannel(string tag, AutoSpawnChannelRuntimeMutation mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            tag = NormalizeTag(tag);

            if (!_defsByTag.TryGetValue(tag, out var def) || def == null)
                return false;

            var changed = ApplyMutation(def, mutation);
            if (!changed)
                return false;

            if (_owner is Component ownerComponent)
                def.EnsureIntegrity(ownerComponent);
            else
                def.Frequency ??= new AutoSpawnChannelFrequency();

            def.Frequency.EnsureIntegrity();
            var frequency = def.Frequency;

            if (_playersByTag.TryGetValue(tag, out var player) && player != null)
            {
                if (mutation.ResetNextSpawnSchedule)
                {
                    player.NextSpawnTime = IsRecurringEnabled(def)
                        ? Time.time + Mathf.Max(0f, frequency.InitialDelaySeconds)
                        : float.PositiveInfinity;
                }
                else if (!IsRecurringEnabled(def))
                {
                    player.NextSpawnTime = float.PositiveInfinity;
                }
            }

            _defsDirty = true;
            return true;
        }

        public async UniTask<int> SpawnExternallyAsync(string tag, int count, float intervalSeconds, CancellationToken ct = default)
        {
            if (!_active)
                return 0;

            if (count <= 0)
                return 0;

            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            tag = NormalizeTag(tag);

            if (!_playersByTag.TryGetValue(tag, out var player) || player == null)
                return 0;

            if (!player.Definition.AllowExternalSpawn)
                return 0;

            return await SpawnCountAsync(
                player,
                count,
                ct,
                updateSchedule: false,
                isInitialBatch: false,
                releaseAutoProcessingFlag: false,
                perSpawnIntervalSeconds: intervalSeconds);
        }

        public async UniTask<int> ClearSpawnedAsync(string tag, CancellationToken ct = default)
        {
            tag = NormalizeTag(tag);

            if (!_playersByTag.TryGetValue(tag, out var player) || player == null)
                return 0;

            var drainedResolvers = DrainManagedResolvers(player);
            if (drainedResolvers.Count == 0)
                return 0;

            return await ReleaseManagedResolversAsync(drainedResolvers, ct);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _owner = scope;
            _vars = ResolveVars(scope);
            _registry = ResolveRegistry(scope);
            _active = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _loggedMissingRegistry = false;
            _loggedMissingAreaHub = false;

            var now = Time.time;
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                player.Processing = false;
                player.InitialSpawnDone = false;
                player.LoggedCondition = false;
                player.LoggedMapping = false;
                player.LoggedArea = false;
                player.NextSpawnTime = IsRecurringEnabled(player.Definition)
                    ? now + Mathf.Max(0f, player.Definition.Frequency.InitialDelaySeconds)
                    : float.PositiveInfinity;
                if (ShouldRunInitialSpawnOnAcquire(player))
                    TryStartInitialSpawn(player);
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _active = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Processing = false;
                RequestReleaseManagedSpawns(_players[i]);
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

            if (_areaHub == null)
            {
                LogErrorOnce(ref _loggedMissingAreaHub, "IAreaChannelHubService is not available.");
                return;
            }

            var now = Time.time;
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (!player.Definition.Enabled || player.Processing)
                    continue;

                if (!player.InitialSpawnDone &&
                    player.Definition.InitialSpawnConditionPolicy == AutoSpawnInitialSpawnConditionPolicy.WaitUntilFirstTrue &&
                    IsConditionMet(player))
                {
                    TryStartInitialSpawn(player);
                    continue;
                }

                if (!IsRecurringEnabled(player.Definition))
                    continue;

                if (now < player.NextSpawnTime)
                    continue;

                if (!IsConditionMet(player))
                    continue;

                var ct = _cts != null ? _cts.Token : CancellationToken.None;
                player.Processing = true;
                UniTask.Void(async () => await SpawnBatchAsync(player, ct));
            }
        }

        void TryStartInitialSpawn(AutoSpawnChannelRuntimePlayer player)
        {
            var initialCount = Mathf.Max(0, player.Definition.Frequency.InitialSpawnCount);
            if (player.InitialSpawnDone)
                return;

            if (initialCount <= 0)
            {
                player.InitialSpawnDone = true;
                return;
            }

            if (player.Processing || !_active)
                return;

            player.InitialSpawnDone = true;
            var ct = _cts != null ? _cts.Token : CancellationToken.None;
            player.Processing = true;
            UniTask.Void(async () => await SpawnCountAsync(
                player,
                initialCount,
                ct,
                updateSchedule: false,
                isInitialBatch: true,
                releaseAutoProcessingFlag: true,
                perSpawnIntervalSeconds: 0f));
        }

        bool ShouldRunInitialSpawnOnAcquire(AutoSpawnChannelRuntimePlayer player)
        {
            return player.Definition.InitialSpawnConditionPolicy switch
            {
                AutoSpawnInitialSpawnConditionPolicy.IgnoreConditions => true,
                AutoSpawnInitialSpawnConditionPolicy.RequireTrueOnAcquire => IsConditionMet(player),
                AutoSpawnInitialSpawnConditionPolicy.WaitUntilFirstTrue => false,
                _ => true,
            };
        }

        bool IsConditionMet(AutoSpawnChannelRuntimePlayer player)
        {
            var owner = _owner;
            if (owner == null)
                return false;

            var conditions = player.Definition.Conditions;
            if (conditions == null || conditions.Count == 0)
                return true;

            var ctx = new SimpleDynamicContext(_vars, owner);
            if (player.Definition.ConditionMode == AutoSpawnChannelConditionMode.Any)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    if (conditions[i].EvaluateBool(ctx))
                        return true;
                }

                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].EvaluateBool(ctx))
                    return false;
            }

            return true;
        }

        async UniTask SpawnBatchAsync(AutoSpawnChannelRuntimePlayer player, CancellationToken ct)
        {
            var count = Mathf.Max(1, player.Definition.Frequency.SpawnCountPerInterval);
            await SpawnCountAsync(
                player,
                count,
                ct,
                updateSchedule: true,
                isInitialBatch: false,
                releaseAutoProcessingFlag: true,
                perSpawnIntervalSeconds: 0f);
        }

        async UniTask<int> SpawnCountAsync(
            AutoSpawnChannelRuntimePlayer player,
            int count,
            CancellationToken ct,
            bool updateSchedule,
            bool isInitialBatch,
            bool releaseAutoProcessingFlag,
            float perSpawnIntervalSeconds)
        {
            var spawnedCount = 0;
            try
            {
                if (!_active)
                    return 0;

                if (!TryResolveSpawner(player.Definition, out var spawner))
                    return 0;

                for (int i = 0; i < count; i++)
                {
                    if (ct.IsCancellationRequested)
                        return spawnedCount;

                    var owner = _owner;
                    if (owner == null)
                        return spawnedCount;

                    var dynCtx = new SimpleDynamicContext(_vars, owner);

                    if (!TryPickMapping(player, dynCtx, out var mapping, out var template))
                        return spawnedCount;

                    if (!TryGetSpawnPosition(player, dynCtx, isInitialBatch, out var position))
                        return spawnedCount;

                    var spawnParams = SpawnParams.ForRuntime(
                        template,
                        position,
                        Quaternion.identity,
                        Vector3.one,
                        identity: null,
                        transformParent: player.Definition.TransformParent,
                        lifetimeScopeParent: null,
                        worldSpace: true,
                        allowPooling: player.Definition.AllowPooling);

                    IObjectResolver? spawnedResolver = null;
                    try
                    {
                        spawnedResolver = await spawner.SpawnAsync(spawnParams, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return spawnedCount;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{LogPrefix} Spawn failed: {ex.Message}");
                        Debug.LogException(ex);
                        continue;
                    }

                    if (spawnedResolver == null)
                    {
                        Debug.LogError($"{LogPrefix} Spawn failed: resolver is null.");
                        continue;
                    }

                    RegisterManagedResolver(player, spawnedResolver);
                    spawnedCount++;

                    UniTask.Void(async () => await RunOnSpawnedCommandsAsync(mapping, spawnedResolver, ct));

                    if (perSpawnIntervalSeconds > 0f && i < count - 1)
                    {
                        try
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(perSpawnIntervalSeconds), cancellationToken: ct);
                        }
                        catch (OperationCanceledException)
                        {
                            return spawnedCount;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Spawn batch failed: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                if (releaseAutoProcessingFlag)
                    player.Processing = false;

                if (updateSchedule && IsRecurringEnabled(player.Definition))
                {
                    player.NextSpawnTime = Time.time + GetNextIntervalSeconds(player.Definition.Frequency);
                }
                else if (!IsRecurringEnabled(player.Definition))
                {
                    player.NextSpawnTime = float.PositiveInfinity;
                }
            }

            return spawnedCount;
        }

        async UniTask RunOnSpawnedCommandsAsync(AutoSpawnChannelMappingEntry mapping, IObjectResolver resolver, CancellationToken ct)
        {
            if (mapping.OnSpawnedCommands == null || mapping.OnSpawnedCommands.Count == 0)
                return;

            if (!resolver.TryResolve<IScopeNode>(out var spawnedScope) || spawnedScope == null)
            {
                Debug.LogError($"{LogPrefix} Spawned resolver does not expose IScopeNode.");
                return;
            }

            EnsureScopeBuiltIfNeeded(spawnedScope);

            if (!TryResolveRunner(spawnedScope, out var runner) || runner == null)
            {
                Debug.LogError($"{LogPrefix} Spawned scope has no ICommandRunner.");
                return;
            }

            var vars = ResolveVars(mapping.VarsPolicy, _vars, spawnedScope);
            var options = CommandRunOptions.Default.WithSuppressCancelLog(true);
            var context = new CommandContext(spawnedScope, vars, runner, actor: spawnedScope, options);

            try
            {
                var result = await runner.ExecuteListAsync(mapping.OnSpawnedCommands, context, ct, options);
                if (result.Status == CommandRunStatus.Canceled)
                    return;

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                {
                    Debug.LogError($"{LogPrefix} OnSpawned commands failed. FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} OnSpawned commands exception: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        bool TryResolveSpawner(AutoSpawnChannelDefinition def, out IAsyncSpawnerService spawner)
        {
            spawner = null!;

            if (def.SpawnerKind != SpawnerKind.RuntimeEntity && def.SpawnerKind != SpawnerKind.RuntimeUIElement)
            {
                Debug.LogError($"{LogPrefix} SpawnerKind must be RuntimeEntity or RuntimeUIElement. Current={def.SpawnerKind}");
                return false;
            }

            var registry = _registry ?? ResolveRegistry(_owner);
            if (registry == null)
            {
                LogErrorOnce(ref _loggedMissingRegistry, "ISceneSpawnerRegistry is not available.");
                return false;
            }

            _registry = registry;

            var allowTagFallback = string.IsNullOrEmpty(def.SpawnerTag);
            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                registry,
                def.SpawnerKind,
                def.SpawnerTag,
                allowTagFallback,
                allowRuntimeUiFallback: true);

            if (resolved.Spawner == null)
            {
                Debug.LogError($"{LogPrefix} Spawner not found. Kind={def.SpawnerKind} Tag={def.SpawnerTag}");
                return false;
            }

            spawner = resolved.Spawner;
            return true;
        }

        bool TryPickMapping(AutoSpawnChannelRuntimePlayer player, IDynamicContext dynCtx, out AutoSpawnChannelMappingEntry mapping, out BaseRuntimeTemplateSO template)
        {
            mapping = null!;
            template = null!;

            var list = player.Definition.Mappings;
            if (list == null || list.Count == 0)
            {
                if (!player.LoggedMapping)
                {
                    player.LoggedMapping = true;
                    Debug.LogError($"{LogPrefix} Mapping list is empty. Tag={player.Definition.Tag}");
                }
                return false;
            }

            Span<int> validIndices = stackalloc int[Mathf.Min(list.Count, 256)];
            int validCount = 0;
            BaseRuntimeTemplateSO? firstTemplate = null;
            AutoSpawnChannelMappingEntry? firstEntry = null;

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null)
                    continue;

                if (!entry.TryResolveRuntimeTemplate(dynCtx, out var resolvedTemplate) || resolvedTemplate == null)
                    continue;

                if (firstEntry == null)
                {
                    firstEntry = entry;
                    firstTemplate = resolvedTemplate;
                }

                if (validCount < validIndices.Length)
                    validIndices[validCount] = i;
                validCount++;
            }

            if (validCount <= 0 || firstEntry == null || firstTemplate == null)
            {
                if (!player.LoggedMapping)
                {
                    player.LoggedMapping = true;
                    Debug.LogError($"{LogPrefix} Mapping has no valid runtime template. Tag={player.Definition.Tag}");
                }
                return false;
            }

            player.LoggedMapping = false;

            if (validCount > validIndices.Length)
            {
                mapping = firstEntry;
                template = firstTemplate;
                return true;
            }

            var pick = UnityEngine.Random.Range(0, validCount);
            var selectedIndex = validIndices[pick];
            mapping = list[selectedIndex];
            var ok = mapping.TryResolveRuntimeTemplate(dynCtx, out var selectedTemplate);
            if (!ok || selectedTemplate == null)
            {
                mapping = firstEntry;
                template = firstTemplate;
                return true;
            }

            template = selectedTemplate;
            return true;
        }

        bool TryGetSpawnPosition(AutoSpawnChannelRuntimePlayer player, IDynamicContext dynCtx, bool isInitialBatch, out Vector3 position)
        {
            position = default;

            if (_areaHub == null)
                return false;

            var tags = ResolveAreaTags(player.Definition, isInitialBatch);
            if (tags == null || tags.Count == 0)
            {
                if (!player.LoggedArea)
                {
                    player.LoggedArea = true;
                    Debug.LogError($"{LogPrefix} Area tags are empty. Tag={player.Definition.Tag}");
                }
                return false;
            }

            if (!_areaHub.TrySamplePosition(tags, player.Definition.AreaSelectionMode, AreaSampleRequest.InteriorRandom, out var sampled, out _))
            {
                if (!player.LoggedArea)
                {
                    player.LoggedArea = true;
                    Debug.LogError($"{LogPrefix} Failed to sample area. Tag={player.Definition.Tag}");
                }
                return false;
            }

            player.LoggedArea = false;

            var offset = player.Definition.SpawnOffset.GetOrDefault(dynCtx, Vector3.zero);
            position = sampled + offset;
            return true;
        }

        bool RegisterChannelInternal(AutoSpawnChannelDefinition? def, bool overwrite)
        {
            if (def == null)
                return false;

            var normalizedTag = NormalizeTag(def.Tag);
            if (string.IsNullOrWhiteSpace(normalizedTag))
                return false;

            if (_defsByTag.ContainsKey(normalizedTag))
            {
                if (!overwrite)
                    return false;

                _defsByTag.Remove(normalizedTag);
                if (_playersByTag.TryGetValue(normalizedTag, out var oldPlayer) && oldPlayer != null)
                {
                    RequestReleaseManagedSpawns(oldPlayer);
                    _players.Remove(oldPlayer);
                }

                _playersByTag.Remove(normalizedTag);
            }

            _defsByTag[normalizedTag] = def;
            var runtimePlayer = new AutoSpawnChannelRuntimePlayer(def);
            _playersByTag[normalizedTag] = runtimePlayer;
            _players.Add(runtimePlayer);
            _defsDirty = true;
            return true;
        }

        static bool ApplyMutation(AutoSpawnChannelDefinition def, AutoSpawnChannelRuntimeMutation mutation)
        {
            var changed = false;

            if (mutation.ApplyEnabled && def.Enabled != mutation.Enabled)
            {
                def.Enabled = mutation.Enabled;
                changed = true;
            }

            if (mutation.ApplyAllowExternalSpawn && def.AllowExternalSpawn != mutation.AllowExternalSpawn)
            {
                def.AllowExternalSpawn = mutation.AllowExternalSpawn;
                changed = true;
            }

            if (mutation.ApplySpawnIntervalSeconds && !Mathf.Approximately(def.Frequency.SpawnIntervalSeconds, mutation.SpawnIntervalSeconds))
            {
                def.Frequency.SpawnIntervalSeconds = mutation.SpawnIntervalSeconds;
                changed = true;
            }

            if (mutation.ApplySpawnCountPerInterval && def.Frequency.SpawnCountPerInterval != mutation.SpawnCountPerInterval)
            {
                def.Frequency.SpawnCountPerInterval = mutation.SpawnCountPerInterval;
                changed = true;
            }

            if (mutation.ApplyInitialSpawnCount && def.Frequency.InitialSpawnCount != mutation.InitialSpawnCount)
            {
                def.Frequency.InitialSpawnCount = mutation.InitialSpawnCount;
                changed = true;
            }

            if (mutation.ApplyInitialDelaySeconds && !Mathf.Approximately(def.Frequency.InitialDelaySeconds, mutation.InitialDelaySeconds))
            {
                def.Frequency.InitialDelaySeconds = mutation.InitialDelaySeconds;
                changed = true;
            }

            if (mutation.ApplyIntervalJitterSeconds && !Mathf.Approximately(def.Frequency.IntervalJitterSeconds, mutation.IntervalJitterSeconds))
            {
                def.Frequency.IntervalJitterSeconds = mutation.IntervalJitterSeconds;
                changed = true;
            }

            if (mutation.ResetNextSpawnSchedule)
                changed = true;

            return changed;
        }

        static void RegisterManagedResolver(AutoSpawnChannelRuntimePlayer player, IObjectResolver resolver)
        {
            lock (player.ManagedSpawnedResolversLock)
                player.ManagedSpawnedResolvers.Add(resolver);
        }

        static List<IObjectResolver> DrainManagedResolvers(AutoSpawnChannelRuntimePlayer player)
        {
            lock (player.ManagedSpawnedResolversLock)
            {
                if (player.ManagedSpawnedResolvers.Count == 0)
                    return new List<IObjectResolver>(0);

                var drained = new List<IObjectResolver>(player.ManagedSpawnedResolvers);
                player.ManagedSpawnedResolvers.Clear();
                return drained;
            }
        }

        void RequestReleaseManagedSpawns(AutoSpawnChannelRuntimePlayer player)
        {
            var drainedResolvers = DrainManagedResolvers(player);
            if (drainedResolvers.Count == 0)
                return;

            UniTask.Void(async () => await ReleaseManagedResolversAsync(drainedResolvers, CancellationToken.None));
        }

        async UniTask<int> ReleaseManagedResolversAsync(List<IObjectResolver> resolvers, CancellationToken ct)
        {
            if (resolvers == null || resolvers.Count == 0)
                return 0;

            var releasedCount = 0;
            for (int i = 0; i < resolvers.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    if (await ReleaseSpawnedResolverAsync(resolvers[i], ct))
                        releasedCount++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogPrefix} Managed spawn release failed: {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            return releasedCount;
        }

        static async UniTask<bool> ReleaseSpawnedResolverAsync(IObjectResolver resolver, CancellationToken ct)
        {
            if (resolver == null)
                return false;

            await UniTask.SwitchToMainThread(ct);

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                if (runtimeScope.Resolver != null &&
                    runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                    pool != null)
                {
                    pool.Release(runtimeScope);
                    return true;
                }

                Debug.LogError($"{LogPrefix} Runtime scope release was requested, but no IRuntimeLifetimeScopePool was found.");
                return false;
            }

            if (resolver.TryResolve<BaseLifetimeScope>(out var baseScope) && baseScope != null)
            {
                await baseScope.DespawnAsync(ct);
                return true;
            }

            Debug.LogError($"{LogPrefix} Managed spawn resolver does not expose releasable LifetimeScope.");
            return false;
        }

        static float GetNextIntervalSeconds(AutoSpawnChannelFrequency frequency)
        {
            var interval = Mathf.Max(0f, frequency.SpawnIntervalSeconds);
            var jitter = Mathf.Max(0f, frequency.IntervalJitterSeconds);
            if (jitter > 0f)
                interval += UnityEngine.Random.Range(0f, jitter);
            return interval;
        }

        static bool IsRecurringEnabled(AutoSpawnChannelDefinition def)
        {
            if (def?.Frequency == null)
                return false;

            return def.Frequency.SpawnIntervalSeconds >= 0f;
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "default";

            return tag.Trim();
        }

        static IReadOnlyList<string> ResolveAreaTags(AutoSpawnChannelDefinition def, bool isInitialBatch)
        {
            if (!isInitialBatch)
                return def.AreaTags;

            if (!def.UseInitialAreaTags)
                return def.AreaTags;

            if (def.InitialAreaTags != null && def.InitialAreaTags.Count > 0)
                return def.InitialAreaTags;

            return def.AreaTags;
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

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
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

        static void LogErrorOnce(ref bool flag, string message)
        {
            if (flag)
                return;

            flag = true;
            Debug.LogError($"{LogPrefix} {message}");
        }
    }
}
