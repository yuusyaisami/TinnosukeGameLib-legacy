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
        public UITraitListSpawnSource SpawnSource = UITraitListSpawnSource.Prefab;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.RuntimeTemplate)]
        [InlineProperty, HideLabel]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), UITraitListSpawnSource.Prefab)]
        public GameObject? Prefab;

        [BoxGroup("Spawn")]
        public bool AllowPooling = true;

        [BoxGroup("Spawn")]
        public Transform? SpawnParentOverride;

        [BoxGroup("Spawn")]
        [LabelText("Override Size")]
        public bool OverrideSize;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Width")]
        [Min(0f)]
        public float Width;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(OverrideSize))]
        [LabelText("Height")]
        [Min(0f)]
        public float Height;

        [BoxGroup("Commands")]
        [CommandListFunctionName("UITraitList.Spawn")]
        public CommandListData SpawnCommands = new();

        [BoxGroup("Commands")]
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
        public TraitDefinitionSO? Definition;
        public CommandListData Commands;
    }
}
