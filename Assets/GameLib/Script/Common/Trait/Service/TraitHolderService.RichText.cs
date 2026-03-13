#nullable enable
using System.Collections.Generic;
using Game.Common;
using Game.Vars.Generated;

namespace Game.Trait
{
    public partial class TraitHolderService
    {
        IRichTextRefService? _richTextRefService;
        readonly Dictionary<ITraitInstance, RichTextKeyPair> _richTextKeys = new();
        string _holderId = string.Empty;

        internal string HolderId => _holderId;

        internal void SetRichTextRefService(IRichTextRefService? service)
        {
            if (!ReferenceEquals(_richTextRefService, service) && _richTextRefService != null)
                ClearRichTextRegistrations();

            _richTextRefService = service;
            if (_richTextRefService == null || _traits.Count == 0)
                return;

            for (int i = 0; i < _traits.Count; i++)
                TryRegisterRichText(_traits[i]);
        }

        internal void SetHolderId(string holderId)
        {
            var normalized = string.IsNullOrWhiteSpace(holderId) ? string.Empty : holderId.Trim();
            if (string.Equals(_holderId, normalized, System.StringComparison.Ordinal))
                return;

            _holderId = normalized;
            WriteHolderVarsToBlackboard();

            if (_richTextRefService == null || _traits.Count == 0)
                return;

            ClearRichTextRegistrations();
            for (int i = 0; i < _traits.Count; i++)
                TryRegisterRichText(_traits[i]);
        }

        void ClearRichTextRegistrations()
        {
            if (_richTextKeys.Count == 0)
                return;

            if (_richTextRefService != null)
            {
                foreach (var keys in _richTextKeys.Values)
                {
                    if (!string.IsNullOrEmpty(keys.DescriptionKey))
                        _richTextRefService.TryUnregister(keys.DescriptionKey);
                    if (!string.IsNullOrEmpty(keys.NameKey))
                        _richTextRefService.TryUnregister(keys.NameKey);
                }
            }

            _richTextKeys.Clear();
        }

        void TryRegisterRichText(ITraitInstance? instance)
        {
            if (_richTextRefService == null || instance == null)
                return;

            if (_richTextKeys.ContainsKey(instance))
                return;

            if (instance.Definition is not IRichTextDescribableTrait describable)
                return;

            var definitionId = instance.Definition.DefinitionId;
            var instanceId = instance.InstanceId;
            if (string.IsNullOrEmpty(definitionId) || string.IsNullOrEmpty(instanceId))
                return;

            var prefix = instance.Definition.RefKeyPrefix;
            var baseKey = BuildRefKey(prefix, definitionId, instanceId);
            if (string.IsNullOrEmpty(baseKey))
                return;

            var keys = new RichTextKeyPair();

            var descKey = baseKey;
            var nameKey = BuildRefKey(prefix, definitionId, instanceId, "name");

            if (TryRegisterTemplate(descKey, describable.Description, out var registeredDescKey))
                keys.DescriptionKey = registeredDescKey;

            if (TryRegisterTemplate(nameKey, describable.Name, out var registeredNameKey))
                keys.NameKey = registeredNameKey;

            var vars = instance.Context.Vars;
            if (!string.IsNullOrEmpty(nameKey))
                vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.nameKey, DynamicVariant.FromString(nameKey));
            if (!string.IsNullOrEmpty(descKey))
                vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.descriptionKey, DynamicVariant.FromString(descKey));

            if (string.IsNullOrEmpty(keys.DescriptionKey) && string.IsNullOrEmpty(keys.NameKey))
                return;

            _richTextKeys[instance] = keys;
        }

        internal bool TryGetRichTextKeys(ITraitInstance instance, out string descriptionKey, out string nameKey)
        {
            descriptionKey = string.Empty;
            nameKey = string.Empty;

            if (instance == null)
                return false;

            if (!_richTextKeys.TryGetValue(instance, out var keys))
                return false;

            descriptionKey = keys.DescriptionKey;
            nameKey = keys.NameKey;
            return true;
        }

        void TryUnregisterRichText(ITraitInstance? instance)
        {
            if (_richTextRefService == null || instance == null)
                return;

            if (!_richTextKeys.TryGetValue(instance, out var keys))
                return;

            if (!string.IsNullOrEmpty(keys.DescriptionKey))
                _richTextRefService.TryUnregister(keys.DescriptionKey);
            if (!string.IsNullOrEmpty(keys.NameKey))
                _richTextRefService.TryUnregister(keys.NameKey);

            _richTextKeys.Remove(instance);
        }

        bool TryRegisterTemplate(string refKey, RichTextTemplateData? data, out string registeredKey)
        {
            registeredKey = string.Empty;
            if (_richTextRefService == null)
                return false;
            if (string.IsNullOrEmpty(refKey))
                return false;
            if (data == null || string.IsNullOrEmpty(data.Template))
                return false;

            var source = new RichTextSource
            {
                Template = data.Template
            };
            source.SetExternalVariables(data.Variables, includeLocalVariables: false);

            var provider = new RichTextProvider(source);
            if (!_richTextRefService.TryRegister(refKey, provider, overwrite: false))
                return false;

            registeredKey = refKey;
            return true;
        }

        string BuildRefKey(string? prefix, string definitionId, string instanceId, string? suffix = null)
        {
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(definitionId) || string.IsNullOrEmpty(instanceId))
                return string.Empty;

            string baseKey = string.IsNullOrEmpty(_holderId)
                ? $"{prefix}:{definitionId}:{instanceId}"
                : $"{prefix}:{_holderId}:{definitionId}:{instanceId}";

            if (string.IsNullOrEmpty(suffix))
                return baseKey;

            return $"{baseKey}:{suffix}";
        }

        struct RichTextKeyPair
        {
            public string DescriptionKey;
            public string NameKey;
        }
    }
}
