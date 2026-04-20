#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    /// <summary>
    /// Inspector 荳翫〒 EquipTraitSlotRuntime 繧定ｨｭ螳壹☆繧九◆繧√・繝・・繧ｿ縲・
    /// TraitHolderSettings 縺ｨ蟇ｾ繧偵↑縺吶・
    /// </summary>
    [Serializable]
    public sealed class EquipTraitSlotSettings
    {
        [BoxGroup("Slot")]
        [LabelText("Slot Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _slotKey = string.Empty;

        [BoxGroup("Slot")]
        [LabelText("Holder Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _holderKey = string.Empty;

        [BoxGroup("Commands")]
        [LabelText("Run On Equip")]
        [SerializeField]
        bool _runOnEquipCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnEquipCommands))]
        [LabelText("On Equip Commands")]
        [SerializeField]
        VNext.CommandListData _onEquipCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Run On Unequip")]
        [SerializeField]
        bool _runOnUnequipCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_runOnUnequipCommands))]
        [LabelText("On Unequip Commands")]
        [SerializeField]
        VNext.CommandListData _onUnequipCommands = new();

        public string SlotKey => _slotKey;
        public string HolderKey => _holderKey;

        internal string NormalizedSlotKey =>
            string.IsNullOrWhiteSpace(_slotKey) ? string.Empty : _slotKey.Trim();

        internal void ApplyTo(EquipTraitSlotRuntime slot)
        {
            slot.SetSlotCommands(
                _runOnEquipCommands,
                _onEquipCommands,
                _runOnUnequipCommands,
                _onUnequipCommands);
        }

        internal EquipTraitSlotSettings Clone()
        {
            return new EquipTraitSlotSettings
            {
                _slotKey = _slotKey,
                _holderKey = _holderKey,
                _runOnEquipCommands = _runOnEquipCommands,
                _onEquipCommands = _onEquipCommands,
                _runOnUnequipCommands = _runOnUnequipCommands,
                _onUnequipCommands = _onUnequipCommands,
            };
        }
    }
}
