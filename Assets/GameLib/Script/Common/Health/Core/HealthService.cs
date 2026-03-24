#nullable enable
// Game.Health.HealthService.cs
//
// Health 管理サービス実装 (v0.2)
// - SO ベースの Modifier システム
// - BoolLayer による無敵管理
// - Tick 処理

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Events.Generated;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.VarStoreKeys;
using Game.Vars.Generated;

namespace Game.Health
{
    /// <summary>
    /// Health 管理サービス実装 (v0.2)。
    /// SO ベースの Modifier システムを使用。
    /// </summary>
    public sealed class HealthService : IHealthService, ITickable, IDisposable
    {
        readonly IScopeNode _scope;
        readonly IBaseScalarService _scalarService;
        readonly IBlackboardService _blackboardService;
        readonly IEntityEventService _eventService;
        readonly IScopeBindingRegistry _profileRegistry;
        readonly VNext.ICommandRunner _commandRunner;
        readonly Transform _transform;

        // SO ベースの Modifier
        readonly List<HealthModifierRuntime> _modifierRuntimes = new(8);
        readonly Dictionary<string, HealthModifierRuntime> _modifierById = new(8, StringComparer.Ordinal);

        // 無敵管理（BoolLayer）
        readonly BoolLayer _invincibleLayer;
        const string DamageInvincibleLayerKey = "HealthService.DamageInvincible";

        // 状態
        bool _isDead;
        bool _disposed;
        float _initialMaxHP;
        bool _enableInvincibleOnDamaged;
        float _invincibleDurationOnDamaged;
        float _damageInvincibleRemaining;
        bool _lastInvincibleState;
        float _lastSyncedCurrentHP = float.NaN;
        float _lastSyncedMaxHP = float.NaN;
        float _lastSyncedHPRatio = float.NaN;

        // ModifierContext（Runtime 生成時に渡す）
        HealthModifierContext _modifierContext;

        // ScalarKey（直接参照）
        static readonly ScalarKey CurrentHPKey = new(ScalarKeys.GameLib.Health.Current);
        static readonly ScalarKey MaxHPKey = new(ScalarKeys.GameLib.Health.Max);
        static readonly ScalarKey HPRatioKey = new(ScalarKeys.GameLib.Health.Ratio);

        // EventKey（直接参照）
        static readonly string OnDamageKey = EventKeys.GameLib.Health.OnDamage;
        static readonly string OnHealKey = EventKeys.GameLib.Health.OnHeal;
        static readonly string OnDeathKey = EventKeys.GameLib.Health.OnDeath;
        static readonly string OnHPChangedKey = EventKeys.GameLib.Health.OnHPChanged;
        static readonly string OnReviveKey = EventKeys.GameLib.Health.OnRevive;
        static readonly string OnInvincibleStartedKey = HealthRuntimeEventKeys.OnInvincibleStarted;
        static readonly string OnInvincibleEndedKey = HealthRuntimeEventKeys.OnInvincibleEnded;

        public float CurrentHP => _scalarService.LocalGet(CurrentHPKey);
        public float MaxHP => _scalarService.LocalGet(MaxHPKey);
        public float HPRatio => MaxHP > 0f ? Mathf.Clamp01(CurrentHP / MaxHP) : 0f;
        public bool IsDead => _isDead;
        public bool IsInvincible => _invincibleLayer.Value;

        /// <summary>
        /// 無敵制御用の BoolLayer（StatusEffect から操作可能）
        /// </summary>
        public BoolLayer InvincibleLayer => _invincibleLayer;

        public HealthService(
            IScopeNode scope,
            IBaseScalarService scalarService,
            IBlackboardService blackboardService,
            IEntityEventService eventService,
            IScopeBindingRegistry profileRegistry,
            VNext.ICommandRunner commandRunner,
            Transform transform)
        {
            _scope = scope;
            _scalarService = scalarService ?? throw new ArgumentNullException(nameof(scalarService));
            _blackboardService = blackboardService;
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _profileRegistry = profileRegistry;
            _commandRunner = commandRunner;
            _transform = transform;
            _invincibleLayer = new BoolLayer(BoolCompositionMode.AnyTrue);
            _lastInvincibleState = _invincibleLayer.Value;

            // ModifierContext を作成
            _modifierContext = new HealthModifierContext(
                this,
                scope,
                scalarService,
                blackboardService,
                eventService,
                commandRunner,
                transform
            );

            // ProfileSO から初期値を取得
            InitializeFromProfile();
            SyncHPRatio(force: true);
        }

        void InitializeFromProfile()
        {
            if (TryResolveProfile(out var profile) && profile != null)
            {
                _initialMaxHP = profile.MaxHPFallback;

                // Profile の ScalarKey 登録は ScopeBindingRegistryService が行う
                // ここでは CurrentHP の初期化のみ
                float initialHP = profile.InitialHPMode switch
                {
                    HealthInitialHPMode.CustomValue => profile.InitialHPValue,
                    _ => _initialMaxHP * profile.InitialHPRatio,
                };
                initialHP = Mathf.Clamp(initialHP, 0f, Mathf.Max(0f, _initialMaxHP));
                _scalarService.SetRuntimeBaseline(CurrentHPKey, initialHP);
                SyncHPRatio(force: true);

                // スポーン時無敵
                if (profile.InvincibleDurationOnSpawn > 0f)
                {
                    // StatusEffectSystem 経由で設定されることを想定
                    // または内部タイマーで制御
                }

                _enableInvincibleOnDamaged = profile.EnableInvincibleOnDamaged;
                _invincibleDurationOnDamaged = Mathf.Max(0f, profile.InvincibleDurationOnDamaged);
            }
            else
            {
                // Profile がない場合のデフォルト
                _initialMaxHP = 100f;
                _scalarService.SetRuntimeBaseline(MaxHPKey, _initialMaxHP);
                _scalarService.SetRuntimeBaseline(CurrentHPKey, _initialMaxHP);
                SyncHPRatio(force: true);
                _enableInvincibleOnDamaged = false;
                _invincibleDurationOnDamaged = 0f;
            }
        }

        bool TryResolveProfile(out HealthPreset? profile)
        {
            profile = null;
            if (_profileRegistry == null)
                return false;

            if (!_profileRegistry.TryResolveDefinition<HealthPreset>(out var preset) || preset == null)
                return false;

            profile = preset;
            return true;
        }

        // ================================================================
        // Modifier 管理（SO ベース）
        // ================================================================

        /// <summary>
        /// ModifierSO を登録。
        /// </summary>
        public void RegisterModifier(BaseHealthModifierSO so)
        {
            if (so == null)
                return;

            if (!so.Enabled)
                return;

            if (_modifierById.ContainsKey(so.ModifierId))
            {
                Debug.LogWarning($"[HealthService] Modifier already exists: {so.ModifierId}. Skipping.");
                return;
            }

            var runtime = so.CreateRuntime(_modifierContext);
            _modifierRuntimes.Add(runtime);
            _modifierById[so.ModifierId] = runtime;

            // 優先度でソート
            _modifierRuntimes.Sort((a, b) => a.SO.Priority.CompareTo(b.SO.Priority));

            // 初期化コールバック
            so.OnInitialize(runtime);
        }

        /// <summary>
        /// ModifierId で Modifier を削除。
        /// </summary>
        public void UnregisterModifier(string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId))
                return;

            if (!_modifierById.TryGetValue(modifierId, out var runtime))
                return;

            _modifierRuntimes.Remove(runtime);
            _modifierById.Remove(modifierId);
            runtime.Dispose();
        }

        /// <summary>
        /// 指定 ModifierId の Runtime を取得。
        /// </summary>
        public bool TryGetModifierRuntime(string modifierId, out HealthModifierRuntime runtime)
        {
            return _modifierById.TryGetValue(modifierId, out runtime);
        }

        // ================================================================
        // ITickable
        // ================================================================

        public void Tick()
        {
            if (_disposed)
                return;

            float dt = Time.deltaTime;

            TickDamageInvincible(dt);

            // 全 ModifierRuntime の Tick を実行
            for (int i = 0; i < _modifierRuntimes.Count; i++)
            {
                var runtime = _modifierRuntimes[i];
                if (runtime.SO.Enabled)
                {
                    runtime.SO.OnTick(runtime, dt);
                }
            }

            // HP Ratio は変化時のみ同期する
            SyncHPRatioIfNeeded();
            PublishInvincibleTransitionIfChanged();
        }

        void TickDamageInvincible(float dt)
        {
            if (_damageInvincibleRemaining <= 0f)
                return;

            _damageInvincibleRemaining -= Mathf.Max(0f, dt);
            if (_damageInvincibleRemaining > 0f)
                return;

            _damageInvincibleRemaining = 0f;
            _invincibleLayer.Set(DamageInvincibleLayerKey, false);
        }

        void SyncHPRatioIfNeeded()
        {
            SyncHPRatio(force: false);
        }

        void SyncHPRatio(bool force)
        {
            float currentHP = CurrentHP;
            float maxHP = MaxHP;

            if (!force &&
                Mathf.Approximately(currentHP, _lastSyncedCurrentHP) &&
                Mathf.Approximately(maxHP, _lastSyncedMaxHP))
            {
                return;
            }

            _lastSyncedCurrentHP = currentHP;
            _lastSyncedMaxHP = maxHP;

            float ratio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
            if (!force && Mathf.Approximately(ratio, _lastSyncedHPRatio))
                return;

            _lastSyncedHPRatio = ratio;
            _scalarService.SetRuntimeBaseline(HPRatioKey, ratio);
        }

        // ================================================================
        // Damage / Heal
        // ================================================================

        public float ApplyDamage(ref DamageContext context)
        {
            if (_isDead || _disposed)
                return 0f;

            // 無敵チェック
            if (IsInvincible)
            {
                // ダメージを 0 にしてイベントは発行（ExtraPayload に isInvincible を追加）
                context.ExtraPayload ??= new VarStore();
                SetVariant(context.ExtraPayload, VarIds.GameLib.Health.isInvincible, DynamicVariant.FromBool(true));
                context.FinalDamage = 0f;
                context.WasBlocked = true;

                // イベント発行
                PublishDamageEvent(context, CurrentHP, CurrentHP);
                return 0f;
            }

            // モディファイア適用
            for (int i = 0; i < _modifierRuntimes.Count; i++)
            {
                var runtime = _modifierRuntimes[i];
                if (!runtime.SO.Enabled)
                    continue;

                if (!runtime.SO.OnDamage(runtime, ref context))
                    break;
            }

            context.FinalDamage = Mathf.Max(0f, context.BaseDamage);

            if (context.FinalDamage <= 0f)
                return 0f;

            float prevHP = CurrentHP;
            float newHP = Mathf.Max(0f, prevHP - context.FinalDamage);
            _scalarService.SetRuntimeBaseline(CurrentHPKey, newHP);
            SyncHPRatio(force: true);

            if (_enableInvincibleOnDamaged && _invincibleDurationOnDamaged > 0f)
                StartDamageInvincible(_invincibleDurationOnDamaged);

            // イベント発行
            PublishDamageEvent(context, prevHP, newHP);

            // 死亡判定
            if (newHP <= 0f && !_isDead)
            {
                _isDead = true;
                StopDamageInvincible();
                PublishDeathEvent(context);
            }

            return context.FinalDamage;
        }

        public float ApplyHeal(ref HealContext context)
        {
            if (_isDead || _disposed)
                return 0f;

            // 無敵チェック（回復は無敵中もブロックする仕様の場合はここで判定）
            // 仕様では無敵はダメージのみブロックなので、回復は通す

            // モディファイア適用
            for (int i = 0; i < _modifierRuntimes.Count; i++)
            {
                var runtime = _modifierRuntimes[i];
                if (!runtime.SO.Enabled)
                    continue;

                if (!runtime.SO.OnHeal(runtime, ref context))
                    break;
            }

            context.FinalHeal = Mathf.Max(0f, context.BaseHeal);

            if (context.FinalHeal <= 0f)
                return 0f;

            float prevHP = CurrentHP;
            float maxHP = MaxHP;
            float newHP = Mathf.Min(maxHP, prevHP + context.FinalHeal);
            context.ActualHeal = newHP - prevHP;

            _scalarService.SetRuntimeBaseline(CurrentHPKey, newHP);
            SyncHPRatio(force: true);

            // イベント発行
            PublishHealEvent(context, prevHP, newHP);

            return context.ActualHeal;
        }

        public void Kill()
        {
            if (_isDead)
                return;

            _scalarService.SetRuntimeBaseline(CurrentHPKey, 0f);
            SyncHPRatio(force: true);
            _isDead = true;
            StopDamageInvincible();

            var ctx = new DamageContext
            {
                BaseDamage = float.MaxValue,
                DamageType = DamageType.Pure,
                Tag = "Kill"
            };
            PublishDeathEvent(ctx);
        }

        public void Revive(float hpRatio = 1f)
        {
            if (!_isDead)
                return;

            float maxHP = MaxHP;
            float newHP = maxHP * Mathf.Clamp01(hpRatio);
            _scalarService.SetRuntimeBaseline(CurrentHPKey, newHP);
            SyncHPRatio(force: true);
            _isDead = false;

            PublishReviveEvent(newHP);
        }

        public void SetHP(float hp)
        {
            float maxHP = MaxHP;
            float newHP = Mathf.Clamp(hp, 0f, maxHP);
            _scalarService.SetRuntimeBaseline(CurrentHPKey, newHP);
            SyncHPRatio(force: true);

            if (newHP <= 0f && !_isDead)
            {
                _isDead = true;
                StopDamageInvincible();
                PublishDeathEvent(default);
            }
            else if (newHP > 0f && _isDead)
            {
                _isDead = false;
            }
        }

        public void SetMaxHP(float maxHP)
        {
            maxHP = Mathf.Max(1f, maxHP);
            _scalarService.SetRuntimeBaseline(MaxHPKey, maxHP);

            // CurrentHP が MaxHP を超えている場合は調整
            float currentHP = CurrentHP;
            if (currentHP > maxHP)
            {
                _scalarService.SetRuntimeBaseline(CurrentHPKey, maxHP);
            }

            SyncHPRatio(force: true);
        }

        // ================================================================
        // イベント発行
        // ================================================================

        void PublishDamageEvent(in DamageContext context, float prevHP, float newHP)
        {
            var payload = new VarStore();
            SetVariant(payload, VarIds.GameLib.Health.damage, DynamicVariant.FromFloat(context.FinalDamage));
            SetVariant(payload, VarIds.GameLib.Health.damageType, DynamicVariant.FromInt((int)context.DamageType));
            SetVariant(payload, VarIds.GameLib.Health.isCritical, DynamicVariant.FromBool(context.IsCritical));
            SetVariant(payload, VarIds.GameLib.Health.wasBlocked, DynamicVariant.FromBool(context.WasBlocked));
            SetVariant(payload, VarIds.GameLib.Health.wasDodged, DynamicVariant.FromBool(context.WasDodged));
            SetVariant(payload, VarIds.GameLib.Health.prevHP, DynamicVariant.FromFloat(prevHP));
            SetVariant(payload, VarIds.GameLib.Health.newHP, DynamicVariant.FromFloat(newHP));
            SetManagedRef(payload, VarIds.GameLib.Health.source, context.Source ?? NullVarStore.Instance);
            SetVariant(payload, VarIds.GameLib.Health.tag, DynamicVariant.FromString(context.Tag ?? string.Empty));

            context.ExtraPayload?.MergeInto(payload, overwrite: true);

            _eventService.PublishAsync(OnDamageKey, payload).Forget(ex => Debug.LogException(ex));
            _eventService.PublishAsync(OnHPChangedKey, payload).Forget(ex => Debug.LogException(ex));
        }

        void PublishHealEvent(in HealContext context, float prevHP, float newHP)
        {
            var payload = new VarStore();
            SetVariant(payload, VarIds.GameLib.Health.heal, DynamicVariant.FromFloat(context.ActualHeal));
            SetVariant(payload, VarIds.GameLib.Health.healType, DynamicVariant.FromInt((int)context.HealType));
            SetVariant(payload, VarIds.GameLib.Health.prevHP, DynamicVariant.FromFloat(prevHP));
            SetVariant(payload, VarIds.GameLib.Health.newHP, DynamicVariant.FromFloat(newHP));
            SetManagedRef(payload, VarIds.GameLib.Health.source, context.Source ?? NullVarStore.Instance);
            SetVariant(payload, VarIds.GameLib.Health.tag, DynamicVariant.FromString(context.Tag ?? string.Empty));

            context.ExtraPayload?.MergeInto(payload, overwrite: true);

            _eventService.PublishAsync(OnHealKey, payload).Forget(ex => Debug.LogException(ex));
            _eventService.PublishAsync(OnHPChangedKey, payload).Forget(ex => Debug.LogException(ex));
        }

        void PublishDeathEvent(in DamageContext context)
        {
            var payload = new VarStore();
            SetVariant(payload, VarIds.GameLib.Health.damageType, DynamicVariant.FromInt((int)context.DamageType));
            SetManagedRef(payload, VarIds.GameLib.Health.source, context.Source ?? NullVarStore.Instance);
            SetVariant(payload, VarIds.GameLib.Health.tag, DynamicVariant.FromString(context.Tag ?? string.Empty));

            _eventService.PublishAsync(OnDeathKey, payload).Forget(ex => Debug.LogException(ex));
        }

        void PublishReviveEvent(float newHP)
        {
            var payload = new VarStore();
            SetVariant(payload, VarIds.GameLib.Health.newHP, DynamicVariant.FromFloat(newHP));

            _eventService.PublishAsync(OnReviveKey, payload).Forget(ex => Debug.LogException(ex));
        }

        void StartDamageInvincible(float duration)
        {
            _damageInvincibleRemaining = Mathf.Max(0f, duration);
            _invincibleLayer.Set(DamageInvincibleLayerKey, _damageInvincibleRemaining > 0f);
            PublishInvincibleTransitionIfChanged();
        }

        void StopDamageInvincible()
        {
            _damageInvincibleRemaining = 0f;
            _invincibleLayer.Set(DamageInvincibleLayerKey, false);
            PublishInvincibleTransitionIfChanged();
        }

        void PublishInvincibleTransitionIfChanged()
        {
            var current = IsInvincible;
            if (current == _lastInvincibleState)
                return;

            _lastInvincibleState = current;
            var payload = new VarStore();
            SetVariant(payload, VarIds.GameLib.Health.isInvincible, DynamicVariant.FromBool(current));
            SetVariant(payload, VarIds.GameLib.Health.tag, DynamicVariant.FromString(DamageInvincibleLayerKey));

            var key = current ? OnInvincibleStartedKey : OnInvincibleEndedKey;
            _eventService.PublishAsync(key, payload).Forget(ex => Debug.LogException(ex));
        }

        static void SetVariant(IVarStore vars, int varId, in DynamicVariant variant)
        {
            if (vars == null) return;
            vars.TrySetVariant(varId, variant);
        }

        static void SetManagedRef(IVarStore vars, int varId, object value)
        {
            if (vars == null) return;
            if (value == null) return;
            vars.TrySetManagedRef(varId, value);
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            // 全 ModifierRuntime を破棄
            for (int i = 0; i < _modifierRuntimes.Count; i++)
            {
                _modifierRuntimes[i].Dispose();
            }
            _modifierRuntimes.Clear();
            _modifierById.Clear();
            _damageInvincibleRemaining = 0f;
            _invincibleLayer.Set(DamageInvincibleLayerKey, false);
            _lastInvincibleState = _invincibleLayer.Value;
            _lastSyncedCurrentHP = float.NaN;
            _lastSyncedMaxHP = float.NaN;
            _lastSyncedHPRatio = float.NaN;
        }
    }
}
