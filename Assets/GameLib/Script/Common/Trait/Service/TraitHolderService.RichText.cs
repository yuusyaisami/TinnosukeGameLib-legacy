#nullable enable
using System.Collections.Generic;
using Game.Common;
using Game.Vars.Generated;

namespace Game.Trait
{
    internal enum TraitRichTextKeyLookupFailureReason
    {
        None = 0,
        InstanceNull = 10,
        DefinitionNull = 20,
        NotRichTextDescribable = 30,
        DefinitionIdMissing = 40,
        InstanceIdMissing = 50,
        RefKeyPrefixMissing = 60,
        RichTextRefServiceMissing = 70,
        BothTemplatesMissing = 80,
        RegistrationMissing = 90,
        BothKeysEmpty = 100,
        DescriptionKeyMissing = 110,
        NameKeyMissing = 120,
    }

    internal readonly struct TraitRichTextKeyLookupDiagnostic
    {
        public TraitRichTextKeyLookupFailureReason FailureReason { get; }
        public string HolderId { get; }
        public string TraitDisplayName { get; }
        public string DefinitionId { get; }
        public string InstanceId { get; }
        public string RefKeyPrefix { get; }
        public string ExpectedDescriptionKey { get; }
        public string ExpectedNameKey { get; }
        public string DescriptionKey { get; }
        public string NameKey { get; }
        public bool HasRichTextRefService { get; }
        public bool IsRichTextDescribable { get; }
        public bool HasDescriptionTemplate { get; }
        public bool HasNameTemplate { get; }
        public bool HasRegistration { get; }

        public TraitRichTextKeyLookupDiagnostic(
            TraitRichTextKeyLookupFailureReason failureReason,
            string holderId,
            string traitDisplayName,
            string definitionId,
            string instanceId,
            string refKeyPrefix,
            string expectedDescriptionKey,
            string expectedNameKey,
            string descriptionKey,
            string nameKey,
            bool hasRichTextRefService,
            bool isRichTextDescribable,
            bool hasDescriptionTemplate,
            bool hasNameTemplate,
            bool hasRegistration)
        {
            FailureReason = failureReason;
            HolderId = holderId ?? string.Empty;
            TraitDisplayName = traitDisplayName ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            RefKeyPrefix = refKeyPrefix ?? string.Empty;
            ExpectedDescriptionKey = expectedDescriptionKey ?? string.Empty;
            ExpectedNameKey = expectedNameKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            HasRichTextRefService = hasRichTextRefService;
            IsRichTextDescribable = isRichTextDescribable;
            HasDescriptionTemplate = hasDescriptionTemplate;
            HasNameTemplate = hasNameTemplate;
            HasRegistration = hasRegistration;
        }
    }

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
            return TryGetRichTextKeys(instance, out descriptionKey, out nameKey, out _);
        }

        internal bool TryGetRichTextKeys(
            ITraitInstance? instance,
            out string descriptionKey,
            out string nameKey,
            out TraitRichTextKeyLookupDiagnostic diagnostic)
        {
            descriptionKey = string.Empty;
            nameKey = string.Empty;
            var keys = default(RichTextKeyPair);
            var hasRegistration = false;
            if (instance != null)
                hasRegistration = _richTextKeys.TryGetValue(instance, out keys);
            if (hasRegistration)
            {
                descriptionKey = keys.DescriptionKey ?? string.Empty;
                nameKey = keys.NameKey ?? string.Empty;
            }

            diagnostic = BuildRichTextLookupDiagnostic(instance, hasRegistration, descriptionKey, nameKey);
            return hasRegistration;
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

        TraitRichTextKeyLookupDiagnostic BuildRichTextLookupDiagnostic(
            ITraitInstance? instance,
            bool hasRegistration,
            string descriptionKey,
            string nameKey)
        {
            var holderId = _holderId;
            var definition = instance?.Definition;
            var definitionId = definition?.DefinitionId ?? string.Empty;
            var instanceId = instance?.InstanceId ?? string.Empty;
            var prefix = definition?.RefKeyPrefix ?? string.Empty;
            var expectedDescriptionKey = BuildRefKey(prefix, definitionId, instanceId);
            var expectedNameKey = BuildRefKey(prefix, definitionId, instanceId, "name");

            var isRichTextDescribable = definition is IRichTextDescribableTrait;
            var describable = definition as IRichTextDescribableTrait;
            var hasDescriptionTemplate = describable?.Description != null &&
                !string.IsNullOrEmpty(describable.Description.Template);
            var hasNameTemplate = describable?.Name != null &&
                !string.IsNullOrEmpty(describable.Name.Template);

            var traitDisplayName = ResolveTraitDisplayName(definition);
            var reason = ResolveRichTextFailureReason(
                instance,
                definition,
                definitionId,
                instanceId,
                prefix,
                hasRegistration,
                descriptionKey,
                nameKey,
                isRichTextDescribable,
                hasDescriptionTemplate,
                hasNameTemplate);

            return new TraitRichTextKeyLookupDiagnostic(
                reason,
                holderId,
                traitDisplayName,
                definitionId,
                instanceId,
                prefix,
                expectedDescriptionKey,
                expectedNameKey,
                descriptionKey,
                nameKey,
                _richTextRefService != null,
                isRichTextDescribable,
                hasDescriptionTemplate,
                hasNameTemplate,
                hasRegistration);
        }

        TraitRichTextKeyLookupFailureReason ResolveRichTextFailureReason(
            ITraitInstance? instance,
            ITraitDefinition? definition,
            string definitionId,
            string instanceId,
            string prefix,
            bool hasRegistration,
            string descriptionKey,
            string nameKey,
            bool isRichTextDescribable,
            bool hasDescriptionTemplate,
            bool hasNameTemplate)
        {
            if (instance == null)
                return TraitRichTextKeyLookupFailureReason.InstanceNull;

            if (definition == null)
                return TraitRichTextKeyLookupFailureReason.DefinitionNull;

            if (!isRichTextDescribable)
                return TraitRichTextKeyLookupFailureReason.NotRichTextDescribable;

            if (string.IsNullOrEmpty(definitionId))
                return TraitRichTextKeyLookupFailureReason.DefinitionIdMissing;

            if (string.IsNullOrEmpty(instanceId))
                return TraitRichTextKeyLookupFailureReason.InstanceIdMissing;

            if (string.IsNullOrEmpty(prefix))
                return TraitRichTextKeyLookupFailureReason.RefKeyPrefixMissing;

            if (_richTextRefService == null)
                return TraitRichTextKeyLookupFailureReason.RichTextRefServiceMissing;

            if (!hasRegistration)
            {
                if (!hasDescriptionTemplate && !hasNameTemplate)
                    return TraitRichTextKeyLookupFailureReason.BothTemplatesMissing;

                return TraitRichTextKeyLookupFailureReason.RegistrationMissing;
            }

            if (string.IsNullOrEmpty(descriptionKey) && string.IsNullOrEmpty(nameKey))
                return TraitRichTextKeyLookupFailureReason.BothKeysEmpty;

            if (string.IsNullOrEmpty(descriptionKey))
                return TraitRichTextKeyLookupFailureReason.DescriptionKeyMissing;

            if (string.IsNullOrEmpty(nameKey))
                return TraitRichTextKeyLookupFailureReason.NameKeyMissing;

            return TraitRichTextKeyLookupFailureReason.None;
        }

        static string ResolveTraitDisplayName(ITraitDefinition? definition)
        {
            if (definition is UnityEngine.Object unityObject && !string.IsNullOrEmpty(unityObject.name))
                return unityObject.name;

            return definition?.GetType().Name ?? string.Empty;
        }

        struct RichTextKeyPair
        {
            public string DescriptionKey;
            public string NameKey;
        }
    }
}
