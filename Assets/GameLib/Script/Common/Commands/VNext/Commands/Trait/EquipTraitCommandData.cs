#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Game.Commands.VNext
{
    /// <summary>
    /// EquipTraitHolder の Equip/Unequip コマンドデータ。
    /// 単一コマンドで Op (Equip / Unequip) を切り替える。
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

        // ───────────────── Slot Specification ─────────────────

        [BoxGroup("Slot")]
        [LabelText("Slot Key")]
        [Tooltip("EquipTraitHolderHub 内のスロットキー。")]
        public string SlotKey = string.Empty;

        // ───────────────── Target (Equip only) ─────────────────

        [BoxGroup("Target")]
        [ShowIf(nameof(IsEquip))]
        [EnumToggleButtons]
        [LabelText("Target Kind")]
        public EquipTraitTargetKind TargetKind = EquipTraitTargetKind.First;

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByDefinition")]
        [LabelText("Definition")]
        public VarUnityObjectSource<TraitDefinitionSO> DefinitionSource = new();

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByDefinitionId")]
        [LabelText("Definition ID")]
        public string TargetDefinitionId = string.Empty;

        [BoxGroup("Target")]
        [ShowIf("@IsEquip && TargetKind == Game.Trait.EquipTraitTargetKind.ByIndex")]
        [LabelText("Index")]
        public DynamicValue<int> TargetIndex;

        // ───────────────── Equip Options ─────────────────

        [BoxGroup("Options")]
        [ShowIf(nameof(IsEquip))]
        [LabelText("Await Unequip")]
        [Tooltip("Equip 前に既存装備の Unequip コマンド完了を待つ。")]
        public bool AwaitUnequip = true;

        [BoxGroup("Options")]
        [ShowIf(nameof(IsEquip))]
        [LabelText("Apply Payload")]
        [Tooltip("Equip 時に追加変数を SlotVars にマージする。")]
        public bool ApplyPayload;

        [BoxGroup("Options")]
        [ShowIf("@IsEquip && ApplyPayload")]
        [LabelText("Payload")]
        public VarStorePayload Payload = new();

        // ───────────────── Hub Source ─────────────────

        [BoxGroup("Source")]
        [LabelText("Use Self Scope")]
        [Tooltip("true = ctx.Scope から EquipTraitHolderHub を解決。\nfalse = ActorSource で指定。")]
        public bool UseSelfScope = true;

        [BoxGroup("Source")]
        [ShowIf("@!UseSelfScope")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HubActorSource)")]
        public ActorSource HubActorSource;

        bool IsEquip => Op == EquipTraitOp.Equip;
    }
}
