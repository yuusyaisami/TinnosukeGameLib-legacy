#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    [Serializable]
    public sealed class TraitHolderSettings
    {
        [BoxGroup("Holder")]
        [LabelText("Key")]
        [Tooltip("Required. External systems resolve ITraitHolderService by this key via the hub.")]
        [SerializeField]
        string _key = string.Empty;

        [BoxGroup("Holder")]
        [LabelText("Holder ID")]
        [Tooltip("Optional. Used as a namespace segment for trait rich-text keys (e.g., prefix:holderId:definitionId:instanceId).")]
        [SerializeField]
        // RichText参照キーに含めるための識別子（Holderごとの名前空間）
        string _holderId = string.Empty;

        [BoxGroup("Holder")]
        [LabelText("Initial Traits")]
        [Tooltip("Traits equipped at startup for this holder.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = false)]
        [SerializeField]
        List<TraitDefinitionSO> _initialTraits = new();

        [BoxGroup("Commands")]
        [ShowInInspector]
        [HideLabel]
        [SerializeField]
#pragma warning disable CS0414 // assigned but its value is never used - field is used by Odin at editor time
        int _commandsGroupAnchor = 0;
#pragma warning restore CS0414

        [BoxGroup("Commands/Equip")]
        [LabelText("Run On Equip")]
        [SerializeField]
        bool _runOnEquipCommands;

        [BoxGroup("Commands/Equip")]
        [ShowIf(nameof(_runOnEquipCommands))]
        [LabelText("On Equip Commands")]
        [SerializeField]
        VNext.CommandListData _onEquipCommands = new();

        [BoxGroup("Commands/Unequip")]
        [LabelText("Run On Unequip")]
        [SerializeField]
        bool _runOnUnequipCommands;

        [BoxGroup("Commands/Unequip")]
        [ShowIf(nameof(_runOnUnequipCommands))]
        [LabelText("On Unequip Commands")]
        [SerializeField]
        VNext.CommandListData _onUnequipCommands = new();

        public string Key => _key;
        public string HolderId => _holderId;

        internal string NormalizedKey => string.IsNullOrWhiteSpace(_key) ? string.Empty : _key.Trim();

        internal void ApplyTo(TraitHolderService service)
        {
            service.SetHolderKey(_key);
            service.SetHolderId(_holderId);
            service.SetHolderCommands(_runOnEquipCommands, _onEquipCommands, _runOnUnequipCommands, _onUnequipCommands);
            service.RegisterInitialTraits(_initialTraits);
        }

        internal TraitHolderSettings Clone()
        {
            return new TraitHolderSettings
            {
                _key = _key,
                _holderId = _holderId,
                _initialTraits = new List<TraitDefinitionSO>(_initialTraits),
                _commandsGroupAnchor = _commandsGroupAnchor,
                _runOnEquipCommands = _runOnEquipCommands,
                _onEquipCommands = _onEquipCommands,
                _runOnUnequipCommands = _runOnUnequipCommands,
                _onUnequipCommands = _onUnequipCommands
            };
        }
    }
}
