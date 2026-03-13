#nullable enable
using UnityEngine;

namespace Game.TextureEffect
{
    /// <summary>
    /// Blur effect: downsample + 2-pass separable gaussian blur + mask composite.
    /// </summary>
    public sealed class BlurEffect : ITextureEffect
    {
        static readonly int s_BlurSize = Shader.PropertyToID("_BlurSize");
        static readonly int s_MainTex = Shader.PropertyToID("_MainTex");
        static readonly int s_SourceTex = Shader.PropertyToID("_SourceTex");
        static readonly int s_MaskTex = Shader.PropertyToID("_MaskTex");
        static readonly int s_UseMask = Shader.PropertyToID("_UseMask");

        Material? _blurMaterial;
        RenderTexture? _pingRT;
        RenderTexture? _pongRT;

        public TextureEffectKind Kind => TextureEffectKind.Blur;

        public void Execute(
            Texture inputTex,
            RenderTexture outputRT,
            RenderTexture? maskRT,
            in TextureEffectParams effectParams,
            float resolutionScale)
        {
            EnsureMaterial();
            if (_blurMaterial == null)
                return;

            int iterations = Mathf.Max(1, effectParams.BlurIterations);
            float spread = effectParams.BlurSpread;
            int downsample = Mathf.Max(1, effectParams.BlurDownsample);

            int w = Mathf.Max(1, inputTex.width / downsample);
            int h = Mathf.Max(1, inputTex.height / downsample);

            EnsureRT(ref _pingRT, w, h, "BlurPing");
            EnsureRT(ref _pongRT, w, h, "BlurPong");

            // Downsample pass (blit input → ping)
            Graphics.Blit(inputTex, _pingRT);

            // Separable blur passes
            for (int i = 0; i < iterations; i++)
            {
                float blurSize = spread * (i + 1);

                // Horizontal
                _blurMaterial.SetFloat(s_BlurSize, blurSize);
                _blurMaterial.SetVector("_BlurDirection", new Vector4(1f / w, 0, 0, 0));
                Graphics.Blit(_pingRT, _pongRT, _blurMaterial, 0);

                // Vertical
                _blurMaterial.SetVector("_BlurDirection", new Vector4(0, 1f / h, 0, 0));
                Graphics.Blit(_pongRT, _pingRT, _blurMaterial, 0);
            }

            // Composite with mask
            if (maskRT != null)
            {
                _blurMaterial.SetTexture(s_SourceTex, inputTex);
                _blurMaterial.SetTexture(s_MaskTex, maskRT);
                _blurMaterial.SetFloat(s_UseMask, 1f);
                Graphics.Blit(_pingRT, outputRT, _blurMaterial, 1);
            }
            else
            {
                Graphics.Blit(_pingRT, outputRT);
            }
        }

        public void ReleaseTemporaryResources()
        {
            // Keep ping/pong for reuse; released in Dispose via pipeline
        }

        void EnsureMaterial()
        {
            if (_blurMaterial != null)
                return;
            var shader = Shader.Find("Hidden/TextureEffect/Blur");
            if (shader != null)
                _blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        static void EnsureRT(ref RenderTexture? rt, int w, int h, string name)
        {
            if (rt != null && rt.width == w && rt.height == h)
                return;
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
        }
    }
}
