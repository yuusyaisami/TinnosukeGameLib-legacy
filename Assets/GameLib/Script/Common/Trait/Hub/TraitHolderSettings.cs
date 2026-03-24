#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    [Serializable]
    public sealed class TraitHolderPlacementSettings
    {
        [LabelText("Position")]
        [SerializeField]
        DynamicValue<Vector3> _position = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [LabelText("Rotation Euler")]
        [SerializeField]
        DynamicValue<Vector3> _rotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [LabelText("Scale")]
        [SerializeField]
        DynamicValue<Vector3> _scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [LabelText("Use Parent")]
        [SerializeField]
        bool _useParent;

        [ShowIf(nameof(_useParent))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(_parentActorSource)")]
        [SerializeField]
        VNext.ActorSource _parentActorSource;

        [LabelText("Run On Placed")]
        [Tooltip("PlaceTraitRuntime 実行成功時に走ります。実行主体は PlaceTraitRuntimeExecutor で、actor は spawn 後の RuntimeLTS、VarStore には配置対象 Trait のデータと spawn 後 Runtime の blackboard が入ります。")]
        [SerializeField]
        bool _runOnPlacedCommands;

        [ShowIf(nameof(_runOnPlacedCommands))]
        [LabelText("On Placed Commands")]
        [Tooltip("PlaceTraitRuntime 実行成功時に走ります。実行主体は PlaceTraitRuntimeExecutor で、actor は spawn 後の RuntimeLTS、VarStore には配置対象 Trait のデータと spawn 後 Runtime の blackboard が入ります。")]
        [SerializeField]
        VNext.CommandListData _onPlacedCommands = new();

        public bool UseParent => _useParent;
        public VNext.ActorSource ParentActorSource => _parentActorSource;
        public bool RunOnPlacedCommands => _runOnPlacedCommands;
        public VNext.CommandListData OnPlacedCommands => _onPlacedCommands;

        public bool TryResolvePosition(IDynamicContext dynamicContext, out Vector3 position)
        {
            return _position.TryGet(dynamicContext, out position);
        }

        public bool TryResolveRotationEuler(IDynamicContext dynamicContext, out Vector3 rotationEuler)
        {
            return _rotationEuler.TryGet(dynamicContext, out rotationEuler);
        }

        public bool TryResolveScale(IDynamicContext dynamicContext, out Vector3 scale)
        {
            return _scale.TryGet(dynamicContext, out scale);
        }

        public TraitHolderPlacementSettings Clone()
        {
            return new TraitHolderPlacementSettings
            {
                _position = _position,
                _rotationEuler = _rotationEuler,
                _scale = _scale,
                _useParent = _useParent,
                _parentActorSource = _parentActorSource,
                _runOnPlacedCommands = _runOnPlacedCommands,
                _onPlacedCommands = _onPlacedCommands,
            };
        }
    }

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

        [BoxGroup("Placement")]
        [LabelText("Place Enabled")]
        [SerializeField]
        bool _placeEnabled;

        [BoxGroup("Placement")]
        [ShowIf(nameof(_placeEnabled))]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        TraitHolderPlacementSettings _placement = new();

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

        internal bool TryGetPlacementSettings(out TraitHolderPlacementSettings settings)
        {
            settings = null!;
            if (!_placeEnabled)
                return false;

            settings = _placement.Clone();
            return true;
        }

        internal TraitHolderSettings Clone()
        {
            return new TraitHolderSettings
            {
                _key = _key,
                _holderId = _holderId,
                _initialTraits = new List<TraitDefinitionSO>(_initialTraits),
                _placeEnabled = _placeEnabled,
                _placement = _placement != null ? _placement.Clone() : new TraitHolderPlacementSettings(),
                _commandsGroupAnchor = _commandsGroupAnchor,
                _runOnEquipCommands = _runOnEquipCommands,
                _onEquipCommands = _onEquipCommands,
                _runOnUnequipCommands = _runOnUnequipCommands,
                _onUnequipCommands = _onUnequipCommands
            };
        }
    }
}
