#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Commands.VNext
{
    [CreateAssetMenu(menuName = "Game/Commands/VNext/Command Catalog")]
    public sealed class CommandCatalogSO : ScriptableObject, ICommandCatalog
    {
        [SerializeField] List<CommandCatalogEntry> entries = new();
        [SerializeField] List<CommandPayloadSchemaAsset> payloadSchemas = new();

        readonly Dictionary<int, int> _keyIdToIndex = new();
        readonly Dictionary<int, CommandPayloadSchema> _payloadSchemaByCommandId = new();
        bool _built;
        bool _payloadSchemasBuilt;
        bool _payloadSchemasInvalid;

        public IReadOnlyList<CommandCatalogEntry> Entries => entries;

        public IReadOnlyList<CommandPayloadSchemaAsset> PayloadSchemas => payloadSchemas;

        public bool TryResolve(CommandKeyId keyId, out ICommandData data)
        {
            data = null!;
            EnsureLookup();
            if (keyId.Value == 0)
                return false;

            if (!_keyIdToIndex.TryGetValue(keyId.Value, out var index))
                return false;

            if (index < 0 || index >= entries.Count)
                return false;

            var entry = entries[index];
            data = entry?.Data!;
            return data != null;
        }

        public bool TryResolve(CommandKeyRef key, out ICommandData data)
        {
            data = null!;
            if (string.IsNullOrEmpty(key.StableKey))
                return false;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null)
            {
                if (registry.IsReservedKey(key.StableKey))
                    return false;

                if (registry.TryResolve(key.StableKey, out var keyId))
                    return TryResolve(keyId, out data);

#if UNITY_EDITOR
                return TryResolveByStableKey(key.StableKey, out data);
#endif
            }

            return false;
        }

        public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
        {
            meta = null!;
            if (string.IsNullOrEmpty(key.StableKey))
                return false;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null)
            {
                if (registry.IsReservedKey(key.StableKey))
                    return false;

                if (registry.TryResolve(key.StableKey, out var keyId))
                {
                    EnsureLookup();
                    if (!_keyIdToIndex.TryGetValue(keyId.Value, out var index))
                        return false;

                    if (index < 0 || index >= entries.Count)
                        return false;

                    meta = entries[index]?.Meta!;
                    return meta != null;
                }

#if UNITY_EDITOR
                return TryGetMetaByStableKey(key.StableKey, out meta);
#endif
            }

            return false;
        }

        public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
        {
            schema = null!;
            EnsurePayloadSchemaLookup();
            if (commandId <= 0 || _payloadSchemasInvalid)
                return false;

            return _payloadSchemaByCommandId.TryGetValue(commandId, out schema!);
        }

        void EnsureLookup()
        {
            if (_built)
                return;

            _keyIdToIndex.Clear();
            _built = true;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Data == null)
                    continue;

                var stableKey = entry.Key.StableKey;
                if (string.IsNullOrEmpty(stableKey))
                {
                    Debug.LogError("[CommandCatalogSO] Entry has empty stableKey.");
                    continue;
                }

                if (!registry.TryResolve(stableKey, out var keyId) || keyId.Value == 0)
                {
                    if (registry.IsReservedKey(stableKey))
                        Debug.LogError($"[CommandCatalogSO] Tombstoned key: '{stableKey}'");
                    else
                        Debug.LogError($"[CommandCatalogSO] Key not registered: '{stableKey}'");
                    continue;
                }

                if (_keyIdToIndex.ContainsKey(keyId.Value))
                {
                    Debug.LogError($"[CommandCatalogSO] Duplicate keyId: {keyId.Value} for '{stableKey}'");
                    continue;
                }

                _keyIdToIndex.Add(keyId.Value, i);
            }
        }

        void EnsurePayloadSchemaLookup()
        {
            if (_payloadSchemasBuilt)
                return;

            _payloadSchemaByCommandId.Clear();
            _payloadSchemasBuilt = true;
            _payloadSchemasInvalid = false;

            var errors = new StringBuilder();

            for (int index = 0; index < payloadSchemas.Count; index++)
            {
                CommandPayloadSchemaAsset asset = payloadSchemas[index];
                if (asset == null)
                {
                    errors.AppendLine($"Payload schema entry is null. Index={index}");
                    continue;
                }

                CommandPayloadSchema schema;
                try
                {
                    schema = asset.ToRuntimeSchema();
                }
                catch (Exception ex)
                {
                    errors.AppendLine($"Payload schema entry is invalid. Index={index} Error={ex.Message}");
                    continue;
                }

                if (_payloadSchemaByCommandId.ContainsKey(schema.CommandId))
                {
                    errors.AppendLine($"Duplicate payload schema for CommandId={schema.CommandId}.");
                    continue;
                }

                _payloadSchemaByCommandId.Add(schema.CommandId, schema);
            }

            if (errors.Length > 0)
            {
                _payloadSchemaByCommandId.Clear();
                _payloadSchemasInvalid = true;
                Debug.LogError("[CommandCatalogSO] Payload schema table is invalid and cannot be used:\n" + errors.ToString().TrimEnd());
            }
        }

        bool TryResolveByStableKey(string stableKey, out ICommandData data)
        {
            data = null!;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Data == null)
                    continue;

                if (string.Equals(entry.Key.StableKey, stableKey, System.StringComparison.Ordinal))
                {
                    data = entry.Data;
                    return true;
                }
            }

            return false;
        }

        bool TryGetMetaByStableKey(string stableKey, out CommandCatalogMeta meta)
        {
            meta = null!;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Meta == null)
                    continue;

                if (string.Equals(entry.Key.StableKey, stableKey, System.StringComparison.Ordinal))
                {
                    meta = entry.Meta;
                    return true;
                }
            }

            return false;
        }

        void OnEnable()
        {
            _built = false;
            _payloadSchemasBuilt = false;
            _payloadSchemasInvalid = false;
        }

        void OnValidate()
        {
            _built = false;
            _payloadSchemasBuilt = false;
            _payloadSchemasInvalid = false;
        }
    }

    [Serializable]
    public sealed class CommandPayloadSchemaAsset
    {
        [SerializeField] int commandId;
        [SerializeField] int schemaId;
        [SerializeField] CommandPayloadUnknownFieldPolicy unknownFieldPolicy = CommandPayloadUnknownFieldPolicy.Reject;
        [SerializeField] List<CommandPayloadFieldSchemaAsset> fields = new();

        public int CommandId => commandId;

        public int SchemaId => schemaId;

        public CommandPayloadUnknownFieldPolicy UnknownFieldPolicy => unknownFieldPolicy;

        public IReadOnlyList<CommandPayloadFieldSchemaAsset> Fields => fields;

        public CommandPayloadSchema ToRuntimeSchema()
        {
            var runtimeFields = new CommandPayloadFieldSchema[fields.Count];
            for (int index = 0; index < fields.Count; index++)
            {
                CommandPayloadFieldSchemaAsset field = fields[index];
                if (field == null)
                    throw new ArgumentException($"Payload schema field entry is null. Index={index}");

                runtimeFields[index] = field.ToRuntimeSchema();
            }

            return new CommandPayloadSchema(commandId, schemaId, unknownFieldPolicy, runtimeFields);
        }
    }

    public sealed class VerifiedCommandPayloadSchemaCatalog : ICommandPayloadSchemaCatalog
    {
        readonly Dictionary<int, CommandPayloadSchema> schemaByCommandId = new();

        public VerifiedCommandPayloadSchemaCatalog(IReadOnlyList<CommandPayloadSchema> schemas)
        {
            if (schemas == null)
                throw new ArgumentNullException(nameof(schemas));

            for (int index = 0; index < schemas.Count; index++)
            {
                CommandPayloadSchema schema = schemas[index] ?? throw new ArgumentException("Verified command payload schema catalog must not contain null schemas.", nameof(schemas));
                if (schemaByCommandId.ContainsKey(schema.CommandId))
                    throw new ArgumentException("Verified command payload schema catalog requires unique CommandTypeId values.", nameof(schemas));

                schemaByCommandId.Add(schema.CommandId, schema);
            }
        }

        public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
        {
            return schemaByCommandId.TryGetValue(commandId, out schema!);
        }
    }

    [Serializable]
    public sealed class CommandPayloadFieldSchemaAsset
    {
        [SerializeField] string fieldPath = string.Empty;
        [SerializeField] CommandPayloadFieldKind kind = CommandPayloadFieldKind.Unknown;
        [SerializeField] CommandPayloadFieldRequirement requirement = CommandPayloadFieldRequirement.Optional;
        [SerializeField] CommandPayloadReferenceKind referenceKind = CommandPayloadReferenceKind.None;
        [SerializeField] bool allowNull;
        [SerializeField] int sourceLocationId;

        public string FieldPath => fieldPath ?? string.Empty;

        public CommandPayloadFieldKind Kind => kind;

        public CommandPayloadFieldRequirement Requirement => requirement;

        public CommandPayloadReferenceKind ReferenceKind => referenceKind;

        public bool AllowNull => allowNull;

        public int SourceLocationId => sourceLocationId;

        public CommandPayloadFieldSchema ToRuntimeSchema()
        {
            return new CommandPayloadFieldSchema(fieldPath, kind, requirement, referenceKind, allowNull, sourceLocationId);
        }
    }
}
