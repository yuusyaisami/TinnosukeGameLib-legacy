// Game.StatusEffect.IStatusEffectService.cs
//
// StatusEffect 管理サービスインターフェース

using System.Collections.Generic;
using Game.Common;
using Game.Health;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect 管理サービスインターフェース。
    /// </summary>
    public interface IStatusEffectService
    {
        /// <summary>現在有効な Effect の数</summary>
        int ActiveEffectCount { get; }

        /// <summary>
        /// Effect 用の BoolLayer（HealthModifier との連携用）
        /// </summary>
        BoolLayer EffectFlagLayer { get; }

        // ================================================================
        // Effect 適用
        // ================================================================

        /// <summary>
        /// StatusEffect を適用
        /// </summary>
        /// <typeparam name="T">Effect の型</typeparam>
        /// <param name="config">設定</param>
        /// <returns>適用された Effect の ID</returns>
        string ApplyEffect<T>(EffectConfig config) where T : BaseEffectRuntime, new();

        /// <summary>
        /// StatusEffect を適用（既存インスタンス）
        /// </summary>
        string ApplyEffect(BaseEffectRuntime effect, EffectConfig config);

        // ================================================================
        // Effect 削除
        // ================================================================

        /// <summary>
        /// 指定 ID の Effect を削除
        /// </summary>
        bool RemoveEffect(string effectId);

        /// <summary>
        /// 指定型の Effect を全て削除
        /// </summary>
        int RemoveEffects<T>() where T : BaseEffectRuntime;

        /// <summary>
        /// 指定タイプ（Buff/Debuff）の Effect を全て削除
        /// </summary>
        int RemoveEffects(EffectType type);

        /// <summary>
        /// 全ての Effect を削除
        /// </summary>
        void ClearAllEffects();

        // ================================================================
        // Effect クエリ
        // ================================================================

        /// <summary>
        /// 指定 ID の Effect が存在するか
        /// </summary>
        bool HasEffect(string effectId);

        /// <summary>
        /// 指定型の Effect が存在するか
        /// </summary>
        bool HasEffect<T>() where T : BaseEffectRuntime;

        /// <summary>
        /// 指定 ID の Effect を取得
        /// </summary>
        bool TryGetEffect(string effectId, out BaseEffectRuntime effect);

        /// <summary>
        /// 指定型の Effect を取得
        /// </summary>
        bool TryGetEffect<T>(out T effect) where T : BaseEffectRuntime;

        /// <summary>
        /// 全ての有効な Effect の状態を取得
        /// </summary>
        void GetActiveEffectStates(List<EffectState> output);

        /// <summary>
        /// 指定タイプの Effect の状態を取得
        /// </summary>
        void GetEffectStates(EffectType type, List<EffectState> output);
    }
}
