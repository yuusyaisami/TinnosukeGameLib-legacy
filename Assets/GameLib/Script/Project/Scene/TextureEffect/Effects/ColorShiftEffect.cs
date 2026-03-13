#nullable enable
using UnityEngine;

namespace Game.TextureEffect
{
    /// <summary>
    /// ColorShift effect: Hue / Saturation / RGB 乗算・加算 を 1 pass で適用 + mask composite.
    /// </summary>
    public sealed class ColorShiftEffect : ITextureEffect
    {
        static readonly int s_HueShift = Shader.PropertyToID("_HueShift");
        static readonly int s_SatMul = Shader.PropertyToID("_SaturationMultiplier");
        static readonly int s_ColorMul = Shader.PropertyToID("_ColorMultiply");
        static readonly int s_ColorAdd = Shader.PropertyToID("_ColorAdd");
        static readonly int s_SourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int s_MaskTex = Shader.PropertyToID("_MaskTex");
        static readonly int s_UseMask = Shader.PropertyToID("_UseMask");

        Material? _colorShiftMaterial;

        public TextureEffectKind Kind => TextureEffectKind.ColorShift;

        public void Execute(
            Texture inputTex,
            RenderTexture outputRT,
            RenderTexture? maskRT,
            in TextureEffectParams effectParams,
            float resolutionScale)
        {
            EnsureMaterial();
            if (_colorShiftMaterial == null)
                return;

            _colorShiftMaterial.SetFloat(s_HueShift, effectParams.HueShift);
            _colorShiftMaterial.SetFloat(s_SatMul, effectParams.SaturationMultiplier);
            _colorShiftMaterial.SetColor(s_ColorMul, effectParams.ColorMultiply);
            _colorShiftMaterial.SetColor(s_ColorAdd, effectParams.ColorAdd);

            if (maskRT != null)
            {
                _colorShiftMaterial.SetTexture(s_SourceTex, inputTex);
                _colorShiftMaterial.SetTexture(s_MaskTex, maskRT);
                _colorShiftMaterial.SetFloat(s_UseMask, 1f);
            }
            else
            {
                _colorShiftMaterial.SetFloat(s_UseMask, 0f);
            }

            Graphics.Blit(inputTex, outputRT, _colorShiftMaterial, 0);
        }

        public void ReleaseTemporaryResources() { }

        void EnsureMaterial()
        {
            if (_colorShiftMaterial != null)
                return;
            var shader = Shader.Find("Hidden/TextureEffect/ColorShift");
            if (shader != null)
                _colorShiftMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
    }
}
