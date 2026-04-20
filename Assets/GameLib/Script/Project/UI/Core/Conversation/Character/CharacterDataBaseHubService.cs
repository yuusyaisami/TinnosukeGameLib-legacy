#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Conversation
{
    public sealed class CharacterRuntimeBinding
    {
        public int CharacterId { get; }
        public string StableKey { get; }
        public CharacterDataBaseDefinition Definition { get; }
        public IScopeNode Scope { get; }
        public IRuntimeResolver Resolver { get; }
        public Transform? SelfTransform => Scope.Identity?.SelfTransform;

        public CharacterRuntimeBinding(
            int characterId,
            string stableKey,
            CharacterDataBaseDefinition definition,
            IScopeNode scope,
            IRuntimeResolver resolver)
        {
            CharacterId = characterId;
            StableKey = stableKey ?? string.Empty;
            Definition = definition;
            Scope = scope;
            Resolver = resolver;
        }
    }

    public interface ICharacterDataBaseService
    {
        int DefinitionCount { get; }
        int RuntimeCount { get; }

        bool TryGetDefinition(int characterId, out CharacterDataBaseDefinition? definition);
        bool TryGetDefinition(string stableKey, out CharacterDataBaseDefinition? definition);
        bool TryResolveCharacterId(string stableKey, out int characterId);

        bool RegisterOrReplace(CharacterDataBaseDefinition definition);
        bool Unregister(int characterId);

        bool TryBindRuntime(int characterId, IScopeNode scope, IRuntimeResolver resolver);
        bool TryGetRuntime(int characterId, out CharacterRuntimeBinding? binding);
        bool TryReleaseRuntime(int characterId);
        void ReleaseAllRuntimes();
    }

    public sealed class CharacterDataBaseService :
        ICharacterDataBaseService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly CharacterDataBaseMB _mb;
        readonly Dictionary<int, CharacterDataBaseDefinition> _definitionsById = new();
        readonly Dictionary<string, int> _idByStableKey = new(StringComparer.Ordinal);
        readonly Dictionary<int, CharacterRuntimeBinding> _runtimeById = new();

        bool _isAcquired;

        public int DefinitionCount => _definitionsById.Count;
        public int RuntimeCount => _runtimeById.Count;

        public CharacterDataBaseService(IScopeNode owner, CharacterDataBaseMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _isAcquired = true;
            RebuildDefinitions();

            if (isReset)
                ReleaseAllRuntimes();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            ReleaseAllRuntimes();
            _definitionsById.Clear();
            _idByStableKey.Clear();
            _isAcquired = false;
        }

        public bool TryGetDefinition(int characterId, out CharacterDataBaseDefinition? definition)
        {
            definition = null;
            if (!_definitionsById.TryGetValue(characterId, out var found) || found == null)
                return false;

            definition = found;
            return true;
        }

        public bool TryGetDefinition(string stableKey, out CharacterDataBaseDefinition? definition)
        {
            definition = null;
            if (!TryResolveCharacterId(stableKey, out var characterId))
                return false;

            return TryGetDefinition(characterId, out definition);
        }

        public bool TryResolveCharacterId(string stableKey, out int characterId)
        {
            characterId = 0;
            if (string.IsNullOrWhiteSpace(stableKey))
                return false;

            var normalized = stableKey.Trim();
            return _idByStableKey.TryGetValue(normalized, out characterId) && characterId > 0;
        }

        public bool RegisterOrReplace(CharacterDataBaseDefinition definition)
        {
            if (definition == null || definition.CharacterId <= 0)
                return false;

            var runtime = definition.CreateRuntimeCopy();

            if (_definitionsById.TryGetValue(runtime.CharacterId, out var previous) && previous != null)
            {
                var previousStableKey = previous.StableKey;
                if (!string.IsNullOrWhiteSpace(previousStableKey) &&
                    _idByStableKey.TryGetValue(previousStableKey, out var indexedId) &&
                    indexedId == runtime.CharacterId)
                {
                    _idByStableKey.Remove(previousStableKey);
                }
            }

            _definitionsById[runtime.CharacterId] = runtime;

            var stableKey = runtime.StableKey;
            if (!string.IsNullOrWhiteSpace(stableKey))
                _idByStableKey[stableKey] = runtime.CharacterId;

            return true;
        }

        public bool Unregister(int characterId)
        {
            if (characterId <= 0)
                return false;

            if (_runtimeById.ContainsKey(characterId))
                return false;

            if (!_definitionsById.Remove(characterId))
                return false;

            RemoveStableKeyIndex(characterId);
            return true;
        }

        public bool TryBindRuntime(int characterId, IScopeNode scope, IRuntimeResolver resolver)
        {
            if (!_isAcquired)
                return false;

            if (characterId <= 0 || scope == null || resolver == null)
                return false;

            if (!TryGetDefinition(characterId, out var definition) || definition == null)
                return false;

            if (_runtimeById.TryGetValue(characterId, out var existing) && existing != null)
            {
                if (ReferenceEquals(existing.Scope, scope) && ReferenceEquals(existing.Resolver, resolver))
                    return true;

                if (existing.Definition.PersistentRuntime)
                    return true;
            }

            _runtimeById[characterId] = new CharacterRuntimeBinding(characterId, definition.StableKey, definition, scope, resolver);
            return true;
        }

        public bool TryGetRuntime(int characterId, out CharacterRuntimeBinding? binding)
        {
            binding = null;
            if (!_runtimeById.TryGetValue(characterId, out var found) || found == null)
                return false;

            binding = found;
            return true;
        }

        public bool TryReleaseRuntime(int characterId)
        {
            if (characterId <= 0)
                return false;

            return _runtimeById.Remove(characterId);
        }

        public void ReleaseAllRuntimes()
        {
            _runtimeById.Clear();
        }

        void RebuildDefinitions()
        {
            _definitionsById.Clear();
            _idByStableKey.Clear();

            var list = _mb.Definitions;
            for (var i = 0; i < list.Count; i++)
            {
                var definition = list[i];
                if (definition == null)
                    continue;

                if (definition.CharacterId <= 0)
                {
                    Debug.LogWarning($"[CharacterDataBase] Invalid CharacterId was skipped. index={i}");
                    continue;
                }

                if (_definitionsById.ContainsKey(definition.CharacterId))
                {
                    Debug.LogWarning($"[CharacterDataBase] Duplicate CharacterId was skipped. id={definition.CharacterId}");
                    continue;
                }

                var runtime = definition.CreateRuntimeCopy();
                _definitionsById.Add(runtime.CharacterId, runtime);

                var stableKey = runtime.StableKey;
                if (!string.IsNullOrWhiteSpace(stableKey) && !_idByStableKey.ContainsKey(stableKey))
                    _idByStableKey.Add(stableKey, runtime.CharacterId);
            }
        }

        void RemoveStableKeyIndex(int characterId)
        {
            string? removeKey = null;
            foreach (var pair in _idByStableKey)
            {
                if (pair.Value != characterId)
                    continue;

                removeKey = pair.Key;
                break;
            }

            if (!string.IsNullOrEmpty(removeKey))
                _idByStableKey.Remove(removeKey);
        }
    }
}
