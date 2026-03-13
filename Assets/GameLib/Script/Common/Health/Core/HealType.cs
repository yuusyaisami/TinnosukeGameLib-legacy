// Game.Health.HealType.cs
//
// 回復タイプの列挙型

namespace Game.Health
{
    /// <summary>
    /// 回復タイプ
    /// </summary>
    public enum HealType
    {
        /// <summary>通常回復</summary>
        Normal,

        /// <summary>アイテム回復</summary>
        Item,

        /// <summary>スキル回復</summary>
        Skill,

        /// <summary>持続回復（HoT）</summary>
        Regeneration,
    }
}
