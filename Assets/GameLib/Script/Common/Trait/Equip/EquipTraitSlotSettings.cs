#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    /// <summary>
    /// Inspector 上で EquipTraitSlotRuntime を設定するためのデータ。
    /// TraitHolderSettings と対をなす。
    /// </summary>
    [Serializable]
    public sealed class EquipTraitSlotSettings
    {
        [BoxGroup("Slot")]
        [LabelText("Slot Key")]
        [Tooltip("このスロットを識別するキー（EquipTraitHolderHub 内で一意）。")]
        [SerializeField]
        string _slotKey = string.Empty;

        [BoxGroup("Slot")]
        [LabelText("Holder Key")]
        [Tooltip("対応する TraitHolderHubService の HolderKey。" +
            "この Holder がスロットの Trait 供給源となる。")]
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
