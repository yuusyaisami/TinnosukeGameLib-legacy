#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.MaterialFx;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class MeshFxAnimationPayload
    {
        [LabelText("Context Tag")]
        public string ContextTag = "default";

        [LabelText("Clear Context First")]
        public bool ClearContextFirst;

        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<MeshFxParameterAnimationEntry> Entries = new();
    }

    [Serializable]
    public sealed class MeshFxMaterialPresetCommandEntry
    {
        [LabelText("Wait For Completion")]
        public bool WaitForCompletion = true;

        [InlineProperty]
        public MaterialFxPresetEntry Entry;
    }

    [Serializable]
    public sealed class MeshFxMaterialPresetPayload
    {
        [LabelText("Context Tag")]
        public string ContextTag = "default";

        [LabelText("Clear Context First")]
        public bool ClearContextFirst;

        [LabelText("Base Priority")]
        public int Priority = 0;

        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = false)]
        public List<MeshFxMaterialPresetCommandEntry> Entries = new();
    }

    [Serializable]
    public sealed class MeshFxAnimationChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.MeshFxAnimationChannel;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(ChannelTag) ? "<none>" : ChannelTag;
                return $"Tag={tag} Anim={ApplyAnimation} Material={ApplyMaterialPreset} Loop={Loop} Await={AwaitMode}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Execution")]
        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.RunInBackground;

        [BoxGroup("Execution")]
        [LabelText("Loop")]
        public bool Loop;

        [BoxGroup("Execution")]
        [ShowIf(nameof(Loop))]
        [LabelText("Loop Count (-1: Infinite)")]
        [MinValue(-1)]
        public int LoopCount = -1;

        [BoxGroup("Animation")]
        [LabelText("Apply Animation")]
        public bool ApplyAnimation = true;

        [BoxGroup("Animation")]
        [LabelText("Animation Payload")]
        [ShowIf(nameof(ApplyAnimation))]
        [InlineProperty]
        public MeshFxAnimationPayload AnimationPayload = new();

        [BoxGroup("MaterialFx")]
        [LabelText("Apply Material Preset")]
        public bool ApplyMaterialPreset;

        [BoxGroup("MaterialFx")]
        [LabelText("Material Payload")]
        [ShowIf(nameof(ApplyMaterialPreset))]
        [InlineProperty]
        public MeshFxMaterialPresetPayload MaterialPayload = new();
    }

    public enum MeshFxChannelControlMode
    {
        Create = 0,
        Remove = 1,
    }

    [Serializable]
    public sealed class MeshFxChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.MeshFxChannelControl;
        public string DebugData
        {
            get
            {
                if (Mode == MeshFxChannelControlMode.Remove)
                    return $"Mode=Remove Tag={RemoveTag}";

                return $"Mode=Create DefTag={CreateDef.Tag}";
            }
        }

        [BoxGroup("Mode")]
        [LabelText("Mode")]
        public MeshFxChannelControlMode Mode = MeshFxChannelControlMode.Create;

        [BoxGroup("Create")]
        [ShowIf(nameof(IsCreateMode))]
        [LabelText("Overwrite If Exists")]
        public bool OverwriteIfExists = true;

        [BoxGroup("Create")]
        [ShowIf(nameof(IsCreateMode))]
        [LabelText("Fail If Cannot Create")]
        public bool FailIfCannotCreate;

        [BoxGroup("Create")]
        [ShowIf(nameof(IsCreateMode))]
        [LabelText("Tag Override (optional)")]
        public string CreateTagOverride = string.Empty;

        [BoxGroup("Create")]
        [ShowIf(nameof(IsCreateMode))]
        [LabelText("Channel Def")]
        [InlineProperty]
        public MeshFxChannelDef CreateDef = new();

        [BoxGroup("Remove")]
        [ShowIf(nameof(IsRemoveMode))]
        [LabelText("Remove Tag")]
        public string RemoveTag = "default";

        bool IsCreateMode => Mode == MeshFxChannelControlMode.Create;
        bool IsRemoveMode => Mode == MeshFxChannelControlMode.Remove;
    }
}
