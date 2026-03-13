using System;
using UnityEngine;

namespace Game.MaterialFx
{
    // ═══════════════════════════════════════════════════════════════════════════
    // TextureSlotTypes.cs - テクスチャスロットシステムの型定義
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // 仕様書 BaseShader-CompositeSystem-v1.0 Part 1/5 準拠
    //
    // Slot Pool 構成:
    //   Slot 0-4: Atlas Slot (Tier/Slice は AtlasSlotBinding で動的バインド)
    //   Slot 5:   External Texture A (_ExtTexA)
    //   Slot 6:   External Texture B (_ExtTexB)
    //   Slot 7:   Custom RenderTexture (_CustomRT)
    //
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// テクスチャソースの種類。
    /// Slot 0-4 は Atlas 用（Tier/Slice は別途バインド）、Slot 5-7 は外部テクスチャ用。
    /// シェーダー側の TEXTURE_SLOT_* と同期すること。
    /// </summary>
    public enum TextureSlotType
    {
        None = -1,

        // Atlas Slots (Tier/Slice は AtlasSlotBinding で指定)
        AtlasSlot0 = 0,
        AtlasSlot1 = 1,
        AtlasSlot2 = 2,
        AtlasSlot3 = 3,
        AtlasSlot4 = 4,

        // External Textures (Texture2D)
        ExternalA = 5,
        ExternalB = 6,

        // Custom RenderTexture
        CustomRT = 7,
    }

    /// <summary>
    /// UV 空間の種類。
    /// シェーダー側の NOISE_UV_SPACE_* と同期すること。
    /// </summary>
    public enum NoiseUVSpace
    {
        SpriteLocal = 0,  // スプライトローカル UV (0..1)
        Screen = 1,       // スクリーン UV
        AtlasRaw = 2,     // アトラス UV そのまま
        WorldXY = 3,      // ワールド座標 XY
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
    /// Atlas Slot に対する (Tier, Slice) のバインド情報。
    /// MaterialFx 経由で動的にセットされる。
    /// </summary>
    [Serializable]
    public struct AtlasSlotBinding : IEquatable<AtlasSlotBinding>
    {
        public int Tier;    // 0-5
        public int Slice;   // Tier 内の Slice インデックス

        public static AtlasSlotBinding Invalid => new() { Tier = -1, Slice = -1 };
        public bool IsValid => Tier >= 0 && Tier <= 5 && Slice >= 0;

        public AtlasSlotBinding(int tier, int slice)
        {
            Tier = tier;
            Slice = slice;
        }

        /// <summary>
        /// シェーダーに渡す Vector4 形式に変換。
        /// x=Tier, y=Slice, zw=reserved
        /// </summary>
        public Vector4 ToVector4() => new(Tier, Slice, 0, 0);

        public bool Equals(AtlasSlotBinding other) => Tier == other.Tier && Slice == other.Slice;
        public override bool Equals(object obj) => obj is AtlasSlotBinding other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tier, Slice);
        public static bool operator ==(AtlasSlotBinding a, AtlasSlotBinding b) => a.Equals(b);
        public static bool operator !=(AtlasSlotBinding a, AtlasSlotBinding b) => !a.Equals(b);
        public override string ToString() => $"AtlasSlot(Tier={Tier}, Slice={Slice})";
    }

    /// <summary>
    /// テクスチャソース参照。
    /// 各エフェクトセクションが「どの Slot を使うか」を指定する。
    /// Slot への (Tier, Slice) バインドは AtlasSlotBinding で別途設定。
    /// </summary>
    [Serializable]
    public struct TextureSlotRef : IEquatable<TextureSlotRef>
    {
        public TextureSlotType SlotType;
        public ChannelMask Channel;
        public NoiseUVSpace UVSpace;
        public Vector4 TilingOffset;  // xy=tiling, zw=offset
        public Vector4 Remap;         // x=bias, y=gain, z=gamma, w=invert

        public static TextureSlotRef Default => new()
        {
            SlotType = TextureSlotType.None,
            Channel = ChannelMask.R,
            UVSpace = NoiseUVSpace.SpriteLocal,
            TilingOffset = new Vector4(1, 1, 0, 0),
            Remap = new Vector4(0.5f, 0.5f, 1f, 0f),
        };

        /// <summary>
        /// 指定した Atlas Slot を参照する TextureSlotRef を作成。
        /// </summary>
        public static TextureSlotRef ForAtlasSlot(int slotIndex, ChannelMask channel = ChannelMask.R, NoiseUVSpace uvSpace = NoiseUVSpace.SpriteLocal)
        {
            if (slotIndex < 0 || slotIndex > 4)
                return Default;

            return new TextureSlotRef
            {
                SlotType = (TextureSlotType)slotIndex,
                Channel = channel,
                UVSpace = uvSpace,
                TilingOffset = new Vector4(1, 1, 0, 0),
                Remap = new Vector4(0.5f, 0.5f, 1f, 0f),
            };
        }

        /// <summary>
        /// 外部テクスチャを参照する TextureSlotRef を作成。
        /// </summary>
        public static TextureSlotRef ForExternal(TextureSlotType externalSlot, ChannelMask channel = ChannelMask.R, NoiseUVSpace uvSpace = NoiseUVSpace.SpriteLocal)
        {
            if (externalSlot < TextureSlotType.ExternalA || externalSlot > TextureSlotType.CustomRT)
                return Default;

            return new TextureSlotRef
            {
                SlotType = externalSlot,
                Channel = channel,
                UVSpace = uvSpace,
                TilingOffset = new Vector4(1, 1, 0, 0),
                Remap = new Vector4(0.5f, 0.5f, 1f, 0f),
            };
        }

        public bool Equals(TextureSlotRef other)
        {
            return SlotType == other.SlotType &&
                   Channel == other.Channel &&
                   UVSpace == other.UVSpace &&
                   TilingOffset == other.TilingOffset &&
                   Remap == other.Remap;
        }

        public override bool Equals(object obj) => obj is TextureSlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SlotType, Channel, UVSpace, TilingOffset, Remap);
        public static bool operator ==(TextureSlotRef a, TextureSlotRef b) => a.Equals(b);
        public static bool operator !=(TextureSlotRef a, TextureSlotRef b) => !a.Equals(b);
    }
}
