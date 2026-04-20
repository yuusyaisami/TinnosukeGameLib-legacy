#nullable enable
// Game.Health.HealthService.cs
//
// Health 邂｡逅・し繝ｼ繝薙せ螳溯｣・(v0.2)
// - SO 繝吶・繧ｹ縺ｮ Modifier 繧ｷ繧ｹ繝・Β
// - BoolLayer 縺ｫ繧医ｋ辟｡謨ｵ邂｡逅・
// - Tick 蜃ｦ逅・

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
    /// Health 邂｡逅・し繝ｼ繝薙せ螳溯｣・(v0.2)縲・
    /// SO 繝吶・繧ｹ縺ｮ Modifier 繧ｷ繧ｹ繝・Β繧剃ｽｿ逕ｨ縲・
    /// </summary>
    public sealed class HealthService : IHealthService, IScopeTickHandler, IDisposable
    {
        readonly IScopeNode _scope;
        readonly IBaseScalarService _scalarService;
        readonly IBlackboardService _blackboardService;
        readonly IEntityEventService _eventService;
        readonly IScopeBindingRegistry _profileRegistry;
        readonly VNext.ICommandRunner _commandRunner;
        readonly Transform _transform;

        // SO 繝吶・繧ｹ縺ｮ Modifier
        readonly List<HealthModifierRuntime> _modifierRuntimes = new(8);
        readonly Dictionary<string, HealthModifierRuntime> _modifierById = new(8, StringComparer.Ordinal);

        // 辟｡謨ｵ邂｡逅・ｼ・oolLayer・・
        readonly BoolLayer _invincibleLayer;
        const string DamageInvincibleLayerKey = "HealthService.DamageInvincible";

        // 迥ｶ諷・
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

        // ModifierContext・・untime 逕滓・譎ゅ↓貂｡縺呻ｼ・
        HealthModifierContext _modifierContext;

        // ScalarKey・育峩謗･蜿ら・・・
        static readonly ScalarKey CurrentHPKey = new(ScalarKeys.GameLib.Health.Current);
        static readonly ScalarKey MaxHPKey = new(ScalarKeys.GameLib.Health.Max);
        static readonly ScalarKey HPRatioKey = new(ScalarKeys.GameLib.Health.Ratio);

        // EventKey・育峩謗･蜿ら・・・
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
        /// 辟｡謨ｵ蛻ｶ蠕｡逕ｨ縺ｮ BoolLayer・・tatusEffect 縺九ｉ謫堺ｽ懷庄閭ｽ・・
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

            // ModifierContext 繧剃ｽ懈・
            _modifierContext = new HealthModifierContext(
                this,
                scope,
                scalarService,
                blackboardService,
                eventService,
                commandRunner,
                transform
            );

            // ProfileSO 縺九ｉ蛻晄悄蛟､繧貞叙蠕・
            InitializeFromProfile();
            SyncHPRatio(force: true);
        }

        void InitializeFromProfile()
        {
            if (TryResolveProfile(out var profile) && profile != null)
            {
                _initialMaxHP = profile.MaxHPFallback;

                // Profile 縺ｮ ScalarKey 逋ｻ骭ｲ縺ｯ ScopeBindingRegistryService 縺瑚｡後≧
                // 縺薙％縺ｧ縺ｯ CurrentHP 縺ｮ蛻晄悄蛹悶・縺ｿ
                float initialHP = profile.InitialHPMode switch
                {
                    HealthInitialHPMode.CustomValue => profile.InitialHPValue,
                    _ => _initialMaxHP * profile.InitialHPRatio,
                };
                initialHP = Mathf.Clamp(initialHP, 0f, Mathf.Max(0f, _initialMaxHP));
                _scalarService.SetRuntimeBaseline(CurrentHPKey, initialHP);
                SyncHPRatio(force: true);

                // 繧ｹ繝昴・繝ｳ譎ら┌謨ｵ
                if (profile.InvincibleDurationOnSpawn > 0f)
                {
                    // StatusEffectSystem 邨檎罰縺ｧ險ｭ螳壹＆繧後ｋ縺薙→繧呈Φ螳・
                    // 縺ｾ縺溘・蜀・Κ繧ｿ繧､繝槭・縺ｧ蛻ｶ蠕｡
                }

                _enableInvincibleOnDamaged = profile.EnableInvincibleOnDamaged;
                _invincibleDurationOnDamaged = Mathf.Max(0f, profile.InvincibleDurationOnDamaged);
            }
            else
            {
                // Profile 縺後↑縺・ｴ蜷医・繝・ヵ繧ｩ繝ｫ繝・
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
        // Modifier 邂｡逅・ｼ・O 繝吶・繧ｹ・・
        // ================================================================

        /// <summary>
        /// ModifierSO 繧堤匳骭ｲ縲・
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

            // 蜆ｪ蜈亥ｺｦ縺ｧ繧ｽ繝ｼ繝・
            _modifierRuntimes.Sort((a, b) => a.SO.Priority.CompareTo(b.SO.Priority));

            // 蛻晄悄蛹悶さ繝ｼ繝ｫ繝舌ャ繧ｯ
            so.OnInitialize(runtime);
        }

        /// <summary>
        /// ModifierId 縺ｧ Modifier 繧貞炎髯､縲・
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
        /// 謖・ｮ・ModifierId 縺ｮ Runtime 繧貞叙蠕励・
        /// </summary>
        public bool TryGetModifierRuntime(string modifierId, out HealthModifierRuntime runtime)
        {
            return _modifierById.TryGetValue(modifierId, out runtime);
        }

        // ================================================================
        // IScopeTickHandler
        // ================================================================

        public void Tick()
        {
            if (_disposed)
                return;

            float dt = Time.deltaTime;

            TickDamageInvincible(dt);

            // 蜈ｨ ModifierRuntime 縺ｮ Tick 繧貞ｮ溯｡・
            for (int i = 0; i < _modifierRuntimes.Count; i++)
            {
                var runtime = _modifierRuntimes[i];
                if (runtime.SO.Enabled)
                {
                    runtime.SO.OnTick(runtime, dt);
                }
            }

            // HP Ratio 縺ｯ螟牙喧譎ゅ・縺ｿ蜷梧悄縺吶ｋ
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

            // 辟｡謨ｵ繝√ぉ繝・け
            if (IsInvincible)
            {
                // 繝繝｡繝ｼ繧ｸ繧・0 縺ｫ縺励※繧､繝吶Φ繝医・逋ｺ陦鯉ｼ・xtraPayload 縺ｫ isInvincible 繧定ｿｽ蜉・・
                context.ExtraPayload ??= new VarStore();
                SetVariant(context.ExtraPayload, VarIds.GameLib.Health.isInvincible, DynamicVariant.FromBool(true));
                context.FinalDamage = 0f;
                context.WasBlocked = true;

                // 繧､繝吶Φ繝育匱陦・
                PublishDamageEvent(context, CurrentHP, CurrentHP);
                return 0f;
            }

            // 繝｢繝・ぅ繝輔ぃ繧､繧｢驕ｩ逕ｨ
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

            // 繧､繝吶Φ繝育匱陦・
            PublishDamageEvent(context, prevHP, newHP);

            // 豁ｻ莠｡蛻､螳・
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

            // 辟｡謨ｵ繝√ぉ繝・け・亥屓蠕ｩ縺ｯ辟｡謨ｵ荳ｭ繧ゅヶ繝ｭ繝・け縺吶ｋ莉墓ｧ倥・蝣ｴ蜷医・縺薙％縺ｧ蛻､螳夲ｼ・
            // 莉墓ｧ倥〒縺ｯ辟｡謨ｵ縺ｯ繝繝｡繝ｼ繧ｸ縺ｮ縺ｿ繝悶Ο繝・け縺ｪ縺ｮ縺ｧ縲∝屓蠕ｩ縺ｯ騾壹☆

            // 繝｢繝・ぅ繝輔ぃ繧､繧｢驕ｩ逕ｨ
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

            // 繧､繝吶Φ繝育匱陦・
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

            // CurrentHP 縺・MaxHP 繧定ｶ・∴縺ｦ縺・ｋ蝣ｴ蜷医・隱ｿ謨ｴ
            float currentHP = CurrentHP;
            if (currentHP > maxHP)
            {
                _scalarService.SetRuntimeBaseline(CurrentHPKey, maxHP);
            }

            SyncHPRatio(force: true);
        }

        // ================================================================
        // 繧､繝吶Φ繝育匱陦・
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

            // 蜈ｨ ModifierRuntime 繧堤ｴ譽・
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
