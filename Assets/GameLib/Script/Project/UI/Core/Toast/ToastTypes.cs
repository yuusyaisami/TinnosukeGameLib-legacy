#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Game.Commands.VNext;
using Game.DI;
using Game.Common;

namespace Game.UI
{
    public enum ToastDisplayMode
    {
        Queue = 0,
        Stack = 1,
    }

    public enum ToastDirection
    {
        Up = 10,
        Down = 20,
        Left = 30,
        Right = 40,
    }

    public enum ToastAnchorPreset
    {
        None = 0,
        TopLeft = 10,
        TopRight = 20,
        BottomLeft = 30,
        BottomRight = 40,
        Center = 50,
    }

    public enum ToastQueueCloseAwaitMode
    {
        WaitCloseCommandsAndChannel = 10,
        WaitCloseCommandsOnly = 20,
    }

    public interface IToastSystemService
    {
        string SystemTag { get; }
        bool TryEnqueue(in ToastRequest request);
        bool TryEnqueue(in ToastRequest request, out ToastRequestHandle? handle);
        int VisibleCount { get; }
        int PendingCount { get; }
    }

    public interface IToastSystemDebugTelemetry
    {
        string SystemTag { get; }
        int VisibleCount { get; }
        int PendingCount { get; }
        bool QueueLoopRunning { get; }
        void GetSnapshot(ToastSystemDebugSnapshot snapshot);
    }

    [Serializable]
    public sealed class ToastSystemDebugSnapshot
    {
        public int Frame;
        public float UnscaledTime;
        public int VisibleCount;
        public int PendingCount;
        public bool QueueLoopRunning;
        public readonly System.Collections.Generic.List<ToastVisibleDebugRow> VisibleRows = new();
        public readonly System.Collections.Generic.List<ToastLogDebugRow> Logs = new();

        public void ClearRows()
        {
            VisibleRows.Clear();
            Logs.Clear();
        }
    }

    [Serializable]
    public sealed class ToastVisibleDebugRow
    {
        public int Id;
        public bool IsClosing;
        public string Name = string.Empty;
        public Vector2 Size;
        public Vector2 StackOffset;
        public Vector2 Position;
    }

    [Serializable]
    public sealed class ToastLogDebugRow
    {
        public int Frame;
        public float UnscaledTime;
        public string Message = string.Empty;
    }

    public sealed class ToastRequestHandle
    {
        readonly UniTaskCompletionSource _shownTcs = new();
        readonly UniTaskCompletionSource _closedTcs = new();

        public bool IsShown { get; private set; }
        public bool IsClosed { get; private set; }

        internal void MarkShown()
        {
            if (IsShown)
                return;

            IsShown = true;
            _shownTcs.TrySetResult();
        }

        internal void MarkClosed()
        {
            if (IsClosed)
                return;

            IsClosed = true;
            _closedTcs.TrySetResult();
            MarkShown();
        }

        public UniTask WaitForShownAsync(CancellationToken ct = default)
        {
            if (IsShown)
                return UniTask.CompletedTask;

            var task = _shownTcs.Task;
            if (!ct.CanBeCanceled)
                return task;

            return task.AttachExternalCancellation(ct);
        }

        public UniTask WaitForClosedAsync(CancellationToken ct = default)
        {
            if (IsClosed)
                return UniTask.CompletedTask;

            var task = _closedTcs.Task;
            if (!ct.CanBeCanceled)
                return task;

            return task.AttachExternalCancellation(ct);
        }
    }

    public readonly struct ToastRequest
    {
        public readonly BaseRuntimeTemplatePreset? OverrideTemplatePreset;
        public readonly CommandListData OnSpawnCommands;
        public readonly CommandListData OnShowCommands;
        public readonly CommandListData OnCloseCommands;
        public readonly CommandListData OnStackAdjustedCommands;
        public readonly float LifetimeSecondsOverride;
        public readonly ToastRequestHandle? Handle;
        public readonly IVarStore? SourceVars;

        public ToastRequest(
            BaseRuntimeTemplatePreset? overrideTemplatePreset,
            CommandListData? onSpawnCommands,
            CommandListData? onShowCommands,
            CommandListData? onCloseCommands,
            CommandListData? onStackAdjustedCommands,
            float lifetimeSecondsOverride,
            ToastRequestHandle? handle = null,
            IVarStore? sourceVars = null)
        {
            OverrideTemplatePreset = overrideTemplatePreset;
            OnSpawnCommands = onSpawnCommands ?? new CommandListData();
            OnShowCommands = onShowCommands ?? new CommandListData();
            OnCloseCommands = onCloseCommands ?? new CommandListData();
            OnStackAdjustedCommands = onStackAdjustedCommands ?? new CommandListData();
            LifetimeSecondsOverride = lifetimeSecondsOverride;
            Handle = handle;
            SourceVars = sourceVars;
        }
    }

    public sealed class ToastSystemConfig
    {
        public string SystemTag = "default";
        public RectTransform ToastRoot = null!;
        public RectTransform? ClampArea;
        public DynamicValue<BaseRuntimeTemplatePreset> DefaultRuntimeTemplate;
        public string SpawnerTag = string.Empty;
        public ToastDisplayMode DisplayMode = ToastDisplayMode.Queue;
        public int MaxVisibleCount = 3;
        public ToastDirection ShowDirection = ToastDirection.Up;
        public ToastDirection CloseDirection = ToastDirection.Down;
        public ToastDirection StackShiftDirection = ToastDirection.Up;
        public ToastQueueCloseAwaitMode QueueCloseAwaitMode = ToastQueueCloseAwaitMode.WaitCloseCommandsAndChannel;
        public float AutoCloseSeconds = 2f;
        public float ShowDistanceMultiplier = 1f;
        public float CloseDistanceMultiplier = 1f;
        public float CloseDistanceMultiplierWhenStack = 0.85f;
        public bool ApplyStackShiftToCloseMultiplier = false;
        public bool UseVisualBounds = true;
        public Vector2 FallbackSize = new Vector2(260f, 64f);
        public bool ClampInsideScreen = true;
        public Vector2 ClampPadding = new Vector2(16f, 16f);
        public ToastAnchorPreset AnchorPreset = ToastAnchorPreset.None;
        public bool AnchorReapplyOnRelayout = false;
        public float StackSpacing = 8f;
        public string ShowTransformChannelTag = "toast_show";
        public string CloseTransformChannelTag = "toast_close";
        public string StackShiftTransformChannelTag = "toast_stack_shift";
        public float ShowAnimationDuration = 0.18f;
        public float CloseAnimationDuration = 0.16f;
        public float StackShiftDuration = 0.12f;
        public bool EnableDebugLog = false;
        public bool EnableMovementDebugLog = false;
        public int DebugLogCapacity = 128;
    }
}
