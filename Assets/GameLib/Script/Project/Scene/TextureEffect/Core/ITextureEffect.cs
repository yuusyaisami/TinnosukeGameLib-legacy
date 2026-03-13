#nullable enable
using UnityEngine;

namespace Game.TextureEffect
{
    /// <summary>
    /// 個別 Effect の処理 interface。
    /// 各 Effect (Blur, Mosaic, etc.) はこれを実装する。
    /// </summary>
    public interface ITextureEffect
    {
        TextureEffectKind Kind { get; }

        /// <summary>
        /// Effect を実行し、結果を outputRT に書き込む。
        /// </summary>
        void Execute(
            Texture inputTex,
            RenderTexture outputRT,
            RenderTexture? maskRT,
            in TextureEffectParams effectParams,
            float resolutionScale);

        /// <summary>確保した中間 RT を解放する。</summary>
        void ReleaseTemporaryResources();
    }
}
