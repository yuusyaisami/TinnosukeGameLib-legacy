// Game.StatusEffect.BaseEffectRuntime.cs
//
// StatusEffect のランタイム基底クラス

using System;
using Game.Common;
using Game.Health;
using Game.Scalar;
using UnityEngine;
using ScalarKey = Game.Scalar.ScalarKey;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect のランタイム基底クラス。
    /// 全ての効果はこのクラスを継承して実装する。
    /// Duration, Intensity 等のパラメータは ScalarKey を通じて管理される。
    /// </summary>
    public abstract class BaseEffectRuntime
    {
        // ================================================================
        // 抽象プロパティ（派生クラスで実装必須）
        // ================================================================

        /// <summary>効果の一意 ID（通常はクラス名ベース）</summary>
        public abstract string EffectId { get; }

        /// <summary>効果タイプ</summary>
        public abstract EffectType Type { get; }

        // ================================================================
        // 表示用データ（派生でオーバーライド）
        // ================================================================

        /// <summary>
        /// 表示用データ。ProfileSO から取得するか、派生クラスでオーバーライド。
        /// </summary>
        public virtual EffectVisualData VisualData => _defaultVisualData;
        static readonly EffectVisualData _defaultVisualData = new()
        {
            DisplayName = "Unknown Effect",
            Description = "",
            EffectType = EffectType.Neutral,
            SortOrder = 0
        };

        /// <summary>UI 表示名（VisualData.DisplayName のショートカット）</summary>
        public string DisplayName => VisualData?.DisplayName ?? EffectId;

        /// <summary>アイコン（VisualData.Icon のショートカット）</summary>
        public Sprite Icon => VisualData?.Icon;

        /// <summary>効果の説明文</summary>
        public string Description => VisualData?.Description ?? string.Empty;

        // ================================================================
        // ランタイム状態
        // ================================================================

        /// <summary>コンテキスト</summary>
        protected EffectContext Context { get; private set; }

        /// <summary>設定</summary>
        protected EffectConfig Config => Context.Config;

        /// <summary>総持続時間（秒）</summary>
        public float TotalDuration { get; private set; }

        /// <summary>残り時間（秒）。-1 で永続。</summary>
        public float RemainingTime { get; private set; }

        /// <summary>現在の強度</summary>
        public float Intensity { get; protected set; }

        /// <summary>スタック数</summary>
        public int StackCount { get; protected set; } = 1;

        /// <summary>
        /// イベント発行時に追加したいペイロード。Effect 独自の情報を設定する。
        /// PublishEffectApplied/Removed で標準ペイロードとマージされる。
        /// </summary>
        public IVarStore EventPayload { get; } = new VarStore();

        /// <summary>永続効果かどうか</summary>
        public bool IsPermanent => TotalDuration < 0f;

        /// <summary>終了要求されているか</summary>
        public bool IsRemoveRequested { get; private set; }

        /// <summary>初期化済みか</summary>
        public bool IsInitialized { get; private set; }

        // ================================================================
        // 内部フラグ
        // ================================================================

        bool _applied;

        // ================================================================
        // ライフサイクル（StatusEffectService から呼ばれる）
        // ================================================================

        /// <summary>
        /// 初期化
        /// </summary>
        internal void Initialize(EffectContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            TotalDuration = context.Config.Duration;
            RemainingTime = TotalDuration;
            Intensity = context.Config.Intensity;
            IsInitialized = true;

            OnInitialize();
        }

        /// <summary>
        /// 適用（初回のみ）
        /// </summary>
        internal void Apply()
        {
            if (_applied)
                return;

            _applied = true;
            OnApply();
        }

        /// <summary>
        /// 毎フレーム更新
        /// </summary>
        internal void Tick(float deltaTime)
        {
            if (!_applied || IsRemoveRequested)
                return;

            // 時間経過
            if (!IsPermanent)
            {
                RemainingTime -= deltaTime;
                if (RemainingTime <= 0f)
                {
                    RequestRemove();
                    return;
                }
            }

            OnTick(deltaTime);
        }

        /// <summary>
        /// 削除要求
        /// </summary>
        public void RequestRemove()
        {
            if (IsRemoveRequested)
                return;
            IsRemoveRequested = true;
        }

        /// <summary>
        /// 削除実行（StatusEffectService から呼ばれる）
        /// </summary>
        internal void Remove()
        {
            if (!_applied)
                return;

            OnRemove();
            _applied = false;
            IsInitialized = false;
        }

        /// <summary>
        /// スタッキング処理（同一効果が重複適用された時）
        /// </summary>
        internal void Stack(EffectConfig newConfig)
        {
            switch (newConfig.StackMode)
            {
                case EffectStackMode.Refresh:
                    RemainingTime = newConfig.Duration;
                    TotalDuration = newConfig.Duration;
                    OnStackRefresh(newConfig);
                    break;

                case EffectStackMode.ExtendDuration:
                    RemainingTime += newConfig.Duration;
                    TotalDuration += newConfig.Duration;
                    OnStackExtend(newConfig);
                    break;

                case EffectStackMode.StackIntensity:
                    Intensity += newConfig.Intensity;
                    StackCount++;
                    OnStackIntensity(newConfig);
                    break;

                case EffectStackMode.StackBoth:
                    RemainingTime += newConfig.Duration;
                    TotalDuration += newConfig.Duration;
                    Intensity += newConfig.Intensity;
                    StackCount++;
                    OnStackBoth(newConfig);
                    break;

                case EffectStackMode.Replace:
                    RemainingTime = newConfig.Duration;
                    TotalDuration = newConfig.Duration;
                    Intensity = newConfig.Intensity;
                    StackCount = 1;
                    OnStackReplace(newConfig);
                    break;

                case EffectStackMode.Ignore:
                default:
                    // 何もしない
                    break;
            }
        }

        /// <summary>
        /// 現在の状態を取得
        /// </summary>
        public EffectState GetState()
        {
            return new EffectState(
                EffectId,
                DisplayName,
                Icon,
                Type,
                RemainingTime,
                TotalDuration,
                Intensity,
                StackCount
            );
        }

        // ================================================================
        // 派生クラスでオーバーライド
        // ================================================================

        /// <summary>初期化時（一度だけ）</summary>
        protected virtual void OnInitialize() { }

        /// <summary>効果適用時（一度だけ）</summary>
        protected virtual void OnApply() { }

        /// <summary>効果削除時（一度だけ）</summary>
        protected virtual void OnRemove() { }

        /// <summary>毎フレーム処理</summary>
        protected virtual void OnTick(float deltaTime) { }

        /// <summary>スタッキング: Refresh 時</summary>
        protected virtual void OnStackRefresh(EffectConfig newConfig) { }

        /// <summary>スタッキング: ExtendDuration 時</summary>
        protected virtual void OnStackExtend(EffectConfig newConfig) { }

        /// <summary>スタッキング: StackIntensity 時</summary>
        protected virtual void OnStackIntensity(EffectConfig newConfig) { }

        /// <summary>スタッキング: StackBoth 時</summary>
        protected virtual void OnStackBoth(EffectConfig newConfig) { }

        /// <summary>スタッキング: Replace 時</summary>
        protected virtual void OnStackReplace(EffectConfig newConfig) { }

        // ================================================================
        // ヘルパーメソッド
        // ================================================================

        /// <summary>
        /// BoolLayer にフラグを設定
        /// </summary>
        protected void SetFlag(string key, bool value)
        {
            Context.EffectFlagLayer?.Set(key, value);
        }

        /// <summary>
        /// BoolLayer からフラグを削除
        /// </summary>
        protected void RemoveFlag(string key)
        {
            Context.EffectFlagLayer?.Remove(key);
        }

        /// <summary>
        /// Scalar に Add 効果を適用
        /// </summary>
        protected ScalarHandle AddScalar(ScalarKey key, string layer, float delta, float duration = -1f)
        {
            return Context.ScalarService?.LocalAdd(key, layer, delta, duration, this, EffectId)
                ?? default;
        }

        /// <summary>
        /// Scalar に Mul 効果を適用
        /// </summary>
        protected ScalarHandle MulScalar(ScalarKey key, string layer, float factor,
            ScalarMulPhase phase = ScalarMulPhase.PreAdd, float duration = -1f)
        {
            return Context.ScalarService?.LocalMul(key, layer, factor, phase, duration, this, EffectId)
                ?? default;
        }

        /// <summary>
        /// Scalar 値を取得
        /// </summary>
        protected float GetScalar(ScalarKey key)
        {
            return Context.ScalarService?.LocalGet(key) ?? 0f;
        }

        /// <summary>
        /// Blackboard に値を設定
        /// </summary>
        protected void SetBlackboard<T>(string key, T value)
        {
            var bb = Context.BlackboardService;
            if (bb == null)
                return;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return;

            var vars = bb.LocalVars;
            if (vars == null)
                return;

            if (value is int i) { vars.TrySetVariant(varId, DynamicVariant.FromInt(i)); return; }
            if (value is float f) { vars.TrySetVariant(varId, DynamicVariant.FromFloat(f)); return; }
            if (value is bool b) { vars.TrySetVariant(varId, DynamicVariant.FromBool(b)); return; }
            if (value is string s) { vars.TrySetVariant(varId, DynamicVariant.FromString(s)); return; }
            if (value is Vector2 v2) { vars.TrySetVariant(varId, DynamicVariant.FromVector2(v2)); return; }
            if (value is Vector3 v3) { vars.TrySetVariant(varId, DynamicVariant.FromVector3(v3)); return; }
            if (value is Color c) { vars.TrySetVariant(varId, DynamicVariant.FromColor(c)); return; }
            if (value is UnityEngine.Object uo) { vars.TrySetVariant(varId, DynamicVariant.FromUnityObject(uo)); return; }

            vars.TrySetManagedRef(varId, value!);
        }

        /// <summary>
        /// Blackboard から値を取得
        /// </summary>
        protected T GetBlackboard<T>(string key, T defaultValue = default)
        {
            var bb = Context.BlackboardService;
            if (bb == null)
                return defaultValue;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return defaultValue;

            var vars = bb.LocalVars;
            if (vars != null && vars.TryGetVariant(varId, out var variant) && variant.TryGet(out T value))
                return value;

            if (vars != null && vars.TryGetManagedRef(varId, out var managed) && managed is T typed)
                return typed;

            return defaultValue;
        }
    }
}
