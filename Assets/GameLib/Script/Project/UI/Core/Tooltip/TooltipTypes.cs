#nullable enable
using System.Collections.Generic;
using Game.Common;
using Game.Commands.VNext;
using Game.DI;
using UnityEngine;

namespace Game.UI
{
    public enum TooltipAdapterKind
    {
        Auto = 0,
        UIScreen = 1,
        World = 2,
    }

    public enum TooltipSpawnMode
    {
        FollowPointer = 0,
        FixedOffset = 1,
    }

    public enum TooltipAnchorX
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public enum TooltipAnchorY
    {
        Up = 0,
        Center = 1,
        Down = 2,
    }

    public enum TooltipInputMode
    {
        AutoByInputService = 0,
        Pointer = 1,
        Navigation = 2,
        PointerNavigation = 3,
    }

    public readonly struct TooltipClampSettings
    {
        public readonly bool EnableClamp;
        public readonly float FlipThresholdX;
        public readonly float FlipThresholdY;

        public TooltipClampSettings(bool enableClamp, float flipThresholdX, float flipThresholdY)
        {
            EnableClamp = enableClamp;
            FlipThresholdX = flipThresholdX;
            FlipThresholdY = flipThresholdY;
        }

        public static TooltipClampSettings Default => new TooltipClampSettings(true, 0.2f, 0.2f);
    }

    public interface ITooltipSystemService
    {
        void RegisterAdapter(ITooltipAdapter adapter);
        void UnregisterAdapter(ITooltipAdapter adapter);
        ITooltipAdapter? ActiveAdapter { get; }
    }

    public interface ITooltipAdapter
    {
        IScopeNode Owner { get; }
        TooltipAdapterKind Kind { get; }
        bool EnablePointerHover { get; }
        bool EnableSelectionHover { get; }
        float HoverDelaySeconds { get; }
        float SelectionDelaySeconds { get; }
        float PointerMoveThreshold { get; }
        TooltipSpawnMode SpawnMode { get; }
        Vector2 FollowPointerOffset { get; }
        Vector2 FollowPointerMoveScale { get; }
        Vector2 FixedOffset { get; }
        TooltipAnchorX AnchorX { get; }
        TooltipAnchorY AnchorY { get; }
        bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate);
        CommandListData ShowCommands { get; }
        CommandListData HideCommands { get; }
        SelfDespawnCommandData SelfDespawn { get; }
        Camera? UiCamera { get; }
        Camera? WorldCamera { get; }
        IReadOnlyList<RectTransform> HitRects { get; }
        IReadOnlyList<SpriteRenderer> HitSprites { get; }
        Transform? AnchorTransform { get; }
        int Priority { get; }
        string SpawnerTag { get; }
    }

    public interface ITooltipAdapterOptions
    {
        TooltipAdapterKind Kind { get; }
        bool EnablePointerHover { get; }
        bool EnableSelectionHover { get; }
        float HoverDelaySeconds { get; }
        float SelectionDelaySeconds { get; }
        float PointerMoveThreshold { get; }
        TooltipSpawnMode SpawnMode { get; }
        Vector2 FollowPointerOffset { get; }
        Vector2 FollowPointerMoveScale { get; }
        Vector2 FixedOffset { get; }
        TooltipAnchorX AnchorX { get; }
        TooltipAnchorY AnchorY { get; }
        bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate);
        CommandListData ShowCommands { get; }
        CommandListData HideCommands { get; }
        SelfDespawnCommandData SelfDespawn { get; }
        Camera? UiCamera { get; }
        Camera? WorldCamera { get; }
        IReadOnlyList<RectTransform> HitRects { get; }
        IReadOnlyList<SpriteRenderer> HitSprites { get; }
        Transform? AnchorTransform { get; }
        int Priority { get; }
        string SpawnerTag { get; }
    }
}
