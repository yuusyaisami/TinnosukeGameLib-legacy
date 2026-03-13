using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.MaterialFx;

namespace Game.Channel
{
    /// <summary>
    /// Sprite/Image + BodyFx を扱うチャネル定義。
    /// AnimationSpriteHub 用。
    /// </summary>
    [Serializable]
    public sealed class AnimationSpriteChannelDef : ChannelDefBase, IChannelSprite, IChannelMaterialFx
    {
        [Header("Target")]
        [SerializeField][ShowIf("@isShowSpriteRenderer()")] SpriteRenderer spriteRenderer;
        [SerializeField][ShowIf("@isShowImage()")] UnityEngine.UI.Image image;

        [Header("Lifecycle")]
        [Tooltip("Spawn 時にSpritePresetを自動再生するか")]
        public bool playOnSpawn;
        [Header("Presets"), ShowIf(nameof(playOnSpawn))]
        [SerializeField] AnimationSpritePreset spritePreset;
        [SerializeField, Sirenix.OdinInspector.ListDrawerSettings(ShowPaging = false, ShowFoldout = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        public List<MaterialFxPresetEntry> materialFxPresetEntries = new();
        [SerializeField]
        [Tooltip("起動時に適用する BaseShader のプリセット。")]
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

            // 両方 null でも一旦許す（純 BodyFx チャネルなどもありうる）
        }
    }
}
