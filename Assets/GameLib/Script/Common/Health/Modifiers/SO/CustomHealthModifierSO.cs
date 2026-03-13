// Game.Health.CustomHealthModifierSO.cs
//
// 汎用式ベースの HealthModifier SO (v0.2)
// - DynamicValue<float> による式評価
// - Extrapayload サポート
// - Tick 処理（Interval ベース）
// - CommandListData (vNext) 実行

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
    /// 汎用式ベースの HealthModifier SO。
    /// DynamicValue<float> で式評価、Extrapayload で追加データ注入。
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
        [Tooltip("式で BaseDamage を再計算。変数: BaseDamage, IsCritical, DamageType, etc.")]
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
        [Tooltip("Tick 間隔（秒）。DynamicValue で動的に変更可能。")]
        [SerializeField]
        DynamicValue<float> _tickInterval;

        [BoxGroup("Tick")]
        [ShowIf(nameof(_enableTick))]
        [LabelText("Tick Expression")]
        [Tooltip("Tick 時に適用するダメージ/回復量。")]
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

            // Vars を作成し、Context の値を格納
            var temp = CreateDamageVars(ref context);

            // Extrapayload を注入
            InjectExtrapayload(temp, _damageExtrapayload, runtime);

            // 式を評価して BaseDamage を更新
            if (_damageExpression.HasSource)
            {
                var dynCtx = new SimpleDynamicContext(temp, runtime.Context.Scope);
                float newDamage = _damageExpression.GetOrDefault(dynCtx, context.BaseDamage);
                context.BaseDamage = newDamage;
            }

            // コマンド実行
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

            // Tick 間隔を取得
            var dynCtx = new SimpleDynamicContext(NullVarStore.Instance, runtime.Context.Scope);
            float interval = _tickInterval.GetOrDefault(dynCtx, 1f);
            if (interval <= 0f)
                interval = 1f;

            // 経過時間を加算
            runtime.ElapsedTime += deltaTime;

            // Tick 間隔に達したら処理
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
                // コマンドのみ実行
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
    /// Tick モード（Damage or Heal）
    /// </summary>
    public enum TickMode
    {
        Damage,
        Heal
    }

    /// <summary>
    /// Extrapayload エントリ（キーと DynamicValue<string>）
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
