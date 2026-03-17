using Game.MaterialFx.Generated;
using UnityEngine;

namespace Game.MaterialFx
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CompositeEffectKeys.cs - MaterialFx キーからの読み取り/書き込みヘルパー
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // MaterialFxService と CompositeEffectBundle の橋渡しを行う。
    // MaterialFxKeys.BaseShader.Dissolve.* などのキーを通じて
    // パラメータを取得/設定する。
    //
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IMaterialFxService を通じて CompositeEffectBundle を設定するヘルパー。
    /// </summary>
    public static class CompositeEffectKeys
    {
        /// <summary>
        /// 指定コンテキストで Dissolve パラメータを設定。
        /// </summary>
        public static void SetDissolve(
            IMaterialFxService service,
            string context,
            in DissolveParams dissolve,
            int priority = 0)
        {
            if (service == null) return;

            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Enabled, context,
                MaterialFxTypedValue.FromBool(dissolve.Enabled), MaterialFxBlendMode.Override, priority);

            // Source
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.SlotType, context,
                MaterialFxTypedValue.FromFloat((int)dissolve.Source.SlotType), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Channel, context,
                MaterialFxTypedValue.FromFloat((int)dissolve.Source.Channel), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.UVSpace, context,
                MaterialFxTypedValue.FromFloat((int)dissolve.Source.UVSpace), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.TilingOffset, context,
                MaterialFxTypedValue.FromVector4(dissolve.Source.TilingOffset), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Remap, context,
                MaterialFxTypedValue.FromVector4(dissolve.Source.Remap), MaterialFxBlendMode.Override, priority);

            // Params
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Threshold, context,
                MaterialFxTypedValue.FromFloat(dissolve.Progress), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeWidth, context,
                MaterialFxTypedValue.FromFloat(dissolve.EdgeWidth), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeColor, context,
                MaterialFxTypedValue.FromVector4(dissolve.EdgeColor), MaterialFxBlendMode.Override, priority);
        }

        /// <summary>
        /// 指定コンテキストで FlowWarp パラメータを設定。
        /// </summary>
        public static void SetFlowWarp(
            IMaterialFxService service,
            string context,
            in FlowWarpParams flowWarp,
            int priority = 0)
        {
            if (service == null) return;

            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Enabled, context,
                MaterialFxTypedValue.FromBool(flowWarp.Enabled), MaterialFxBlendMode.Override, priority);

            // Source
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.SlotType, context,
                MaterialFxTypedValue.FromFloat((int)flowWarp.Source.SlotType), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.Channel, context,
                MaterialFxTypedValue.FromFloat((int)flowWarp.Source.Channel), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.UVSpace, context,
                MaterialFxTypedValue.FromFloat((int)flowWarp.Source.UVSpace), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.TilingOffset, context,
                MaterialFxTypedValue.FromVector4(flowWarp.Source.TilingOffset), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Source.Remap, context,
                MaterialFxTypedValue.FromVector4(flowWarp.Source.Remap), MaterialFxBlendMode.Override, priority);

            // Params
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Strength, context,
                MaterialFxTypedValue.FromFloat(flowWarp.Strength.magnitude), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.FlowWarp.Speed, context,
                MaterialFxTypedValue.FromFloat(flowWarp.Speed), MaterialFxBlendMode.Override, priority);
        }

        /// <summary>
        /// 指定コンテキストで Mask パラメータを設定。
        /// </summary>
        public static void SetMask(
            IMaterialFxService service,
            string context,
            in MaskParams mask,
            int priority = 0)
        {
            if (service == null) return;

            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Enabled, context,
            MaterialFxTypedValue.FromBool(mask.Enabled), MaterialFxBlendMode.Override, priority);

            // Source
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.SlotType, context,
            MaterialFxTypedValue.FromFloat((int)mask.Source.SlotType), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.Channel, context,
            MaterialFxTypedValue.FromFloat((int)mask.Source.Channel), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.UVSpace, context,
            MaterialFxTypedValue.FromFloat((int)mask.Source.UVSpace), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.TilingOffset, context,
            MaterialFxTypedValue.FromVector4(mask.Source.TilingOffset), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Source.Remap, context,
            MaterialFxTypedValue.FromVector4(mask.Source.Remap), MaterialFxBlendMode.Override, priority);

            // Params
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Threshold, context,
            MaterialFxTypedValue.FromFloat(mask.Threshold), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Mask.Softness, context,
            MaterialFxTypedValue.FromFloat(mask.Softness), MaterialFxBlendMode.Override, priority);
        }

        /// <summary>
        /// 指定コンテキストで Emission パラメータを設定。
        /// </summary>
        public static void SetEmission(
            IMaterialFxService service,
            string context,
            in EmissionParams emission,
            int priority = 0)
        {
            if (service == null) return;

            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Emission.Enabled, context,
                MaterialFxTypedValue.FromBool(emission.Enabled), MaterialFxBlendMode.Override, priority);
            service.SetLayer(MaterialFxKeys.BaseShader.CompositeSystems.Emission.Color, context,
                MaterialFxTypedValue.FromVector4(emission.GetShaderVector()), MaterialFxBlendMode.Override, priority);
        }

        /// <summary>
        /// 指定コンテキストで CompositeEffectBundle 全体を設定。
        /// </summary>
        public static void SetAll(
            IMaterialFxService service,
            string context,
            in CompositeEffectBundle bundle,
            int priority = 0)
        {
            SetDissolve(service, context, bundle.Dissolve, priority);
            SetFlowWarp(service, context, bundle.FlowWarp, priority);
            SetMask(service, context, bundle.Mask, priority);
            SetEmission(service, context, bundle.Emission, priority);
        }

        /// <summary>
        /// 指定コンテキストのコンポジットエフェクトをクリア（デフォルト値に戻す）。
        /// </summary>
        public static void Clear(
            IMaterialFxService service,
            string context)
        {
            if (service == null) return;

            var defaultBundle = CompositeEffectBundle.Default;
            SetAll(service, context, defaultBundle, MaterialFxContextTags.DefaultPriority);
        }
    }
}
