// Game.Health.HealContext.cs
//
// 回復適用時のコンテキスト（構造体で GC 回避）

using Game.Common;

namespace Game.Health
{
    /// <summary>
    /// 回復適用時のコンテキスト（構造体で GC 回避）。
    /// </summary>
    public struct HealContext
    {
        /// <summary>基本回復量</summary>
        public float BaseHeal;

        /// <summary>回復ソース（DamageContext と同様に Vars）</summary>
        public IVarStore Source;

        /// <summary>回復タイプ</summary>
        public HealType HealType;

        /// <summary>追加データ</summary>
        public string Tag;

        /// <summary>
        /// カスタムイベント用の追加ペイロード。
        /// PublishHealEvent でマージされる。
        /// </summary>
        public IVarStore ExtraPayload;

        // 計算後の値
        internal float FinalHeal;
        internal float ActualHeal; // 実際に回復した量（MaxHP による制限後）

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static HealContext Create(float heal, HealType type = HealType.Normal, IVarStore source = null)
        {
            return new HealContext
            {
                BaseHeal = heal,
                HealType = type,
                Source = source ?? new VarStore(),
                Tag = null,
                ExtraPayload = null,
            };
        }
    }
}
