#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Search;

namespace Game.Targeting
{
    public sealed class NullTargetChannelRuntime : ITargetChannelRuntime
    {
        readonly TargetChannelPreset _preset;
        readonly List<DynamicSearchHit> _hits = new();
        int _lastUpdatedFrame = int.MinValue;

        public NullTargetChannelRuntime(TargetChannelPreset preset)
        {
            _preset = preset?.CreateRuntimeCopy() ?? throw new System.ArgumentNullException(nameof(preset));
        }

        public string Tag => _preset.Tag;

        public bool Enabled
        {
            get => _preset.Enabled;
            set
            {
                _preset.Enabled = value;
                if (!_preset.Enabled)
                    _hits.Clear();
            }
        }

        public TargetChannelPreset CurrentPreset => _preset;
        public int LastUpdatedFrame => _lastUpdatedFrame;

        public List<DynamicSearchHit> Hits => _hits;

        public void Invalidate()
        {
            _lastUpdatedFrame = int.MinValue;
            _hits.Clear();
        }

        public void ForceRefresh()
        {
            _lastUpdatedFrame = Time.frameCount;
            _hits.Clear();
        }

        public bool SwapPreset(TargetChannelPreset preset)
        {
            _ = preset;
            return false;
        }

        public bool MutateSettings(TargetChannelRuntimeMutation mutation)
        {
            _ = mutation;
            return false;
        }

        public bool ResetRuntimeOverrides()
        {
            _hits.Clear();
            return false;
        }

        public bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits)
        {
            _ = hits;
            return false;
        }

        public bool ClearDirectTargets()
        {
            var changed = _hits.Count > 0;
            _hits.Clear();
            return changed;
        }
    }
}
