using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Game;

namespace Game.Times
{
    // ================================================================
    // TimeService - タイムスケール管理サービス実装
    // ================================================================

    public sealed class TimeService : ITimeService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        readonly Dictionary<TimeScaleKind, float> _base = new();
        readonly Dictionary<TimeScaleKind, float> _effective = new();
        readonly Dictionary<TimeScaleKind, Tween> _baseTweens = new();

        float _unityTimeScale = 1f;
        readonly float _baseFixedDeltaTime;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public TimeService()
        {
            _baseFixedDeltaTime = UnityEngine.Time.fixedDeltaTime;

            // デフォルト値
            _base[TimeScaleKind.GamePlay] = Mathf.Max(0f, UnityEngine.Time.timeScale);
            _base[TimeScaleKind.Pause] = 1f;
        }

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        public float UnityTimeScale => _unityTimeScale;

        public event System.Action UnityTimeScaleChanged;

        // ----------------------------------------------------------------
        // IScopeAcquireHandler / IScopeReleaseHandler
        // ----------------------------------------------------------------

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            Recalculate(force: true);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            KillAllBaseTweens();
        }

        // ----------------------------------------------------------------
        // 基準値
        // ----------------------------------------------------------------

        public float GetBaseScale(TimeScaleKind kind)
            => _base.TryGetValue(kind, out var v) ? v : 1f;

        public void SetBaseScale(TimeScaleKind kind, float scale)
        {
            scale = Mathf.Max(0f, scale);
            KillBaseTween(kind);
            SetBaseScaleInternal(kind, scale);
        }

        // ----------------------------------------------------------------
        // 実効値
        // ----------------------------------------------------------------

        public float GetEffectiveScale(TimeScaleKind kind)
            => _effective.TryGetValue(kind, out var v) ? v : GetBaseScale(kind);

        public float GetCompositeScale(TimeScaleKindMask mask)
        {
            if (mask == TimeScaleKindMask.None)
                return 1f;

            float min = 1f;
            foreach (TimeScaleKind kind in Enum.GetValues(typeof(TimeScaleKind)))
            {
                if (!mask.Contains(kind)) continue;
                min = Mathf.Min(min, GetEffectiveScale(kind));
            }
            return Mathf.Max(0f, min);
        }

        public void AnimateBaseScale(TimeScaleKind kind, float scale, float duration, Ease ease)
        {
            scale = Mathf.Max(0f, scale);
            if (duration <= 0f)
            {
                SetBaseScale(kind, scale);
                return;
            }

            KillBaseTween(kind);
            float value = GetBaseScale(kind);
            var tween = DOTween.To(
                    () => value,
                    v =>
                    {
                        value = v;
                        SetBaseScaleInternal(kind, v);
                    },
                    scale,
                    duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnKill(() => _baseTweens.Remove(kind));

            _baseTweens[kind] = tween;
        }

        // ----------------------------------------------------------------
        // 内部メソッド
        // ----------------------------------------------------------------

        void Recalculate(bool force = false)
        {
            _effective.Clear();

            foreach (TimeScaleKind kind in Enum.GetValues(typeof(TimeScaleKind)))
            {
                _effective[kind] = Mathf.Max(0f, GetBaseScale(kind));
            }

            // Unity への適用（常に全 Kind の min）
            float unity = GetCompositeScale(TimeScaleKindMask.All);
            if (!force && Mathf.Approximately(_unityTimeScale, unity))
                return;

            _unityTimeScale = unity;

            UnityEngine.Time.timeScale = _unityTimeScale;
            UnityEngine.Time.fixedDeltaTime = _baseFixedDeltaTime * Mathf.Max(0.0001f, _unityTimeScale);

            UnityTimeScaleChanged?.Invoke();
        }

        void SetBaseScaleInternal(TimeScaleKind kind, float scale)
        {
            _base[kind] = Mathf.Max(0f, scale);
            Recalculate();
        }

        void KillBaseTween(TimeScaleKind kind)
        {
            if (_baseTweens.TryGetValue(kind, out var tween))
            {
                _baseTweens.Remove(kind);
                if (tween != null && tween.active)
                    tween.Kill();
            }
        }

        void KillAllBaseTweens()
        {
            if (_baseTweens.Count == 0)
                return;

            var kinds = new List<TimeScaleKind>(_baseTweens.Keys);
            for (int i = 0; i < kinds.Count; i++)
            {
                KillBaseTween(kinds[i]);
            }
        }
    }
}
