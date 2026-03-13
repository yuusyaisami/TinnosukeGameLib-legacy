using System;
using System.Collections.Generic;
using Game.LineDraw;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum LineDrawCreateKind
    {
        /// <summary>2点間のセグメント</summary>
        Segment = 0,
        /// <summary>複数点のパス</summary>
        Path = 1
    }

    [Serializable]
    public sealed class LineDrawCreateCommandData : ICommandData
    {
        public int CommandId => CommandIds.LineDrawCreate;
        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Kind={Kind}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("ILineDrawServiceを持つスコープ")]
        public ActorSource Target;

        [LabelText("Create Kind")]
        public LineDrawCreateKind Kind;

        [BoxGroup("Segment")]
        [ShowIf("@Kind == LineDrawCreateKind.Segment")]
        [LabelText("Space")]
        public LineSpace Space = LineSpace.Local;

        [BoxGroup("Segment")]
        [ShowIf("@Kind == LineDrawCreateKind.Segment")]
        [LabelText("From Position")]
        public Vector3 FromPosition;

        [BoxGroup("Segment")]
        [ShowIf("@Kind == LineDrawCreateKind.Segment")]
        [LabelText("To Position")]
        public Vector3 ToPosition;

        [BoxGroup("Path")]
        [ShowIf("@Kind == LineDrawCreateKind.Path")]
        [LabelText("Path Points")]
        public List<Vector3> PathPoints = new();

        [BoxGroup("Path")]
        [ShowIf("@Kind == LineDrawCreateKind.Path")]
        [LabelText("Closed")]
        public bool Closed;

        [BoxGroup("Style")]
        [LabelText("Style")]
        public LineStyle Style = LineStyle.Default;
    }
}
