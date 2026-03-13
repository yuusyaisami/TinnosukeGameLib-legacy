#nullable enable
using UnityEngine;

namespace Game.TextureEffect
{
    /// <summary>
    /// Distort effect: ノイズ Texture を参照して UV をずらす + mask composite.
    /// </summary>
    public sealed class DistortEffect : ITextureEffect
    {
        static readonly int s_Strength = Shader.PropertyToID("_DistortStrength");
        static readonly int s_NoiseTex = Shader.PropertyToID("_NoiseTex");
        static readonly int s_SourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int s_MaskTex = Shader.PropertyToID("_MaskTex");
        static readonly int s_UseMask = Shader.PropertyToID("_UseMask");
        static readonly int s_Time = Shader.PropertyToID("_TimeParam");

        Material? _distortMaterial;

        public TextureEffectKind Kind => TextureEffectKind.Distort;

        public void Execute(
            Texture inputTex,
            RenderTexture outputRT,
            RenderTexture? maskRT,
            in TextureEffectParams effectParams,
            float resolutionScale)
        {
            EnsureMaterial();
            if (_distortMaterial == null)
                return;

            _distortMaterial.SetFloat(s_Strength, effectParams.DistortStrength);
            _distortMaterial.SetFloat(s_Time, Time.time);

            if (effectParams.DistortNoiseTex != null)
                _distortMaterial.SetTexture(s_NoiseTex, effectParams.DistortNoiseTex);

            if (maskRT != null)
            {
                _distortMaterial.SetTexture(s_SourceTex, inputTex);
                _distortMaterial.SetTexture(s_MaskTex, maskRT);
                _distortMaterial.SetFloat(s_UseMask, 1f);
            }
            else
            {
                _distortMaterial.SetFloat(s_UseMask, 0f);
            }

            Graphics.Blit(inputTex, outputRT, _distortMaterial, 0);
        }

        public void ReleaseTemporaryResources() { }

        void EnsureMaterial()
        {
            if (_distortMaterial != null)
                return;
            var shader = Shader.Find("Hidden/TextureEffect/Distort");
            if (shader != null)
                _distortMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
    }
}
