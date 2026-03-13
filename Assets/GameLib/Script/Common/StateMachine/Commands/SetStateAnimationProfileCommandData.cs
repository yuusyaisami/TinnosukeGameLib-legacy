#nullable enable
using Game.Common;
using Game.StateMachine;
using Sirenix.OdinInspector;
using UnityEngine;
using System;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SetStateAnimationProfileCommandData : ICommandData
    {
        public int CommandId => CommandIds.StateAnimationSetProfile;
        public string DebugData
        {
            get
            {
                return $"Preset={Preset.SourceTypeName}:{Preset.SourceDebugData}";
            }
        }

        [BoxGroup("Profile")]
        [LabelText("Preset")]
        [SerializeField]
        public DynamicValue<StateAnimationPreset> Preset;

        [SerializeField, HideInInspector]
        public StateAnimationProfileSO? Profile;

        [BoxGroup("Profile")]
        [LabelText("Legacy Profile")]
        [ShowIf("@!Preset.HasSource && Profile != null")]
        [ShowInInspector, ReadOnly]
        StateAnimationProfileSO? LegacyProfilePreview => Profile;

        [BoxGroup("Profile")]
        [LabelText("Restart Immediately")]
        [SerializeField]
        public DynamicValue<bool> RestartImmediately = DynamicValueExtensions.FromLiteral(true);

        public void EnsurePresetMigrated()
        {
            if (Preset.HasSource || Profile == null)
                return;

            Preset = DynamicValue<StateAnimationPreset>.FromSource(AssetStateAnimationPresetSource.FromAsset(Profile));
        }
    }
}
