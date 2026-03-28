#nullable enable
using System;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    internal enum SliderInteractionSignalKind
    {
        Submit = 10,
        Cancel = 20,
        Navigate = 30,
        Scroll = 40,
        PointerPrimary = 50,
        PointerSecondary = 60,
        PointerMove = 70,
    }

    internal enum SliderInteractionSignalPhase
    {
        Down = 10,
        Held = 20,
        Up = 30,
        Instant = 40,
    }

    internal readonly struct SliderInteractionSignal
    {
        public readonly SliderInteractionSignalKind Kind;
        public readonly SliderInteractionSignalPhase Phase;
        public readonly float DeltaTime;
        public readonly Vector2 PointerPosition;
        public readonly Vector2 Direction;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;

        public SliderInteractionSignal(
            SliderInteractionSignalKind kind,
            SliderInteractionSignalPhase phase,
            float deltaTime,
            Vector2 pointerPosition,
            Vector2 direction,
            Vector3 worldPosition,
            bool hasWorldPosition)
        {
            Kind = kind;
            Phase = phase;
            DeltaTime = deltaTime;
            PointerPosition = pointerPosition;
            Direction = direction;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
        }
    }

    internal interface ISliderInteractionAdapter : IDisposable
    {
        SliderEnvironmentKind EnvironmentKind { get; }
        bool IsAvailable { get; }
        bool IsSelected { get; }
        bool IsHovered { get; }
        bool AllowsDirectPointerPressWithoutSelection { get; }
        IUIElementState? ElementState { get; }

        void OnAcquire(IScopeNode scope, bool isReset);
        void OnRelease(IScopeNode scope, bool isReset);
        void Tick();
        void SetBlockMask(UISelectionBlockMask mask);
        bool TryEnsureSelected();
    }

    internal interface ISliderInteractionRuntime
    {
        UISelectionBlockMask DesiredSelectionBlockMask { get; }

        void BindAdapter(ISliderInteractionAdapter? adapter);
        void OnAcquire(IScopeNode scope, bool isReset);
        void OnRelease(IScopeNode scope, bool isReset);
        void Tick();
        bool HandleSignal(SliderInteractionSignal signal);
    }
}
