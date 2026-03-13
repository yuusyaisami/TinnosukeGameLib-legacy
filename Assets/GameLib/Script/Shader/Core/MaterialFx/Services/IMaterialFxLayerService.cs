#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;

namespace Game.MaterialFx
{
    /// <summary>
    /// Layer 管理・合成・Fade 更新の中核サービス。
    /// StableKey ごとに LayerStack を保持し、Priority 順に合成して FinalValue を生成する。
    /// </summary>
    public interface IMaterialFxLayerService : IDisposable
    {
        /// <summary>Layer を即時設定</summary>
        /// <param name="lifetimeSeconds">
        /// -1 で無期限。
        /// 0 以上で「この Layer の寿命」を加算する（同じ (stableKey, contextTag) に再度 Set すると残り時間に加算される）。
        /// </param>
        void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                      MaterialFxBlendMode blend, int priority, float lifetimeSeconds = -1f);

        /// <summary>Layer を Fade 付きで設定（値自体の補間）</summary>
        void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                 float duration, Ease easing, MaterialFxBlendMode blend, int priority, float lifetimeSeconds = -1f);

        /// <summary>Layer の Weight（寄与率）を Fade（0=影響なし, 1=完全適用）</summary>
        void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                float duration, Ease easing);

        /// <summary>Layer を削除（Default は削除不可）</summary>
        bool RemoveLayer(string stableKey, string contextTag);

        /// <summary>指定 ContextTag の全 Layer を削除</summary>
        void ClearContext(string contextTag);

        /// <summary>Default 以外の全 Layer を削除</summary>
        void ClearAll();

        /// <summary>Fade 更新（毎フレーム呼び出し）</summary>
        void UpdateFades(float deltaTime);

        /// <summary>Fading/Timed キーが存在するか（UpdateFades のスキップ判定用）</summary>
        bool HasFadingOrTimedKeys { get; }

        int GetActiveLayerCount(string stableKey, string contextTag = "");

        /// <summary>dirty な StableKey を列挙（★GC 対策: IReadOnlyList）</summary>
        IReadOnlyList<string> GetDirtyKeys();

        /// <summary>FinalValue を計算</summary>
        MaterialFxTypedValue ComputeFinalValue(string stableKey);

        /// <summary>dirty フラグをクリア</summary>
        void ClearDirty(string stableKey);

        /// <summary>特定 Layer の現在値を取得</summary>
        bool TryGetLayerValue(string stableKey, string contextTag, out MaterialFxTypedValue value);

        /// <summary>LayerStack が存在するか</summary>
        bool HasStack(string stableKey);

        /// <summary>指定 ContextTag を持つ全 StableKey を列挙</summary>
        IEnumerable<string> GetKeysByContext(string contextTag);
    }
}
