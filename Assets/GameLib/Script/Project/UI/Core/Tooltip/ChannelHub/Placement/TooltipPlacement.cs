#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    internal readonly struct TooltipPlacementRequest
    {
        public readonly TooltipChannelPlayerRuntime Runtime;
        public readonly int Priority;
        public readonly int Order;
        public readonly Vector2 BaseAnchorScreenPosition;
        public readonly Vector2 ScreenSize;
        public readonly TooltipChannelAnchorX AnchorX;
        public readonly TooltipChannelAnchorY AnchorY;

        public TooltipPlacementRequest(
            TooltipChannelPlayerRuntime runtime,
            int priority,
            int order,
            Vector2 baseAnchorScreenPosition,
            Vector2 screenSize,
            TooltipChannelAnchorX anchorX,
            TooltipChannelAnchorY anchorY)
        {
            Runtime = runtime;
            Priority = priority;
            Order = order;
            BaseAnchorScreenPosition = baseAnchorScreenPosition;
            ScreenSize = screenSize;
            AnchorX = anchorX;
            AnchorY = anchorY;
        }
    }

    internal readonly struct TooltipPlacementSolution
    {
        public readonly TooltipChannelPlayerRuntime Runtime;
        public readonly TooltipChannelAnchorX AnchorX;
        public readonly TooltipChannelAnchorY AnchorY;
        public readonly Vector2 AnchorScreenPosition;
        public readonly Rect ScreenRect;

        public TooltipPlacementSolution(
            TooltipChannelPlayerRuntime runtime,
            TooltipChannelAnchorX anchorX,
            TooltipChannelAnchorY anchorY,
            Vector2 anchorScreenPosition,
            Rect screenRect)
        {
            Runtime = runtime;
            AnchorX = anchorX;
            AnchorY = anchorY;
            AnchorScreenPosition = anchorScreenPosition;
            ScreenRect = screenRect;
        }
    }

    internal interface ITooltipPlacementSolver
    {
        TooltipChannelSpaceKind SpaceKind { get; }
        bool TryBuildRequest(TooltipChannelPlayerRuntime runtime, Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request);
        TooltipPlacementSolution Solve(
            TooltipPlacementRequest request,
            IReadOnlyList<TooltipPlacementSolution> placed,
            TooltipHubPreset preset,
            Rect screenRect,
            IScreenClampService clampService);
        void ApplySolution(TooltipChannelPlayerRuntime runtime, in TooltipPlacementSolution solution, Camera? camera);
        void MoveOffscreen(TooltipChannelPlayerRuntime runtime);
    }

    internal abstract class TooltipPlacementSolverBase : ITooltipPlacementSolver
    {
        public abstract TooltipChannelSpaceKind SpaceKind { get; }
        public abstract bool TryBuildRequest(TooltipChannelPlayerRuntime runtime, Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request);
        public abstract void ApplySolution(TooltipChannelPlayerRuntime runtime, in TooltipPlacementSolution solution, Camera? camera);
        public abstract void MoveOffscreen(TooltipChannelPlayerRuntime runtime);

        public TooltipPlacementSolution Solve(
            TooltipPlacementRequest request,
            IReadOnlyList<TooltipPlacementSolution> placed,
            TooltipHubPreset preset,
            Rect screenRect,
            IScreenClampService clampService)
        {
            var anchorX = request.AnchorX;
            var anchorY = request.AnchorY;
            var rect = BuildRect(request.BaseAnchorScreenPosition, request.ScreenSize, anchorX, anchorY);

            if (preset.EnableClamp)
            {
                var clamp = clampService.Evaluate(screenRect, rect);
                if (clamp.RightRate > preset.FlipThresholdX)
                    anchorX = TooltipChannelAnchorX.Left;
                else if (clamp.LeftRate > preset.FlipThresholdX)
                    anchorX = TooltipChannelAnchorX.Right;

                if (clamp.TopRate > preset.FlipThresholdY)
                    anchorY = TooltipChannelAnchorY.Down;
                else if (clamp.BottomRate > preset.FlipThresholdY)
                    anchorY = TooltipChannelAnchorY.Up;

                rect = BuildRect(request.BaseAnchorScreenPosition, request.ScreenSize, anchorX, anchorY);
                rect = ClampRect(rect, screenRect);
            }

            var bestRect = rect;
            var bestOverlap = ComputeOverlapArea(bestRect, placed);
            if (bestOverlap > 0.0001f)
            {
                var direction = ResolveDirectionVector(preset.StackDirection);
                var stepDistance = ComputePrimaryDistance(request.ScreenSize, preset.StackDirection) + preset.StackGap;
                if (stepDistance <= 0.001f)
                    stepDistance = 8f;

                for (var i = 1; i <= 16; i++)
                {
                    var candidate = OffsetRect(rect, direction * (stepDistance * i));
                    if (preset.EnableClamp)
                        candidate = ClampRect(candidate, screenRect);

                    var overlap = ComputeOverlapArea(candidate, placed);
                    if (overlap + 0.0001f < bestOverlap)
                    {
                        bestRect = candidate;
                        bestOverlap = overlap;
                        if (bestOverlap <= 0.0001f)
                            break;
                    }
                }
            }

            var anchorScreen = ResolveAnchorScreen(bestRect, anchorX, anchorY);
            return new TooltipPlacementSolution(request.Runtime, anchorX, anchorY, anchorScreen, bestRect);
        }

        internal static Rect BuildRect(
            Vector2 anchorScreen,
            Vector2 size,
            TooltipChannelAnchorX anchorX,
            TooltipChannelAnchorY anchorY)
        {
            var width = Mathf.Max(1f, size.x);
            var height = Mathf.Max(1f, size.y);

            float xMin = anchorX switch
            {
                TooltipChannelAnchorX.Left => anchorScreen.x - width,
                TooltipChannelAnchorX.Center => anchorScreen.x - (width * 0.5f),
                TooltipChannelAnchorX.Right => anchorScreen.x,
                _ => anchorScreen.x,
            };

            float yMin = anchorY switch
            {
                TooltipChannelAnchorY.Up => anchorScreen.y,
                TooltipChannelAnchorY.Center => anchorScreen.y - (height * 0.5f),
                TooltipChannelAnchorY.Down => anchorScreen.y - height,
                _ => anchorScreen.y,
            };

            return new Rect(xMin, yMin, width, height);
        }

        internal static Rect ClampRect(Rect rect, Rect screenRect)
        {
            if (screenRect.width <= 0f || screenRect.height <= 0f)
                return rect;

            var x = rect.x;
            var y = rect.y;
            if (rect.xMin < screenRect.xMin)
                x += screenRect.xMin - rect.xMin;
            if (rect.xMax > screenRect.xMax)
                x += screenRect.xMax - rect.xMax;
            if (rect.yMin < screenRect.yMin)
                y += screenRect.yMin - rect.yMin;
            if (rect.yMax > screenRect.yMax)
                y += screenRect.yMax - rect.yMax;

            return new Rect(x, y, rect.width, rect.height);
        }

        internal static Rect OffsetRect(Rect rect, Vector2 delta)
        {
            return new Rect(rect.x + delta.x, rect.y + delta.y, rect.width, rect.height);
        }

        internal static Vector2 ResolveAnchorScreen(Rect rect, TooltipChannelAnchorX anchorX, TooltipChannelAnchorY anchorY)
        {
            float x = anchorX switch
            {
                TooltipChannelAnchorX.Left => rect.xMax,
                TooltipChannelAnchorX.Center => rect.center.x,
                TooltipChannelAnchorX.Right => rect.xMin,
                _ => rect.xMin,
            };

            float y = anchorY switch
            {
                TooltipChannelAnchorY.Up => rect.yMin,
                TooltipChannelAnchorY.Center => rect.center.y,
                TooltipChannelAnchorY.Down => rect.yMax,
                _ => rect.yMin,
            };

            return new Vector2(x, y);
        }

        internal static float ComputeOverlapArea(Rect rect, IReadOnlyList<TooltipPlacementSolution> placed)
        {
            var total = 0f;
            for (var i = 0; i < placed.Count; i++)
            {
                var other = placed[i].ScreenRect;
                if (!rect.Overlaps(other, true))
                    continue;

                var x = Mathf.Max(0f, Mathf.Min(rect.xMax, other.xMax) - Mathf.Max(rect.xMin, other.xMin));
                var y = Mathf.Max(0f, Mathf.Min(rect.yMax, other.yMax) - Mathf.Max(rect.yMin, other.yMin));
                total += x * y;
            }

            return total;
        }

        internal static float ComputePrimaryDistance(Vector2 size, TooltipChannelStackDirection direction)
        {
            return direction == TooltipChannelStackDirection.Left || direction == TooltipChannelStackDirection.Right
                ? Mathf.Max(1f, size.x)
                : Mathf.Max(1f, size.y);
        }

        internal static Vector2 ResolveDirectionVector(TooltipChannelStackDirection direction)
        {
            return direction switch
            {
                TooltipChannelStackDirection.Up => Vector2.up,
                TooltipChannelStackDirection.Down => Vector2.down,
                TooltipChannelStackDirection.Left => Vector2.left,
                TooltipChannelStackDirection.Right => Vector2.right,
                _ => Vector2.up,
            };
        }
    }

    internal sealed class UITooltipPlacementSolver : TooltipPlacementSolverBase
    {
        public override TooltipChannelSpaceKind SpaceKind => TooltipChannelSpaceKind.UIScreen;

        public override bool TryBuildRequest(TooltipChannelPlayerRuntime runtime, Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request)
        {
            return runtime.TryBuildUiPlacementRequest(pointerScreen, camera, out request);
        }

        public override void ApplySolution(TooltipChannelPlayerRuntime runtime, in TooltipPlacementSolution solution, Camera? camera)
        {
            runtime.ApplyUiPlacement(solution, camera);
        }

        public override void MoveOffscreen(TooltipChannelPlayerRuntime runtime)
        {
            runtime.MoveUiOffscreen();
        }
    }

    internal sealed class WorldTooltipPlacementSolver : TooltipPlacementSolverBase
    {
        public override TooltipChannelSpaceKind SpaceKind => TooltipChannelSpaceKind.World;

        public override bool TryBuildRequest(TooltipChannelPlayerRuntime runtime, Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request)
        {
            return runtime.TryBuildWorldPlacementRequest(pointerScreen, camera, out request);
        }

        public override void ApplySolution(TooltipChannelPlayerRuntime runtime, in TooltipPlacementSolution solution, Camera? camera)
        {
            runtime.ApplyWorldPlacement(solution, camera);
        }

        public override void MoveOffscreen(TooltipChannelPlayerRuntime runtime)
        {
            runtime.MoveWorldOffscreen();
        }
    }
}
