#nullable enable
using UnityEngine;

namespace Game.Channel
{
    public enum GridObjectChannelOrder
    {
        RowMajor = 10,
        ColumnMajor = 20,
    }

    public enum GridObjectChannelHorizontalAlignment
    {
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum GridObjectChannelVerticalAlignment
    {
        Top = 10,
        Center = 20,
        Bottom = 30,
    }

    public enum GridObjectChannelRefreshMode
    {
        FullRebuild = 10,
        Incremental = 20,
        LayoutOnly = 30,
    }

    public enum GridObjectChannelSpawnAnchorMode
    {
        LayoutTarget = 10,
        FixedAnchor = 20,
    }

    public enum GridObjectChannelVisualizerSizeSource
    {
        VisualBounds = 10,
        RectTransform = 20,
        Fixed = 30,
    }

    public enum GridObjectChannelSparseLayoutMode
    {
        PreserveSparseCoordinates = 10,
        CompressOccupiedCells = 20,
    }

    public enum GridObjectChoiceConcurrencyPolicy
    {
        ErrorIfActive = 10,
        CancelAndReplace = 20,
        Queue = 30,
    }

    public enum GridObjectChoiceDecisionPhase
    {
        AnyDecision = 10,
        CompletedWaitingRelease = 20,
        Short = 30,
        Long = 40,
        LongMax = 50,
        HoldReached = 60,
        Pressed = 70,
    }

    public enum GridObjectChoiceCompletionKind
    {
        None = 0,
        Selected = 10,
        Canceled = 20,
        Timeout = 30,
        Replaced = 40,
        Failed = 50,
    }
}
