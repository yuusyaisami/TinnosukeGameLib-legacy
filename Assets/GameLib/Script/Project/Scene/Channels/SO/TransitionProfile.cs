using System;
using DG.Tweening;
using Game.MaterialFx;
using Game.MaterialFx.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public interface ITransitionProfile
    {
        float Duration { get; }
        Ease Ease { get; }
        int BlendMode { get; }
        Vector4 TransitionParams { get; }
        string LayerName { get; }

        void ApplyBegin(IMaterialFxService fx, Sprite fromSprite);
        void ApplyProgress(IMaterialFxService fx, float t01);
        void ApplyEnd(IMaterialFxService fx);
    }

    /// <summary>
    /// Transition profile data stored as a managed reference (SerializeReference).
    /// Transition profile data (managed reference).
    /// </summary>
    [Serializable]
    public sealed class TransitionProfile : ITransitionProfile
    {
        public const string DefaultLayerName = "Transition";

        [Header("Timing")]
        [Tooltip("繝医Λ繝ｳ繧ｸ繧ｷ繝ｧ繝ｳ蜈ｨ菴薙・遘呈焚")]
        [Min(0.001f)]
        public float duration = 0.3f;

        [Tooltip("繧､繝ｼ繧ｸ繝ｳ繧ｰ髢｢謨ｰ")]
        public Ease ease = Ease.OutQuad;

        [Header("Blend Settings")]
        [Tooltip("0 = CrossFade, 1 = Dissolve, 2 = Wipe, ...")]
        public int blendMode;

        [Tooltip("Params (x: edgeWidth, y: softness, z: direction, w: reserved)")]
        public Vector4 transitionParams = new(0.1f, 0.05f, 1f, 0f);

        [Header("Layer")]
        [Tooltip("Inspector setting.")]
        public string layerName = DefaultLayerName;

        float ITransitionProfile.Duration => duration;
        Ease ITransitionProfile.Ease => ease;
        int ITransitionProfile.BlendMode => blendMode;
        Vector4 ITransitionProfile.TransitionParams => transitionParams;
        string ITransitionProfile.LayerName => string.IsNullOrEmpty(layerName) ? DefaultLayerName : layerName;

        public void ApplyBegin(IMaterialFxService fx, Sprite fromSprite)
        {
            if (fx == null)
                return;

            string layer = string.IsNullOrEmpty(layerName) ? DefaultLayerName : layerName;

            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Enabled,
                layer,
                MaterialFxTypedValue.FromBool(true),
                MaterialFxBlendMode.Override);

            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.BlendMode,
                layer,
                MaterialFxTypedValue.FromInt(blendMode),
                MaterialFxBlendMode.Override);

            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Progress,
                layer,
                MaterialFxTypedValue.FromFloat(0f),
                MaterialFxBlendMode.Override);

            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Params,
                layer,
                MaterialFxTypedValue.FromVector4(transitionParams),
                MaterialFxBlendMode.Override);

            if (fromSprite != null && fromSprite.texture != null)
            {
                fx.SetLayer(
                    MaterialFxKeys.BaseShader.ExternalTextures.ExtTexA,
                    layer,
                    MaterialFxTypedValue.FromTexture(fromSprite.texture),
                    MaterialFxBlendMode.Override);

                Vector4 uvRect = CalcSpriteUVRect(fromSprite);
                fx.SetLayer(
                    MaterialFxKeys.BaseShader.Transition.FromSpriteUVRect,
                    layer,
                    MaterialFxTypedValue.FromVector4(uvRect),
                    MaterialFxBlendMode.Override);
            }
        }

        public void ApplyProgress(IMaterialFxService fx, float t01)
        {
            if (fx == null)
                return;

            string layer = string.IsNullOrEmpty(layerName) ? DefaultLayerName : layerName;
            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Progress,
                layer,
                MaterialFxTypedValue.FromFloat(Mathf.Clamp01(t01)),
                MaterialFxBlendMode.Override);
        }

        public void ApplyEnd(IMaterialFxService fx)
        {
            if (fx == null)
                return;

            string layer = string.IsNullOrEmpty(layerName) ? DefaultLayerName : layerName;
            fx.SetLayer(
                MaterialFxKeys.BaseShader.Transition.Enabled,
                layer,
                MaterialFxTypedValue.FromBool(false),
                MaterialFxBlendMode.Override);
        }

        static Vector4 CalcSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(0, 0, 1, 1);

            Rect r = sprite.textureRect;
            float tw = sprite.texture.width;
            float th = sprite.texture.height;

            float minU = r.xMin / tw;
            float minV = r.yMin / th;
            float maxU = r.xMax / tw;
            float maxV = r.yMax / th;

            return new Vector4(minU, minV, maxU, maxV);
        }
    }
}
