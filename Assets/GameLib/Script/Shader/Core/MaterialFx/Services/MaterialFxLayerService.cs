#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// IMaterialFxLayerService の実装。
    /// LayerStack を管理し、Fade 更新・合成を行う。
    /// </summary>
    public sealed class MaterialFxLayerService : IMaterialFxLayerService, IMaterialFxLayerTelemetry
    {

        readonly IMaterialFxPropertyRegistry _registry;
        readonly IMaterialFxTargetAdapter _adapter;
        readonly Dictionary<string, MaterialFxLayerStack> _stacks;
        readonly List<string> _dirtyKeys;
        readonly Dictionary<string, int> _dirtyKeyIndices;

        readonly List<string> _fadingKeys;
        readonly Dictionary<string, int> _fadingKeyIndices;

        readonly List<string> _timedKeys;
        readonly Dictionary<string, int> _timedKeyIndices;
        float _currentTime;



        public MaterialFxLayerService(IMaterialFxPropertyRegistry registry, IMaterialFxTargetAdapter adapter)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _stacks = new Dictionary<string, MaterialFxLayerStack>(StringComparer.Ordinal);
            _dirtyKeys = new List<string>(16);
            _dirtyKeyIndices = new Dictionary<string, int>(16, StringComparer.Ordinal);

            _fadingKeys = new List<string>(8);
            _fadingKeyIndices = new Dictionary<string, int>(8, StringComparer.Ordinal);

            _timedKeys = new List<string>(8);
            _timedKeyIndices = new Dictionary<string, int>(8, StringComparer.Ordinal);
        }

        public bool HasFadingOrTimedKeys => _fadingKeys.Count > 0 || _timedKeys.Count > 0;

        void IMaterialFxLayerTelemetry.GetTelemetrySnapshot(List<MaterialFxStackTelemetry> dst)
        {
            if (dst == null)
                return;

            dst.Clear();

            foreach (var kvp in _stacks)
            {
                var stack = kvp.Value;
                if (stack == null)
                    continue;

                var stackView = new MaterialFxStackTelemetry
                {
                    StableKey = kvp.Key,
                    ValueType = stack.ValueType,
                    LayerCount = stack.LayerCount,
                };

                var layers = stack.Layers;
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    stackView.Layers.Add(new MaterialFxLayerTelemetry
                    {
                        ContextTag = layer.ContextTag,
                        Priority = layer.Priority,
                        BlendMode = layer.BlendMode,
                        CurrentValue = layer.CurrentValue.ToString(),
                        IsValueFading = layer.IsFading,
                        IsWeightFading = layer.IsWeightFading,
                        RemainingLifetimeSeconds = layer.RemainingLifetimeSeconds,
                    });
                }

                dst.Add(stackView);
            }
        }

        public void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                             MaterialFxBlendMode blend, int priority, float lifetimeSeconds = -1f)
        {
            contextTag = NormalizeContextTag(contextTag);
            var stack = GetOrCreateStack(stableKey);

            // ★型不一致チェック（境界で弾く）
            if (value.Type != stack.ValueType)
            {
                Debug.LogError(
                    $"[MaterialFxLayerService] ValueType mismatch key='{stableKey}' expected={stack.ValueType} actual={value.Type} ctx='{contextTag}'");
                return;
            }

            var layer = stack.GetOrCreateLayer(contextTag);



            // Priority 変更をチェック
            if (layer.Priority != priority)
            {
                stack.NotifyPriorityChanged();
            }

            layer.SetImmediate(value, blend, priority);
            layer.ApplyLifetime(lifetimeSeconds);
            stack.MarkDirty();
            MarkDirtyKey(stableKey);



            if (layer.HasFiniteLifetime)
                MarkTimedKey(stableKey);
        }

        public void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                                 float duration, Ease easing, MaterialFxBlendMode blend, int priority, float lifetimeSeconds = -1f)
        {
            contextTag = NormalizeContextTag(contextTag);
            var stack = GetOrCreateStack(stableKey);

            // ★型不一致チェック（境界で弾く）
            if (value.Type != stack.ValueType)
            {
                Debug.LogError(
                    $"[MaterialFxLayerService] ValueType mismatch key='{stableKey}' expected={stack.ValueType} actual={value.Type} ctx='{contextTag}'");
                return;
            }

            var layer = stack.GetOrCreateLayer(contextTag);



            // Priority 変更をチェック
            if (layer.Priority != priority)
            {
                stack.NotifyPriorityChanged();
            }

            layer.StartFade(value, duration, easing, blend, priority, _currentTime);
            layer.ApplyLifetime(lifetimeSeconds);
            stack.MarkDirty();
            MarkDirtyKey(stableKey);

            if (duration > 0f)
                MarkFadingKey(stableKey);
        }

        /// <summary>
        /// レイヤーの Weight（寄与率）を Fade。
        /// targetWeight = 0 で「このレイヤーの影響を 0 にする」。
        /// </summary>
        public void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                       float duration, Ease easing)
        {
            contextTag = NormalizeContextTag(contextTag);

            if (!_stacks.TryGetValue(stableKey, out var stack))
                return; // Stack がなければ何もしない

            var layer = stack.FindLayer(contextTag);
            if (layer == null)
                return; // Layer がなければ何もしない



            layer.StartWeightFade(targetWeight, duration, easing, _currentTime);
            stack.MarkDirty();
            MarkDirtyKey(stableKey);



            if (duration > 0f)
                MarkFadingKey(stableKey);
        }

        public int GetActiveLayerCount(string stableKey, string contextTag = "")
        {
            // contextTag が null/empty の場合は全 Layer をカウント
            if (_stacks.TryGetValue(stableKey, out MaterialFxLayerStack stack))
            {
                if (string.IsNullOrEmpty(contextTag))
                {
                    return stack.LayerCount;
                }
                else
                {
                    var normalizedContext = NormalizeContextTag(contextTag);
                    var layer = stack.FindLayer(normalizedContext);
                    return layer != null ? 1 : 0;
                }
            }
            return 0;
        }

        public bool RemoveLayer(string stableKey, string contextTag)
        {
            if (!_stacks.TryGetValue(stableKey, out var stack))
                return false;

            var normalizedContext = NormalizeContextTag(contextTag);
            var removed = stack.RemoveLayer(normalizedContext);
            if (removed)
                MarkDirtyKey(stableKey);
            return removed;
        }

        public void ClearContext(string contextTag)
        {
            var normalizedContext = NormalizeContextTag(contextTag);
            if (string.Equals(normalizedContext, MaterialFxContextTags.Default, StringComparison.Ordinal))
                return; // Default は削除不可

            foreach (var kvp in _stacks)
            {
                var stableKey = kvp.Key;
                var stack = kvp.Value;
                if (stack.RemoveLayer(normalizedContext))
                {
                    MarkDirtyKey(stableKey);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var kvp in _stacks)
            {
                var stableKey = kvp.Key;
                var stack = kvp.Value;
                stack.ClearNonDefault();
                if (stack.IsDirty)
                    MarkDirtyKey(stableKey);
            }
        }

        public void UpdateFades(float deltaTime)
        {
            // 内部で時刻を積算（呼び元が currentTime と deltaTime を取り違える誤用を防止）
            _currentTime += Mathf.Max(0f, deltaTime);



            // 0. Lifetime 更新（期限切れ Layer を除去）
            if (_timedKeys.Count > 0)
            {
                for (int i = _timedKeys.Count - 1; i >= 0; i--)
                {
                    var stableKey = _timedKeys[i];
                    if (!_stacks.TryGetValue(stableKey, out var stack) || stack == null)
                    {
                        RemoveTimedKeyAt(i);
                        continue;
                    }

                    if (stack.UpdateLifetimes(deltaTime))
                    {
                        MarkDirtyKey(stableKey);
                    }

                    if (!stack.HasFiniteLifetimeLayer())
                    {
                        RemoveTimedKeyAt(i);
                    }
                }
            }

            if (_fadingKeys.Count == 0)
                return;

            for (int i = _fadingKeys.Count - 1; i >= 0; i--)
            {
                var stableKey = _fadingKeys[i];
                if (!_stacks.TryGetValue(stableKey, out var stack) || stack == null)
                {
                    RemoveFadingKeyAt(i);
                    continue;
                }

                if (stack.UpdateFades(_currentTime))
                {
                    MarkDirtyKey(stableKey);
                }
                else
                {

                    RemoveFadingKeyAt(i);
                }
            }
        }

        public IReadOnlyList<string> GetDirtyKeys()
        {
            return _dirtyKeys;
        }

        public MaterialFxTypedValue ComputeFinalValue(string stableKey)
        {
            if (_stacks.TryGetValue(stableKey, out var stack))
            {
                return stack.ComputeFinalValue();
            }

            // Stack がない場合はフォールバック
            if (_registry.TryGetValueType(stableKey, out var type))
            {
                return MaterialFxTypedValue.GetDefaultFallback(type);
            }
            return default;
        }

        public void ClearDirty(string stableKey)
        {
            if (_stacks.TryGetValue(stableKey, out var stack))
            {
                stack.ClearDirty();
            }

            RemoveDirtyKey(stableKey);
        }

        public bool TryGetLayerValue(string stableKey, string contextTag, out MaterialFxTypedValue value)
        {
            if (_stacks.TryGetValue(stableKey, out var stack))
            {
                var normalizedContext = NormalizeContextTag(contextTag);
                var layer = stack.FindLayer(normalizedContext);
                if (layer != null)
                {
                    value = layer.CurrentValue;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public bool HasStack(string stableKey) => _stacks.ContainsKey(stableKey);

        /// <summary>
        /// 指定 ContextTag を持つ全 StableKey を列挙。
        /// FadeOutPreset などで「dirty に関係なく contextTag 全体を対象にする」ために使用。
        /// </summary>
        public IEnumerable<string> GetKeysByContext(string contextTag)
        {
            var normalizedContext = NormalizeContextTag(contextTag);
            foreach (var kvp in _stacks)
            {
                if (kvp.Value.FindLayer(normalizedContext) != null)
                {
                    yield return kvp.Key;
                }
            }
        }

        MaterialFxLayerStack GetOrCreateStack(string stableKey)
        {
            if (_stacks.TryGetValue(stableKey, out var stack))
                return stack;

            if (!_registry.TryGet(stableKey, out var meta))
                throw new InvalidOperationException($"[MaterialFxLayerService] Unknown StableKey: '{stableKey}'");

            stack = new MaterialFxLayerStack(stableKey, meta.ValueType);
            _stacks[stableKey] = stack;

            // Default Layer を作成し、Material から初期値を読み取る
            InitializeDefaultLayer(stack, meta);

            return stack;
        }

        void InitializeDefaultLayer(MaterialFxLayerStack stack, MaterialFxPropertyMeta meta)
        {
            var defaultLayer = stack.GetOrCreateLayer(MaterialFxContextTags.Default);
            defaultLayer.Priority = MaterialFxContextTags.DefaultPriority;
            defaultLayer.BlendMode = MaterialFxBlendMode.Override;

            // Material から初期値を読み取る
            var propertyId = Shader.PropertyToID(meta.ShaderPropertyName);
            if (_adapter.TryReadPropertyValue(propertyId, meta.ValueType, out var initialValue))
            {
                defaultLayer.CurrentValue = initialValue;
                defaultLayer.TargetValue = initialValue;
            }
            else
            {
                // 読み取り失敗時はフォールバック
                var fallback = MaterialFxTypedValue.GetDefaultFallback(meta.ValueType);
                defaultLayer.CurrentValue = fallback;
                defaultLayer.TargetValue = fallback;
            }

            // 次の Tick で Dispatch させる
            stack.MarkDirty();
            MarkDirtyKey(stack.StableKey);
        }

        void MarkDirtyKey(string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey))
                return;

            if (_dirtyKeyIndices.ContainsKey(stableKey))
                return;

            _dirtyKeyIndices.Add(stableKey, _dirtyKeys.Count);
            _dirtyKeys.Add(stableKey);
        }

        static string NormalizeContextTag(string? contextTag)
        {
            if (string.IsNullOrWhiteSpace(contextTag))
                return MaterialFxContextTags.Default;

            return contextTag;
        }

        void RemoveDirtyKey(string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey))
                return;

            if (!_dirtyKeyIndices.TryGetValue(stableKey, out var index))
                return;

            var lastIndex = _dirtyKeys.Count - 1;
            var lastKey = _dirtyKeys[lastIndex];

            _dirtyKeys[index] = lastKey;
            _dirtyKeyIndices[lastKey] = index;

            _dirtyKeys.RemoveAt(lastIndex);
            _dirtyKeyIndices.Remove(stableKey);
        }

        void MarkFadingKey(string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey))
                return;

            if (_fadingKeyIndices.ContainsKey(stableKey))
                return;

            _fadingKeyIndices.Add(stableKey, _fadingKeys.Count);
            _fadingKeys.Add(stableKey);
        }

        void RemoveFadingKeyAt(int index)
        {
            if (index < 0 || index >= _fadingKeys.Count)
                return;

            var stableKey = _fadingKeys[index];

            var lastIndex = _fadingKeys.Count - 1;
            var lastKey = _fadingKeys[lastIndex];

            _fadingKeys[index] = lastKey;
            _fadingKeyIndices[lastKey] = index;

            _fadingKeys.RemoveAt(lastIndex);
            _fadingKeyIndices.Remove(stableKey);
        }

        void MarkTimedKey(string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey))
                return;

            if (_timedKeyIndices.ContainsKey(stableKey))
                return;

            _timedKeyIndices.Add(stableKey, _timedKeys.Count);
            _timedKeys.Add(stableKey);
        }

        void RemoveTimedKeyAt(int index)
        {
            if (index < 0 || index >= _timedKeys.Count)
                return;

            var stableKey = _timedKeys[index];

            var lastIndex = _timedKeys.Count - 1;
            var lastKey = _timedKeys[lastIndex];

            _timedKeys[index] = lastKey;
            _timedKeyIndices[lastKey] = index;

            _timedKeys.RemoveAt(lastIndex);
            _timedKeyIndices.Remove(stableKey);
        }

        public void Dispose()
        {
            _stacks.Clear();
            _dirtyKeys.Clear();
            _dirtyKeyIndices.Clear();
            _fadingKeys.Clear();
            _fadingKeyIndices.Clear();
            _timedKeys.Clear();
            _timedKeyIndices.Clear();
        }
    }
}
