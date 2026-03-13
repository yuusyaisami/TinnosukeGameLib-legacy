#nullable enable
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// BaseShader（MPB/Material/Global）専用の Binder。
    /// Sender = BaseShader のみ許可。
    /// ★GC 対策: delegate/ラムダを使わず switch で分岐。
    /// </summary>
    public sealed class MaterialFxBindBaseShader
    {
        readonly IMaterialFxPropertyRegistry _registry;

        public MaterialFxBindBaseShader(IMaterialFxPropertyRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat<TSink>(string stableKey, ref TSink sink, float v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Float);
            sink.SetFloat(id, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt<TSink>(string stableKey, ref TSink sink, int v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Int);
            sink.SetInt(id, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBool<TSink>(string stableKey, ref TSink sink, bool v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Bool);
            sink.SetInt(id, v ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2<TSink>(string stableKey, ref TSink sink, Vector2 v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Float2);
            sink.SetVector(id, new Vector4(v.x, v.y, 0f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector3<TSink>(string stableKey, ref TSink sink, Vector3 v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Float3);
            sink.SetVector(id, new Vector4(v.x, v.y, v.z, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector4<TSink>(string stableKey, ref TSink sink, Vector4 v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Float4);
            sink.SetVector(id, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColor<TSink>(string stableKey, ref TSink sink, Color v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Color);
            sink.SetColor(id, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix<TSink>(string stableKey, ref TSink sink, Matrix4x4 v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Matrix4x4);
            sink.SetMatrix(id, v);
        }

        /// <summary>
        /// ★nullable: null は "クリア" を意図するケースがある。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture<TSink>(string stableKey, ref TSink sink, Texture? v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.Texture);
            sink.SetTexture(id, v);
        }

        /// <summary>
        /// ★Texture2DArray? で型安全を確保。Texture の親型を受けない。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTextureArray<TSink>(string stableKey, ref TSink sink, Texture2DArray? v) where TSink : struct, IFxPropertySink
        {
            var id = GetValidatedPropertyId(stableKey, ValueKind.TextureArray);
            sink.SetTexture(id, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetValidatedPropertyId(string stableKey, ValueKind expectedType)
        {
            if (!_registry.TryGet(stableKey, out var meta))
                throw new InvalidOperationException($"[MaterialFxBindBaseShader] Unknown key: '{stableKey}'");

            if (meta.Sender != MaterialFxSenderKind.BaseShader)
                throw new InvalidOperationException($"[MaterialFxBindBaseShader] Sender mismatch key='{stableKey}' expected=BaseShader actual={meta.Sender}");

            if (meta.ValueType != expectedType)
                throw new InvalidOperationException($"[MaterialFxBindBaseShader] ValueType mismatch key='{stableKey}' expected={expectedType} actual={meta.ValueType}");

            if (string.IsNullOrEmpty(meta.ShaderPropertyName))
                throw new InvalidOperationException($"[MaterialFxBindBaseShader] ShaderPropertyName empty key='{stableKey}'");

            return Shader.PropertyToID(meta.ShaderPropertyName);
        }
    }

}
