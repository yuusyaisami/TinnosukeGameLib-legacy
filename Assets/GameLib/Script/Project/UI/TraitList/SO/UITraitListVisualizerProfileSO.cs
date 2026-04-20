#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI.TraitList
{
    [CreateAssetMenu(
        fileName = "UITraitListVisualizerProfile",
        menuName = "Game/UI/Trait List/Visualizer Profile")]
    public sealed class UITraitListVisualizerProfileSO : ScriptableObject
    {
        [BoxGroup("Spawn")]
        [Tooltip("Inspector setting.")]
        public UITraitListSpawnSource SpawnSource = UITraitListSpawnSource.Prefab;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.RuntimeTemplate)]
        [InlineProperty, HideLabel]
        [Tooltip("Inspector setting.")]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.Prefab)]
        [Tooltip("Inspector setting.")]
        public GameObject? Prefab;

        [BoxGroup("Spawn")]
        [Tooltip("Inspector setting.")]
        public bool AllowPooling = true;

        [BoxGroup("Spawn")]
        [Tooltip("Inspector setting.")]
        public Transform? SpawnParentOverride;

        [BoxGroup("Spawn")]
        [LabelText("Override Size")]
        [Tooltip("Inspector setting.")]
        public bool OverrideSize;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Width")]
        [Min(0f)]
        [Tooltip("Inspector setting.")]
        public float Width;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Height")]
        [Min(0f)]
        [Tooltip("Inspector setting.")]
        public float Height;

        [BoxGroup("Commands")]
        [CommandListFunctionName("UITraitList.Spawn")]
        [Tooltip("Inspector setting.")]
        public CommandListData SpawnCommands = new();

        [BoxGroup("Commands")]
        [Tooltip("Inspector setting.")]
        public List<UITraitDefinitionCommand> ByDefinition = new();

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!RuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        void BindDebugOwners()
        {
            SpawnCommands?.BindDebugOwner(this, nameof(SpawnCommands));
            if (ByDefinition == null)
                return;
            for (int i = 0; i < ByDefinition.Count; i++)
            {
                var entry = ByDefinition[i];
                entry.Commands?.BindDebugOwner(this, $"ByDefinition[{i}].Commands");
            }
        }

    }

    [Serializable]
    public struct UITraitDefinitionCommand
    {
        [Tooltip("Inspector setting.")]
        public TraitDefinitionSO? Definition;
        [Tooltip("Inspector setting.")]
        public CommandListData Commands;
    }
}
