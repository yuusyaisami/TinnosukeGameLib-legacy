// Game.Health.DamageType.cs
//
// ダメージタイプの列挙型

namespace Game.Health
{
    /// <summary>
    /// ダメージタイプ
    /// </summary>
    public enum DamageType
    {
        /// <summary>物理ダメージ</summary>
        Physical,

        /// <summary>魔法ダメージ</summary>
        Magical,

        /// <summary>純粋ダメージ（軽減不可）</summary>
        Pure,

        /// <summary>毒ダメージ（DoT）</summary>
        Poison,

        /// <summary>炎上ダメージ（DoT）</summary>
        Burn,

        /// <summary>落下ダメージ</summary>
        Fall,

        /// <summary>環境ダメージ</summary>
        Environmental,
    }
}
