#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Targeting
{
    [Serializable]
    public sealed class TargetChannelHubConfig
    {
        public bool AutoInitializeOnStart;
        public List<TargetChannelDef> InitialChannels = new();

        public TargetChannelHubConfig(bool autoInitializeOnStart, List<TargetChannelDef> initialChannels)
        {
            AutoInitializeOnStart = autoInitializeOnStart;
            InitialChannels = initialChannels ?? new List<TargetChannelDef>();
        }
    }
}

