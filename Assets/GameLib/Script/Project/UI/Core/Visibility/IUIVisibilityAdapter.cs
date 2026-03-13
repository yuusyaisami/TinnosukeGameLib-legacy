#nullable enable
namespace Game.UI
{
    /// <summary>
    /// UI要素の「見た目の差し替え口」。
    /// 可視化処理（例: Fade/Scale/etc.）の実体はこのAdapter内に閉じ込め、CanvasGroup版/Shader版などに差し替え可能にする。
    /// </summary>
    public interface IUIVisibilityAdapter
    {
        /// <summary>0..1 の表示度合い。実装はアルファ、スケール、サイズ変更など任意の可視度合いの指標を用いて構いません。</summary>
        float Visibility { get; set; }

        /// <summary>
        /// 描画の最終停止（Graphic.enabled など）。
        /// GameObject.SetActive(false) は使用しない。
        /// </summary>
        void SetRenderEnabled(bool enabled);

        /// <summary>
        /// 要素自体の入力可否（CanvasGroup.interactable/blocksRaycasts等）。
        /// 実装依存でOK。
        /// </summary>
        void SetInteractable(bool interactable);
    }
}

