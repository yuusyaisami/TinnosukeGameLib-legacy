// Game.Health.BaseHealthModifierSO.cs
//
// HealthModifier の SO 基底クラス (v0.2)
// - ModifierId, Priority, Enabled
// - OnDamage/OnHeal/OnTick 用のコールバック設定
// - Extrapayload サポート
// - CommandListData (vNext) 実行サポート

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
    /// HealthModifier の SO 基底クラス。
    /// 派生クラスで OnDamage/OnHeal/OnTick をオーバーライドする。
    /// </summary>
    public abstract class BaseHealthModifierSO : ScriptableObject
    {
        [BoxGroup("Modifier Info")]
        [LabelText("Modifier ID")]
        [Tooltip("一意識別子（重複時は警告）")]
        [SerializeField]
        string _modifierId = string.Empty;

        [BoxGroup("Modifier Info")]
        [LabelText("Priority")]
        [Tooltip("優先度（小さいほど先に適用）")]
        [SerializeField]
        int _priority = 100;

        [BoxGroup("Modifier Info")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        /// <summary>一意識別子</summary>
        public string ModifierId => _modifierId;

        /// <summary>優先度（小さいほど先に適用）</summary>
        public int Priority => _priority;

        /// <summary>有効フラグ</summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Runtime インスタンスを生成。
        /// HealthService 登録時に呼ばれる。
        /// </summary>
        public virtual HealthModifierRuntime CreateRuntime(HealthModifierContext context)
        {
            return new HealthModifierRuntime(this, context);
        }

        /// <summary>
        /// ダメージ処理コールバック。
        /// </summary>
        /// <param name="runtime">Runtime インスタンス</param>
        /// <param name="context">ダメージコンテキスト</param>
        /// <returns>処理を継続するか（false で以降のモディファイアをスキップ）</returns>
        public virtual bool OnDamage(HealthModifierRuntime runtime, ref DamageContext context)
        {
            return true; // デフォルトは何もせず継続
        }

        /// <summary>
        /// 回復処理コールバック。
        /// </summary>
        public virtual bool OnHeal(HealthModifierRuntime runtime, ref HealContext context)
        {
            return true;
        }

        /// <summary>
        /// Tick 処理コールバック。
        /// </summary>
        public virtual void OnTick(HealthModifierRuntime runtime, float deltaTime)
        {
            // デフォルトは何もしない
        }

        /// <summary>
        /// 初期化コールバック（Runtime 生成後）。
        /// </summary>
        public virtual void OnInitialize(HealthModifierRuntime runtime)
        {
        }

        /// <summary>
        /// 破棄コールバック。
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
    /// HealthModifier の Runtime コンテキスト。
    /// HealthService, Scope, Services への参照を提供。
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
    /// HealthModifierSO の Runtime インスタンス。
    /// 各 Entity ごとに生成される。
    /// </summary>
    public class HealthModifierRuntime : IDisposable
    {
        public BaseHealthModifierSO SO { get; }
        public HealthModifierContext Context { get; }

        /// <summary>Tick 用の経過時間</summary>
        public float ElapsedTime { get; set; }

        /// <summary>カスタムデータ用のディクショナリ</summary>
        public Dictionary<string, object> CustomData { get; } = new();

        bool _disposed;

        public HealthModifierRuntime(BaseHealthModifierSO so, HealthModifierContext context)
        {
            SO = so ?? throw new ArgumentNullException(nameof(so));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// CommandListData (vNext) を実行するヘルパー。
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
