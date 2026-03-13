#nullable enable
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// シェーダープロパティを送信する共通インターフェース。
    /// MPB / Material / ComputeShader / Global を同一コードで扱える。
    /// struct + ref で boxing を回避するための基盤。
    /// </summary>
    public interface IFxPropertySink
    {
        void SetInt(int id, int v);
        void SetFloat(int id, float v);
        void SetVector(int id, Vector4 v);
        void SetColor(int id, Color v);
        void SetMatrix(int id, Matrix4x4 v);
        void SetTexture(int id, Texture? v); // ★ null でクリア可能
    }

    /// <summary>
    /// MaterialPropertyBlock への送信用 Sink。
    /// SpriteRenderer / Renderer 系で使用。
    /// </summary>
    public struct MpbSink : IFxPropertySink
    {
        readonly MaterialPropertyBlock _mpb;
        public MpbSink(MaterialPropertyBlock mpb) => _mpb = mpb;

        public void SetInt(int id, int v) => _mpb.SetInt(id, v);
        public void SetFloat(int id, float v) => _mpb.SetFloat(id, v);
        public void SetVector(int id, Vector4 v) => _mpb.SetVector(id, v);
        public void SetColor(int id, Color v) => _mpb.SetColor(id, v);
        public void SetMatrix(int id, Matrix4x4 v) => _mpb.SetMatrix(id, v);
        public void SetTexture(int id, Texture? v) => _mpb.SetTexture(id, v!); // ★ null でクリア
    }

    /// <summary>
    /// Material インスタンスへの送信用 Sink。
    /// Graphic (uGUI) / TMP_Text 用。
    /// </summary>
    public struct MaterialSink : IFxPropertySink
    {
        readonly Material _material;
        public MaterialSink(Material material) => _material = material;

        public void SetInt(int id, int v) => _material.SetInt(id, v);
        public void SetFloat(int id, float v) => _material.SetFloat(id, v);
        public void SetVector(int id, Vector4 v) => _material.SetVector(id, v);
        public void SetColor(int id, Color v) => _material.SetColor(id, v);
        public void SetMatrix(int id, Matrix4x4 v) => _material.SetMatrix(id, v);
        public void SetTexture(int id, Texture? v) => _material.SetTexture(id, v); // ★ null でクリア
    }

    /// <summary>
    /// ComputeShader への送信用 Sink。
    /// ★設計判断: Compute には「クリア」概念が存在しない。
    /// - BaseShader (MPB/Material/Global) は null でクリア可能
    /// - Compute は「毎フレーム必ず上書き」が前提（null 送信は無視）
    /// - 理由: ComputeShader.SetTexture は null を許可しない環境がある
    /// </summary>
    public struct ComputeSink : IFxPropertySink
    {
        readonly ComputeShader _cs;
        readonly int _kernel;
        public ComputeSink(ComputeShader cs, int kernel)
        {
            _cs = cs;
            _kernel = kernel;
        }

        public void SetInt(int id, int v) => _cs.SetInt(id, v);
        public void SetFloat(int id, float v) => _cs.SetFloat(id, v);
        public void SetVector(int id, Vector4 v) => _cs.SetVector(id, v);
        public void SetColor(int id, Color v) => _cs.SetVector(id, v); // Color は Vector として送る
        public void SetMatrix(int id, Matrix4x4 v) => _cs.SetMatrix(id, v);
        
        /// <summary>
        /// ★Compute は null を無視（クリア概念なし）。
        /// BaseShader と異なり「前回値を維持」が正しい挙動。
        /// </summary>
        public void SetTexture(int id, Texture? v)
        {
            if (v != null) _cs.SetTexture(_kernel, id, v);
        }
    }

    /// <summary>
    /// Shader.SetGlobal* への送信用 Sink。
    /// Texture2DArray 共有バインドなど、全シェーダー共通のプロパティに使用。
    /// </summary>
    public struct GlobalShaderSink : IFxPropertySink
    {
        public void SetInt(int id, int v) => Shader.SetGlobalInt(id, v);
        public void SetFloat(int id, float v) => Shader.SetGlobalFloat(id, v);
        public void SetVector(int id, Vector4 v) => Shader.SetGlobalVector(id, v);
        public void SetColor(int id, Color v) => Shader.SetGlobalColor(id, v);
        public void SetMatrix(int id, Matrix4x4 v) => Shader.SetGlobalMatrix(id, v);
        public void SetTexture(int id, Texture? v) => Shader.SetGlobalTexture(id, v); // ★ null でクリア
    }
}
