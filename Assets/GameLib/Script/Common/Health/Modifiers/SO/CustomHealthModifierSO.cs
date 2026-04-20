// Game.Health.CustomHealthModifierSO.cs
//
// 豎守畑蠑上・繝ｼ繧ｹ縺ｮ HealthModifier SO (v0.2)
// - DynamicValue<float> 縺ｫ繧医ｋ蠑剰ｩ穂ｾ｡
// - Extrapayload 繧ｵ繝昴・繝・
// - Tick 蜃ｦ逅・ｼ・nterval 繝吶・繧ｹ・・
// - CommandListData (vNext) 螳溯｡・

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Vars.Generated;

namespace Game.Health
{
    /// <summary>
    /// 豎守畑蠑上・繝ｼ繧ｹ縺ｮ HealthModifier SO縲・
    /// DynamicValue<float> 縺ｧ蠑剰ｩ穂ｾ｡縲・xtrapayload 縺ｧ霑ｽ蜉繝・・繧ｿ豕ｨ蜈･縲・
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Health/CustomHealthModifier", fileName = "CustomHealthModifier")]
    public sealed class CustomHealthModifierSO : BaseHealthModifierSO
    {
        // ================================================================
        // Damage Settings
        // ================================================================

        [BoxGroup("Damage")]
        [LabelText("Enable Damage Modifier")]
        [SerializeField]
        bool _enableDamageModifier = true;

        [BoxGroup("Damage")]
        [ShowIf(nameof(_enableDamageModifier))]
        [LabelText("Damage Expression")]
        [Tooltip("蠑上〒 BaseDamage 繧貞・險育ｮ励ょ､画焚: BaseDamage, IsCritical, DamageType, etc.")]
        [SerializeField]
        DynamicValue<float> _damageExpression;

        [BoxGroup("Damage")]
        [ShowIf(nameof(_enableDamageModifier))]
        [LabelText("Damage Extrapayload")]
        [SerializeField]
        List<ExtrapayloadEntry> _damageExtrapayload = new();

        [BoxGroup("Damage")]
        [ShowIf(nameof(_enableDamageModifier))]
        [LabelText("On Damage Commands")]
        [SerializeField]
        VNext.CommandListData _onDamageCommands;

        // ================================================================
        // Heal Settings
        // ================================================================

        [BoxGroup("Heal")]
        [LabelText("Enable Heal Modifier")]
        [SerializeField]
        bool _enableHealModifier;

        [BoxGroup("Heal")]
        [ShowIf(nameof(_enableHealModifier))]
        [LabelText("Heal Expression")]
        [SerializeField]
        DynamicValue<float> _healExpression;

        [BoxGroup("Heal")]
        [ShowIf(nameof(_enableHealModifier))]
        [LabelText("Heal Extrapayload")]
        [SerializeField]
        List<ExtrapayloadEntry> _healExtrapayload = new();

        [BoxGroup("Heal")]
        [ShowIf(nameof(_enableHealModifier))]
        [LabelText("On Heal Commands")]
        [SerializeField]
        VNext.CommandListData _onHealCommands;

        // ================================================================
        // Tick Settings (DoT/HoT)
        // ================================================================

        [BoxGroup("Tick")]
        [LabelText("Enable Tick")]
        [SerializeField]
        bool _enableTick;

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("Tick Mode")]
        [EnumToggleButtons]
        [SerializeField]
        TickMode _tickMode = TickMode.Damage;

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("Tick Interval")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<float> _tickInterval;

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("Tick Expression")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<float> _tickExpression;

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("Tick Extrapayload")]
        [SerializeField]
        List<ExtrapayloadEntry> _tickExtrapayload = new();

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("On Tick Commands")]
        [SerializeField]
        VNext.CommandListData _onTickCommands;

        // ================================================================
        // Other Callbacks
        // ================================================================

        [BoxGroup("Callbacks")]
        [LabelText("On Death Commands")]
        [SerializeField]
        VNext.CommandListData _onDeathCommands;

        [BoxGroup("Callbacks")]
        [LabelText("On Revive Commands")]
        [SerializeField]
        VNext.CommandListData _onReviveCommands;

        // ================================================================
        // Implementation
        // ================================================================

        public override bool OnDamage(HealthModifierRuntime runtime, ref DamageContext context)
        {
            if (!_enableDamageModifier)
                return true;

            // Vars 繧剃ｽ懈・縺励，ontext 縺ｮ蛟､繧呈ｼ邏・
            var temp = CreateDamageVars(ref context);

            // Extrapayload 繧呈ｳｨ蜈･
            InjectExtrapayload(temp, _damageExtrapayload, runtime);

            // 蠑上ｒ隧穂ｾ｡縺励※ BaseDamage 繧呈峩譁ｰ
            if (_damageExpression.HasSource)
            {
                var dynCtx = new SimpleDynamicContext(temp, runtime.Context.Scope);
                float newDamage = _damageExpression.GetOrDefault(dynCtx, context.BaseDamage);
                context.BaseDamage = newDamage;
            }

            // 繧ｳ繝槭Φ繝牙ｮ溯｡・
            runtime.ExecuteCommands(_onDamageCommands, temp);

            return true;
        }

        public override bool OnHeal(HealthModifierRuntime runtime, ref HealContext context)
        {
            if (!_enableHealModifier)
                return true;

            var temp = CreateHealVars(ref context);
            InjectExtrapayload(temp, _healExtrapayload, runtime);

            if (_healExpression.HasSource)
            {
                var dynCtx = new SimpleDynamicContext(temp, runtime.Context.Scope);
                float newHeal = _healExpression.GetOrDefault(dynCtx, context.BaseHeal);
                context.BaseHeal = newHeal;
            }

            runtime.ExecuteCommands(_onHealCommands, temp);

            return true;
        }

        public override void OnTick(HealthModifierRuntime runtime, float deltaTime)
        {
            if (!_enableTick)
                return;

            // Tick 髢馴囈繧貞叙蠕・
            var dynCtx = new SimpleDynamicContext(NullVarStore.Instance, runtime.Context.Scope);
            float interval = _tickInterval.GetOrDefault(dynCtx, 1f);
            if (interval <= 0f)
                interval = 1f;

            // 邨碁℃譎る俣繧貞刈邂・
            runtime.ElapsedTime += deltaTime;

            // Tick 髢馴囈縺ｫ驕斐＠縺溘ｉ蜃ｦ逅・
            while (runtime.ElapsedTime >= interval)
            {
                runtime.ElapsedTime -= interval;
                ExecuteTick(runtime);
            }
        }

        void ExecuteTick(HealthModifierRuntime runtime)
        {
            var temp = new VarStore();
            InjectExtrapayload(temp, _tickExtrapayload, runtime);

            var dynCtx = new SimpleDynamicContext(temp, runtime.Context.Scope);
            float tickValue = _tickExpression.GetOrDefault(dynCtx, 0f);

            if (tickValue <= 0f)
            {
                // 繧ｳ繝槭Φ繝峨・縺ｿ螳溯｡・
                runtime.ExecuteCommands(_onTickCommands, temp);
                return;
            }

            var healthService = runtime.Context.HealthService;
            if (healthService == null)
                return;

            switch (_tickMode)
            {
                case TickMode.Damage:
                    var dmgCtx = DamageContext.Create(tickValue, DamageType.Physical);
                    dmgCtx.Tag = $"Tick:{ModifierId}";
                    healthService.ApplyDamage(ref dmgCtx);
                    break;

                case TickMode.Heal:
                    var healCtx = HealContext.Create(tickValue, HealType.Regeneration);
                    healCtx.Tag = $"Tick:{ModifierId}";
                    healthService.ApplyHeal(ref healCtx);
                    break;
            }

            runtime.ExecuteCommands(_onTickCommands, temp);
        }

        // ================================================================
        // Helper Methods
        // ================================================================

        VarStore CreateDamageVars(ref DamageContext context)
        {
            var vars = new VarStore();

            vars.TrySetVariant(VarIds.GameLib.Health.damage, DynamicVariant.FromFloat(context.BaseDamage));
            vars.TrySetVariant(VarIds.GameLib.Health.isCritical, DynamicVariant.FromBool(context.IsCritical));
            vars.TrySetVariant(VarIds.GameLib.Health.damageType, DynamicVariant.FromString(context.DamageType.ToString()));
            vars.TrySetVariant(VarIds.GameLib.Health.tag, DynamicVariant.FromString(context.Tag ?? string.Empty));
            vars.TrySetVariant(VarIds.GameLib.Health.knockbackForce, DynamicVariant.FromFloat(context.KnockbackForce));

            context.Source?.MergeInto(vars, overwrite: false);
            return vars;
        }

        VarStore CreateHealVars(ref HealContext context)
        {
            var vars = new VarStore();


            vars.TrySetVariant(VarIds.GameLib.Health.heal, DynamicVariant.FromFloat(context.BaseHeal));
            vars.TrySetVariant(VarIds.GameLib.Health.healType, DynamicVariant.FromString(context.HealType.ToString()));
            vars.TrySetVariant(VarIds.GameLib.Health.tag, DynamicVariant.FromString(context.Tag ?? string.Empty));

            context.Source?.MergeInto(vars, overwrite: false);
            return vars;
        }

        void InjectExtrapayload(IVarStore vars, List<ExtrapayloadEntry> entries, HealthModifierRuntime runtime)
        {
            if (entries == null || entries.Count == 0)
                return;

            var dynCtx = new SimpleDynamicContext(vars, runtime.Context.Scope);

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    continue;

                string value = entry.Value.GetOrDefault(dynCtx, string.Empty);
                if (VarIdResolver.TryResolve(entry.Key, out var varId) && varId != 0)
                    vars.TrySetVariant(varId, DynamicVariant.FromString(value));
            }
        }
    }

    /// <summary>
    /// Tick 繝｢繝ｼ繝会ｼ・amage or Heal・・
    /// </summary>
    public enum TickMode
    {
        Damage,
        Heal
    }

    /// <summary>
    /// Extrapayload 繧ｨ繝ｳ繝医Μ・医く繝ｼ縺ｨ DynamicValue<string>・・
    /// </summary>
    [Serializable]
    public struct ExtrapayloadEntry
    {
        [LabelText("Key")]
        public string Key;

        [LabelText("Value")]
        public DynamicValue<string> Value;
    }
}
