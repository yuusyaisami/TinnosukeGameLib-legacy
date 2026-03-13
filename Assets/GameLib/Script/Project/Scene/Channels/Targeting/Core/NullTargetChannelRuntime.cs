#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Search;

namespace Game.Targeting
{
    public sealed class NullTargetChannelRuntime : ITargetChannelRuntime
    {
        readonly TargetChannelDef _def;
        readonly List<DynamicSearchHit> _hits = new();
        int _lastUpdatedFrame = int.MinValue;

        public NullTargetChannelRuntime(TargetChannelDef def)
        {
            _def = def ?? throw new System.ArgumentNullException(nameof(def));
        }

        public string Tag => _def.Tag;

        public bool Enabled
        {
            get => _def.Enabled;
            set
            {
                _def.Enabled = value;
                if (!_def.Enabled)
                    _hits.Clear();
            }
        }

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
    }
}
