#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using UnityEngine;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public class MeshChannelHubAuthoring : MonoBehaviour
    {
        [SerializeField]
        MeshChannelEntry[] _entries = Array.Empty<MeshChannelEntry>();

        public static AuthoringComponentKind ComponentKind => AuthoringComponentKind.Declaration;

        protected MeshChannelEntry[] Entries => _entries ?? Array.Empty<MeshChannelEntry>();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_entries == null)
            {
                _entries = Array.Empty<MeshChannelEntry>();
                return;
            }

            var seenTags = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                MeshChannelEntry entry = _entries[i];
                if (entry == null)
                    continue;

                var normalizedTag = string.IsNullOrWhiteSpace(entry.Tag) ? string.Empty : entry.Tag.Trim();
                if (string.IsNullOrEmpty(normalizedTag))
                {
                    Debug.LogError($"[MeshChannelHub] Entry {i} on '{name}' is missing a channel tag.", this);
                    continue;
                }

                if (!seenTags.Add(normalizedTag))
                    Debug.LogError($"[MeshChannelHub] Duplicate channel tag '{normalizedTag}' found on '{name}'.", this);

                entry.Tag = normalizedTag;
            }
        }
#endif
    }
}