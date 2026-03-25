using System;
using System.Collections.Generic;
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

        bool _touched;
        internal bool HasLocalData => _hasBaselineFromConfig || _touched;
        internal bool HasLocalOverride => _touched;

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

                if (config.UseClampMod)
                    RegisterModifier(new ClampScalarModifier(config.Clamp, _invalidateCache));
            }
        }

        public ScalarKey Key => _key;

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

            // Clamp modifier handling
            if (config != null && config.UseClampMod)
            {
                if (ResolveModifier<ClampScalarModifier>() is ClampScalarModifier clamp)
                    clamp.Clamp = config.Clamp;
                else
                    RegisterModifier(new ClampScalarModifier(config.Clamp, _invalidateCache));
            }
            else
            {
                // remove clamp modifier
                RemoveModifier<ClampScalarModifier>();
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
            MarkDirty();
        }

        public float LocalBase => _localBase;

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

            var upsert = UpsertAddByLayerTag(ctx.Layer, ctx.Value, duration, source, tag);
            if (upsert != null)
                return upsert;

            var entry = new ModEntry
            {
                Id = Guid.NewGuid(),
                Kind = ScalarModKind.Add,
                Phase = ScalarMulPhase.PostAdd,
                Value = ctx.Value,
                Remain = duration,
                Source = source,
                Tag = tag,
                Layer = ctx.Layer
            };

            int index = _mods.Count;
            _mods.Add(entry);
            _indexById[entry.Id] = index;

            if (duration >= 0f)
                _timedIndices.Add(index);

            MarkDirty();
            return new ScalarHandle(this, entry.Id);
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

            var upsert = UpsertMulByLayerTag(ctx.Layer, ctx.Factor, ctx.Phase, duration, source, tag);
            if (upsert != null)
                return upsert;

            var entry = new ModEntry
            {
                Id = Guid.NewGuid(),
                Kind = ScalarModKind.Mul,
                Phase = ctx.Phase,
                Value = ctx.Factor,
                Remain = duration,
                Source = source,
                Tag = tag,
                Layer = ctx.Layer,
            };

            int index = _mods.Count;
            _mods.Add(entry);
            _indexById[entry.Id] = index;
            if (duration >= 0f)
                _timedIndices.Add(index);
            MarkDirty();

            return new ScalarHandle(this, entry.Id);
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

            var id = handle.Id;
            if (!_indexById.TryGetValue(id, out var index))
                return;

            _mods[index] = _mods[_mods.Count - 1];
            _mods.RemoveAt(_mods.Count - 1);
            RebuildIndexCaches();

            MarkDirty();
        }

        public void SetHandleValue(Guid id, float value)
        {
            if (!_indexById.TryGetValue(id, out var index))
                return;

            var e = _mods[index];
            e.Value = value;
            _mods[index] = e;
            MarkDirty();
        }

        public void Tick(float dt)
        {
            if (_timedIndices.Count == 0)
                return;

            var toRemove = _tickRemovalBuffer;
            toRemove.Clear();

            foreach (var index in _timedIndices)
            {
                if (index < 0 || index >= _mods.Count)
                    continue;

                var e = _mods[index];
                if (e.Remain < 0f)
                    continue;

                e.Remain -= dt;
                _mods[index] = e;
                if (e.Remain <= 0f)
                    toRemove.Add(index);
            }

            if (toRemove.Count > 0)
            {
                toRemove.Sort((a, b) => b.CompareTo(a));
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int idx = toRemove[i];
                    if (idx < 0 || idx >= _mods.Count)
                        continue;

                    _mods.RemoveAt(idx);
                }

                RebuildIndexCaches();

                MarkDirty();
            }

            toRemove.Clear();
        }

        float EvaluateRaw(bool includeAllLayers, string layer)
        {
            float baseAdd = _baseline + _localBase;
            float sumAdd = 0f;
            float preMul = 1f;
            float postMul = 1f;

            for (int i = 0; i < _mods.Count; i++)
            {
                var e = _mods[i];
                if (!includeAllLayers && !string.Equals(e.Layer, layer, StringComparison.Ordinal))
                    continue;

                if (e.Kind == ScalarModKind.Add)
                {
                    sumAdd += e.Value;
                }
                else
                {
                    if (e.Phase == ScalarMulPhase.PreAdd)
                        preMul *= e.Value;
                    else
                        postMul *= e.Value;
                }
            }

            float v = (baseAdd * preMul) + sumAdd;
            v *= postMul;
            return v;
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
                value = EvaluateRaw(includeAllLayers, layer);

                var ctx = new ScalarGetContext
                {
                    Key = _key,
                    IncludeAllLayers = includeAllLayers,
                    Layer = layer,
                    Value = value,
                    Runtime = this,
                    Service = service,
                    DynamicContext = service is BaseScalarService baseService ? baseService.DynamicContext : null
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
            for (int i = 0; i < _mods.Count; i++)
            {
                var e = _mods[i];
                yield return new ScalarSnapshot(
                    _key,
                    e.Kind,
                    e.Phase,
                    e.Value,
                    e.Remain,
                    e.Source,
                    e.Tag,
                    e.Layer,
                    e.Id);
            }
        }
    }
}
