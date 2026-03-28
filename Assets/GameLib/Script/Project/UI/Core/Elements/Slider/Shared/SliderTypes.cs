#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    public enum SliderAreaFillAxis
    {
        SizeX = 10,
        SizeY = 20,
    }

    public enum SliderAreaOriginSide
    {
        Min = 10,
        Max = 20,
    }

    public enum SliderBindingPriority
    {
        Scalar = 10,
        Blackboard = 20,
    }

    public enum SliderSegmentPlacementMode
    {
        EqualInterval = 10,
        CustomEntries = 20,
    }

    public enum SliderSegmentDisplayMode
    {
        Continuous = 10,
        ReachedStageFloor = 20,
    }

    public enum SliderSegmentCrossingDirection
    {
        Increase = 10,
        Decrease = 20,
    }

    public enum SliderRangeSourceMode
    {
        AreaChannel = 10,
        RectTransform = 20,
    }

    public enum SliderUIInputMode
    {
        SubmitToggle = 10,
        PointerCapture = 20,
        None = 30,
    }

    public enum SliderWorldTriggerButton
    {
        Left = 10,
        Right = 20,
    }

    public enum SliderControlOperation
    {
        SwapPreset = 10,
        MutateSettings = 20,
        ResetRuntimeOverrides = 30,
    }

    internal enum SliderEnvironmentKind
    {
        World = 10,
        ScreenUI = 20,
    }

    internal enum SliderChangeSource
    {
        UserPointer = 10,
        UserNavigate = 20,
        ExternalBinding = 30,
        Initialization = 40,
    }

    internal enum SliderInteractionEndReason
    {
        PointerUp = 10,
        SubmitToggle = 20,
        SelectionLost = 30,
        Cancel = 40,
        Disabled = 50,
    }

    internal enum SliderSpawnUnitKind
    {
        SegmentBar = 20,
        Marker = 30,
        Background = 40,
        Handle = 50,
    }

    public readonly struct SliderOutputSnapshot
    {
        public readonly bool IsVisible;
        public readonly float TargetRawValue;
        public readonly float TargetNormalizedValue;
        public readonly float DisplayedRawValue;
        public readonly float DisplayedNormalizedValue;

        public SliderOutputSnapshot(
            bool isVisible,
            float targetRawValue,
            float targetNormalizedValue,
            float displayedRawValue,
            float displayedNormalizedValue)
        {
            IsVisible = isVisible;
            TargetRawValue = targetRawValue;
            TargetNormalizedValue = targetNormalizedValue;
            DisplayedRawValue = displayedRawValue;
            DisplayedNormalizedValue = displayedNormalizedValue;
        }
    }

    public interface ISliderOutput
    {
        bool IsVisible { get; }
        float TargetRawValue { get; }
        float TargetNormalizedValue { get; }
        float DisplayedRawValue { get; }
        float DisplayedNormalizedValue { get; }
        event Action<SliderOutputSnapshot>? OnUpdated;
    }

    public interface ISliderChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        bool TryGetOutput(string tag, out ISliderOutput? output);
        bool TryGetControl(string tag, out ISliderControlService? control);
        void GetTags(List<string> output);
    }

    public interface ISliderControlService
    {
        bool SwapPreset(
            bool applyVisualizer,
            SliderVisualizerPreset? visualizerPreset,
            bool applyPlayer,
            SliderPlayerPreset? playerPreset);

        bool MutateSettings(
            SliderVisualizerRuntimeMutation? visualizerMutation,
            SliderPlayerRuntimeMutation? playerMutation,
            ICommandListRuntimeMutationService? mutationService);

        bool ResetRuntimeOverrides(bool resetVisualizer, bool resetPlayer);
    }

    public interface ISliderOptions
    {
        DynamicValue<SliderVisualizerPreset> VisualizerPresetValue { get; }
        DynamicValue<SliderPlayerPreset> PlayerPresetValue { get; }
        Transform? SegmentBarsRoot { get; }
        Transform? SegmentMarkersRoot { get; }
        ActorSource AreaActorSource { get; }
        string AreaChannelTag { get; }
        SliderRangeSourceMode RangeSourceMode { get; }
        RectTransform? RangeRectTransform { get; }
        Transform OwnerTransform { get; }
    }

    internal interface ISliderRuntimePresetProvider
    {
        SliderVisualizerPreset CurrentVisualizerPreset { get; }
        SliderPlayerPreset CurrentPlayerPreset { get; }
        event Action? OnVisualizerPresetChanged;
        event Action? OnPlayerPresetChanged;
    }

    internal interface ISliderPlayerRuntime :
        ISliderOutput
    {
        bool IsInteracting { get; }
        bool IsUserInputEnabled { get; }
        SliderUIInputMode UIInputMode { get; }
        SliderWorldTriggerButton WorldTriggerButton { get; }
        float NavigateRepeatDelay { get; }
        float NavigateRepeatInterval { get; }
        float ScrollRepeatDelay { get; }
        float ScrollRepeatInterval { get; }
        float PaddingStart { get; }
        float PaddingEnd { get; }
        int BoundaryCount { get; }
        int CurrentBoundaryIndex { get; }

        void Tick();
        bool RequestBeginInteraction();
        void RequestEndInteraction(SliderInteractionEndReason reason);
        bool RequestBoundaryIndex(int index, SliderChangeSource source);
        int ResolveNearestBoundaryIndex(float normalizedValue);
        float ResolveBoundaryNormalizedValue(int index);
        float ResolveBoundaryRawValue(int index);
    }
}
