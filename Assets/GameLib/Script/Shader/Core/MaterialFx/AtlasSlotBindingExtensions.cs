#nullable enable
using System;
using UnityEngine;
using Game.MaterialFx.Generated;

namespace Game.MaterialFx
{
    public static class AtlasSlotBindingExtensions
    {
        const int DefaultAtlasSlotBindingPriority = 0;

        static readonly string[] AtlasSlotKeys = new[]
        {
            MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot0,
            MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot1,
            MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot2,
            MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot3,
            MaterialFxKeys.BaseShader.TextureSlot.AtlasSlot4,
        };

        public static void ApplyToSlot(this AtlasSlotBinding binding, IMaterialFxService service, string contextTag,
                                       int slotIndex, int priority = DefaultAtlasSlotBindingPriority)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (string.IsNullOrEmpty(contextTag))
                throw new ArgumentException("contextTag is required", nameof(contextTag));

            if (slotIndex < 0 || slotIndex >= AtlasSlotKeys.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            if (!binding.IsValid)
                return;

            var key = AtlasSlotKeys[slotIndex];
            if (string.IsNullOrEmpty(key))
                return;

            service.SetLayer(key, contextTag,
                MaterialFxTypedValue.FromVector4(binding.ToVector4()),
                MaterialFxBlendMode.Override,
                priority);
        }

        public static void ApplyBindings(this IMaterialFxService service, string contextTag, int priority,
                                         params (int slotIndex, AtlasSlotBinding binding)[] bindings)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            foreach (var (slotIndex, binding) in bindings)
            {
                binding.ApplyToSlot(service, contextTag, slotIndex, priority);
            }
        }
    }

    public static class TextureSlotRefExtensions
    {
        const int DefaultTextureSlotRefPriority = 0;

        public static TextureSlotRef ForSlot(int slotIndex, ChannelMask channel = ChannelMask.R,
                                             NoiseUVSpace uvSpace = NoiseUVSpace.SpriteLocal)
        {
            return TextureSlotRef.ForAtlasSlot(slotIndex, channel, uvSpace);
        }

        public static void ApplyToLayer(this TextureSlotRef slotRef, IMaterialFxService service, string contextTag,
                                        string sourcePath, int priority = DefaultTextureSlotRefPriority)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (string.IsNullOrEmpty(contextTag))
                throw new ArgumentException("contextTag is required", nameof(contextTag));

            if (string.IsNullOrEmpty(sourcePath))
                return;

            var basePath = sourcePath.TrimEnd('/');

            service.SetLayer(basePath + "/SlotType", contextTag,
                MaterialFxTypedValue.FromFloat((int)slotRef.SlotType), MaterialFxBlendMode.Override, priority);
            service.SetLayer(basePath + "/Channel", contextTag,
                MaterialFxTypedValue.FromFloat((int)slotRef.Channel), MaterialFxBlendMode.Override, priority);
            service.SetLayer(basePath + "/UVSpace", contextTag,
                MaterialFxTypedValue.FromFloat((int)slotRef.UVSpace), MaterialFxBlendMode.Override, priority);
            service.SetLayer(basePath + "/TilingOffset", contextTag,
                MaterialFxTypedValue.FromVector4(slotRef.TilingOffset), MaterialFxBlendMode.Override, priority);
            service.SetLayer(basePath + "/Remap", contextTag,
                MaterialFxTypedValue.FromVector4(slotRef.Remap), MaterialFxBlendMode.Override, priority);
        }
    }
}
