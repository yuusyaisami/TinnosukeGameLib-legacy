#nullable enable
using System;
using DG.Tweening;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx サービスのインターフェース。
    /// Renderer や Graphic 単位でレイヤーベースのプロパティ管理を提供する。
    /// </summary>
    public interface IMaterialFxService : IDisposable
    {
        // ── TimeScale 設定 ──
        /// <summary>
        /// true の場合、Tick で unscaledDeltaTime を使用する。
        /// LTS の TimeScaleBehavior.Unscaled に対応。
        /// </summary>
        bool UseUnscaledTime { get; }

        int GetActiveLayerCount(string stableKey, string contextTag = "");

        // ── Layer 操作 ──
        /// <summary>レイヤーに値を設定</summary>
        void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                      MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f);

        /// <summary>レイヤーに値を設定し、値自体をフェード（Value Fade）</summary>
        void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                  float duration = 0f, Ease ease = Ease.Linear,
                  MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f);

        /// <summary>レイヤーの Weight（寄与率）をフェード（0=影響なし, 1=完全適用）</summary>
        void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                float duration = 0f, Ease ease = Ease.Linear);

        /// <summary>レイヤーを削除</summary>
        void RemoveLayer(string stableKey, string contextTag);

        /// <summary>コンテキスト全体をクリア</summary>
        void ClearContext(string contextTag);

        // ── Preset 適用 ──
        /// <summary>プリセットを適用（各 Entry を SetLayer で適用）</summary>
        void ApplyPreset(string contextTag, MaterialFxPresetSO preset, int priority = 0);

        /// <summary>プリセットエントリの列を適用（各 Entry を SetLayer で適用）</summary>
        void ApplyPreset(string contextTag, System.Collections.Generic.IEnumerable<MaterialFxPresetEntry> entries, int priority = 0);

        /// <summary>プリセットをフェードアウトしてクリア（Weight を 0 へ Fade）</summary>
        void FadeOutPreset(string contextTag, float duration, Ease ease = Ease.Linear);

        // ── Tick（システムから呼ばれる） ──
        /// <summary>毎フレーム更新。Fade 更新 → 合成 → Dispatch → Apply</summary>
        void Tick(float deltaTime);

        /// <summary>旧 API 互換（内部で ClearContext を呼ぶ）</summary>
        void Clear(string channelTag);
    }

    /// <summary>
    /// MaterialFxService: Layer → Dispatch を統合したファサード。
    /// Renderer / Graphic 単位で 1 インスタンス生成される。
    /// </summary>
    public sealed class MaterialFxService : IMaterialFxService, IMaterialFxSpriteSync, IMaterialFxTelemetry
    {
        // singleton変数 BaseMaterial - MaterialFxInstallerで設定する
        public static Material? BaseMaterial;

        readonly IMaterialFxLayerService _layer;
        readonly IMaterialFxDispatchService _dispatch;
        readonly IMaterialFxTargetAdapter _adapter;
        readonly IMaterialFxPropertyRegistry _registry;
        readonly IMaterialFxSystemService? _system;
        readonly bool _needsAlwaysApply;

        bool _disposed;

        string _telemetryId = string.Empty;

        public string TelemetryId => _telemetryId;

        /// <summary>
        /// true の場合、Tick で unscaledDeltaTime を使用する。
        /// </summary>
        public bool UseUnscaledTime { get; set; }

        public MaterialFxService(
            IMaterialFxLayerService layer,
            IMaterialFxDispatchService dispatch,
            IMaterialFxTargetAdapter adapter,
            IMaterialFxPropertyRegistry registry,
            IMaterialFxSystemService? system = null,
            bool useUnscaledTime = false)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _system = system;
            UseUnscaledTime = useUnscaledTime;
            _needsAlwaysApply = adapter is SpriteRendererAdapter || adapter is GraphicAdapter;

            _system?.Register(this);
        }

        /// <summary>
        /// Optional debug identifier (owner/channel label). Set by callers that know the context.
        /// </summary>
        public void SetTelemetryId(string id)
        {
            _telemetryId = id ?? string.Empty;
        }

        public void GetSnapshot(System.Collections.Generic.List<MaterialFxStackTelemetry> dst)
        {
            if (dst == null)
                return;

            if (_layer is IMaterialFxLayerTelemetry telemetry)
            {
                telemetry.GetTelemetrySnapshot(dst);
                return;
            }

            dst.Clear();
        }

        // ── Layer 操作 ──
        public void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                             MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
        {
            if (lifetimeSeconds == 0f) lifetimeSeconds = -1f; // 永続化
            _layer.SetLayer(stableKey, contextTag, value, blend, priority, lifetimeSeconds);
        }

        public void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                                 float duration = 0f, Ease ease = Ease.Linear,
                                 MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
        {
            if (lifetimeSeconds == 0f) lifetimeSeconds = -1f;
            _layer.SetLayerFade(stableKey, contextTag, value, duration, ease, blend, priority, lifetimeSeconds);
        }

        public void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                       float duration = 0f, Ease ease = Ease.Linear)
        {
            _layer.SetLayerWeightFade(stableKey, contextTag, targetWeight, duration, ease);
        }

        public void RemoveLayer(string stableKey, string contextTag)
        {
            _layer.RemoveLayer(stableKey, contextTag);
        }

        public void ClearContext(string contextTag)
        {
            _layer.ClearContext(contextTag);
        }
        public int GetActiveLayerCount(string stableKey, string contextTag = "")
        {
            return _layer.GetActiveLayerCount(stableKey, contextTag);
        }

        // ── Preset 適用 ──
        public void ApplyPreset(string contextTag, MaterialFxPresetSO preset, int priority = 0)
        {
            if (preset == null) return;

            foreach (var entry in preset.Entries)
            {
                if (!_registry.TryGetValueType(entry.Key, out var type)) continue;

                var value = entry.Value.ToTypedValue(type);
                if (entry.ApplyWeightFade)
                {
                    // NOTE: Weight Fade は設計上の寄与率だが、実運用では「値そのものの Fade」を期待されるケースが多い。
                    // 互換のため ApplyWeightFade は Value Fade として扱う（TargetWeight は無視）。
                    SetLayerFade(entry.Key, contextTag, value, entry.ResolveFadeDuration(context: null), entry.FadeEase, entry.BlendMode, priority, entry.LifetimeSeconds);
                }
                else
                {
                    SetLayer(entry.Key, contextTag, value, entry.BlendMode, priority, entry.LifetimeSeconds);
                }
            }
        }

        public void ApplyPreset(string contextTag, System.Collections.Generic.IEnumerable<MaterialFxPresetEntry> entries, int priority = 0)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (!_registry.TryGetValueType(entry.Key, out var type)) continue;

                var value = entry.Value.ToTypedValue(type);
                if (entry.ApplyWeightFade)
                {
                    SetLayerFade(entry.Key, contextTag, value, entry.ResolveFadeDuration(context: null), entry.FadeEase, entry.BlendMode, priority, entry.LifetimeSeconds);
                }
                else
                {
                    SetLayer(entry.Key, contextTag, value, entry.BlendMode, priority, entry.LifetimeSeconds);
                }
            }
        }

        public void FadeOutPreset(string contextTag, float duration, Ease ease = Ease.Linear)
        {
            // ★修正: dirty ではなく contextTag を持つ全キーを対象にする
            // Weight を 0 へ Fade することで「このレイヤーの影響を徐々になくす」
            foreach (var key in _layer.GetKeysByContext(contextTag))
            {
                SetLayerWeightFade(key, contextTag, 0f, duration, ease);
            }
        }

        // ── Tick ──
        public void Tick(float deltaTime)
        {
            if (_disposed || !_adapter.IsValid) return;

            // 1. Fade 更新（fading/timed キーが無い場合はスキップ）
            if (_layer.HasFadingOrTimedKeys)
                _layer.UpdateFades(deltaTime);

            // 2. Dirty Keys を取得して合成 → Dispatch
            var dirtyKeys = _layer.GetDirtyKeys();
            if (dirtyKeys.Count == 0)
            {
                // SpriteRenderer/Graphic は外部要因で MPB が戻るケースがあるため、Dirty が無くても毎フレーム Apply する。
                if (_needsAlwaysApply)
                    _dispatch.Apply();
                return;
            }

            for (int i = dirtyKeys.Count - 1; i >= 0; i--)
            {
                var key = dirtyKeys[i];
                if (!_registry.TryGetValueType(key, out _))
                {
                    _layer.ClearDirty(key);
                    continue;
                }

                var finalValue = _layer.ComputeFinalValue(key);
                _dispatch.Dispatch(key, finalValue);

                // ★重要: dirty をクリアしないと毎フレーム無限再送
                _layer.ClearDirty(key);
            }

            // 3. Apply
            _dispatch.Apply();
        }

        public void NotifySpriteChanged(Sprite? sprite)
        {
            if (_disposed || !_adapter.IsValid) return;

            if (_adapter is SpriteRendererAdapter spriteAdapter)
            {
                spriteAdapter.NotifySpriteChanged(sprite);
                _dispatch.Apply();
            }
            else if (_adapter is GraphicAdapter)
            {
                // uGUI の Image は内部で sprite を保持しているため、Apply するだけで UV同期が走る。
                _dispatch.Apply();
            }
        }

        public void NotifyFlipChanged(bool flipX, bool flipY)
        {
            if (_disposed || !_adapter.IsValid) return;

            if (_adapter is SpriteRendererAdapter spriteAdapter)
            {
                spriteAdapter.NotifyFlipChanged(flipX, flipY);
                _dispatch.Apply();
            }
        }


        public void Clear(string channelTag)
        {
            ClearContext(channelTag);
        }

        // ── Dispose ──
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _system?.Unregister(this);
            _adapter.Dispose();
        }
    }

    /// <summary>
    /// No-op fallback to keep systems running when MaterialFx is not wired.
    /// </summary>
    public sealed class NullMaterialFxService : IMaterialFxService, IMaterialFxTelemetry
    {
        public static readonly NullMaterialFxService Instance = new();

        NullMaterialFxService() { }

        public bool UseUnscaledTime => false;

        public string TelemetryId => "Null";

        public void GetSnapshot(System.Collections.Generic.List<MaterialFxStackTelemetry> dst)
        {
            dst?.Clear();
        }

        public void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                             MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
        { }
        public void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                     float duration = 0f, Ease ease = Ease.Linear,
                     MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
        { }
        public void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                       float duration = 0f, Ease ease = Ease.Linear)
        { }
        public int GetActiveLayerCount(string stableKey, string contextTag) { return 0; }
        public void RemoveLayer(string stableKey, string contextTag) { }
        public void ClearContext(string contextTag) { }
        public void ApplyPreset(string contextTag, MaterialFxPresetSO preset, int priority = 0) { }
        public void ApplyPreset(string contextTag, System.Collections.Generic.IEnumerable<MaterialFxPresetEntry> entries, int priority = 0) { }
        public void FadeOutPreset(string contextTag, float duration, Ease ease = Ease.Linear) { }
        public void Tick(float deltaTime) { }
        public void ApplyPreset(string channelTag, MaterialFxPresetSO preset) { }
        public void Clear(string channelTag) { }
        public void Dispose() { }
    }
}
