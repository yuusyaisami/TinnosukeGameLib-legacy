#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// Default ContextTag の定数。
    /// </summary>
    public static class MaterialFxContextTags
    {
        /// <summary>Material 初期値を保持する土台レイヤー</summary>
        public const string Default = "Default";

        /// <summary>Default Layer の Priority（常に最下層）</summary>
        public const int DefaultPriority = int.MinValue;
    }

    /// <summary>
    /// 1つの (StableKey, ContextTag) に対応するレコード。
    /// 値・Priority・BlendMode・Fade状態・Weight（寄与率）を保持。
    /// </summary>
    public sealed class MaterialFxLayer
    {
        // ★安定ソート用: 生成順序。Priority 同値時の tie-break に使用。
        static int s_orderCounter;

        public string ContextTag { get; }
        public ValueKind ValueType { get; }

        /// <summary>生成順序（Priority 同値時の tie-break 用）</summary>
        public int Order { get; }

        public int Priority { get; set; }
        public MaterialFxBlendMode BlendMode { get; set; }

        // 現在の値
        public MaterialFxTypedValue CurrentValue;

        // ★ Weight（寄与率 0..1）: 合成時に「このレイヤーがどれだけ影響するか」
        public float Weight;

        // Weight Fade 状態
        public float TargetWeight;
        public float StartWeight;
        public float WeightFadeStartTime;
        public float WeightFadeDuration;
        public Ease WeightFadeEasing;
        public bool IsWeightFading;

        // Value Fade 状態（値自体の Fade 用）
        public MaterialFxTypedValue TargetValue;
        public MaterialFxTypedValue StartValue;
        public float StartTime;
        public float Duration;
        public Ease Easing;
        public bool IsFading;

        // ── Lifetime（TTL） ──
        // -1: infinite
        // >=0: remaining seconds until this layer should be removed.
        public float RemainingLifetimeSeconds;

        public MaterialFxLayer(string contextTag, ValueKind valueType)
        {
            ContextTag = contextTag ?? throw new ArgumentNullException(nameof(contextTag));
            ValueType = valueType;
            Order = s_orderCounter++;
            Priority = 0;
            BlendMode = MaterialFxBlendMode.Override;
            var initial = MaterialFxTypedValue.GetDefaultFallback(valueType);
            CurrentValue = initial;
            TargetValue = initial;
            StartValue = initial;
            Weight = 1f;  // デフォルトは完全に寄与
            TargetWeight = 1f;
            IsFading = false;
            IsWeightFading = false;
            RemainingLifetimeSeconds = -1f;
        }

        public bool HasFiniteLifetime => RemainingLifetimeSeconds >= 0f;

        public void ApplyLifetime(float lifetimeSeconds)
        {
            if (string.Equals(ContextTag, MaterialFxContextTags.Default, StringComparison.Ordinal))
            {
                RemainingLifetimeSeconds = -1f;
                return;
            }

            if (lifetimeSeconds < 0f)
            {
                RemainingLifetimeSeconds = -1f;
                return;
            }

            if (RemainingLifetimeSeconds < 0f)
            {
                RemainingLifetimeSeconds = lifetimeSeconds;
            }
            else
            {
                RemainingLifetimeSeconds += lifetimeSeconds;
            }
        }

        public bool UpdateLifetime(float deltaTime)
        {
            if (RemainingLifetimeSeconds < 0f)
                return false;

            RemainingLifetimeSeconds -= Mathf.Max(0f, deltaTime);
            return RemainingLifetimeSeconds <= 0f;
        }

        /// <summary>即時設定（値を設定）</summary>
        public void SetImmediate(MaterialFxTypedValue value, MaterialFxBlendMode blend, int priority)
        {
            CurrentValue = value;
            TargetValue = value;
            BlendMode = blend;
            Priority = priority;
            // Spec: if an overwrite comes in while fading, stop the old fade and start the new one.
            IsFading = false;
            IsWeightFading = false;

            // Reset contribution when explicitly set.
            // This prevents "stuck at weight=0" after a previous fade-out.
            Weight = 1f;
            StartWeight = 1f;
            TargetWeight = 1f;
        }

        /// <summary>Value Fade 開始（値自体の補間）</summary>
        public void StartFade(
            MaterialFxTypedValue target,
            float duration,
            Ease easing,
            MaterialFxBlendMode blend,
            int priority,
            float currentTime)
        {
            // Do not allow concurrent fades on the same layer.
            IsWeightFading = false;

            StartValue = CurrentValue;
            TargetValue = target;
            StartTime = currentTime;
            Duration = duration;
            Easing = easing;
            BlendMode = blend;
            Priority = priority;

            // duration <= 0 は即時反映
            IsFading = duration > 0f;
            if (!IsFading)
            {
                CurrentValue = target;
            }
        }

        /// <summary>Weight Fade 開始（レイヤーの寄与率の補間）</summary>
        public void StartWeightFade(float targetWeight, float duration, Ease easing, float currentTime)
        {
            // Do not allow concurrent fades on the same layer.
            IsFading = false;

            StartWeight = Weight;
            TargetWeight = Mathf.Clamp01(targetWeight);
            WeightFadeStartTime = currentTime;
            WeightFadeDuration = duration;
            WeightFadeEasing = easing;

            IsWeightFading = duration > 0f;
            if (!IsWeightFading)
            {
                Weight = TargetWeight;
            }
        }

        /// <summary>
        /// Fade 更新（Value と Weight 両方）。
        /// </summary>
        /// <returns>まだ Fade 中なら true</returns>
        public bool UpdateFade(float currentTime)
        {
            bool changed = false;

            // Value Fade 更新
            if (IsFading)
            {
                float elapsed = currentTime - StartTime;
                if (elapsed >= Duration)
                {
                    CurrentValue = TargetValue;
                    IsFading = false;
                }
                else
                {
                    float t = DOVirtual.EasedValue(0f, 1f, elapsed / Duration, Easing);
                    CurrentValue = MaterialFxTypedValueOps.Lerp(StartValue, TargetValue, t);
                }
                changed = true;
            }

            // Weight Fade 更新
            if (IsWeightFading)
            {
                float elapsed = currentTime - WeightFadeStartTime;
                if (elapsed >= WeightFadeDuration)
                {
                    Weight = TargetWeight;
                    IsWeightFading = false;
                }
                else
                {
                    float t = DOVirtual.EasedValue(0f, 1f, elapsed / WeightFadeDuration, WeightFadeEasing);
                    Weight = Mathf.Lerp(StartWeight, TargetWeight, t);
                }
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Fade を即時完了させる。
        /// </summary>
        public void CompleteFade()
        {
            if (IsFading)
            {
                CurrentValue = TargetValue;
                IsFading = false;
            }
            if (IsWeightFading)
            {
                Weight = TargetWeight;
                IsWeightFading = false;
            }
        }
    }

    /// <summary>
    /// 同一 StableKey の全 Layer を管理。Priority 順で合成。
    /// </summary>
    public sealed class MaterialFxLayerStack
    {
        public string StableKey { get; }
        public ValueKind ValueType { get; }

        readonly List<MaterialFxLayer> _layers = new(4);
        bool _dirty;
        bool _sortNeeded;

        public bool IsDirty => _dirty;
        public int LayerCount => _layers.Count;

        public IReadOnlyList<MaterialFxLayer> Layers => _layers;

        public MaterialFxLayerStack(string stableKey, ValueKind valueType)
        {
            StableKey = stableKey ?? throw new ArgumentNullException(nameof(stableKey));
            ValueType = valueType;
        }

        /// <summary>
        /// 指定 ContextTag の Layer を取得。なければ作成。
        /// </summary>
        public MaterialFxLayer GetOrCreateLayer(string contextTag)
        {
            if (string.IsNullOrEmpty(contextTag))
                throw new ArgumentException("ContextTag cannot be null or empty", nameof(contextTag));

            var layer = FindLayer(contextTag);
            if (layer == null)
            {
                layer = new MaterialFxLayer(contextTag, ValueType);
                _layers.Add(layer);
                _sortNeeded = true;
            }
            return layer;
        }

        /// <summary>
        /// 指定 ContextTag の Layer を検索。
        /// </summary>
        public MaterialFxLayer? FindLayer(string contextTag)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (string.Equals(_layers[i].ContextTag, contextTag, StringComparison.Ordinal))
                    return _layers[i];
            }
            return null;
        }

        /// <summary>
        /// Layer を削除。Default は削除不可。
        /// </summary>
        public bool RemoveLayer(string contextTag)
        {
            // Default は削除不可
            if (string.Equals(contextTag, MaterialFxContextTags.Default, StringComparison.Ordinal))
                return false;

            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_layers[i].ContextTag, contextTag, StringComparison.Ordinal))
                {
                    _layers.RemoveAt(i);
                    _dirty = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Default 以外の全 Layer を削除。
        /// </summary>
        public void ClearNonDefault()
        {
            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_layers[i].ContextTag, MaterialFxContextTags.Default, StringComparison.Ordinal))
                {
                    _layers.RemoveAt(i);
                    _dirty = true;
                }
            }
        }

        public void MarkDirty() => _dirty = true;
        public void ClearDirty() => _dirty = false;

        public bool HasFiniteLifetimeLayer()
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].HasFiniteLifetime)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Lifetime 更新。
        /// 期限切れの Layer は (Default を除き) 自動で除去する。
        /// </summary>
        /// <returns>削除が発生した場合 true</returns>
        public bool UpdateLifetimes(float deltaTime)
        {
            bool removedAny = false;

            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                if (layer.UpdateLifetime(deltaTime))
                {
                    if (!string.Equals(layer.ContextTag, MaterialFxContextTags.Default, StringComparison.Ordinal))
                    {
                        _layers.RemoveAt(i);
                        removedAny = true;
                        _dirty = true;
                    }
                    else
                    {
                        layer.RemainingLifetimeSeconds = -1f;
                    }
                }
            }

            return removedAny;
        }

        /// <summary>
        /// Fade 更新。
        /// </summary>
        /// <returns>まだ Fade 中の Layer があれば true</returns>
        public bool UpdateFades(float currentTime)
        {
            bool anyFading = false;
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].UpdateFade(currentTime))
                {
                    anyFading = true;
                    _dirty = true;
                }
            }
            return anyFading;
        }

        /// <summary>
        /// Priority 昇順で合成し FinalValue を返す。
        /// Weight を考慮した合成を行う。
        /// </summary>
        public MaterialFxTypedValue ComputeFinalValue()
        {
            if (_layers.Count == 0)
            {
                return MaterialFxTypedValue.GetDefaultFallback(ValueType);
            }

            // 必要な場合のみソート（Priority 変更時）
            if (_sortNeeded)
            {
                _layers.Sort(ComparePriority);
                _sortNeeded = false;
            }

            var result = _layers[0].CurrentValue;



            for (int i = 1; i < _layers.Count; i++)
            {
                var layer = _layers[i];

                // Weight が 0 のレイヤーはスキップ
                if (layer.Weight <= 0f) continue;

                // Weight が 1 未満の場合は Lerp で適用度を調整
                if (layer.Weight < 1f)
                {
                    // Weight に応じて "base → incoming" を lerp
                    result = MaterialFxTypedValueOps.BlendWithWeight(result, layer.CurrentValue, layer.BlendMode, layer.Weight);
                }
                else
                {
                    // Weight = 1 の場合は従来どおり
                    result = MaterialFxTypedValueOps.Blend(result, layer.CurrentValue, layer.BlendMode);
                }

            }
            return result;
        }

        /// <summary>
        /// Priority 変更を通知。次回の ComputeFinalValue でソートが走る。
        /// </summary>
        public void NotifyPriorityChanged()
        {
            _sortNeeded = true;
            _dirty = true;
        }

        /// <summary>
        /// Priority 昇順で比較。同値の場合は Order（生成順）で安定ソート。
        /// </summary>
        static int ComparePriority(MaterialFxLayer a, MaterialFxLayer b)
        {
            int cmp = a.Priority.CompareTo(b.Priority);
            return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
        }
    }
}
