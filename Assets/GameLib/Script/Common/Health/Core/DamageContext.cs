// Game.Health.DamageContext.cs
//
// ダメージ適用時のコンテキスト（構造体で GC 回避）

using Game.Common;
using Game.Scalar;
using UnityEngine;

namespace Game.Health
{
    /// <summary>
    /// ダメージ適用時のコンテキスト（構造体で GC 回避）。
    /// </summary>
    public struct DamageContext
    {
        /// <summary>基本ダメージ量</summary>
        public float BaseDamage;

        /// <summary>ダメージソース（攻撃者の情報などを格納するペイロード）</summary>
        public IVarStore Source;

        /// <summary>ダメージを与えた側の ScalarService（クリティカル判定などで使用）</summary>
        public IBaseScalarService AttackerScalarService;

        /// <summary>ダメージタイプ</summary>
        public DamageType DamageType;

        /// <summary>クリティカルヒットか</summary>
        public bool IsCritical;

        /// <summary>ノックバックベクトル（オプション）</summary>
        public Vector3 KnockbackDirection;

        /// <summary>ノックバック力（オプション）</summary>
        public float KnockbackForce;

        /// <summary>追加データ（タグ等）</summary>
        public string Tag;

        /// <summary>
        /// カスタムイベント用の追加ペイロード。
        /// Modifier などが isCritical などの補足情報を渡す場合に使用する。
        /// PublishDamageEvent で標準ペイロードとマージされる。
        /// </summary>
        public IVarStore ExtraPayload;

        // 計算後の値（Modifier 適用後に設定）
        internal float FinalDamage;
        internal bool WasBlocked;
        internal bool WasDodged;

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static DamageContext Create(
            float damage,
            DamageType type = DamageType.Physical,
            IVarStore source = null,
            IBaseScalarService attackerScalarService = null)
        {
            return new DamageContext
            {
                BaseDamage = damage,
                DamageType = type,
                Source = source ?? new VarStore(),
                AttackerScalarService = attackerScalarService,
                IsCritical = false,
                Tag = null,
                ExtraPayload = null,
                FinalDamage = 0f,
                WasBlocked = false,
                WasDodged = false,
            };
        }
    }
}
