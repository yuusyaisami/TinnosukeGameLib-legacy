#nullable enable
using System;
using DG.Tweening;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum CameraZoomCommandAction
    {
        SetLayer = 0,
        ClearLayer = 1,
        ClearAll = 2,
        Reset = 3,
    }

    [Serializable]
    public sealed class CameraZoomCommandData : ICommandData
    {
        public int CommandId => CommandIds.CameraZoom;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(LayerTag) ? "<none>" : LayerTag;
                return $"Action={Action} Tag={tag}";
            }
        }

        [BoxGroup("Action")]
        [LabelText("Action")]
        [EnumToggleButtons]
        public CameraZoomCommandAction Action = CameraZoomCommandAction.SetLayer;

        [BoxGroup("Layer")]
        [LabelText("Layer Tag")]
        public string LayerTag = "zoom";

        [BoxGroup("Set Layer")]
        [LabelText("Target Size")]
        [ShowIf(nameof(Action), CameraZoomCommandAction.SetLayer)]
        public DynamicValue<float> TargetSize;

        [BoxGroup("Set Layer")]
        [LabelText("Priority")]
        [ShowIf(nameof(Action), CameraZoomCommandAction.SetLayer)]
        public int Priority;

        [BoxGroup("Set Layer")]
        [LabelText("Lambda")]
        [ShowIf(nameof(Action), CameraZoomCommandAction.SetLayer)]
        public Ease Lambda;
    }
}
