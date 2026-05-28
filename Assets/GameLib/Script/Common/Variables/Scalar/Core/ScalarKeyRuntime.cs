using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.Scalar
{
    public sealed class ScalarKeyRuntime
    {
        struct ModEntry
        {
            public Guid Id;
            public ScalarModKind Kind;
            public ScalarMulPhase Phase; // Mul の場合のみ使用
            public float Value;
            public float Remain;
            public object Source;
            public string Tag;
            public string Layer;
        }

        readonly ScalarKey _key;
        float _baseline;
        bool _hasBaselineFromConfig;
        readonly System.Action _invalidateCache;
        readonly LayeredNumericRuntime _numeric = new();

        float _localBase;
        readonly List<ModEntry> _mods = new();
        readonly Dictionary<Guid, int> _indexById = new();
        readonly HashSet<int> _timedIndices = new();

        float _cachedDefault;
        bool _dirty = true;

        readonly List<IScalarAddModifier> _addModifiers = new();
        readonly List<IScalarMulModifier> _mulModifiers = new();
        readonly List<IScalarGetModifier> _getModifiers = new();
        readonly Dictionary<Type, IScalarModifier> _modByType = new();
        readonly List<int> _tickRemovalBuffer = new();
        readonly HashSet<ScalarHandle> _liveHandles = new();

        bool _touched;
        internal bool HasLocalData => _hasBaselineFromConfig || _touched;
        internal bool HasLocalOverride => _touched;
        internal bool HasTimedEntries => _numeric.HasTimedEntries;

        public ScalarKeyRuntime(ScalarKey key, ScalarRuntimeConfig config, System.Action invalidateCache)
        {
            _key = key;
            _baseline = config?.BaseValue ?? 0f;
            _hasBaselineFromConfig = config != null;
            _invalidateCache = invalidateCache ?? (() => { });

            if (config != null)
            {
                if (config.UseEffectMod)
                    RegisterModifier(new EffectScalarModifier(this, _invalidateCache));

                if (config.UseRoundMod)
                    RegisterGetModifierBefore(typeof(ClampScalarModifier), new RoundScalarModifier(config.RoundDigits, _invalidateCache));

                if (config.UseClampMod)
                    SetFinalClamp(config.Clamp);
            }

            _numeric.SetBase(_baseline + _localBase);
        }

        public ScalarKey Key => _key;

        public int Revision => _numeric.Revision;

        /// <summary>
        /// Current baseline value (from config or set externally).
        /// </summary>
        public float Baseline => _baseline;

        /// <summary>
        /// Indicates whether a baseline came from runtime config or explicit set.
        /// </summary>
        public bool HasBaselineFromConfig => _hasBaselineFromConfig;

        /// <summary>
        /// Change the baseline value for this runtime. This will mark the runtime as having baseline (HasLocalData)
        /// and will invalidate caches so consumers observe the change.
        /// </summary>
        public void SetBaseline(float baseline, bool markAsFromConfig = false)
        {
            if (Mathf.Approximately(_baseline, baseline) &&
                _hasBaselineFromConfig == markAsFromConfig &&
                (_hasBaselineFromConfig || _touched))
                return;

            _baseline = baseline;
            _hasBaselineFromConfig = markAsFromConfig || _hasBaselineFromConfig;
            if (!markAsFromConfig)
            {
                // SetRuntimeBaseline で設定した値も LocalGet の対象にする。
                _touched = true;
            }
            _numeric.SetBase(_baseline + _localBase);
            _invalidateCache();
            MarkDirty();
        }

        /// <summary>
        /// Apply/replace runtime config. This updates baseline and modifiers according to the given config.
        /// </summary>
        public void ApplyRuntimeConfig(ScalarRuntimeConfig config)
        {
            // baseline
            _baseline = config?.BaseValue ?? 0f;
            _hasBaselineFromConfig = config != null;
            _numeric.SetBase(_baseline + _localBase);

            // Clamp modifier handling
            if (config != null && config.UseClampMod)
            {
                SetFinalClamp(config.Clamp);
            }
            else
            {
                // remove clamp modifier
                RemoveModifier<ClampScalarModifier>();
                _numeric.ClearFinalClamp();
            }

            // Round modifier handling
            if (config != null && config.UseRoundMod)
            {
                if (ResolveModifier<RoundScalarModifier>() is RoundScalarModifier round)
                {
                    round.Digits = config.RoundDigits;
                }
                else
                {
                    RegisterGetModifierBefore(typeof(ClampScalarModifier), new RoundScalarModifier(config.RoundDigits, _invalidateCache));
                }
            }
            else
            {
                RemoveModifier<RoundScalarModifier>();
            }

            // Effect modifier handling
            if (config != null && config.UseEffectMod)
            {
                if (ResolveModifier<EffectScalarModifier>() == null)
                    RegisterModifier(new EffectScalarModifier(this, _invalidateCache));
            }
            else
            {
                RemoveModifier<EffectScalarModifier>();
            }

            _invalidateCache();
            MarkDirty();
        }

        /// <summary>
        /// Remove a specific modifier type if present.
        /// </summary>
        public void RemoveModifier<TMod>() where TMod : class, IScalarModifier
        {
            var t = typeof(TMod);
            if (!_modByType.TryGetValue(t, out var mod))
                return;

            _modByType.Remove(t);

            if (mod is IScalarAddModifier add)
                _addModifiers.Remove(add);
            if (mod is IScalarMulModifier mul)
                _mulModifiers.Remove(mul);
            if (mod is IScalarGetModifier get)
                _getModifiers.Remove(get);

            MarkDirty();
        }

        internal void RegisterHandle(ScalarHandle handle)
        {
            if (handle != null)
                _liveHandles.Add(handle);
        }

        internal void UnregisterHandle(ScalarHandle handle)
        {
            if (handle != null)
                _liveHandles.Remove(handle);
        }

        internal void InvalidateAllHandles()
        {
            if (_liveHandles.Count == 0)
                return;

            foreach (var handle in _liveHandles)
            {
                handle?.Invalidate();
            }

            _liveHandles.Clear();
        }

        public void RegisterModifier(IScalarModifier mod)
        {
            if (mod == null)
                return;

            _modByType[mod.GetType()] = mod;

            if (mod is IScalarAddModifier add)
                _addModifiers.Add(add);
            if (mod is IScalarMulModifier mul)
                _mulModifiers.Add(mul);
            if (mod is IScalarGetModifier get)
                _getModifiers.Add(get);
        }

        void RegisterGetModifierBefore(Type beforeType, IScalarGetModifier mod)
        {
            if (mod == null)
                return;

            _modByType[mod.GetType()] = mod;

            if (beforeType != null)
            {
                for (int i = 0; i < _getModifiers.Count; i++)
                {
                    var existing = _getModifiers[i];
                    if (existing != null && existing.GetType() == beforeType)
                    {
                        _getModifiers.Insert(i, mod);
                        return;
                    }
                }
            }

            _getModifiers.Add(mod);
        }

        public TMod ResolveModifier<TMod>() where TMod : class, IScalarModifier
        {
            if (_modByType.TryGetValue(typeof(TMod), out var mod))
                return mod as TMod;
            return null;
        }

        public void SetLocalBase(float value)
        {
            if (Mathf.Approximately(_localBase, value) && _touched)
                return;

            _localBase = value;
            _touched = true;
            _numeric.SetBase(_baseline + _localBase);
            MarkDirty();
        }

        public float LocalBase => _localBase;

        public void SetFinalClamp(ScalarClamp clamp)
        {
            if (!clamp.TryCreateLiteralClamp(out var runtimeClamp))
            {
                Debug.LogError($"[Scalar] SCALAR_CLAMP_DYNAMIC_UNSUPPORTED key={_key.Id} name={_key.Name ?? string.Empty}");
                RemoveModifier<ClampScalarModifier>();
                _numeric.ClearFinalClamp();
                _invalidateCache();
                MarkDirty();
                return;
            }

            _numeric.SetFinalClamp(runtimeClamp);

            if (ResolveModifier<ClampScalarModifier>() is ClampScalarModifier existingClamp)
                existingClamp.Clamp = runtimeClamp;
            else
                RegisterModifier(new ClampScalarModifier(runtimeClamp, _invalidateCache));

            _invalidateCache();
            MarkDirty();
        }

        public void RestoreRevision(int revision)
        {
            _numeric.RestoreRevision(revision);
            _invalidateCache();
            MarkDirty();
        }

        internal void RestoreSnapshot(in ScalarSnapshot snapshot)
        {
            if (snapshot.Lane == LayeredNumericLaneKind.FinalClamp && snapshot.Kind == ScalarModKind.Clamp)
            {
                var clamp = new ScalarClamp
                {
                    UseMin = snapshot.HasClampMin,
                    UseMax = snapshot.HasClampMax,
                };

                if (snapshot.HasClampMin)
                    clamp.Min = DynamicValueExtensions.FromLiteral(snapshot.ClampMin);

                if (snapshot.HasClampMax)
                    clamp.Max = DynamicValueExtensions.FromLiteral(snapshot.ClampMax);

                SetFinalClamp(clamp);
                return;
            }

            _numeric.RestoreContribution(snapshot);
        }

        void MarkDirty()
        {
            _dirty = true;
        }

        void RebuildIndexCaches()
        {
            _indexById.Clear();
            _timedIndices.Clear();

            for (int i = 0; i < _mods.Count; i++)
            {
                var e = _mods[i];
                _indexById[e.Id] = i;
                if (e.Remain >= 0f)
                    _timedIndices.Add(i);
            }
        }

        static bool IsSameLayerTag(in ModEntry e, string layer, string tag)
            => string.Equals(e.Layer, layer, StringComparison.Ordinal)
               && string.Equals(e.Tag, tag, StringComparison.Ordinal);

        ScalarHandle UpsertAddByLayerTag(string layer, float value, float duration, object source, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            int keepIndex = -1;
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                var e = _mods[i];
                if (e.Kind == ScalarModKind.Add && IsSameLayerTag(in e, layer, tag))
                {
                    keepIndex = i;
                    break;
                }
            }

            if (keepIndex < 0)
                return null;

            var keep = _mods[keepIndex];
            keep.Kind = ScalarModKind.Add;
            keep.Phase = ScalarMulPhase.PostAdd;
            keep.Value = value;
            keep.Remain = duration;
            keep.Source = source;
            keep.Tag = tag;
            keep.Layer = layer;
            _mods[keepIndex] = keep;

            bool removed = false;
            for (int i = keepIndex - 1; i >= 0; i--)
            {
                var e = _mods[i];
                if (e.Kind == ScalarModKind.Add && IsSameLayerTag(in e, layer, tag))
                {
                    _mods.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                RebuildIndexCaches();
            }
            else
            {
                if (duration >= 0f)
                    _timedIndices.Add(keepIndex);
                else
                    _timedIndices.Remove(keepIndex);
            }

            MarkDirty();
            return new ScalarHandle(this, keep.Id);
        }

        ScalarHandle UpsertMulByLayerTag(string layer, float factor, ScalarMulPhase phase, float duration, object source, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            int keepIndex = -1;
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                var e = _mods[i];
                if (e.Kind == ScalarModKind.Mul && e.Phase == phase && IsSameLayerTag(in e, layer, tag))
                {
                    keepIndex = i;
                    break;
                }
            }

            if (keepIndex < 0)
                return null;

            var keep = _mods[keepIndex];
            keep.Kind = ScalarModKind.Mul;
            keep.Phase = phase;
            keep.Value = factor;
            keep.Remain = duration;
            keep.Source = source;
            keep.Tag = tag;
            keep.Layer = layer;
            _mods[keepIndex] = keep;

            bool removed = false;
            for (int i = keepIndex - 1; i >= 0; i--)
            {
                var e = _mods[i];
                if (e.Kind == ScalarModKind.Mul && e.Phase == phase && IsSameLayerTag(in e, layer, tag))
                {
                    _mods.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                RebuildIndexCaches();
            }
            else
            {
                if (duration >= 0f)
                    _timedIndices.Add(keepIndex);
                else
                    _timedIndices.Remove(keepIndex);
            }

            MarkDirty();
            return new ScalarHandle(this, keep.Id);
        }
        public float AddLocalBase(IBaseScalarService service, string layer, float delta)
        {
            // layerでEffectの影響を受ける場合があるため計算で考慮する
            var ctx = new ScalarAddContext
            {
                Key = _key,
                Layer = layer,
                Value = delta,
                Source = null,
                Tag = null,
                Runtime = this,
                Service = service,
            };

            for (int i = 0; i < _addModifiers.Count; i++)
            {
                _addModifiers[i].OnBeforeAdd(ref ctx);
            }

            float finalDelta = ctx.Value;
            _touched = true;
            SetLocalBase(_localBase + finalDelta);
            return _localBase;
        }

        public ScalarHandle Add(
            IBaseScalarService service,
            string layer,
            float value,
            float duration,
            object source,
            string tag)
        {
            duration = NormalizeDuration(duration);
            _touched = true;

            var ctx = new ScalarAddContext
            {
                Key = _key,
                Layer = layer,
                Value = value,
                Source = source,
                Tag = tag,
                Runtime = this,
                Service = service
            };

            for (int i = 0; i < _addModifiers.Count; i++)
            {
                _addModifiers[i].OnBeforeAdd(ref ctx);
            }

            Guid contributionId = _numeric.UpsertContribution(
                LayeredNumericLaneKind.Add,
                ScalarModKind.Add,
                ScalarMulPhase.PostAdd,
                ctx.Value,
                duration,
                source,
                tag,
                ctx.Layer);

            _invalidateCache();
            MarkDirty();
            return new ScalarHandle(this, contributionId);
        }

        public ScalarHandle Mul(
            IBaseScalarService service,
            string layer,
            float factor,
            ScalarMulPhase phase,
            float duration,
            object source,
            string tag)
        {
            duration = NormalizeDuration(duration);
            _touched = true;

            var ctx = new ScalarMulContext
            {
                Key = _key,
                Layer = layer,
                Factor = factor,
                Phase = phase,
                Source = source,
                Tag = tag,
                Runtime = this,
                Service = service
            };

            for (int i = 0; i < _mulModifiers.Count; i++)
            {
                _mulModifiers[i].OnBeforeMul(ref ctx);
            }

            LayeredNumericLaneKind lane = ctx.Phase == ScalarMulPhase.PreAdd
                ? LayeredNumericLaneKind.PrefixMul
                : LayeredNumericLaneKind.SuffixMul;

            Guid contributionId = _numeric.UpsertContribution(
                lane,
                ScalarModKind.Mul,
                ctx.Phase,
                ctx.Factor,
                duration,
                source,
                tag,
                ctx.Layer);
            _invalidateCache();
            MarkDirty();

            return new ScalarHandle(this, contributionId);
        }

        static float NormalizeDuration(float duration)
        {
            // Duration=0 is usually an uninitialized/default inspector input.
            // Treat it as infinite to avoid immediate expiry on next tick.
            if (Mathf.Approximately(duration, 0f))
                return -1f;

            return duration;
        }

        public void RemoveHandle(ScalarHandle handle)
        {
            if (handle == null)
                return;

            UnregisterHandle(handle);

            if (_numeric.RemoveContribution(handle.Id))
            {
                _invalidateCache();
                MarkDirty();
            }
        }

        public void SetHandleValue(Guid id, float value)
        {
            if (_numeric.SetContributionValue(id, value))
            {
                _invalidateCache();
                MarkDirty();
            }
        }

        public void Tick(float dt)
        {
            if (_numeric.Tick(dt))
            {
                _invalidateCache();
                MarkDirty();
            }
        }

        float EvaluateRaw(bool includeAllLayers, string layer, IDynamicContext dynamicContext)
        {
            return _numeric.Evaluate(includeAllLayers, layer, dynamicContext);
        }

        public float Get(IBaseScalarService service, bool includeAllLayers, string layer)
        {
            bool useCache = includeAllLayers && string.IsNullOrEmpty(layer) && !HasDynamicGetModifiers();
            float value;

            if (useCache && !_dirty)
            {
                value = _cachedDefault;
            }
            else
            {
                IDynamicContext dynamicContext = service is ScalarRuntimeService runtimeService ? runtimeService.DynamicContext : null;
                value = EvaluateRaw(includeAllLayers, layer, dynamicContext);

                var ctx = new ScalarGetContext
                {
                    Key = _key,
                    IncludeAllLayers = includeAllLayers,
                    Layer = layer,
                    Value = value,
                    Runtime = this,
                    Service = service,
                    DynamicContext = dynamicContext
                };

                for (int i = 0; i < _getModifiers.Count; i++)
                {
                    _getModifiers[i].OnAfterEvaluate(ref ctx);
                }

                value = ctx.Value;

                if (useCache)
                {
                    _cachedDefault = value;
                    _dirty = false;
                }
            }

            return value;
        }

        bool HasDynamicGetModifiers()
        {
            return ResolveModifier<ClampScalarModifier>() is ClampScalarModifier clamp && clamp.UsesDynamicBounds;
        }

        public void ForceInvalidate()
        {
            MarkDirty();
        }

        public IEnumerable<ScalarSnapshot> EnumerateSnapshots()
        {
            foreach (var snapshot in _numeric.EnumerateSnapshots(_key))
            {
                yield return snapshot;
            }
        }
    }

    internal sealed class LayeredNumericRuntime
    {
        struct Entry
        {
            public Guid Id;
            public LayeredNumericLaneKind Lane;
            public ScalarModKind Kind;
            public ScalarMulPhase Phase;
            public float Value;
            public float Remain;
            public object Source;
            public string Tag;
            public string Layer;
            public ScalarClamp Clamp;
            public bool HasClamp;
            public int Revision;
        }

        readonly List<Entry> _entries = new();
        readonly Dictionary<Guid, int> _indexById = new();
        float _baseValue;
        ScalarClamp _finalClamp;
        bool _hasFinalClamp;
        int _revision;

        public int Revision => _revision;

        public bool HasTimedEntries
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Remain >= 0f)
                        return true;
                }

                return false;
            }
        }

        public bool HasDynamicFinalClamp => _hasFinalClamp && _finalClamp.UsesDynamicBounds;

        public void SetBase(float value)
        {
            if (Mathf.Approximately(_baseValue, value))
                return;

            _baseValue = value;
            BumpRevision();
        }

        public void SetFinalClamp(ScalarClamp clamp)
        {
            _finalClamp = clamp;
            _hasFinalClamp = clamp.UseMin || clamp.UseMax;
            BumpRevision();
        }

        public void ClearFinalClamp()
        {
            if (!_hasFinalClamp)
                return;

            _hasFinalClamp = false;
            _finalClamp = default;
            BumpRevision();
        }

        public void RestoreRevision(int revision)
        {
            _revision = revision < 0 ? 0 : revision;
        }

        public Guid UpsertContribution(
            LayeredNumericLaneKind lane,
            ScalarModKind kind,
            ScalarMulPhase phase,
            float value,
            float duration,
            object source,
            string tag,
            string layer)
        {
            if (lane != LayeredNumericLaneKind.Add && lane != LayeredNumericLaneKind.PrefixMul && lane != LayeredNumericLaneKind.SuffixMul && lane != LayeredNumericLaneKind.FinalClamp)
                throw new ArgumentOutOfRangeException(nameof(lane));

            string normalizedLayer = layer ?? string.Empty;
            string normalizedTag = tag ?? string.Empty;

            if (!string.IsNullOrEmpty(normalizedTag))
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    Entry existing = _entries[i];
                    if (existing.Lane == lane && existing.Kind == kind && existing.Phase == phase &&
                        string.Equals(existing.Layer, normalizedLayer, StringComparison.Ordinal) &&
                        string.Equals(existing.Tag, normalizedTag, StringComparison.Ordinal))
                    {
                        existing.Value = value;
                        existing.Remain = duration;
                        existing.Source = source;
                        existing.Tag = normalizedTag;
                        existing.Layer = normalizedLayer;
                        existing.Revision += 1;
                        _entries[i] = existing;
                        BumpRevision();
                        return existing.Id;
                    }
                }
            }

            Entry entry = new Entry
            {
                Id = Guid.NewGuid(),
                Lane = lane,
                Kind = kind,
                Phase = phase,
                Value = value,
                Remain = duration,
                Source = source,
                Tag = normalizedTag,
                Layer = normalizedLayer,
                Revision = 1,
            };

            if (lane == LayeredNumericLaneKind.FinalClamp)
            {
                entry.HasClamp = true;
                entry.Clamp = value >= 0f ? _finalClamp : default;
            }

            int index = _entries.Count;
            _entries.Add(entry);
            _indexById[entry.Id] = index;
            BumpRevision();
            return entry.Id;
        }

        public bool RemoveContribution(Guid id)
        {
            if (!_indexById.TryGetValue(id, out int index))
                return false;

            _entries.RemoveAt(index);
            RebuildIndex();
            BumpRevision();
            return true;
        }

        public bool SetContributionValue(Guid id, float value)
        {
            if (!_indexById.TryGetValue(id, out int index))
                return false;

            Entry entry = _entries[index];
            if (Mathf.Approximately(entry.Value, value))
                return false;

            entry.Value = value;
            entry.Revision += 1;
            _entries[index] = entry;
            BumpRevision();
            return true;
        }

        public void RestoreContribution(in ScalarSnapshot snapshot)
        {
            Entry entry = new Entry
            {
                Id = snapshot.Id == Guid.Empty ? Guid.NewGuid() : snapshot.Id,
                Lane = snapshot.Lane,
                Kind = snapshot.Kind,
                Phase = snapshot.Phase,
                Value = snapshot.Value,
                Remain = snapshot.Remain,
                Source = snapshot.Source,
                Tag = snapshot.Tag ?? string.Empty,
                Layer = snapshot.Layer ?? string.Empty,
                Revision = snapshot.Revision < 0 ? 0 : snapshot.Revision,
            };

            if (snapshot.Lane == LayeredNumericLaneKind.FinalClamp && snapshot.Kind == ScalarModKind.Clamp)
            {
                entry.HasClamp = snapshot.HasClampMin || snapshot.HasClampMax;
                if (entry.HasClamp)
                {
                    var clamp = new ScalarClamp
                    {
                        UseMin = snapshot.HasClampMin,
                        UseMax = snapshot.HasClampMax,
                    };

                    if (snapshot.HasClampMin)
                        clamp.Min = DynamicValueExtensions.FromLiteral(snapshot.ClampMin);

                    if (snapshot.HasClampMax)
                        clamp.Max = DynamicValueExtensions.FromLiteral(snapshot.ClampMax);

                    entry.Clamp = clamp;
                }
            }

            int index = _entries.Count;
            _entries.Add(entry);
            _indexById[entry.Id] = index;
            if (entry.Revision > _revision)
                _revision = entry.Revision;
        }

        public bool Tick(float dt)
        {
            bool changed = false;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Remain < 0f)
                    continue;

                float before = entry.Remain;
                entry.Remain -= dt;
                if (!Mathf.Approximately(before, entry.Remain))
                {
                    entry.Revision += 1;
                    changed = true;
                }

                _entries[i] = entry;
            }

            if (!changed)
                return false;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Remain < 0f)
                    continue;

                if (_entries[i].Remain <= 0f)
                    _entries.RemoveAt(i);
            }

            if (changed)
            {
                RebuildIndex();
                BumpRevision();
            }

            return true;
        }

        public float Evaluate(bool includeAllLayers, string layer, IDynamicContext dynamicContext)
        {
            float value = _baseValue;
            ApplyLane(ref value, LayeredNumericLaneKind.PrefixMul, includeAllLayers, layer);
            ApplyLane(ref value, LayeredNumericLaneKind.Add, includeAllLayers, layer);
            ApplyLane(ref value, LayeredNumericLaneKind.SuffixMul, includeAllLayers, layer);

            return value;
        }

        public IEnumerable<ScalarSnapshot> EnumerateSnapshots(ScalarKey key)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                yield return new ScalarSnapshot(
                    key,
                    entry.Lane,
                    entry.Kind,
                    entry.Phase,
                    entry.Value,
                    entry.Remain,
                    entry.Source,
                    entry.Tag,
                    entry.Layer,
                    entry.Id,
                    entry.Revision,
                    entry.HasClamp ? entry.Clamp.Min.GetOrDefault(null, 0f) : 0f,
                    entry.HasClamp ? entry.Clamp.Max.GetOrDefault(null, 0f) : 0f,
                    entry.HasClamp && entry.Clamp.UseMin,
                    entry.HasClamp && entry.Clamp.UseMax);
            }

            if (_hasFinalClamp)
            {
                yield return new ScalarSnapshot(
                    key,
                    LayeredNumericLaneKind.FinalClamp,
                    ScalarModKind.Clamp,
                    ScalarMulPhase.PostAdd,
                    0f,
                    -1f,
                    null,
                    string.Empty,
                    string.Empty,
                    Guid.Empty,
                    _revision,
                    _finalClamp.Min.GetOrDefault(null, 0f),
                    _finalClamp.Max.GetOrDefault(null, 0f),
                    _finalClamp.UseMin,
                    _finalClamp.UseMax);
            }
        }

        void ApplyLane(ref float value, LayeredNumericLaneKind lane, bool includeAllLayers, string layer)
        {
            if (lane == LayeredNumericLaneKind.PrefixMul)
            {
                float factor = 1f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Lane != lane || !ShouldIncludeEntry(entry, includeAllLayers, layer))
                        continue;

                    factor *= entry.Value;
                }

                value *= factor;
                return;
            }

            if (lane == LayeredNumericLaneKind.Add)
            {
                float add = 0f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Lane != lane || !ShouldIncludeEntry(entry, includeAllLayers, layer))
                        continue;

                    add += entry.Value;
                }

                value += add;
                return;
            }

            if (lane == LayeredNumericLaneKind.SuffixMul)
            {
                float factor = 1f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Lane != lane || !ShouldIncludeEntry(entry, includeAllLayers, layer))
                        continue;

                    factor *= entry.Value;
                }

                value *= factor;
            }
        }

        static bool ShouldIncludeEntry(in Entry entry, bool includeAllLayers, string layer)
        {
            return includeAllLayers || string.Equals(entry.Layer, layer ?? string.Empty, StringComparison.Ordinal);
        }

        void BumpRevision()
        {
            _revision += 1;
        }

        void RebuildIndex()
        {
            _indexById.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                _indexById[_entries[i].Id] = i;
            }
        }
    }
}
