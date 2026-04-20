// Game.Health.BaseHealthModifierSO.cs
//
// HealthModifier 縺ｮ SO 蝓ｺ蠎輔け繝ｩ繧ｹ (v0.2)
// - ModifierId, Priority, Enabled
// - OnDamage/OnHeal/OnTick 逕ｨ縺ｮ繧ｳ繝ｼ繝ｫ繝舌ャ繧ｯ險ｭ螳・
// - Extrapayload 繧ｵ繝昴・繝・
// - CommandListData (vNext) 螳溯｡後し繝昴・繝・

#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Health
{
    /// <summary>
    /// HealthModifier 縺ｮ SO 蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// 豢ｾ逕溘け繝ｩ繧ｹ縺ｧ OnDamage/OnHeal/OnTick 繧偵が繝ｼ繝舌・繝ｩ繧､繝峨☆繧九・
    /// </summary>
    public abstract class BaseHealthModifierSO : ScriptableObject
    {
        [BoxGroup("Modifier Info")]
        [LabelText("Modifier ID")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _modifierId = string.Empty;

        [BoxGroup("Modifier Info")]
        [LabelText("Priority")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _priority = 100;

        [BoxGroup("Modifier Info")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        /// <summary>荳諢剰ｭ伜挨蟄・/summary>
        public string ModifierId => _modifierId;

        /// <summary>蜆ｪ蜈亥ｺｦ・亥ｰ上＆縺・⊇縺ｩ蜈医↓驕ｩ逕ｨ・・/summary>
        public int Priority => _priority;

        /// <summary>譛牙柑繝輔Λ繧ｰ</summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Runtime 繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ繧堤函謌舌・
        /// HealthService 逋ｻ骭ｲ譎ゅ↓蜻ｼ縺ｰ繧後ｋ縲・
        /// </summary>
        public virtual HealthModifierRuntime CreateRuntime(HealthModifierContext context)
        {
            return new HealthModifierRuntime(this, context);
        }

        /// <summary>
        /// 繝繝｡繝ｼ繧ｸ蜃ｦ逅・さ繝ｼ繝ｫ繝舌ャ繧ｯ縲・
        /// </summary>
        /// <param name="runtime">Runtime 繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ</param>
        /// <param name="context">繝繝｡繝ｼ繧ｸ繧ｳ繝ｳ繝・く繧ｹ繝・/param>
        /// <returns>蜃ｦ逅・ｒ邯咏ｶ壹☆繧九°・・alse 縺ｧ莉･髯阪・繝｢繝・ぅ繝輔ぃ繧､繧｢繧偵せ繧ｭ繝・・・・/returns>
        public virtual bool OnDamage(HealthModifierRuntime runtime, ref DamageContext context)
        {
            return true; // 繝・ヵ繧ｩ繝ｫ繝医・菴輔ｂ縺帙★邯咏ｶ・
        }

        /// <summary>
        /// 蝗槫ｾｩ蜃ｦ逅・さ繝ｼ繝ｫ繝舌ャ繧ｯ縲・
        /// </summary>
        public virtual bool OnHeal(HealthModifierRuntime runtime, ref HealContext context)
        {
            return true;
        }

        /// <summary>
        /// Tick 蜃ｦ逅・さ繝ｼ繝ｫ繝舌ャ繧ｯ縲・
        /// </summary>
        public virtual void OnTick(HealthModifierRuntime runtime, float deltaTime)
        {
            // 繝・ヵ繧ｩ繝ｫ繝医・菴輔ｂ縺励↑縺・
        }

        /// <summary>
        /// 蛻晄悄蛹悶さ繝ｼ繝ｫ繝舌ャ繧ｯ・・untime 逕滓・蠕鯉ｼ峨・
        /// </summary>
        public virtual void OnInitialize(HealthModifierRuntime runtime)
        {
        }

        /// <summary>
        /// 遐ｴ譽・さ繝ｼ繝ｫ繝舌ャ繧ｯ縲・
        /// </summary>
        public virtual void OnDispose(HealthModifierRuntime runtime)
        {
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrEmpty(_modifierId))
            {
                _modifierId = name;
            }
        }
#endif
    }

    /// <summary>
    /// HealthModifier 縺ｮ Runtime 繧ｳ繝ｳ繝・く繧ｹ繝医・
    /// HealthService, Scope, Services 縺ｸ縺ｮ蜿ら・繧呈署萓帙・
    /// </summary>
    public sealed class HealthModifierContext
    {
        public IHealthService HealthService { get; }
        public IScopeNode Scope { get; }
        public IBaseScalarService ScalarService { get; }
        public IBlackboardService BlackboardService { get; }
        public IEntityEventService EventService { get; }
        public VNext.ICommandRunner CommandRunner { get; }
        public Transform Transform { get; }

        public HealthModifierContext(
            IHealthService healthService,
            IScopeNode scope,
            IBaseScalarService scalarService,
            IBlackboardService blackboardService,
            IEntityEventService eventService,
            VNext.ICommandRunner commandRunner,
            Transform transform)
        {
            HealthService = healthService;
            Scope = scope;
            ScalarService = scalarService;
            BlackboardService = blackboardService;
            EventService = eventService;
            CommandRunner = commandRunner;
            Transform = transform;
        }
    }

    /// <summary>
    /// HealthModifierSO 縺ｮ Runtime 繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縲・
    /// 蜷・Entity 縺斐→縺ｫ逕滓・縺輔ｌ繧九・
    /// </summary>
    public class HealthModifierRuntime : IDisposable
    {
        public BaseHealthModifierSO SO { get; }
        public HealthModifierContext Context { get; }

        /// <summary>Tick 逕ｨ縺ｮ邨碁℃譎る俣</summary>
        public float ElapsedTime { get; set; }

        /// <summary>繧ｫ繧ｹ繧ｿ繝繝・・繧ｿ逕ｨ縺ｮ繝・ぅ繧ｯ繧ｷ繝ｧ繝翫Μ</summary>
        public Dictionary<string, object> CustomData { get; } = new();

        bool _disposed;

        public HealthModifierRuntime(BaseHealthModifierSO so, HealthModifierContext context)
        {
            SO = so ?? throw new ArgumentNullException(nameof(so));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// CommandListData (vNext) 繧貞ｮ溯｡後☆繧九・繝ｫ繝代・縲・
        /// </summary>
        public void ExecuteCommands(VNext.CommandListData commands, IVarStore? variables = null)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (Context.CommandRunner == null)
            {
                Debug.LogWarning($"[HealthModifierRuntime] CommandRunner is null, cannot execute commands for {SO.ModifierId}");
                return;
            }

            var ctx = new VNext.CommandContext(Context.Scope, variables ?? new VarStore(), Context.CommandRunner);
            Context.CommandRunner.ExecuteListAsync(commands, ctx, default, ctx.Options).Forget();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            SO.OnDispose(this);
            CustomData.Clear();
        }
    }
}
