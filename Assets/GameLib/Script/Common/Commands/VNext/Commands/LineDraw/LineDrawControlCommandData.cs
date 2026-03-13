#nullable enable
using System;
using Game.Common;
using Game.LineDraw;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum LineDrawControlOperation
    {
        /// <summary>スタイルを更新（太さ、色、パターン等）</summary>
        UpdateStyle = 0,
        /// <summary>MaterialFxを適用</summary>
        ApplyMaterialFx = 1,
        /// <summary>パターンオフセットを設定</summary>
        SetPatternOffset = 2,
        /// <summary>パターンオフセット速度を設定</summary>
        SetPatternOffsetVelocity = 3,
        /// <summary>線の太さを設定</summary>
        SetBaseWidth = 4,
        /// <summary>線を解放</summary>
        Release = 5,
    }

    public enum LineDrawHandleTarget
    {
        /// <summary>全てのアクティブな線に適用</summary>
        All = 0,
        /// <summary>最初の線に適用</summary>
        First = 1,
        /// <summary>インデックスで指定</summary>
        ByIndex = 2,
    }

    [Serializable]
    public sealed class LineDrawControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.LineDrawControl;
        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} HandleTarget={HandleTarget} Op={Operation}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("ILineDrawServiceを持つスコープ")]
        public ActorSource Target;

        [BoxGroup("Handle")]
        [LabelText("Handle Target")]
        [Tooltip("操作対象のハンドル指定方法")]
        public LineDrawHandleTarget HandleTarget = LineDrawHandleTarget.First;

        [BoxGroup("Handle")]
        [ShowIf("@HandleTarget == LineDrawHandleTarget.ByIndex")]
        [LabelText("Handle Index")]
        [Tooltip("操作対象のハンドルインデックス（0始まり）")]
        public int HandleIndex;

        [LabelText("Operation")]
        public LineDrawControlOperation Operation;

        [BoxGroup("Style")]
        [ShowIf("@Operation == LineDrawControlOperation.UpdateStyle")]
        [LabelText("Style")]
        public LineStyle Style = LineStyle.Default;

        [BoxGroup("MaterialFx")]
        [ShowIf("@Operation == LineDrawControlOperation.ApplyMaterialFx")]
        [LabelText("MaterialFx Source")]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [BoxGroup("Pattern")]
        [ShowIf("@Operation == LineDrawControlOperation.SetPatternOffset")]
        [LabelText("Offset")]
        public float PatternOffset;

        [BoxGroup("Pattern")]
        [ShowIf("@Operation == LineDrawControlOperation.SetPatternOffsetVelocity")]
        [LabelText("Offset Velocity")]
        public float PatternOffsetVelocity;

        [BoxGroup("Width")]
        [ShowIf("@Operation == LineDrawControlOperation.SetBaseWidth")]
        [LabelText("Base Width")]
        public float BaseWidth = 1f;
    }
}
