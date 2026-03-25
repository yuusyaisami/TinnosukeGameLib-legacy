#nullable enable
using System;
using Game.Common;
using Game.Commands.VNext;
using UnityEngine;

namespace Game.UI
{
    public enum WorldSliderVisualizerMode
    {
        Simple = 10,
        Segmented = 20,
    }

    public enum WorldSliderAreaFillAxis
    {
        SizeX = 10,
        SizeY = 20,
    }

    public enum WorldSliderAreaOriginSide
    {
        Min = 10,
        Max = 20,
    }

    public enum WorldSliderBindingPriority
    {
        Scalar = 10,
        Blackboard = 20,
    }

    public enum WorldSliderSegmentPlacementMode
    {
        EqualInterval = 10,
        CustomEntries = 20,
    }

    public enum WorldSliderSegmentDisplayMode
    {
        Continuous = 10,
        ReachedStageFloor = 20,
    }

    public enum WorldSliderSegmentCrossingDirection
    {
        Increase = 10,
        Decrease = 20,
    }

    public readonly struct WorldSliderOutputSnapshot
    {
        public readonly float TargetRawValue;
        public readonly float TargetNormalizedValue;
        public readonly float DisplayedRawValue;
        public readonly float DisplayedNormalizedValue;

        public WorldSliderOutputSnapshot(
            float targetRawValue,
            float targetNormalizedValue,
            float displayedRawValue,
            float displayedNormalizedValue)
        {
            TargetRawValue = targetRawValue;
            TargetNormalizedValue = targetNormalizedValue;
            DisplayedRawValue = displayedRawValue;
            DisplayedNormalizedValue = displayedNormalizedValue;
        }
    }

    public interface IWorldSliderOutput
    {
        float TargetRawValue { get; }
        float TargetNormalizedValue { get; }
        float DisplayedRawValue { get; }
        float DisplayedNormalizedValue { get; }
        event Action<WorldSliderOutputSnapshot>? OnUpdated;
    }

    public interface IWorldSliderPlayerService : IWorldSliderOutput
    {
    }

    public interface IWorldSliderVisualizerService
    {
    }

    public interface IWorldSliderOptions
    {
        DynamicValue<WorldSliderVisualizerPreset> VisualizerPresetValue { get; }
        DynamicValue<WorldSliderPlayerPreset> PlayerPresetValue { get; }
        SpriteRenderer? SimpleBarRenderer { get; }
        Transform? SegmentBarsRoot { get; }
        Transform? SegmentMarkersRoot { get; }
        ActorSource AreaActorSource { get; }
        string AreaChannelTag { get; }
        Transform OwnerTransform { get; }
    }
}
