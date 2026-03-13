// Game.StatusEffect.EffectContext.cs
//
// BaseEffectRuntime に渡されるコンテキスト

using Game.Common;
using Game.Health;
using Game.Profile;
using Game.Scalar;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// BaseEffectRuntime に渡されるコンテキスト。
    /// 各種サービスへのアクセスを提供する。
    /// </summary>
    public sealed class EffectContext
    {
        /// <summary>StatusEffectService への参照</summary>
        public IStatusEffectService StatusEffectService { get; }

        /// <summary>HealthService への参照（null 可）</summary>
        public IHealthService HealthService { get; }

        /// <summary>Scalar Service への参照</summary>
        public IBaseScalarService ScalarService { get; }

        /// <summary>Blackboard Service への参照</summary>
        public IBlackboardService BlackboardService { get; }

        /// <summary>Event Service への参照</summary>
        public IEntityEventService EventService { get; }

        /// <summary>ProfileRegistry への参照</summary>
        public IScopeBindingRegistry ProfileRegistry { get; }

        /// <summary>
        /// Effect 用の BoolLayer（StatusEffect が HealthModifier と連携するためのレイヤー）
        /// </summary>
        public BoolLayer EffectFlagLayer { get; }

        /// <summary>Entity の Transform</summary>
        public Transform Transform { get; }

        /// <summary>Effect 設定</summary>
        public EffectConfig Config { get; internal set; }

        public EffectContext(
            IStatusEffectService statusEffectService,
            IHealthService healthService,
            IBaseScalarService scalarService,
            IBlackboardService blackboardService,
            IEntityEventService eventService,
            IScopeBindingRegistry profileRegistry,
            BoolLayer effectFlagLayer,
            Transform transform)
        {
            StatusEffectService = statusEffectService;
            HealthService = healthService;
            ScalarService = scalarService;
            BlackboardService = blackboardService;
            EventService = eventService;
            ProfileRegistry = profileRegistry;
            EffectFlagLayer = effectFlagLayer;
            Transform = transform;
        }
    }
}
