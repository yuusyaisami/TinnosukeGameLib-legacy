using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Common;
using VNext = Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum TransformAnimationOperation
    {
        Wait = 0,
        WorldPosition = 1,
        LocalPosition = 2,
        LocalRotate = 3,
        LocalScale = 4,
        AnchoredPosition = 5,   // RectTransform.anchoredPosition
        DeltaSize = 6,          // RectTransform.sizeDelta
        Pivot = 7,              // RectTransform.pivot (UI蟆ら畑縲∬｡ｨ遉ｺ菴咲ｽｮ縺ｯ邯ｭ謖√＆繧後ｋ)
        Command = 8,            // Step蛻ｰ驕疲凾縺ｫ繧ｳ繝槭Φ繝牙ｮ溯｡・
        Scroll = 9,             // Velocity 縺ｧ duration 遘堤ｧｻ蜍・
    }

    public enum AnchoredPositionInputMode
    {
        Anchored = 0,
        LeftTop = 1,
    }

    public enum TransformCurveControlSide
    {
        Inner = 0,
        Outer = 1,
    }

    public enum TransformPositionPathMode
    {
        Linear = 10,
        Curve = 20,
        Poly = 30,
    }

    public enum TransformPolyDirection
    {
        Clockwise = 10,
        CounterClockwise = 20,
    }

    public enum TransformScrollSpace
    {
        WorldPosition = 10,
        AnchoredPosition = 20,
    }

    public interface ITransformAnimationPreset
    {
        bool Loop { get; }
        int LoopCount { get; }
        IReadOnlyList<ITransformAnimationStep> Steps { get; }
    }

    public interface ITransformAnimationStep
    {
        TransformAnimationOperation Operation { get; }
        DynamicValue<float> Duration { get; }
        Ease Ease { get; }
        bool Relative { get; }
        bool FireAndForget { get; }
        bool EnsureShortestLocalRotatePath { get; }
        DynamicValue<Vector3> Vector3Value { get; }
        DynamicValue<Vector2> Vector2Value { get; }
        AnchoredPositionInputMode AnchoredInputMode { get; }
        TransformPositionPathMode PositionPathMode { get; }
        bool UseBezierCurve { get; }
        TransformCurveControlSide CurveControlSide { get; }
        float CurveHeight { get; }
        int CurveSamplingCount { get; }
        Vector3 CurveControlOffset { get; }
        int PolySides { get; }
        float PolyRadius { get; }
        float PolyRotationDeg { get; }
        TransformPolyDirection PolyDirection { get; }
        VNext.CommandListData Commands { get; }
        VNext.FlowRunAwaitMode CommandAwaitMode { get; }
        TransformScrollSpace ScrollSpace { get; }
        bool ScrollUseLocalVelocity { get; }
    }

    [Serializable]
    public sealed class TransformAnimationPreset : ITransformAnimationPreset
    {
        [Header("Loop")]
        [Tooltip("繧ｷ繝ｼ繧ｱ繝ｳ繧ｹ蜈ｨ菴薙ｒ繝ｫ繝ｼ繝励☆繧九°")]
        public bool loop;

        [ShowIf(nameof(loop))]
        [Tooltip("Inspector setting.")]
        [MinValue(-1)]
        public int loopCount = -1;

        [Header("Steps")]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            DraggableItems = true,
            ShowIndexLabels = true,
            ListElementLabelName = nameof(TransformAnimationPresetStep.ListLabel))]
        public List<TransformAnimationPresetStep> steps = new();
        public List<TransformAnimationPresetStep> Steps => steps;

        bool ITransformAnimationPreset.Loop => loop;
        int ITransformAnimationPreset.LoopCount => loopCount;
        IReadOnlyList<ITransformAnimationStep> ITransformAnimationPreset.Steps => steps;

    }

    [Serializable]
    public sealed class TransformAnimationPresetStep : ITransformAnimationStep
    {
        [TableColumnWidth(130)]
        [LabelText("Op")]
        public TransformAnimationOperation operation;

        [ShowIf(nameof(UsesTweenOptions))]
        [LabelText("Duration")]
        public DynamicValue<float> duration = new();

        [ShowIf(nameof(UsesTweenOptions))]
        [LabelText("Ease")]
        public Ease ease = Ease.Linear;

        [ShowIf(nameof(UsesTweenOptions))]
        [LabelText("Relative")]
        public bool relative;

        [ShowIf(nameof(UsesTweenOptions))]
        [LabelText("Fire&Forget")]
        public bool fireAndForget;

        [ShowIf(nameof(UsesScroll))]
        [LabelText("Scroll Space")]
        [EnumToggleButtons]
        public TransformScrollSpace scrollSpace = TransformScrollSpace.WorldPosition;

        [ShowIf(nameof(UsesWorldScroll))]
        [LabelText("Use Local Velocity")]
        [Tooltip("Inspector setting.")]
        public bool scrollUseLocalVelocity;

        [ShowIf(nameof(UsesLocalRotate))]
        [LabelText("Shortest Path")]
        [Tooltip("Inspector setting.")]
        public bool ensureShortestLocalRotatePath;

        [ShowIf(nameof(UsesVector3))]
        [LabelText("Value (Vec3)")]
        public DynamicValue<Vector3> vector3 = new();

        [ShowIf(nameof(UsesPosition))]
        [LabelText("Path Mode")]
        [EnumToggleButtons]
        public TransformPositionPathMode positionPathMode = TransformPositionPathMode.Linear;

        [ShowIf(nameof(UsesCurveMode))]
        [LabelText("Use Bezier")]
        public bool useBezierCurve = true;

        [ShowIf(nameof(UsesCurveMode))]
        [LabelText("Control Side")]
        public TransformCurveControlSide curveControlSide = TransformCurveControlSide.Outer;

        [ShowIf(nameof(UsesCurveMode))]
        [LabelText("Curve Height")]
        public float curveHeight = 1f;

        [ShowIf(nameof(UsesCurveMode))]
        [LabelText("Control Offset")]
        public Vector3 curveControlOffset;

        [ShowIf(nameof(UsesCurveMode))]
        [LabelText("Sampling")]
        [MinValue(2)]
        public int curveSamplingCount = 16;

        [ShowIf(nameof(UsesPolyMode))]
        [LabelText("Poly Sides")]
        [MinValue(3)]
        public int polySides = 5;

        [ShowIf(nameof(UsesPolyMode))]
        [LabelText("Poly Radius")]
        [MinValue(0.01f)]
        public float polyRadius = 1f;

        [ShowIf(nameof(UsesPolyMode))]
        [LabelText("Poly Rotation")]
        public float polyRotationDeg;

        [ShowIf(nameof(UsesPolyMode))]
        [LabelText("Poly Direction")]
        [EnumToggleButtons]
        public TransformPolyDirection polyDirection = TransformPolyDirection.Clockwise;

        [ShowIf(nameof(UsesVector2))]
        [LabelText("Value (Vec2)")]
        public DynamicValue<Vector2> vector2 = new();

        [ShowIf(nameof(UsesAnchoredPosition))]
        [LabelText("Anchored Input")]
        public AnchoredPositionInputMode anchoredInputMode = AnchoredPositionInputMode.Anchored;

        [ShowIf(nameof(UsesCommand))]
        [LabelText("Await Mode")]
        public VNext.FlowRunAwaitMode commandAwaitMode = VNext.FlowRunAwaitMode.WaitForCompletion;

        [ShowIf(nameof(UsesCommand))]
        [LabelText("Commands")]
        [VNext.CommandListFunctionName("TransformAnimation.Step.Command")]
        public VNext.CommandListData commands = new();

        public string ListLabel => BuildListLabel();

        bool UsesVector3 =>
            operation == TransformAnimationOperation.WorldPosition ||
            operation == TransformAnimationOperation.LocalPosition ||
            operation == TransformAnimationOperation.LocalRotate ||
            operation == TransformAnimationOperation.LocalScale ||
            operation == TransformAnimationOperation.Scroll;

        bool UsesScroll => operation == TransformAnimationOperation.Scroll;

        bool UsesWorldScroll =>
            operation == TransformAnimationOperation.Scroll &&
            scrollSpace == TransformScrollSpace.WorldPosition;

        bool UsesVector2 =>
            operation == TransformAnimationOperation.AnchoredPosition ||
            operation == TransformAnimationOperation.DeltaSize ||
            operation == TransformAnimationOperation.Pivot;

        bool UsesPosition =>
            operation == TransformAnimationOperation.WorldPosition ||
            operation == TransformAnimationOperation.LocalPosition;

        bool UsesCurveMode =>
            UsesPosition && positionPathMode == TransformPositionPathMode.Curve;

        bool UsesPolyMode =>
            UsesPosition && positionPathMode == TransformPositionPathMode.Poly;

        bool UsesAnchoredPosition =>
            operation == TransformAnimationOperation.AnchoredPosition;

        bool UsesLocalRotate =>
            operation == TransformAnimationOperation.LocalRotate;

        bool UsesTweenOptions =>
            operation != TransformAnimationOperation.Command &&
            operation != TransformAnimationOperation.Scroll;

        bool UsesCommand =>
            operation == TransformAnimationOperation.Command;

        string BuildListLabel()
        {
            if (operation == TransformAnimationOperation.Scroll)
            {
                var velocity = vector3.GetOrDefaultWithoutContext(Vector3.zero);
                var localFlag = scrollSpace == TransformScrollSpace.WorldPosition && (scrollUseLocalVelocity || relative) ? "T" : "F";
                return $"  op: {operation}, Space: {scrollSpace}, LocalVel: {localFlag}, Vel: {velocity}";
            }

            var durationValue = duration.GetOrDefaultWithoutContext(0f);
            var relativeFlag = relative ? "T" : "F";
            var fireAndForgetFlag = fireAndForget ? "T" : "F";
            return $"  op: {operation}, Dur: {durationValue:0.##}, Rel: {relativeFlag}, F&F: {fireAndForgetFlag}";
        }

        TransformAnimationOperation ITransformAnimationStep.Operation => operation;
        DynamicValue<float> ITransformAnimationStep.Duration => duration;
        Ease ITransformAnimationStep.Ease => ease;
        bool ITransformAnimationStep.Relative => relative;
        bool ITransformAnimationStep.FireAndForget => fireAndForget;
        bool ITransformAnimationStep.EnsureShortestLocalRotatePath => ensureShortestLocalRotatePath;
        DynamicValue<Vector3> ITransformAnimationStep.Vector3Value => vector3;
        DynamicValue<Vector2> ITransformAnimationStep.Vector2Value => vector2;
        AnchoredPositionInputMode ITransformAnimationStep.AnchoredInputMode => anchoredInputMode;
        TransformPositionPathMode ITransformAnimationStep.PositionPathMode => positionPathMode;
        bool ITransformAnimationStep.UseBezierCurve => useBezierCurve;
        TransformCurveControlSide ITransformAnimationStep.CurveControlSide => curveControlSide;
        float ITransformAnimationStep.CurveHeight => curveHeight;
        int ITransformAnimationStep.CurveSamplingCount => curveSamplingCount;
        Vector3 ITransformAnimationStep.CurveControlOffset => curveControlOffset;
        int ITransformAnimationStep.PolySides => polySides;
        float ITransformAnimationStep.PolyRadius => polyRadius;
        float ITransformAnimationStep.PolyRotationDeg => polyRotationDeg;
        TransformPolyDirection ITransformAnimationStep.PolyDirection => polyDirection;
        VNext.CommandListData ITransformAnimationStep.Commands => commands;
        VNext.FlowRunAwaitMode ITransformAnimationStep.CommandAwaitMode => commandAwaitMode;
        TransformScrollSpace ITransformAnimationStep.ScrollSpace => scrollSpace;
        bool ITransformAnimationStep.ScrollUseLocalVelocity => scrollUseLocalVelocity;
    }
}
