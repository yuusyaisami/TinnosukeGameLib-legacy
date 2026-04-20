#nullable enable
using System;
using UnityEngine;

namespace Game.SharedTexture
{
    // 笏笏 SharedTextureBindSlot 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

    public enum SharedTextureBindSlot
    {
        ExternalA = 10,
        ExternalB = 20,
        CustomRT = 30,
    }

    // 笏笏 SharedTextureBindingDef 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

    [Serializable]
    public struct SharedTextureBindingDef
    {
        [Tooltip("蟇ｾ雎｡ Player 縺ｮ tag (AnimationSpriteChannelPlayer 遲・")]
        public string TargetPlayerTag;

        [Tooltip("SharedTextureChannelHub 荳翫・繧ｿ繧ｰ")]
        public string SharedTextureTag;

        [Tooltip("BaseShader 縺ｮ螟夜Κ Texture 繧ｹ繝ｭ繝・ヨ")]
        public SharedTextureBindSlot BindSlot;

        [Tooltip("MaterialFx 縺ｮ繧ｳ繝ｳ繝・く繧ｹ繝医ち繧ｰ")]
        public string ContextTag;

        [Tooltip("Binding 縺ｮ蜆ｪ蜈亥ｺｦ")]
        public int Priority;

        [Tooltip("Inspector setting.")]
        public bool ClearWhenMissing;
    }
}
