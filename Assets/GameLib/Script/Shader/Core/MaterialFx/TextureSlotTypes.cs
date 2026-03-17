using System;
using UnityEngine;

namespace Game.MaterialFx
{
    // ═══════════════════════════════════════════════════════════════════════════
    // TextureSlotTypes.cs - テクスチャスロットシステムの型定義
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // Slot Pool 構成:
    //   Slot 5: External Texture A (_ExtTexA)
    //   Slot 6: External Texture B (_ExtTexB)
    //   Slot 7: Custom RenderTexture (_CustomRT)
    //
    // Atlas / Tier / Slice ベースの旧契約は削除済み。
    //
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// テクスチャソースの種類。
    /// シェーダー側の TEXTURE_SLOT_* と同期すること。
    /// </summary>
    public enum TextureSlotType
    {
        None = -1,
        ExternalA = 5,
        ExternalB = 6,
        CustomRT = 7,
    }

    /// <summary>
    /// UV 空間の種類。
    /// シェーダー側の NOISE_UV_SPACE_* と同期すること。
    /// </summary>
    public enum NoiseUVSpace
    {
        SpriteLocal = 0,
        Screen = 1,
        TextureRaw = 2,
        WorldXY = 3,
    }

    /// <summary>
    /// チャンネルマスク。
    /// シェーダー側の CHANNEL_* と同期すること。
    /// </summary>
    [Flags]
    public enum ChannelMask
    {
        None = 0,
        R = 1,
        G = 2,
        B = 4,
        A = 8,
        RG = R | G,
        RGB = R | G | B,
        RGBA = R | G | B | A,
    }

    /// <summary>
    /// テクスチャソース参照。
    /// 各エフェクトセクションが「どの external slot を使うか」を指定する。
    /// </summary>
    [Serializable]
    public struct TextureSlotRef : IEquatable<TextureSlotRef>
    {
        public TextureSlotType SlotType;
        public ChannelMask Channel;
        public NoiseUVSpace UVSpace;
        public Vector4 TilingOffset;
        public Vector4 Remap;

        public static TextureSlotRef Default => new()
        {
            SlotType = TextureSlotType.None,
            Channel = ChannelMask.R,
            UVSpace = NoiseUVSpace.SpriteLocal,
            TilingOffset = new Vector4(1f, 1f, 0f, 0f),
            Remap = new Vector4(0.5f, 0.5f, 1f, 0f),
        };

        /// <summary>
        /// 外部テクスチャを参照する TextureSlotRef を作成。
        /// </summary>
        public static TextureSlotRef ForExternal(
            TextureSlotType externalSlot,
            ChannelMask channel = ChannelMask.R,
            NoiseUVSpace uvSpace = NoiseUVSpace.SpriteLocal)
        {
            if (externalSlot < TextureSlotType.ExternalA || externalSlot > TextureSlotType.CustomRT)
                return Default;

            return new TextureSlotRef
            {
                SlotType = externalSlot,
                Channel = channel,
                UVSpace = uvSpace,
                TilingOffset = new Vector4(1f, 1f, 0f, 0f),
                Remap = new Vector4(0.5f, 0.5f, 1f, 0f),
            };
        }

        public bool Equals(TextureSlotRef other)
        {
            return SlotType == other.SlotType
                   && Channel == other.Channel
                   && UVSpace == other.UVSpace
                   && TilingOffset == other.TilingOffset
                   && Remap == other.Remap;
        }

        public override bool Equals(object obj) => obj is TextureSlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SlotType, Channel, UVSpace, TilingOffset, Remap);
        public static bool operator ==(TextureSlotRef a, TextureSlotRef b) => a.Equals(b);
        public static bool operator !=(TextureSlotRef a, TextureSlotRef b) => !a.Equals(b);
    }
}
