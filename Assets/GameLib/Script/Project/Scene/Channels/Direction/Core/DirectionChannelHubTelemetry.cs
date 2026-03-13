using System.Collections.Generic;
using UnityEngine;

namespace Game.Direction
{
    public readonly struct DirectionLayerTelemetrySnapshot
    {
        public readonly string Tag;
        public readonly bool Enabled;
        public readonly Vector2 Direction;
        public readonly int Priority;
        public readonly DirectionBlendMode BlendMode;
        public readonly float Influence;
        public readonly float TransitionSpeedOverride;

        public DirectionLayerTelemetrySnapshot(
            string tag,
            bool enabled,
            Vector2 direction,
            int priority,
            DirectionBlendMode blendMode,
            float influence,
            float transitionSpeedOverride)
        {
            Tag = tag;
            Enabled = enabled;
            Direction = direction;
            Priority = priority;
            BlendMode = blendMode;
            Influence = influence;
            TransitionSpeedOverride = transitionSpeedOverride;
        }
    }

    public readonly struct DirectionChannelHubTelemetrySnapshot
    {
        public readonly int Version;
        public readonly Vector2 Target;
        public readonly Vector2 Output;
        public readonly float TransitionSpeedOverride;
        public readonly bool IsDirty;
        public readonly bool IsSortDirty;
        public readonly float DefaultRiseSpeed;
        public readonly float DefaultFallSpeed;
        public readonly float OppositeDotThreshold;
        public readonly float DownwardBiasStrength;
        public readonly IReadOnlyList<DirectionLayerTelemetrySnapshot> Layers;

        public DirectionChannelHubTelemetrySnapshot(
            int version,
            Vector2 target,
            Vector2 output,
            float transitionSpeedOverride,
            bool isDirty,
            bool isSortDirty,
            float defaultRiseSpeed,
            float defaultFallSpeed,
            float oppositeDotThreshold,
            float downwardBiasStrength,
            IReadOnlyList<DirectionLayerTelemetrySnapshot> layers)
        {
            Version = version;
            Target = target;
            Output = output;
            TransitionSpeedOverride = transitionSpeedOverride;
            IsDirty = isDirty;
            IsSortDirty = isSortDirty;
            DefaultRiseSpeed = defaultRiseSpeed;
            DefaultFallSpeed = defaultFallSpeed;
            OppositeDotThreshold = oppositeDotThreshold;
            DownwardBiasStrength = downwardBiasStrength;
            Layers = layers;
        }
    }

    public interface IDirectionChannelHubTelemetry
    {
        int TelemetryVersion { get; }
        DirectionChannelHubTelemetrySnapshot GetTelemetrySnapshot();
    }
}
