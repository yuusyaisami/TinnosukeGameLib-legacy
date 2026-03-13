#nullable enable
using UnityEngine;

namespace Game.TextureEffect
{
    /// <summary>
    /// Mosaic effect: UV 量子化 による block 化 + mask composite.
    /// </summary>
    public sealed class MosaicEffect : ITextureEffect
    {
        static readonly int s_BlockSize = Shader.PropertyToID("_BlockSize");
        static readonly int s_TexSize = Shader.PropertyToID("_TexSize");
        static readonly int s_SourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int s_MaskTex = Shader.PropertyToID("_MaskTex");
        static readonly int s_UseMask = Shader.PropertyToID("_UseMask");

        Material? _mosaicMaterial;

        public TextureEffectKind Kind => TextureEffectKind.Mosaic;

        public void Execute(
            Texture inputTex,
            RenderTexture outputRT,
            RenderTexture? maskRT,
            in TextureEffectParams effectParams,
            float resolutionScale)
        {
            EnsureMaterial();
            if (_mosaicMaterial == null)
                return;

            _mosaicMaterial.SetFloat(s_BlockSize, Mathf.Max(1f, effectParams.MosaicBlockSize));
            _mosaicMaterial.SetVector(s_TexSize, new Vector4(inputTex.width, inputTex.height, 0, 0));

            if (maskRT != null)
            {
                _mosaicMaterial.SetTexture(s_SourceTex, inputTex);
                _mosaicMaterial.SetTexture(s_MaskTex, maskRT);
                _mosaicMaterial.SetFloat(s_UseMask, 1f);
            }
            else
            {
                _mosaicMaterial.SetFloat(s_UseMask, 0f);
            }

            Graphics.Blit(inputTex, outputRT, _mosaicMaterial, 0);
        }

        public void ReleaseTemporaryResources() { }

        void EnsureMaterial()
        {
            if (_mosaicMaterial != null)
                return;
            var shader = Shader.Find("Hidden/TextureEffect/Mosaic");
            if (shader != null)
                _mosaicMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
    }
}
