#nullable enable

using System;
using System.Collections.Generic;
using Game.Animation;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Conversation
{
    public enum CharacterDataBaseScopeMode
    {
        Scene = 10,
        Project = 20,
        Global = 30,
    }

    public enum CharacterRuntimeModuleKind
    {
        BaseName = 10,
        DefaultImage = 20,
        Expression = 30,
    }

    [Serializable]
    public abstract class CharacterRuntimeModulePreset : IDynamicManagedRefValue
    {
        [BoxGroup("Module")]
        [LabelText("Module Key")]
        [SerializeField]
        string _moduleKey = string.Empty;

        public string ModuleKey => string.IsNullOrWhiteSpace(_moduleKey) ? string.Empty : _moduleKey.Trim();
        public abstract CharacterRuntimeModuleKind ModuleKind { get; }

        public abstract CharacterRuntimeModulePreset CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class CharacterBaseNameModulePreset : CharacterRuntimeModulePreset
    {
        [BoxGroup("Base Name")]
        [LabelText("Base Name")]
        [SerializeField]
        DynamicValue<string> _baseName = DynamicValueExtensions.FromLiteral(string.Empty);

        public override CharacterRuntimeModuleKind ModuleKind => CharacterRuntimeModuleKind.BaseName;
        public DynamicValue<string> BaseName => _baseName;

        public string ResolveBaseName(IDynamicContext context)
        {
            return _baseName.GetOrDefault(context, string.Empty);
        }

        public override CharacterRuntimeModulePreset CreateRuntimeCopy()
        {
            return new CharacterBaseNameModulePreset
            {
                _baseName = _baseName,
            };
        }
    }

    [Serializable]
    public sealed class CharacterDefaultImageModulePreset : CharacterRuntimeModulePreset
    {
        [BoxGroup("Default Image")]
        [LabelText("Sprite Channel Tag")]
        [SerializeField]
        string _spriteChannelTag = "default";

        [BoxGroup("Default Image")]
        [LabelText("Default Portrait Animation")]
        [SerializeField]
        AnimationDataSource _defaultPortraitAnimation = new();

        public override CharacterRuntimeModuleKind ModuleKind => CharacterRuntimeModuleKind.DefaultImage;
        public string SpriteChannelTag => string.IsNullOrWhiteSpace(_spriteChannelTag) ? "default" : _spriteChannelTag.Trim();
        public AnimationDataSource DefaultPortraitAnimation => _defaultPortraitAnimation;

        public override CharacterRuntimeModulePreset CreateRuntimeCopy()
        {
            return new CharacterDefaultImageModulePreset
            {
                _spriteChannelTag = _spriteChannelTag,
                _defaultPortraitAnimation = _defaultPortraitAnimation ?? new AnimationDataSource(),
            };
        }
    }

    [Serializable]
    public sealed class CharacterExpressionEntryPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Expression")]
        [LabelText("Key")]
        [SerializeField]
        string _key = string.Empty;

        [BoxGroup("Expression")]
        [LabelText("Debug Text")]
        [SerializeField]
        string _debugText = string.Empty;

        [BoxGroup("Expression")]
        [LabelText("Portrait Animation")]
        [SerializeField]
        AnimationDataSource _portraitAnimation = new();

        public string Key => string.IsNullOrWhiteSpace(_key) ? string.Empty : _key.Trim();
        public string DebugText => _debugText ?? string.Empty;
        public AnimationDataSource PortraitAnimation => _portraitAnimation;

        public CharacterExpressionEntryPreset CreateRuntimeCopy()
        {
            return new CharacterExpressionEntryPreset
            {
                _key = _key,
                _debugText = _debugText,
                _portraitAnimation = _portraitAnimation ?? new AnimationDataSource(),
            };
        }
    }

    [Serializable]
    public sealed class CharacterExpressionModulePreset : CharacterRuntimeModulePreset
    {
        [BoxGroup("Expression")]
        [LabelText("Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeField]
        List<CharacterExpressionEntryPreset> _entries = new();

        public override CharacterRuntimeModuleKind ModuleKind => CharacterRuntimeModuleKind.Expression;
        public IReadOnlyList<CharacterExpressionEntryPreset> Entries => _entries;

        public bool TryResolve(string expressionKey, out CharacterExpressionEntryPreset? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(expressionKey))
                return false;

            var normalized = expressionKey.Trim();
            for (var i = 0; i < _entries.Count; i++)
            {
                var candidate = _entries[i];
                if (candidate == null)
                    continue;

                if (!string.Equals(candidate.Key, normalized, StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        public override CharacterRuntimeModulePreset CreateRuntimeCopy()
        {
            var copy = new CharacterExpressionModulePreset
            {
                _entries = new List<CharacterExpressionEntryPreset>(),
            };

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null)
                    continue;

                copy._entries.Add(entry.CreateRuntimeCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class CharacterDataBaseDefinition : IDynamicManagedRefValue
    {
        [BoxGroup("Identity")]
        [LabelText("Character Id")]
        [MinValue(1)]
        [SerializeField]
        int _characterId = 1;

        [BoxGroup("Identity")]
        [LabelText("Stable Key")]
        [SerializeField]
        string _stableKey = string.Empty;

        [BoxGroup("Identity")]
        [LabelText("Display Name")]
        [SerializeField]
        string _displayName = string.Empty;

        [BoxGroup("Runtime")]
        [LabelText("Runtime Template")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplateValue =
            DynamicValue<BaseRuntimeTemplatePreset>.FromSource(
                new ManagedRefLiteralSource<BaseRuntimeTemplatePreset>(new BaseRuntimeTemplatePreset()));

        [BoxGroup("Runtime")]
        [LabelText("Default Slot")]
        [SerializeField]
        ConversationCharacterSlot _defaultSlot = ConversationCharacterSlot.Center;

        [BoxGroup("Runtime")]
        [LabelText("Persistent Runtime")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _persistentRuntime = true;

        [BoxGroup("Modules")]
        [LabelText("Runtime Modules")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeReference]
        List<CharacterRuntimeModulePreset> _modules =
            new()
            {
                new CharacterBaseNameModulePreset(),
                new CharacterDefaultImageModulePreset(),
                new CharacterExpressionModulePreset(),
            };

        public int CharacterId => _characterId;
        public string StableKey => string.IsNullOrWhiteSpace(_stableKey) ? string.Empty : _stableKey.Trim();
        public string DisplayName => _displayName ?? string.Empty;
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplateValue => _runtimeTemplateValue;
        public ConversationCharacterSlot DefaultSlot => _defaultSlot;
        public bool PersistentRuntime => _persistentRuntime;
        public IReadOnlyList<CharacterRuntimeModulePreset> Modules => _modules;

        public bool TryGetModule<TModule>(out TModule? module) where TModule : CharacterRuntimeModulePreset
        {
            module = null;
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is not TModule typed)
                    continue;

                module = typed;
                return true;
            }

            return false;
        }

        public CharacterDataBaseDefinition CreateRuntimeCopy()
        {
            var copy = new CharacterDataBaseDefinition
            {
                _characterId = _characterId,
                _stableKey = _stableKey,
                _displayName = _displayName,
                _runtimeTemplateValue = _runtimeTemplateValue,
                _defaultSlot = _defaultSlot,
                _persistentRuntime = _persistentRuntime,
                _modules = new List<CharacterRuntimeModulePreset>(),
            };

            if (_modules != null)
            {
                for (var i = 0; i < _modules.Count; i++)
                {
                    var module = _modules[i];
                    if (module == null)
                        continue;

                    var moduleCopy = module.CreateRuntimeCopy();
                    if (moduleCopy == null)
                        continue;

                    copy._modules.Add(moduleCopy);
                }
            }

            return copy;
        }
    }

    [CreateAssetMenu(menuName = "Game/Conversation/Character Definition")]
    public sealed class CharacterDataBaseDefinitionSO : ScriptableObject, IDynamicValueAsset<CharacterDataBaseDefinition>
    {
        [SerializeReference]
        [HideLabel]
        [InlineProperty]
        [SerializeField]
        CharacterDataBaseDefinition? _preset = new();

        public CharacterDataBaseDefinition? Preset => _preset;
    }
}
