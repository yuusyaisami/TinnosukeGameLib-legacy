#if false // Unused: file is preserved for reference but excluded from compilation
using Game.MaterialFx.Generated;
using UnityEngine;

namespace Game.MaterialFx
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CompositeEffectBinder.cs - シェーダーへのコンポジットエフェクト適用ユーティリティ
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // 仕様書 BaseShader-CompositeSystem-v1.0 Part 4/5 準拠
    //
    // CompositeEffectBundle の各パラメータを MaterialPropertyBlock または
    // Material にバインドする。
    //
    // プロパティ名は MaterialFxKeys.g.cs から取得。
    // IMaterialFxPropertyRegistry を通じてシェーダープロパティ名を解決する。
    //
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // 【MaterialFxPropertyRegistry に追加が必要なプロパティ】
    //
    // ■ TextureSlot:
    //   - ExtTexA: ShaderPropertyName が "_AtlasSlot0" になっている → "_ExtTexA" に修正、ValueType を Texture2D に
    //   - ExtTexB: ShaderPropertyName が空 → "_ExtTexB" を設定、ValueType を Texture2D に
    //   - CustomRT: ShaderPropertyName が空 → "_CustomRT" を設定、ValueType を Texture2D に
    //
    // ■ FlowWarp:
    //   - Strength: ValueType が Float → Float2 に修正 (ShaderPropertyName: _FlowWarpStrength)
    //
    // ■ Emission:
    //   - Intensity: ShaderPropertyName が空 → 削除推奨（_EmissionColor.a に埋め込み済み）
    //
    // ■ Mask:
    //   - Invert: 新規追加が必要 (ShaderPropertyName: _MaskInvert, ValueType: Bool)
    //
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CompositeEffectBundle を MaterialPropertyBlock または Material にバインドするユーティリティ。
    /// IMaterialFxPropertyRegistry を通じてシェーダープロパティ名を解決する。
    /// </summary>
    public static class CompositeEffectBinder
    {
        // ────────────────────────────────────────────────────────────────────
        // Atlas Slot Binding
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定した Atlas Slot のバインドを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetAtlasSlotBinding(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            int slotIndex,
            AtlasSlotBinding binding)
        {
            if (block == null || registry == null || slotIndex < 0 || slotIndex > 4) return;

            string key = slotIndex switch
            {
                0 => MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot0,
                1 => MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot1,
                2 => MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot2,
                3 => MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot3,
                4 => MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot4,
                _ => null
            };

            if (string.IsNullOrEmpty(key)) return;
            var propName = registry.GetShaderPropertyName(key);
            if (string.IsNullOrEmpty(propName)) return;

            block.SetVector(Shader.PropertyToID(propName), binding.ToVector4());
        }

        /// <summary>
        /// すべての Atlas Slot バインドを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetAllAtlasSlotBindings(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in CompositeEffectBundle bundle)
        {
            if (block == null || registry == null) return;

            SetAtlasSlotBinding(block, registry, 0, bundle.Slot0);
            SetAtlasSlotBinding(block, registry, 1, bundle.Slot1);
            SetAtlasSlotBinding(block, registry, 2, bundle.Slot2);
            SetAtlasSlotBinding(block, registry, 3, bundle.Slot3);
            SetAtlasSlotBinding(block, registry, 4, bundle.Slot4);
        }

        // ────────────────────────────────────────────────────────────────────
        // TextureSlotRef 共通設定
        // ────────────────────────────────────────────────────────────────────

        static void SetTextureSlotRef(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            TextureSlotRef slotRef,
            string slotTypeKey,
            string channelKey,
            string uvSpaceKey,
            string tilingOffsetKey,
            string remapKey)
        {
            SetFloat(block, registry, slotTypeKey, (int)slotRef.SlotType);
            SetFloat(block, registry, channelKey, (int)slotRef.Channel);
            SetFloat(block, registry, uvSpaceKey, (int)slotRef.UVSpace);
            SetVector(block, registry, tilingOffsetKey, slotRef.TilingOffset);
            SetVector(block, registry, remapKey, slotRef.Remap);
        }

        // ────────────────────────────────────────────────────────────────────
        // Dissolve
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dissolve パラメータを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetDissolve(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in DissolveParams dissolve)
        {
            if (block == null || registry == null) return;

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Enabled, dissolve.Enabled ? 1f : 0f);

            SetTextureSlotRef(block, registry, dissolve.Source,
                MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.SlotType,
                MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Channel,
                MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.UVSpace,
                MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.TilingOffset,
                MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Remap);

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Threshold, dissolve.Progress);
            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeWidth, dissolve.EdgeWidth);
            SetVector(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeColor, dissolve.EdgeColor);
        }

        // ────────────────────────────────────────────────────────────────────
        // FlowWarp
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// FlowWarp パラメータを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetFlowWarp(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in FlowWarpParams flowWarp)
        {
            if (block == null || registry == null) return;

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Enabled, flowWarp.Enabled ? 1f : 0f);

            SetTextureSlotRef(block, registry, flowWarp.Source,
                MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.SlotType,
                MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.Channel,
                MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.UVSpace,
                MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.TilingOffset,
                MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.Remap);

            // Strength は Float2 だが、現在 MaterialFxKeys では Float として定義されている
            // TODO: MaterialFxPropertyRegistry で FlowWarp.Strength を Float2 に修正後、SetVector に変更
            SetVector(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Strength,
                new Vector4(flowWarp.Strength.x, flowWarp.Strength.y, 0, 0));
            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Speed, flowWarp.Speed);
        }

        // ────────────────────────────────────────────────────────────────────
        // Mask
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mask パラメータを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetMask(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in MaskParams mask)
        {
            if (block == null || registry == null) return;

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Mask.Enabled, mask.Enabled ? 1f : 0f);

            SetTextureSlotRef(block, registry, mask.Source,
                MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.SlotType,
                MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.Channel,
                MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.UVSpace,
                MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.TilingOffset,
                MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.Remap);

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Mask.Threshold, mask.Threshold);
            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Mask.Softness, mask.Softness);
            // TODO: Invert は MaterialFxKeys に未定義。追加後に有効化:
            // SetFloat(block, registry, MaterialFxKeys.BaseShader.Mask.Invert, mask.Invert ? 1f : 0f);
        }

        // ────────────────────────────────────────────────────────────────────
        // Emission
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Emission パラメータを MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetEmission(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in EmissionParams emission)
        {
            if (block == null || registry == null) return;

            SetFloat(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Emission.Enabled, emission.Enabled ? 1f : 0f);
            SetVector(block, registry, MaterialFxKeys.BaseShader.CompositeSystems.Emission.Color, emission.GetShaderVector());
            // Note: Intensity は _EmissionColor.a に埋め込み済み
        }

        // ────────────────────────────────────────────────────────────────────
        // Full Bundle
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// CompositeEffectBundle 全体を MaterialPropertyBlock に設定。
        /// </summary>
        public static void SetAll(
            MaterialPropertyBlock block,
            IMaterialFxPropertyRegistry registry,
            in CompositeEffectBundle bundle)
        {
            if (block == null || registry == null) return;

            SetAllAtlasSlotBindings(block, registry, bundle);
            SetDissolve(block, registry, bundle.Dissolve);
            SetFlowWarp(block, registry, bundle.FlowWarp);
            SetMask(block, registry, bundle.Mask);
            SetEmission(block, registry, bundle.Emission);
        }

        // ────────────────────────────────────────────────────────────────────
        // Private Helpers
        // ────────────────────────────────────────────────────────────────────

        static void SetFloat(MaterialPropertyBlock block, IMaterialFxPropertyRegistry registry, string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return;
            var propName = registry.GetShaderPropertyName(key);
            if (string.IsNullOrEmpty(propName)) return;
            block.SetFloat(Shader.PropertyToID(propName), value);
        }

        static void SetVector(MaterialPropertyBlock block, IMaterialFxPropertyRegistry registry, string key, Vector4 value)
        {
            if (string.IsNullOrEmpty(key)) return;
            var propName = registry.GetShaderPropertyName(key);
            if (string.IsNullOrEmpty(propName)) return;
            block.SetVector(Shader.PropertyToID(propName), value);
        }
    }
}

#endif // end unused
