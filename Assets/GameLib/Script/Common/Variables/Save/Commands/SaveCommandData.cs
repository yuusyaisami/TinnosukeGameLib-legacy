#nullable enable
using System;
using Game.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum SaveCommandMode
    {
        Manual = 10,
        SaveAll = 20,
    }

    [Serializable]
    public sealed class SaveProfileCommandData : ICommandData
    {
        public int CommandId => CommandIds.SaveProfile;
        public string DebugData
        {
            get
            {
                var layer = LayerOverride.ToString();
                return IsManualMode
                    ? $"Mode={Mode} Layer={layer} UpdateBackup={UpdateBackup} Target={TargetScope.Kind}"
                    : $"Mode={Mode} Layer={layer} UpdateBackup={UpdateBackup}";
            }
        }

        [BoxGroup("Options")]
        [EnumToggleButtons]
        [LabelText("Mode")]
        [SerializeField]
        public SaveCommandMode Mode = SaveCommandMode.Manual;

        [BoxGroup("Options")]
        [EnumToggleButtons]
        [LabelText("Layer")]
        [SerializeField]
        public SaveLayer LayerOverride = SaveLayer.GameLogic;

        [BoxGroup("Options")]
        [ShowIf(nameof(IsManualMode))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target Scope\", TargetScope)")]
        [SerializeField]
        public ActorSource TargetScope;

        [BoxGroup("Options")]
        [LabelText("Update Backup")]
        [SerializeField]
        public bool UpdateBackup;

        bool IsManualMode => Mode != SaveCommandMode.SaveAll;
    }

    [Serializable]
    public sealed class LoadProfileCommandData : ICommandData
    {
        public int CommandId => CommandIds.LoadProfile;
        public string DebugData
        {
            get
            {
                var layer = LayerOverride.ToString();
                return $"Layer={layer}";
            }
        }

        [BoxGroup("Options")]
        [EnumToggleButtons]
        [LabelText("Layer")]
        [SerializeField]
        public SaveLayer LayerOverride = SaveLayer.GameLogic;
    }

    [Serializable]
    public sealed class ClearProfileCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearProfile;
        public string DebugData
        {
            get
            {
                var layer = LayerOverride.ToString();
                return $"Layer={layer}";
            }
        }

        [BoxGroup("Options")]
        [EnumToggleButtons]
        [LabelText("Layer")]
        [SerializeField]
        public SaveLayer LayerOverride = SaveLayer.GameLogic;
    }

    [Serializable]
    public sealed class ProfileChangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.ProfileChange;
        public string DebugData => $"ProfileId={ProfileId}";

        [BoxGroup("Target")]
        [LabelText("Profile Id")]
        [Min(0)]
        [SerializeField]
        public int ProfileId;
    }

    [Serializable]
    public sealed class DeleteAllSaveDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.DeleteAllSaveData;
        public string DebugData => "DeleteAllSaveData";
    }
}
