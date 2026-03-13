// Game.Health.IHealthService.cs
//
// Health 管理サービスインターフェース (v0.2)

using Game.Common;

namespace Game.Health
{
    /// <summary>
    /// Health 管理サービスインターフェース (v0.2)。
    /// Entity LifetimeScope で登録。
    /// </summary>
    public interface IHealthService
    {
        /// <summary>現在 HP</summary>
        float CurrentHP { get; }

        /// <summary>最大 HP</summary>
        float MaxHP { get; }

        /// <summary>HP 割合 (0.0 - 1.0)</summary>
        float HPRatio { get; }

        /// <summary>死亡状態か</summary>
        bool IsDead { get; }

        /// <summary>無敵状態か（BoolLayer ベース）</summary>
        bool IsInvincible { get; }

        /// <summary>
        /// 無敵制御用の BoolLayer（StatusEffect から操作可能）
        /// </summary>
        BoolLayer InvincibleLayer { get; }


        /// <summary>
        /// ダメージを適用
        /// </summary>
        /// <param name="context">ダメージコンテキスト</param>
        /// <returns>実際に与えたダメージ量</returns>
        float ApplyDamage(ref DamageContext context);

        /// <summary>
        /// 回復を適用
        /// </summary>
        /// <param name="context">回復コンテキスト</param>
        /// <returns>実際に回復した量</returns>
        float ApplyHeal(ref HealContext context);

        /// <summary>
        /// 即死（イベントは発行される）
        /// </summary>
        void Kill();

        /// <summary>
        /// 復活
        /// </summary>
        /// <param name="hpRatio">復活時の HP 割合 (0.0 - 1.0)</param>
        void Revive(float hpRatio = 1f);

        /// <summary>
        /// HP を直接設定（デバッグ用）
        /// </summary>
        void SetHP(float hp);

        /// <summary>
        /// MaxHP を設定
        /// </summary>
        void SetMaxHP(float maxHP);

        // ================================================================
        // Modifier 管理 (v0.2 SO ベース)
        // ================================================================

        /// <summary>
        /// ModifierSO を登録
        /// </summary>
        void RegisterModifier(BaseHealthModifierSO so);

        /// <summary>
        /// ModifierId で Modifier を削除
        /// </summary>
        void UnregisterModifier(string modifierId);

        /// <summary>
        /// 指定 ModifierId の Runtime を取得
        /// </summary>
        bool TryGetModifierRuntime(string modifierId, out HealthModifierRuntime runtime);
    }
}
