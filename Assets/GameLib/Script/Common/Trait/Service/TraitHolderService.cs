#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Trait
{
    // NOTE: External systems must resolve holders via ITraitHolderHubService with a key.
    public interface ITraitHolderService
    {
        IReadOnlyList<ITraitInstance> Traits { get; }
        event System.Action<IReadOnlyList<ITraitInstance>>? OnTraitsChanged;
        bool TryRegister(ITraitDefinition? definition, out ITraitInstance? instance);
        bool TryRemove(ITraitInstance? instance);
        bool TryRemove(ITraitDefinition? definition);
        bool TryUse(ITraitInstance? instance);
        bool TryUse(ITraitDefinition? definition);
        bool TryGetInstance(ITraitDefinition? definition, out ITraitInstance? instance);
        void Clear();
    }

    public partial class TraitHolderService :
        ITraitHolderService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode? _scope;
        protected readonly List<ITraitInstance> _traits = new(8);
        readonly HashSet<ITraitInstance> _held = new();
        bool _isActive;

        bool _runOnEquipCommands;
        VNext.CommandListData _onEquipCommands = new();
        bool _runOnUnequipCommands;
        VNext.CommandListData _onUnequipCommands = new();
        bool _allowDuplicateDefinitions;

        public TraitHolderService(IScopeNode? scope)
        {
            _scope = scope;
        }

        public IReadOnlyList<ITraitInstance> Traits => _traits;

        public event Action<IReadOnlyList<ITraitInstance>>? OnTraitsChanged;

        public bool TryRegister(ITraitDefinition? definition, out ITraitInstance? instance)
        {
            instance = null;
            if (definition == null)
                return false;

            if (!_allowDuplicateDefinitions && TryGetInstance(definition, out _))
                return false;

            var context = new TraitInstanceContext(_scope);
            instance = definition.CreateInstance(context);
            if (instance == null)
                return false;

            _traits.Add(instance);
            if (_isActive)
                ApplyHold(instance);

            if (_scope != null)
                instance.OnLtsInstantiated(_scope);

            TryRegisterRichText(instance);
            NotifyTraitsChanged();
            return true;
        }

        public bool TryRemove(ITraitInstance? instance)
        {
            if (instance == null)
                return false;

            int index = _traits.IndexOf(instance);
            if (index < 0)
                return false;

            if (_held.Remove(instance))
            {
                instance.OnRemove();
                ExecuteHolderCommands(instance, _onUnequipCommands, _runOnUnequipCommands);
            }

            if (_scope != null)
                instance.OnLtsInstantiated(_scope);

            TryUnregisterRichText(instance);
            _traits.RemoveAt(index);
            NotifyTraitsChanged();
            return true;
        }

        public bool TryRemove(ITraitDefinition? definition)
        {
            if (!TryGetInstance(definition, out var instance))
                return false;

            return TryRemove(instance);
        }

        public bool TryUse(ITraitInstance? instance)
        {
            if (instance == null)
                return false;
            if (!_isActive)
                return false;
            if (!_traits.Contains(instance))
                return false;

            instance.OnUse();
            return true;
        }

        public bool TryUse(ITraitDefinition? definition)
        {
            if (!TryGetInstance(definition, out var instance))
                return false;

            return TryUse(instance);
        }

        public bool TryGetInstance(ITraitDefinition? definition, out ITraitInstance? instance)
        {
            instance = null;
            if (definition == null)
                return false;

            for (int i = 0; i < _traits.Count; i++)
            {
                var trait = _traits[i];
                if (ReferenceEquals(trait.Definition, definition))
                {
                    instance = trait;
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            ClearRichTextRegistrations();
            if (_traits.Count == 0)
            {
                _held.Clear();
                return;
            }

            for (int i = 0; i < _traits.Count; i++)
            {
                var trait = _traits[i];
                if (_held.Remove(trait))
                {
                    trait.OnRemove();
                    ExecuteHolderCommands(trait, _onUnequipCommands, _runOnUnequipCommands);
                }

                if (_scope != null)
                    trait.OnLtsInstantiated(_scope);
            }

            _traits.Clear();
            _held.Clear();
            NotifyTraitsChanged();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _isActive = true;
            WriteHolderVarsToBlackboard();
            if (_traits.Count == 0)
                return;

            for (int i = 0; i < _traits.Count; i++)
                ApplyHold(_traits[i]);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Clear();
            _isActive = false;
        }

        public void SetHolderKey(string holderKey)
        {
            var normalized = string.IsNullOrWhiteSpace(holderKey) ? string.Empty : holderKey.Trim();
            if (string.Equals(_holderKey, normalized, StringComparison.Ordinal))
                return;

            _holderKey = normalized;
            WriteHolderVarsToBlackboard();
        }

        public void SetAllowDuplicateDefinitions(bool allowDuplicateDefinitions)
        {
            _allowDuplicateDefinitions = allowDuplicateDefinitions;
        }

        internal void SetHolderCommands(
            bool runOnEquip,
            VNext.CommandListData? onEquipCommands,
            bool runOnUnequip,
            VNext.CommandListData? onUnequipCommands)
        {
            _runOnEquipCommands = runOnEquip;
            _runOnUnequipCommands = runOnUnequip;
            _onEquipCommands = onEquipCommands ?? new VNext.CommandListData();
            _onUnequipCommands = onUnequipCommands ?? new VNext.CommandListData();
        }

        internal void RegisterInitialTraits(IReadOnlyList<TraitDefinitionSO>? definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                TryRegister(definition, out _);
            }
        }

        void ApplyHold(ITraitInstance? instance)
        {
            if (instance == null || _held.Contains(instance))
                return;

            instance.OnHold();
            _held.Add(instance);
            ExecuteHolderCommands(instance, _onEquipCommands, _runOnEquipCommands);
        }

        void ExecuteHolderCommands(ITraitInstance? instance, VNext.CommandListData? commands, bool shouldRun)
        {
            if (!shouldRun || commands == null || commands.Count == 0)
                return;

            if (_scope == null)
                return;

            var resolver = _scope.Resolver;
            if (resolver == null)
                return;

            resolver.TryResolve(out VNext.ICommandRunner? runner);
            if (runner == null)
                return;

            var vars = instance?.Context?.Vars ?? new VarStore();
            var ctx = new VNext.CommandContext(_scope, vars, runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            });
        }

        void NotifyTraitsChanged()
        {
            WriteHolderVarsToBlackboard();
            OnTraitsChanged?.Invoke(_traits);
        }
    }
}
