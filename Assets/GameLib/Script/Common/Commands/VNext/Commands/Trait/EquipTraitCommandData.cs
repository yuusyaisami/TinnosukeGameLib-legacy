#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Game.Commands.VNext
{
    /// <summary>
    /// EquipTraitHolder 縺ｮ Equip/Unequip 繧ｳ繝槭Φ繝峨ョ繝ｼ繧ｿ縲・
    /// 蜊倅ｸ繧ｳ繝槭Φ繝峨〒 Op (Equip / Unequip) 繧貞・繧頑崛縺医ｋ縲・
    /// </summary>
    [Serializable]
    public sealed class EquipTraitCommandData : ICommandData
    {
        public int CommandId => CommandIds.EquipTrait;

        public string DebugData =>
            $"Op={Op} Slot={SlotKey} Target={TargetKind}";

        [BoxGroup("Operation")]
        [EnumToggleButtons]
        [LabelText("Op")]
        public EquipTraitOp Op = EquipTraitOp.Equip;

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏 Slot Specification 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Slot")]
        [LabelText("Slot Key")]
        [Tooltip("Inspector setting.")]
        public string SlotKey = string.Empty;

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏 Target (Equip only) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Target")]
        [ShowIf(nameof(IsEquip))]
        [EnumToggleButtons]
        [LabelText("Target Kind")]
        public EquipTraitTargetKind TargetKind = EquipTraitTargetKind.First;

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByDefinition")]
        [LabelText("Definition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<TraitDefinitionSO> DefinitionSource;

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByDefinitionId")]
        [LabelText("Definition ID")]
        public string TargetDefinitionId = string.Empty;

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByIndex")]
        [LabelText("Index")]
        public DynamicValue<int> TargetIndex;

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏 Equip Options 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Options")]
        [ShowIf(nameof(IsEquip))]
        [LabelText("Await Unequip")]
        [Tooltip("Inspector setting.")]
        public bool AwaitUnequip = true;

        [BoxGroup("Options")]
        [ShowIf(nameof(IsEquip))]
        [LabelText("Apply Payload")]
        [Tooltip("Inspector setting.")]
        public bool ApplyPayload;

        [BoxGroup("Options")]
        [ShowIf("@IsEquip && ApplyPayload")]
        [LabelText("Payload")]
        public VarStorePayload Payload = new();

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏 Hub Source 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Source")]
        [LabelText("Use Self Scope")]
        [Tooltip("Inspector setting.")]
        public bool UseSelfScope = true;

        [BoxGroup("Source")]
        [ShowIf("@!UseSelfScope")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HubActorSource)")]
        public ActorSource HubActorSource;

        bool IsEquip => Op == EquipTraitOp.Equip;
    }
}
