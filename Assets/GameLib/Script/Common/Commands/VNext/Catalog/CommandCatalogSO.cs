#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Game.Kernel.Authoring;
using Game.Kernel.Contributions;
using Game.Kernel.IR;
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

    [DisallowMultipleComponent]
    public sealed class CommandCatalogDeclarationAuthoringMB : MonoBehaviour, ICommandDeclarationAuthoring
    {
        [SerializeField] List<CommandCatalogSO> commandCatalogs = new();

        public IReadOnlyList<CommandCatalogSO> CommandCatalogs => commandCatalogs;

        public bool TryCreateCommandDeclarations(ModuleId ownerModule, out CommandDeclarationInput[] declarations, out string failureReason)
        {
            declarations = Array.Empty<CommandDeclarationInput>();
            failureReason = string.Empty;

#if !UNITY_EDITOR
            failureReason = "Command catalog declaration authoring requires editor-only asset database access.";
            return false;
#else
            if (ownerModule.Value == 0)
            {
                failureReason = "Command catalog declaration authoring requires a non-zero owner module.";
                return false;
            }

            if (commandCatalogs == null || commandCatalogs.Count == 0)
                return true;

            List<CommandDeclarationInput> declarationBuffer = new List<CommandDeclarationInput>();
            HashSet<int> seenCommandTypeIds = new HashSet<int>();
            HashSet<string> seenStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < commandCatalogs.Count; index++)
            {
                CommandCatalogSO catalog = commandCatalogs[index];
                if (catalog == null)
                {
                    failureReason = "Command catalog declaration authoring encountered a null catalog reference at index " + index + ".";
                    return false;
                }

                if (!TryCreateCatalogSourceLocation(index, catalog, out UnitySourceLocation source, out failureReason))
                    return false;

                if (!CommandCatalogDeclarationBridge.TryCreateCommandDeclarations(catalog, ownerModule, source, out CommandDeclarationInput[] catalogDeclarations, out _, out failureReason))
                    return false;

                for (int declarationIndex = 0; declarationIndex < catalogDeclarations.Length; declarationIndex++)
                {
                    CommandDeclarationInput declaration = catalogDeclarations[declarationIndex];
                    if (!seenCommandTypeIds.Add(declaration.TypeId.Value))
                    {
                        failureReason = "Command catalog declaration authoring requires command type ids to be unique across referenced catalogs. CommandTypeId=" + declaration.TypeId.Value + ".";
                        return false;
                    }

                    if (!seenStableIds.Add(declaration.StableId))
                    {
                        failureReason = "Command catalog declaration authoring requires stable keys to be unique across referenced catalogs. StableKey=" + declaration.StableId + ".";
                        return false;
                    }

                    declarationBuffer.Add(declaration);
                }
            }

            declarations = declarationBuffer.ToArray();
            return true;
#endif
        }

#if UNITY_EDITOR
        static bool TryCreateCatalogSourceLocation(int index, CommandCatalogSO catalog, out UnitySourceLocation source, out string failureReason)
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(catalog);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                source = default;
                failureReason = "Command catalog declaration authoring requires referenced catalogs to be saved assets. Index=" + index + ".";
                return false;
            }

            if (!UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(catalog, out string guid, out long localFileId))
            {
                source = default;
                failureReason = "Command catalog declaration authoring could not resolve asset traceability for catalog '" + assetPath + "'.";
                return false;
            }

            source = new UnitySourceLocation(
                UnityAuthoringSourceKind.ScriptableObjectAsset,
                guid,
                assetPath,
                localFileId,
                null,
                null,
                nameof(CommandCatalogDeclarationAuthoringMB),
                "commandCatalogs[" + index + "]");
            failureReason = string.Empty;
            return true;
        }
#endif
    }

    public static class CommandCatalogDeclarationBridge
    {
        readonly struct PayloadSchemaRecord
        {
            public PayloadSchemaRecord(CommandPayloadSchemaAsset asset, int index)
            {
                Asset = asset ?? throw new ArgumentNullException(nameof(asset));
                Index = index;
            }

            public CommandPayloadSchemaAsset Asset { get; }

            public int Index { get; }
        }

        public static bool TryCreateCommandDeclarations(
            CommandCatalogSO catalog,
            ModuleId ownerModule,
            in UnitySourceLocation catalogSource,
            out CommandDeclarationInput[] declarations,
            out ContributionItem[] contributions,
            out string failureReason)
        {
            declarations = Array.Empty<CommandDeclarationInput>();
            contributions = Array.Empty<ContributionItem>();
            failureReason = string.Empty;

            if (catalog == null)
            {
                failureReason = "Command catalog declaration bridge requires a catalog asset.";
                return false;
            }

            if (ownerModule.Value == 0)
            {
                failureReason = "Command catalog declaration bridge requires a non-zero owner module.";
                return false;
            }

            if (!catalogSource.IsSpecified)
            {
                failureReason = "Command catalog declaration bridge requires a specified Unity source location for the catalog asset.";
                return false;
            }

            IReadOnlyList<CommandCatalogEntry> entries = catalog.Entries;
            if (entries == null || entries.Count == 0)
            {
                failureReason = "Command catalog declaration bridge requires at least one command entry.";
                return false;
            }

            if (!TryBuildPayloadSchemaLookup(catalog.PayloadSchemas, out Dictionary<int, PayloadSchemaRecord> schemasByCommandId, out failureReason))
                return false;

            List<CommandDeclarationInput> declarationBuffer = new List<CommandDeclarationInput>(entries.Count);
            List<ContributionItem> contributionBuffer = new List<ContributionItem>(entries.Count);
            HashSet<int> seenCommandIds = new HashSet<int>();
            HashSet<string> seenStableIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<int, string> authoringKeyIds = new Dictionary<int, string>();
            Dictionary<int, string> categoryIds = new Dictionary<int, string>();
            ContributionSource contributionSource = ToContributionSource(catalogSource.Kind);
            ContributionAvailability availability = new ContributionAvailability(null, null, null);

            for (int index = 0; index < entries.Count; index++)
            {
                CommandCatalogEntry entry = entries[index];
                if (entry == null)
                {
                    failureReason = "Command catalog declaration bridge encountered a null entry at index " + index + ".";
                    return false;
                }

                ICommandData data = entry.Data;
                if (data == null)
                {
                    failureReason = "Command catalog declaration bridge requires every entry to provide ICommandData. Index=" + index + ".";
                    return false;
                }

                string stableId = entry.Key.StableKey == null ? string.Empty : entry.Key.StableKey.Trim();
                if (stableId.Length == 0)
                {
                    failureReason = "Command catalog declaration bridge requires every entry to provide a stable key. Index=" + index + ".";
                    return false;
                }

                if (!seenStableIds.Add(stableId))
                {
                    failureReason = "Command catalog declaration bridge requires stable keys to be unique. StableKey=" + stableId + ".";
                    return false;
                }

                int commandIdValue = data.CommandId;
                if (commandIdValue <= 0)
                {
                    failureReason = "Command catalog declaration bridge requires every entry to provide a positive CommandId. StableKey=" + stableId + ".";
                    return false;
                }

                if (!seenCommandIds.Add(commandIdValue))
                {
                    failureReason = "Command catalog declaration bridge requires CommandId values to be unique. CommandId=" + commandIdValue + ".";
                    return false;
                }

                if (!schemasByCommandId.TryGetValue(commandIdValue, out PayloadSchemaRecord schemaRecord))
                {
                    failureReason = "Command catalog declaration bridge requires an explicit payload schema asset for CommandId=" + commandIdValue + " (StableKey=" + stableId + ").";
                    return false;
                }

                string category = entry.Meta?.Category == null ? string.Empty : entry.Meta.Category.Trim();
                if (category.Length == 0)
                {
                    failureReason = "Command catalog declaration bridge requires every entry to provide a non-empty category. StableKey=" + stableId + ".";
                    return false;
                }

                int authoringKeyIdValue = ComputeDeterministicId("CommandAuthoringKey", stableId);
                if (!TryRegisterDeterministicId(authoringKeyIds, authoringKeyIdValue, stableId, "authoring key", out failureReason))
                    return false;

                int categoryIdValue = ComputeDeterministicId("CommandCategory", category);
                if (!TryRegisterDeterministicId(categoryIds, categoryIdValue, category, "command category", out failureReason))
                    return false;

                UnitySourceLocation commandUnitySource = CreatePropertySource(catalogSource, "entries[" + index + "]");
                UnitySourceLocation keyUnitySource = CreatePropertySource(catalogSource, "entries[" + index + "].key");
                UnitySourceLocation executorUnitySource = CreatePropertySource(catalogSource, "entries[" + index + "].data");

                if (!TryCreatePayloadSchemaDeclaration(schemaRecord, catalogSource, out CommandPayloadSchemaDeclarationInput payloadSchema, out failureReason))
                    return false;

                SourceLocationIR commandSource = UnityAuthoringBridge.ToKernelSourceLocation(commandUnitySource);
                declarationBuffer.Add(new CommandDeclarationInput(
                    ownerModule,
                    new CommandTypeId(commandIdValue),
                    data.GetType().Name,
                    new CommandAuthoringKeyId(authoringKeyIdValue),
                    stableId,
                    UnityAuthoringBridge.ToKernelSourceLocation(keyUnitySource),
                    new CommandCategoryId(categoryIdValue),
                    payloadSchema,
                    new CommandExecutorId(commandIdValue),
                    UnityAuthoringBridge.ToKernelSourceLocation(executorUnitySource),
                    commandSource));

                contributionBuffer.Add(new ContributionItem(
                    ContributionKind.CommandContribution,
                    ownerModule,
                    contributionSource,
                    commandSource,
                    stableId,
                    availability,
                    debugName: data.GetType().Name));
            }

            declarations = declarationBuffer.ToArray();
            contributions = contributionBuffer.ToArray();
            return true;
        }

        public static bool TryBuildVerifiedCommands(
            CommandCatalogSO catalog,
            ModuleId ownerModule,
            in UnitySourceLocation catalogSource,
            out CommandDeclarationBuildResult buildResult,
            out ContributionItem[] contributions,
            out string failureReason)
        {
            buildResult = null!;

            if (!TryCreateCommandDeclarations(catalog, ownerModule, catalogSource, out CommandDeclarationInput[] declarations, out contributions, out failureReason))
                return false;

            buildResult = CommandDeclarationInputProjector.Build(declarations);
            return true;
        }

        static bool TryBuildPayloadSchemaLookup(
            IReadOnlyList<CommandPayloadSchemaAsset> payloadSchemas,
            out Dictionary<int, PayloadSchemaRecord> schemasByCommandId,
            out string failureReason)
        {
            schemasByCommandId = new Dictionary<int, PayloadSchemaRecord>();
            failureReason = string.Empty;

            if (payloadSchemas == null)
            {
                failureReason = "Command catalog declaration bridge requires a payload schema collection.";
                return false;
            }

            for (int index = 0; index < payloadSchemas.Count; index++)
            {
                CommandPayloadSchemaAsset schema = payloadSchemas[index];
                if (schema == null)
                {
                    failureReason = "Command catalog declaration bridge encountered a null payload schema at index " + index + ".";
                    return false;
                }

                if (schema.CommandId <= 0)
                {
                    failureReason = "Command catalog declaration bridge requires payload schema CommandId values to be positive. Index=" + index + ".";
                    return false;
                }

                if (schema.SchemaId <= 0)
                {
                    failureReason = "Command catalog declaration bridge requires payload schema SchemaId values to be positive. CommandId=" + schema.CommandId + ".";
                    return false;
                }

                if (schemasByCommandId.ContainsKey(schema.CommandId))
                {
                    failureReason = "Command catalog declaration bridge requires payload schema CommandId values to be unique. CommandId=" + schema.CommandId + ".";
                    return false;
                }

                schemasByCommandId.Add(schema.CommandId, new PayloadSchemaRecord(schema, index));
            }

            return true;
        }

        static bool TryCreatePayloadSchemaDeclaration(
            in PayloadSchemaRecord schemaRecord,
            in UnitySourceLocation catalogSource,
            out CommandPayloadSchemaDeclarationInput declaration,
            out string failureReason)
        {
            failureReason = string.Empty;

            IReadOnlyList<CommandPayloadFieldSchemaAsset> fields = schemaRecord.Asset.Fields;
            CommandPayloadFieldDeclarationInput[] fieldDeclarations = new CommandPayloadFieldDeclarationInput[fields.Count];
            for (int index = 0; index < fields.Count; index++)
            {
                CommandPayloadFieldSchemaAsset field = fields[index];
                if (field == null)
                {
                    declaration = default;
                    failureReason = "Command catalog declaration bridge encountered a null payload field at payloadSchemas[" + schemaRecord.Index + "].fields[" + index + "].";
                    return false;
                }

                try
                {
                    fieldDeclarations[index] = new CommandPayloadFieldDeclarationInput(
                        field.FieldPath,
                        ToFieldKind(field.Kind),
                        ToFieldRequirement(field.Requirement),
                        UnityAuthoringBridge.ToKernelSourceLocation(CreatePropertySource(catalogSource, "payloadSchemas[" + schemaRecord.Index + "].fields[" + index + "]")),
                        ToReferenceKind(field.ReferenceKind),
                        field.AllowNull);
                }
                catch (Exception exception)
                {
                    declaration = default;
                    failureReason = "Command catalog declaration bridge failed to convert payload field schema for CommandId=" + schemaRecord.Asset.CommandId + ": " + exception.Message;
                    return false;
                }
            }

            declaration = new CommandPayloadSchemaDeclarationInput(
                new CommandPayloadSchemaId(schemaRecord.Asset.SchemaId),
                UnityAuthoringBridge.ToKernelSourceLocation(CreatePropertySource(catalogSource, "payloadSchemas[" + schemaRecord.Index + "]")),
                ToUnknownFieldPolicy(schemaRecord.Asset.UnknownFieldPolicy),
                fieldDeclarations);
            return true;
        }

        static UnitySourceLocation CreatePropertySource(in UnitySourceLocation catalogSource, string propertyPath)
        {
            string? combinedPropertyPath = string.IsNullOrEmpty(catalogSource.PropertyPath)
                ? propertyPath
                : catalogSource.PropertyPath + "." + propertyPath;

            return new UnitySourceLocation(
                catalogSource.Kind,
                catalogSource.AssetGuid,
                catalogSource.AssetPath,
                catalogSource.LocalFileId,
                catalogSource.ScenePath,
                catalogSource.GameObjectPath,
                catalogSource.ComponentType,
                combinedPropertyPath);
        }

        static bool TryRegisterDeterministicId(
            IDictionary<int, string> tokensById,
            int id,
            string token,
            string label,
            out string failureReason)
        {
            if (tokensById.TryGetValue(id, out string existingToken))
            {
                if (!string.Equals(existingToken, token, StringComparison.Ordinal))
                {
                    failureReason = "Command catalog declaration bridge detected a deterministic " + label + " identity collision between '" + existingToken + "' and '" + token + "'.";
                    return false;
                }

                failureReason = string.Empty;
                return true;
            }

            tokensById.Add(id, token);
            failureReason = string.Empty;
            return true;
        }

        static int ComputeDeterministicId(string scope, string token)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < scope.Length; index++)
                {
                    hash ^= scope[index];
                    hash *= 16777619u;
                }

                hash ^= (uint)':';
                hash *= 16777619u;

                for (int index = 0; index < token.Length; index++)
                {
                    hash ^= token[index];
                    hash *= 16777619u;
                }

                int value = (int)(hash & 0x7fffffff);
                return value == 0 ? 1 : value;
            }
        }

        static ContributionSource ToContributionSource(UnityAuthoringSourceKind kind)
        {
            return kind switch
            {
                UnityAuthoringSourceKind.SceneObject => ContributionSource.SceneObject,
                UnityAuthoringSourceKind.PrefabAsset => ContributionSource.PrefabAsset,
                UnityAuthoringSourceKind.PrefabInstance => ContributionSource.PrefabInstance,
                UnityAuthoringSourceKind.PrefabVariant => ContributionSource.PrefabVariant,
                UnityAuthoringSourceKind.ScriptableObjectAsset => ContributionSource.ScriptableObjectAsset,
                UnityAuthoringSourceKind.GeneratedAsset => ContributionSource.GeneratedAsset,
                UnityAuthoringSourceKind.CodeDefinedModule => ContributionSource.CodeDefinedModule,
                UnityAuthoringSourceKind.LegacyBridge => ContributionSource.LegacyBridge,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Unity authoring source kind for command catalog declaration bridge."),
            };
        }

        static CommandPayloadFieldKindIR ToFieldKind(CommandPayloadFieldKind kind)
        {
            return kind switch
            {
                CommandPayloadFieldKind.Bool => CommandPayloadFieldKindIR.Bool,
                CommandPayloadFieldKind.Int => CommandPayloadFieldKindIR.Int,
                CommandPayloadFieldKind.Float => CommandPayloadFieldKindIR.Float,
                CommandPayloadFieldKind.String => CommandPayloadFieldKindIR.String,
                CommandPayloadFieldKind.Object => CommandPayloadFieldKindIR.Object,
                CommandPayloadFieldKind.ValueKeyId => CommandPayloadFieldKindIR.ValueKeyId,
                CommandPayloadFieldKind.RuntimeQueryId => CommandPayloadFieldKindIR.RuntimeQueryId,
                CommandPayloadFieldKind.TargetReference => CommandPayloadFieldKindIR.TargetReference,
                CommandPayloadFieldKind.CommandList => CommandPayloadFieldKindIR.CommandList,
                CommandPayloadFieldKind.VarStorePayload => CommandPayloadFieldKindIR.VarStorePayload,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported command payload field kind."),
            };
        }

        static CommandPayloadFieldRequirementIR ToFieldRequirement(CommandPayloadFieldRequirement requirement)
        {
            return requirement switch
            {
                CommandPayloadFieldRequirement.Optional => CommandPayloadFieldRequirementIR.Optional,
                CommandPayloadFieldRequirement.Required => CommandPayloadFieldRequirementIR.Required,
                _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unsupported command payload field requirement."),
            };
        }

        static CommandPayloadReferenceKindIR ToReferenceKind(CommandPayloadReferenceKind referenceKind)
        {
            return referenceKind switch
            {
                CommandPayloadReferenceKind.None => CommandPayloadReferenceKindIR.None,
                CommandPayloadReferenceKind.ValueKeyId => CommandPayloadReferenceKindIR.ValueKeyId,
                CommandPayloadReferenceKind.RuntimeQueryId => CommandPayloadReferenceKindIR.RuntimeQueryId,
                CommandPayloadReferenceKind.TargetReference => CommandPayloadReferenceKindIR.TargetReference,
                _ => throw new ArgumentOutOfRangeException(nameof(referenceKind), referenceKind, "Unsupported command payload reference kind."),
            };
        }

        static CommandPayloadUnknownFieldPolicyIR ToUnknownFieldPolicy(CommandPayloadUnknownFieldPolicy policy)
        {
            return policy switch
            {
                CommandPayloadUnknownFieldPolicy.Reject => CommandPayloadUnknownFieldPolicyIR.Reject,
                CommandPayloadUnknownFieldPolicy.Ignore => CommandPayloadUnknownFieldPolicyIR.Ignore,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported command payload unknown-field policy."),
            };
        }
    }
}
