#nullable enable
using System;
using UnityEngine;

namespace Game.SharedTexture
{
    // ── SharedTextureBindSlot ───────────────────────────────────

    public enum SharedTextureBindSlot
    {
        ExternalA = 10,
        ExternalB = 20,
        CustomRT = 30,
    }

    // ── SharedTextureBindingDef ─────────────────────────────────

    [Serializable]
    public struct SharedTextureBindingDef
    {
        [Tooltip("対象 Player の tag (AnimationSpriteChannelPlayer 等)")]
        public string TargetPlayerTag;

        [Tooltip("SharedTextureChannelHub 上のタグ")]
        public string SharedTextureTag;

        [Tooltip("BaseShader の外部 Texture スロット")]
        public SharedTextureBindSlot BindSlot;

        [Tooltip("MaterialFx のコンテキストタグ")]
        public string ContextTag;

        [Tooltip("Binding の優先度")]
        public int Priority;

        [Tooltip("Texture が見つからないときに明示的にクリアするか")]
        public bool ClearWhenMissing;
    }
}
