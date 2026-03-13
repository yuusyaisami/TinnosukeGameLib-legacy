#nullable enable
using System;
using Game.CameraSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum CameraPostProcessCommandAction
    {
        ApplyPreset = 0,
        ClearLayer = 1,
        ClearAll = 2,
        Reset = 3,
    }

    [Serializable]
    public sealed class CameraPostProcessCommandData : ICommandData
    {
        public int CommandId => CommandIds.CameraPostProcess;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(Tag) ? "<none>" : Tag;
                return $"Action={Action} Tag={tag}";
            }
        }

        [BoxGroup("Action")]
        [LabelText("Action")]
        [EnumToggleButtons]
        public CameraPostProcessCommandAction Action = CameraPostProcessCommandAction.ApplyPreset;

        [BoxGroup("Target")]
        [LabelText("Tag")]
        public string Tag = "camera";

        [BoxGroup("Preset")]
        [ShowIf(nameof(Action), CameraPostProcessCommandAction.ApplyPreset)]
        [SerializeReference, InlineProperty]
        public ICameraPostProcessPreset? Preset;
    }
}
