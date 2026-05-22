#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using UnityEngine;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public class AnimationSpriteHubAuthoring : MonoBehaviour
    {
        [SerializeField]
        AnimationSpriteChannelDef[] channels = Array.Empty<AnimationSpriteChannelDef>();

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool replaceWithTransparentOnRelease = false;

        [SerializeField]
        [Tooltip("VisualSystem selector 逕ｨ縺ｮ HubTag")]
        string hubTag = string.Empty;

        public static AuthoringComponentKind ComponentKind => AuthoringComponentKind.Declaration;

        public IReadOnlyList<AnimationSpriteChannelDef> Channels => channels ?? Array.Empty<AnimationSpriteChannelDef>();

        public bool ReplaceWithTransparentOnRelease => replaceWithTransparentOnRelease;

        public string HubTag => hubTag;

        internal void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(hubTag))
                throw new InvalidOperationException("AnimationSpriteHubAuthoring requires a non-empty hub tag.");

            var seenTags = new HashSet<string>(StringComparer.Ordinal);
            if (channels == null)
                return;

            for (int i = 0; i < channels.Length; i++)
            {
                AnimationSpriteChannelDef channel = channels[i] ?? throw new InvalidOperationException($"AnimationSpriteHubAuthoring contains a null channel entry at index {i}.");
                var tag = channel.Tag;
                if (string.IsNullOrWhiteSpace(tag))
                    throw new InvalidOperationException($"AnimationSpriteHubAuthoring channel entry at index {i} has an empty tag.");

                tag = tag.Trim();
                if (string.Equals(tag, "default", StringComparison.Ordinal))
                    throw new InvalidOperationException($"AnimationSpriteHubAuthoring channel entry at index {i} must use an explicit non-default tag.");

                if (!seenTags.Add(tag))
                    throw new InvalidOperationException($"AnimationSpriteHubAuthoring contains a duplicate channel tag '{tag}'.");

                if (channel.SpriteRenderer == null && channel.Image == null)
                    throw new InvalidOperationException($"AnimationSpriteHubAuthoring channel '{tag}' requires an explicit SpriteRenderer or Image target.");
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message, this);
            }
        }
#endif
    }
}