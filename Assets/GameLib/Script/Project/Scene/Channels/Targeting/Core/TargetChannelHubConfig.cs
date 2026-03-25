#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.Targeting
{
    [Serializable]
    public sealed class TargetChannelHubConfig
    {
        public bool AutoInitializeOnStart;
        public List<DynamicValue<TargetChannelPreset>> InitialChannels = new();

        public TargetChannelHubConfig(bool autoInitializeOnStart, List<DynamicValue<TargetChannelPreset>> initialChannels)
        {
            AutoInitializeOnStart = autoInitializeOnStart;
            InitialChannels = initialChannels ?? new List<DynamicValue<TargetChannelPreset>>();
        }
    }
}
