#nullable enable
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AIClipSO の実行時インスタンス。
    /// </summary>
    public abstract class AIClipRuntime
    {
        public AIClipSO Source { get; private set; } = null!;
        public string StableKey => Source.StableKey;
        public int Priority => Source.Priority;

        // 状態
        bool _requestPop;
        int _updateFrameCounter;

        // Interrupt Runtime（事前生成で GC 回避）
        InterruptRuleRuntime[] _interruptRuntimes = System.Array.Empty<InterruptRuleRuntime>();

        /// <summary>Pop 要求フラグ（自発終了用）</summary>
        public bool IsPopRequested => _requestPop;

        /// <summary>初期化（AIStateService が呼ぶ）</summary>
        internal void Initialize(AIClipSO source, in AIAgentContext ctx)
        {
            Source = source;
            _requestPop = false;
            _updateFrameCounter = 0;

            // Interrupt Runtime を事前生成
            var rules = source.InterruptRules;
            _interruptRuntimes = new InterruptRuleRuntime[rules.Count];
            for (int i = 0; i < rules.Count; i++)
            {
                _interruptRuntimes[i] = rules[i].CreateRuntime(ctx);
            }

            OnInitialize(ctx);
        }

        /// <summary>Pop を要求（次の ApplyTransition で実行される）</summary>
        public void RequestPop() => _requestPop = true;

        /// <summary>Pop 要求をクリア（AIStateService が呼ぶ）</summary>
        internal void ClearPopRequest() => _requestPop = false;

        /// <summary>Interrupt ルールを評価（AIStateService が呼ぶ）</summary>
        internal InterruptRuleRuntime? EvaluateInterrupts(in AIAgentContext ctx)
        {
            for (int i = 0; i < _interruptRuntimes.Length; i++)
            {
                var rule = _interruptRuntimes[i];
                if (rule.Evaluate(ctx))
                    return rule;
            }
            return null;
        }

        /// <summary>Update 可能か判定（Interval 考慮）</summary>
        internal bool ShouldUpdate(int frameCount)
        {
            if (Source.UpdateMode == AIClipUpdateMode.Manual)
                return false;
            if (Source.UpdateMode == AIClipUpdateMode.EveryFrame)
                return true;

            _updateFrameCounter++;
            if (_updateFrameCounter >= Source.UpdateIntervalFrames)
            {
                _updateFrameCounter = 0;
                return true;
            }
            return false;
        }

        // ================================================================
        // 派生クラスでオーバーライド
        // ================================================================

        protected virtual void OnInitialize(in AIAgentContext ctx) { }

        /// <summary>スタックに Push された直後</summary>
        public virtual void OnEnter(in AIAgentContext ctx) { }

        /// <summary>Top になった瞬間（Enter 直後 or 上の Clip が Pop された後）</summary>
        public virtual void OnResume(in AIAgentContext ctx) { }

        /// <summary>Top でなくなる瞬間（上に Clip が Push される or 自身が Pop される前）</summary>
        public virtual void OnSuspend(in AIAgentContext ctx) { }

        /// <summary>スタックから Pop される直前</summary>
        public virtual void OnExit(in AIAgentContext ctx) { }

        /// <summary>Top の間、毎フレーム（または Interval ごと）</summary>
        public virtual void OnUpdate(in AIAgentContext ctx) { }

        /// <summary>破棄時（Agent 破棄時）</summary>
        public virtual void OnDispose() { }
    }
}
