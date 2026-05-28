#nullable enable
using System;
using System.Collections.Generic;
namespace Game.Save
{
    public interface ISaveBinder
    {
        void Collect(in SaveContext ctx, SavePlan plan, in SaveScopeRegistration reg, ref SavePayload payload);
        void Apply(in SaveContext ctx, SavePlan plan, in SaveScopeRegistration reg, in SavePayload payload, ISaveLayerPolicy layerPolicy);
    }

    public sealed class SaveManager : ISaveManager, ISaveManagerDebug
    {
        readonly ISaveStore _store;
        readonly ISavePlatform _platform;
        readonly ISaveSerializer _serializer;
        readonly ISaveLogger _logger;
        readonly ISaveThreadGuard _threadGuard;
        readonly ISaveBinder _binder;
        readonly ISaveLayerPolicy _layerPolicy;
        readonly ISaveBackupPolicy _backupPolicy;

        readonly Dictionary<ScopeKey, SaveScopeRegistration> _registrations = new();
        readonly Dictionary<PlanCacheKey, CachedPlan> _planCache = new();

        const int HistoryCapacity = 64;
        readonly SaveOperationRecord[] _history = new SaveOperationRecord[HistoryCapacity];
        int _historyNext;
        int _historyCount;

        readonly List<SaveEntry> _tmpEntries = new(64);
        readonly List<SaveEntry> _tmpDistinct = new(64);
        readonly HashSet<SaveEntry> _tmpDedup = new(new SaveEntryComparer());
        readonly List<PlanCacheKey> _tmpRemoveKeys = new(8);
        static readonly SaveLayer[] PersistentLayers =
        {
            SaveLayer.Global,
            SaveLayer.SystemSetting,
            SaveLayer.Profile,
            SaveLayer.GameLogic,
        };

        public int ActiveProfileId { get; private set; }
        public SavePlatformCaps PlatformCaps => _platform.Caps;
        public int PlanCacheCount => _planCache.Count;

        public SaveManager(
            ISaveStore store,
            ISavePlatform platform,
            ISaveSerializer serializer,
            ISaveLogger logger,
            ISaveThreadGuard threadGuard,
            ISaveBinder binder,
            ISaveLayerPolicy layerPolicy,
            ISaveBackupPolicy backupPolicy)
        {
            _store = store;
            _platform = platform;
            _serializer = serializer;
            _logger = logger;
            _threadGuard = threadGuard;
            _binder = binder;
            _layerPolicy = layerPolicy;
            _backupPolicy = backupPolicy;
            ActiveProfileId = 0;
        }

        public IDisposable RegisterScope(in SaveScopeRegistration reg)
        {
            _registrations[reg.ScopeKey] = reg;
            RemoveCachedPlans(reg.ScopeKey);
            return new RegistrationHandle(this, reg.ScopeKey);
        }

        public void GetRegisteredScopeKeys(List<ScopeKey> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var pair in _registrations)
                results.Add(pair.Key);
        }

        public SaveResult Save(in SaveContext ctx, bool updateBackup = false)
        {
            string key = string.Empty;
            var bytesLength = 0;
            var result = SaveResult.Success();

            try
            {
                if (!_threadGuard.TryAssertMainThread("Save", out var threadMsg))
                    return result = SaveResult.Failed(SaveError.NotMainThread, threadMsg);

                if (!ctx.TryValidate(out var validationError))
                    return result = validationError;

                if (!_registrations.TryGetValue(ctx.ScopeKey, out var reg))
                    return result = SaveResult.Failed(SaveError.ScopeNotReady, $"Scope {ctx.ScopeKey} not registered.");

                if (!TryValidateRegistration(in reg, out var regErr))
                    return result = regErr;

                var planRes = TryGetPlan(in reg, ctx.Layer, out var plan);
                if (!planRes.IsSuccess)
                    return result = planRes;

                if (plan.Entries.Count == 0)
                {
                    return result = SaveResult.NoData();
                }

                var payload = new SavePayload
                {
                    SaveVer = 2,
                    Blackboard = Array.Empty<BlackboardVarPayload>(),
                    GridBlackboard = Array.Empty<GridBlackboardVarPayload>(),
                    Scalars = Array.Empty<ScalarKeyPayload>(),
                };

                try
                {
                    _binder.Collect(in ctx, plan, in reg, ref payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Save] Binder Collect failed: {ctx}", ex);
                    return result = SaveResult.Failed(SaveError.UnknownException, ex.Message);
                }

                SaveSerializeResult serRes;
                try
                {
                    serRes = _serializer.TrySerialize(in payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Save] Serialize failed: {ctx}", ex);
                    return result = SaveResult.Failed(SaveError.SerializationError, ex.Message);
                }

                if (serRes.Status != SaveSerializerStatus.Success || serRes.Bytes == null)
                    return result = SaveResult.Failed(SaveError.SerializationError, serRes.Message);

                bytesLength = serRes.Bytes.Length;

                if (!SaveKeys.TryBuildPayloadKey(ctx.ProfileId, ctx.ScopeKey, ctx.Layer, out key, out var keyErr))
                    return result = keyErr;

                SaveStoreSaveResult storeRes;
                try
                {
                    storeRes = _store.Save(key, serRes.Bytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Save] Store.Save threw: {ctx} key={key}", ex);
                    return result = SaveResult.Failed(SaveError.IOError, ex.Message);
                }

                if (storeRes.Status != SaveStoreSaveStatus.Success)
                {
                    var err = storeRes.Status == SaveStoreSaveStatus.StorageFull ? SaveError.StorageFull : SaveError.IOError;
                    return result = SaveResult.Failed(err, storeRes.Message);
                }

                var flushRes = SafeFlush("Save");
                if (!flushRes.IsSuccess)
                    return result = flushRes;

                if (updateBackup && _backupPolicy.ShouldBackup(in ctx))
                    ApplyBackupBestEffort(in ctx, serRes.Bytes);

                return result = SaveResult.Success();
            }
            finally
            {
                AddHistory(SaveOperationKind.Save, in ctx, result, key, bytesLength);
            }
        }

        public SaveResult Load(in SaveContext ctx)
        {
            string key = string.Empty;
            var bytesLength = 0;
            var result = SaveResult.Success();

            try
            {
                if (!_threadGuard.TryAssertMainThread("Load", out var threadMsg))
                    return result = SaveResult.Failed(SaveError.NotMainThread, threadMsg);

                if (!ctx.TryValidate(out var validationError))
                    return result = validationError;

                if (!_registrations.TryGetValue(ctx.ScopeKey, out var reg))
                    return result = SaveResult.Failed(SaveError.ScopeNotReady, $"Scope {ctx.ScopeKey} not registered.");

                if (!TryValidateRegistration(in reg, out var regErr))
                    return result = regErr;

                if (!SaveKeys.TryBuildPayloadKey(ctx.ProfileId, ctx.ScopeKey, ctx.Layer, out key, out var keyErr))
                    return result = keyErr;

                // Check if there are any elements to load for this layer
                var planRes = TryGetPlan(in reg, ctx.Layer, out var plan);
                if (!planRes.IsSuccess)
                    return result = planRes;

                if (plan.Entries.Count == 0)
                {
                    return result = SaveResult.NoData();
                }

                SaveStoreLoadResult loadRes;
                try
                {
                    loadRes = _store.Load(key);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Load] Store.Load threw: {ctx} key={key}", ex);
                    return result = SaveResult.Failed(SaveError.IOError, ex.Message);
                }

                if (loadRes.Status == SaveStoreLoadStatus.NotFound)
                    return result = SaveResult.NoData();

                if (loadRes.Status != SaveStoreLoadStatus.Success || loadRes.Bytes == null)
                    return result = SaveResult.Failed(SaveError.IOError, loadRes.Message);

                bytesLength = loadRes.Bytes.Length;

                SaveDeserializeResult deRes;
                SavePayload payload;
                try
                {
                    deRes = _serializer.TryDeserialize(loadRes.Bytes, out payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Load] Deserialize threw: {ctx}", ex);
                    return result = SaveResult.Failed(SaveError.SerializationError, ex.Message);
                }

                if (deRes.Status != SaveSerializerStatus.Success)
                    return result = SaveResult.Failed(SaveError.SerializationError, deRes.Message);

                try
                {
                    _binder.Apply(in ctx, plan, in reg, in payload, _layerPolicy);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Load] Binder Apply failed: {ctx}", ex);
                    return result = SaveResult.Failed(SaveError.UnknownException, ex.Message);
                }

                return result = SaveResult.Success();
            }
            finally
            {
                AddHistory(SaveOperationKind.Load, in ctx, result, key, bytesLength);
            }
        }

        public SaveResult Clear(in SaveContext ctx)
        {
            string key = string.Empty;
            var result = SaveResult.Success();

            try
            {
                if (!_threadGuard.TryAssertMainThread("Clear", out var threadMsg))
                    return result = SaveResult.Failed(SaveError.NotMainThread, threadMsg);

                if (!ctx.TryValidate(out var validationError))
                    return result = validationError;

                if (!SaveKeys.TryBuildPayloadKey(ctx.ProfileId, ctx.ScopeKey, ctx.Layer, out key, out var keyErr))
                    return result = keyErr;

                try
                {
                    _store.DeleteKey(key);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Clear] Store.DeleteKey threw: {ctx} key={key}", ex);
                    return result = SaveResult.Failed(SaveError.IOError, ex.Message);
                }

                return result = SafeFlush("Clear");
            }
            finally
            {
                AddHistory(SaveOperationKind.Clear, in ctx, result, key, 0);
            }
        }

        public SaveResult ChangeActiveProfile(int profileId)
        {
            var result = SaveResult.Success();
            try
            {
                if (!_threadGuard.TryAssertMainThread("ChangeActiveProfile", out var threadMsg))
                    return result = SaveResult.Failed(SaveError.NotMainThread, threadMsg);

                if (profileId < 0)
                    return result = SaveResult.Failed(SaveError.InvalidKey, "ProfileId must be non-negative.");

                ActiveProfileId = profileId;
                return result = ReloadRegisteredPersistentScopes(profileId);
            }
            finally
            {
                AddHistory(
                    SaveOperationKind.Load,
                    new SaveContext(profileId < 0 ? 0 : profileId, SaveLayer.Profile, default),
                    result,
                    "ProfileChange",
                    0);
            }
        }

        public SaveResult DeleteAllPersistedData()
        {
            var result = SaveResult.Success();
            try
            {
                if (!_threadGuard.TryAssertMainThread("DeleteAllPersistedData", out var threadMsg))
                    return result = SaveResult.Failed(SaveError.NotMainThread, threadMsg);

                SaveStoreDeleteAllResult deleteRes;
                try
                {
                    deleteRes = _store.DeleteAll();
                }
                catch (Exception ex)
                {
                    _logger.LogError("[DeleteAllPersistedData] Store.DeleteAll threw", ex);
                    return result = SaveResult.Failed(SaveError.IOError, ex.Message);
                }

                if (deleteRes.Status != SaveStoreDeleteAllStatus.Success)
                    return result = SaveResult.Failed(SaveError.IOError, deleteRes.Message);

                var flushRes = SafeFlush("DeleteAllPersistedData");
                if (!flushRes.IsSuccess)
                    return result = flushRes;

                ReapplyRegisteredProfileDefaults();
                return result = SaveResult.Success();
            }
            finally
            {
                AddHistory(
                    SaveOperationKind.Clear,
                    new SaveContext(ActiveProfileId, SaveLayer.Profile, default),
                    result,
                    "DeleteAllPersistedData",
                    0);
            }
        }

        void AddHistory(SaveOperationKind kind, in SaveContext ctx, SaveResult result, string key, int bytesLength)
        {
            var record = new SaveOperationRecord(DateTime.UtcNow.Ticks, kind, ctx, result, key, bytesLength);
            _history[_historyNext] = record;
            _historyNext++;
            if (_historyNext >= HistoryCapacity)
                _historyNext = 0;
            if (_historyCount < HistoryCapacity)
                _historyCount++;
        }

        public void GetRegistrations(List<SaveScopeRegistrationInfo> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var pair in _registrations)
            {
                var reg = pair.Value;
                var planType = reg.PlanSource != null ? reg.PlanSource.GetType().Name : string.Empty;
                var planVersion = reg.PlanSource?.Version ?? 0;
                results.Add(new SaveScopeRegistrationInfo(
                    reg.ScopeKey,
                    planVersion,
                    reg.Blackboard != null,
                    reg.Scalars != null,
                    planType));
            }
        }

        public void GetRecentOperations(List<SaveOperationRecord> results)
        {
            if (results == null)
                return;

            results.Clear();
            // Newest-first
            for (int i = 0; i < _historyCount; i++)
            {
                var idx = _historyNext - 1 - i;
                if (idx < 0)
                    idx += HistoryCapacity;
                results.Add(_history[idx]);
            }
        }

        SaveResult SafeFlush(string opName)
        {
            SavePlatformResult platRes;
            try
            {
                platRes = _platform.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{opName}] Platform.Flush threw", ex);
                return SaveResult.Failed(SaveError.IOError, ex.Message);
            }

            return platRes.Status == SavePlatformStatus.Success
                ? SaveResult.Success()
                : SaveResult.Failed(SaveError.IOError, platRes.Message);
        }

        bool TryValidateRegistration(in SaveScopeRegistration reg, out SaveResult error)
        {
            if (!reg.PlanSource.ScopeKey.Equals(reg.ScopeKey))
            {
                error = SaveResult.Failed(SaveError.ScopeMismatch, "PlanSource.ScopeKey mismatch.");
                return false;
            }

            if (reg.Blackboard == null)
            {
                error = SaveResult.Failed(SaveError.MissingDependency, "Value store missing in registration.");
                return false;
            }

            if (reg.Scalars == null)
            {
                error = SaveResult.Failed(SaveError.MissingDependency, "Scalars missing in registration.");
                return false;
            }

            error = SaveResult.Success();
            return true;
        }

        SaveResult TryGetPlan(in SaveScopeRegistration reg, SaveLayer layer, out SavePlan plan)
        {
            var cacheKey = new PlanCacheKey(reg.ScopeKey, layer);
            var version = reg.PlanSource.Version;

            if (_planCache.TryGetValue(cacheKey, out var cached) && cached.Version == version)
            {
                plan = cached.Plan;
                return SaveResult.Success();
            }

            _tmpEntries.Clear();
            _tmpDistinct.Clear();
            _tmpDedup.Clear();

            reg.PlanSource.CollectEntries(layer, _tmpEntries);

            for (int i = 0; i < _tmpEntries.Count; i++)
            {
                var e = _tmpEntries[i];
                if (!e.IsValid)
                    continue;

                if (!_tmpDedup.Add(e))
                    continue;

                _tmpDistinct.Add(e);
            }

            var arr = new SaveEntry[_tmpDistinct.Count];
            for (int i = 0; i < _tmpDistinct.Count; i++)
                arr[i] = _tmpDistinct[i];

            plan = new SavePlan(arr);
            _planCache[cacheKey] = new CachedPlan(version, plan);
            return SaveResult.Success();
        }

        SaveResult ReloadRegisteredPersistentScopes(int profileId)
        {
            ReapplyRegisteredProfileDefaults();

            foreach (var pair in _registrations)
            {
                var reg = pair.Value;
                for (int i = 0; i < PersistentLayers.Length; i++)
                {
                    var ctx = new SaveContext(profileId, PersistentLayers[i], reg.ScopeKey);
                    var loadRes = Load(in ctx);
                    if (loadRes.IsFailed)
                        return loadRes;
                }
            }

            return SaveResult.Success();
        }

        void ReapplyRegisteredProfileDefaults()
        {
            foreach (var pair in _registrations)
            {
                pair.Value.Profiles?.ReapplyAllBindings();
            }
        }

        void ApplyBackupBestEffort(in SaveContext ctx, byte[] bytes)
        {
            var mode = _backupPolicy.GetBackupMode(in ctx);
            if (mode == SaveBackupMode.None)
                return;

            if (mode == SaveBackupMode.Export)
            {
                try
                {
                    var exportName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                    var exportRes = _platform.ExportBytes(exportName, bytes, "application/octet-stream");
                    if (exportRes.Status != SavePlatformStatus.Success)
                        _logger.LogWarning($"[Save] Backup export failed: {exportRes.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[Save] Backup export threw: {ex.Message}");
                }

                return;
            }

            if (mode == SaveBackupMode.Slotted)
            {
                var max = _backupPolicy.GetMaxSlots(in ctx);
                if (max <= 0)
                {
                    _logger.LogWarning("[Save] Backup slots invalid (<=0). Skipping.");
                    return;
                }

                var slot = 0;
                if (!SaveKeys.TryBuildBackupKey(ctx.ProfileId, ctx.ScopeKey, ctx.Layer, slot, out var key, out var keyErr))
                {
                    _logger.LogWarning($"[Save] Backup key build failed: {keyErr.Error} {keyErr.Message}");
                    return;
                }

                try
                {
                    var storeRes = _store.Save(key, bytes);
                    if (storeRes.Status != SaveStoreSaveStatus.Success)
                        _logger.LogWarning($"[Save] Backup store failed: {storeRes.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[Save] Backup store threw: {ex.Message}");
                }
            }
        }

        void RemoveCachedPlans(ScopeKey scopeKey)
        {
            if (_planCache.Count == 0)
                return;

            _tmpRemoveKeys.Clear();
            foreach (var pair in _planCache)
            {
                if (pair.Key.ScopeKey.Equals(scopeKey))
                    _tmpRemoveKeys.Add(pair.Key);
            }

            for (int i = 0; i < _tmpRemoveKeys.Count; i++)
                _planCache.Remove(_tmpRemoveKeys[i]);
        }

        void UnregisterScope(ScopeKey key)
        {
            _registrations.Remove(key);
            RemoveCachedPlans(key);
        }

        sealed class RegistrationHandle : IDisposable
        {
            SaveManager? _owner;
            readonly ScopeKey _key;

            public RegistrationHandle(SaveManager owner, ScopeKey key)
            {
                _owner = owner;
                _key = key;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                    return;

                _owner = null;
                owner.UnregisterScope(_key);
            }
        }

        readonly struct PlanCacheKey : IEquatable<PlanCacheKey>
        {
            public readonly ScopeKey ScopeKey;
            public readonly SaveLayer Layer;

            public PlanCacheKey(ScopeKey scopeKey, SaveLayer layer)
            {
                ScopeKey = scopeKey;
                Layer = layer;
            }

            public bool Equals(PlanCacheKey other) => ScopeKey.Equals(other.ScopeKey) && Layer == other.Layer;
            public override bool Equals(object obj) => obj is PlanCacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ScopeKey, Layer);
        }

        readonly struct CachedPlan
        {
            public readonly int Version;
            public readonly SavePlan Plan;

            public CachedPlan(int version, SavePlan plan)
            {
                Version = version;
                Plan = plan;
            }
        }
    }
}
