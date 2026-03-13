#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Unity標準UI向けのVisibilityAdapter実装。
    /// - Visibility: CanvasGroup.alpha
    /// - 入力: CanvasGroup.interactable / blocksRaycasts
    /// - Render停止: Graphic.enabled（初期値を復元）
    /// </summary>
    public sealed class CanvasGroupVisibilityAdapter : IUIVisibilityAdapter
    {
        readonly CanvasGroup _canvasGroup;
        readonly Graphic[] _graphics;

        public CanvasGroupVisibilityAdapter(CanvasGroup canvasGroup, Graphic[] graphics)
        {
            _canvasGroup = canvasGroup;
            _graphics = graphics ?? System.Array.Empty<Graphic>();
        }

        public float Visibility
        {
            get => _canvasGroup.alpha;
            set => _canvasGroup.alpha = Mathf.Clamp01(value);
        }

        public void SetRenderEnabled(bool enabled)
        {
            if (!enabled)
            {
                Visibility = 0f;
            }

            for (int i = 0; i < _graphics.Length; i++)
            {
                var g = _graphics[i];
                if (g == null) continue;
                // Graphic.enabled の切替は副作用が強いので、描画コスト低減は cull に寄せる。
                g.canvasRenderer.cull = !enabled;
            }
        }

        public void SetInteractable(bool interactable)
        {
            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
        }
    }
}
