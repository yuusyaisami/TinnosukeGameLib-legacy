using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.MaterialFx;

namespace Game.Channel
{
    /// <summary>
    /// Sprite/Image + BodyFx 繧呈桶縺・メ繝｣繝阪Ν螳夂ｾｩ縲・
    /// AnimationSpriteHub 逕ｨ縲・
    /// </summary>
    [Serializable]
    public sealed class AnimationSpriteChannelDef : ChannelDefBase, IChannelSprite, IChannelMaterialFx
    {
        [Header("Target")]
        [SerializeField][ShowIf("@isShowSpriteRenderer()")] SpriteRenderer spriteRenderer;
        [SerializeField][ShowIf("@isShowImage()")] UnityEngine.UI.Image image;

        [Header("Lifecycle")]
        [Tooltip("Spawn 譎ゅ↓SpritePreset繧定・蜍募・逕溘☆繧九°")]
        public bool playOnSpawn;
        [Header("Presets"), ShowIf(nameof(playOnSpawn))]
        [SerializeField] AnimationSpritePreset spritePreset;
        [SerializeField, Sirenix.OdinInspector.ListDrawerSettings(ShowPaging = false, ShowFoldout = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> materialFxPresetEntries = new();
        [SerializeField]
        [Tooltip("Inspector setting.")]
        [InlineProperty]
        BaseShaderFxPresetReference baseShaderPreset = new();

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public UnityEngine.UI.Image Image => image;
        public AnimationSpritePreset SpritePreset => spritePreset;
        public bool PlayOnSpawn => playOnSpawn;
        public IReadOnlyList<MaterialFxPresetEntry> MaterialFxPresetEntries => materialFxPresetEntries;
        public BaseShaderFxPreset BaseShaderPreset => baseShaderPreset.ResolvePreset();

        private bool isShowSpriteRenderer() => image == null || spriteRenderer != null;
        private bool isShowImage() => spriteRenderer == null || image != null;
        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (!spriteRenderer && owner)
            {
                spriteRenderer = owner.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (!image && owner)
            {
                image = owner.GetComponentInChildren<UnityEngine.UI.Image>(true);
            }

            // 荳｡譁ｹ null 縺ｧ繧ゆｸ譌ｦ險ｱ縺呻ｼ育ｴ・BodyFx 繝√Ε繝阪Ν縺ｪ縺ｩ繧ゅ≠繧翫≧繧具ｼ・
        }
    }
}
