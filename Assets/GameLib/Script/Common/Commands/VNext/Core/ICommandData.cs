#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Commands.VNext
{
    public interface ICommandData
    {
        int CommandId { get; }
        string DebugData { get; }
    }

    public enum CommandPayloadFieldKind
    {
        Unknown = 0,
        Bool = 10,
        Int = 20,
        Float = 30,
        String = 40,
        Object = 50,
        ValueKeyId = 60,
        RuntimeQueryId = 70,
        TargetReference = 80,
        CommandList = 90,
        VarStorePayload = 100,
    }

    public enum CommandPayloadFieldRequirement
    {
        Optional = 10,
        Required = 20,
    }

    public enum CommandPayloadUnknownFieldPolicy
    {
        Reject = 10,
        Ignore = 20,
    }

    public enum CommandPayloadReferenceKind
    {
        None = 0,
        ValueKeyId = 10,
        RuntimeQueryId = 20,
        TargetReference = 30,
    }

    public readonly struct CommandPayloadFieldValue
    {
        CommandPayloadFieldValue(CommandPayloadFieldKind kind, bool hasValue, int intValue, float floatValue, bool boolValue, string stringValue, object? objectValue)
        {
            Kind = kind;
            HasValue = hasValue;
            IntValue = intValue;
            FloatValue = floatValue;
            BoolValue = boolValue;
            StringValue = stringValue ?? string.Empty;
            ObjectValue = objectValue;
        }

        public CommandPayloadFieldKind Kind { get; }

        public bool HasValue { get; }

        public int IntValue { get; }

        public float FloatValue { get; }

        public bool BoolValue { get; }

        public string StringValue { get; }

        public object? ObjectValue { get; }

        public static CommandPayloadFieldValue Missing() => new(CommandPayloadFieldKind.Unknown, false, 0, 0f, false, string.Empty, null);

        public static CommandPayloadFieldValue FromBool(bool value) => new(CommandPayloadFieldKind.Bool, true, value ? 1 : 0, value ? 1f : 0f, value, string.Empty, null);

        public static CommandPayloadFieldValue FromInt(int value) => new(CommandPayloadFieldKind.Int, true, value, value, value != 0, string.Empty, null);

        public static CommandPayloadFieldValue FromFloat(float value) => new(CommandPayloadFieldKind.Float, true, 0, value, value != 0f, string.Empty, null);

        public static CommandPayloadFieldValue FromString(string value) => new(CommandPayloadFieldKind.String, true, 0, 0f, !string.IsNullOrEmpty(value), value, value);

        public static CommandPayloadFieldValue FromObject(CommandPayloadFieldKind kind, object? value) => new(kind, true, 0, 0f, value != null, string.Empty, value);

        public static CommandPayloadFieldValue FromReference(CommandPayloadFieldKind kind, int id) => new(kind, true, id, id, id != 0, string.Empty, null);
    }

    public interface ICommandPayloadFieldReader
    {
        bool TryReadPayloadField(string fieldPath, out CommandPayloadFieldValue value);
        void CollectPayloadFieldPaths(ICollection<string> fieldPaths);
    }

    public interface ICommandPayloadSchemaCatalog
    {
        bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema);
    }

    public interface ICommandPayloadFieldReaderProvider
    {
        bool TryCreateReader(ICommandData data, out ICommandPayloadFieldReader reader);
    }

    public interface ICommandPayloadReferenceValidator
    {
        bool TryValidateReference(CommandPayloadReferenceKind referenceKind, CommandPayloadFieldValue value, out string message);
    }

    public sealed class MissingCommandPayloadReferenceValidator : ICommandPayloadReferenceValidator
    {
        public static readonly MissingCommandPayloadReferenceValidator Instance = new();

        MissingCommandPayloadReferenceValidator() { }

        public bool TryValidateReference(CommandPayloadReferenceKind referenceKind, CommandPayloadFieldValue value, out string message)
        {
            _ = value;
            if (referenceKind == CommandPayloadReferenceKind.None)
            {
                message = string.Empty;
                return true;
            }

            message = "No verified command payload reference registry is bound for this reference kind.";
            return false;
        }
    }

    public delegate bool CommandPayloadFieldReadHandler<in TCommand>(TCommand data, string fieldPath, out CommandPayloadFieldValue value)
        where TCommand : ICommandData;

    public delegate void CommandPayloadFieldPathCollector<in TCommand>(TCommand data, ICollection<string> fieldPaths)
        where TCommand : ICommandData;

    public sealed class CommandPayloadFieldReaderProvider : ICommandPayloadFieldReaderProvider
    {
        readonly Dictionary<Type, ICommandPayloadFieldAccessor> accessors = new();
        readonly ICommandPayloadFieldReaderProvider fallback;

        public CommandPayloadFieldReaderProvider(ICommandPayloadFieldReaderProvider? fallback = null)
        {
            this.fallback = fallback ?? SelfCommandPayloadFieldReaderProvider.Instance;
        }

        public void Register<TCommand>(
            CommandPayloadFieldReadHandler<TCommand> read,
            CommandPayloadFieldPathCollector<TCommand> collect)
            where TCommand : ICommandData
        {
            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (collect == null)
                throw new ArgumentNullException(nameof(collect));

            Type commandType = typeof(TCommand);
            if (accessors.ContainsKey(commandType))
                throw new ArgumentException("Command payload field reader provider already has an accessor for this command data type.", nameof(TCommand));

            accessors.Add(commandType, new CommandPayloadFieldAccessor<TCommand>(read, collect));
        }

        public bool TryCreateReader(ICommandData data, out ICommandPayloadFieldReader reader)
        {
            if (data == null)
            {
                reader = null!;
                return false;
            }

            Type dataType = data.GetType();
            if (accessors.TryGetValue(dataType, out ICommandPayloadFieldAccessor accessor))
            {
                reader = new AccessorReader(data, accessor);
                return true;
            }

            return fallback.TryCreateReader(data, out reader);
        }

        interface ICommandPayloadFieldAccessor
        {
            bool TryRead(ICommandData data, string fieldPath, out CommandPayloadFieldValue value);
            void Collect(ICommandData data, ICollection<string> fieldPaths);
        }

        sealed class CommandPayloadFieldAccessor<TCommand> : ICommandPayloadFieldAccessor
            where TCommand : ICommandData
        {
            readonly CommandPayloadFieldReadHandler<TCommand> read;
            readonly CommandPayloadFieldPathCollector<TCommand> collect;

            public CommandPayloadFieldAccessor(
                CommandPayloadFieldReadHandler<TCommand> read,
                CommandPayloadFieldPathCollector<TCommand> collect)
            {
                this.read = read;
                this.collect = collect;
            }

            public bool TryRead(ICommandData data, string fieldPath, out CommandPayloadFieldValue value)
            {
                if (data is TCommand typed)
                    return read(typed, fieldPath, out value);

                value = CommandPayloadFieldValue.Missing();
                return false;
            }

            public void Collect(ICommandData data, ICollection<string> fieldPaths)
            {
                if (data is TCommand typed)
                    collect(typed, fieldPaths);
            }
        }

        sealed class AccessorReader : ICommandPayloadFieldReader
        {
            readonly ICommandData data;
            readonly ICommandPayloadFieldAccessor accessor;

            public AccessorReader(ICommandData data, ICommandPayloadFieldAccessor accessor)
            {
                this.data = data;
                this.accessor = accessor;
            }

            public bool TryReadPayloadField(string fieldPath, out CommandPayloadFieldValue value)
            {
                return accessor.TryRead(data, fieldPath, out value);
            }

            public void CollectPayloadFieldPaths(ICollection<string> fieldPaths)
            {
                accessor.Collect(data, fieldPaths);
            }
        }
    }

    public sealed class CommandPayloadReferenceRegistry : ICommandPayloadReferenceValidator
    {
        readonly HashSet<int>? valueKeyIds;
        readonly HashSet<int>? runtimeQueryIds;
        readonly HashSet<int>? targetReferenceIds;

        public CommandPayloadReferenceRegistry(
            IEnumerable<int>? valueKeyIds = null,
            IEnumerable<int>? runtimeQueryIds = null,
            IEnumerable<int>? targetReferenceIds = null)
        {
            this.valueKeyIds = CreateSet(valueKeyIds, nameof(valueKeyIds));
            this.runtimeQueryIds = CreateSet(runtimeQueryIds, nameof(runtimeQueryIds));
            this.targetReferenceIds = CreateSet(targetReferenceIds, nameof(targetReferenceIds));
        }

        public bool TryValidateReference(CommandPayloadReferenceKind referenceKind, CommandPayloadFieldValue value, out string message)
        {
            switch (referenceKind)
            {
                case CommandPayloadReferenceKind.None:
                    message = string.Empty;
                    return true;
                case CommandPayloadReferenceKind.ValueKeyId:
                    return Contains(valueKeyIds, value.IntValue, "ValueKeyId", out message);
                case CommandPayloadReferenceKind.RuntimeQueryId:
                    return Contains(runtimeQueryIds, value.IntValue, "RuntimeQueryId", out message);
                case CommandPayloadReferenceKind.TargetReference:
                    return Contains(targetReferenceIds, value.IntValue, "TargetReference", out message);
                default:
                    message = "Unsupported command payload reference kind.";
                    return false;
            }
        }

        static HashSet<int>? CreateSet(IEnumerable<int>? source, string parameterName)
        {
            if (source == null)
                return null;

            HashSet<int> set = new();
            foreach (int id in source)
            {
                if (id <= 0)
                    throw new ArgumentException("Verified command payload reference registries must contain only positive ids.", parameterName);

                if (!set.Add(id))
                    throw new ArgumentException("Verified command payload reference registries must not contain duplicate ids.", parameterName);
            }

            return set;
        }

        static bool Contains(HashSet<int>? set, int id, string label, out string message)
        {
            if (set == null)
            {
                message = "No verified " + label + " registry is bound.";
                return false;
            }

            if (set.Contains(id))
            {
                message = string.Empty;
                return true;
            }

            message = label + " is not present in the verified registry.";
            return false;
        }
    }

    public sealed class SelfCommandPayloadFieldReaderProvider : ICommandPayloadFieldReaderProvider
    {
        public static readonly SelfCommandPayloadFieldReaderProvider Instance = new();

        SelfCommandPayloadFieldReaderProvider() { }

        public bool TryCreateReader(ICommandData data, out ICommandPayloadFieldReader reader)
        {
            if (data is ICommandPayloadFieldReader payloadFieldReader)
            {
                reader = payloadFieldReader;
                return true;
            }

            reader = null!;
            return false;
        }
    }

    public readonly struct CommandPayloadValidationContext
    {
        public CommandPayloadValidationContext(
            ICommandPayloadSchemaCatalog schemaCatalog,
            ICommandPayloadFieldReaderProvider? fieldReaderProvider = null,
            ICommandPayloadReferenceValidator? referenceValidator = null)
        {
            SchemaCatalog = schemaCatalog;
            FieldReaderProvider = fieldReaderProvider ?? SelfCommandPayloadFieldReaderProvider.Instance;
            ReferenceValidator = referenceValidator;
        }

        public ICommandPayloadSchemaCatalog SchemaCatalog { get; }

        public ICommandPayloadFieldReaderProvider FieldReaderProvider { get; }

        public ICommandPayloadReferenceValidator? ReferenceValidator { get; }
    }

    public sealed class CommandPayloadFieldSchema
    {
        public CommandPayloadFieldSchema(
            string fieldPath,
            CommandPayloadFieldKind kind,
            CommandPayloadFieldRequirement requirement,
            CommandPayloadReferenceKind referenceKind = CommandPayloadReferenceKind.None,
            bool allowNull = false,
            int sourceLocationId = 0)
        {
            if (string.IsNullOrWhiteSpace(fieldPath))
                throw new ArgumentException("Command payload field schemas must provide a field path.", nameof(fieldPath));

            FieldPath = fieldPath.Trim();
            Kind = kind;
            Requirement = requirement;
            ReferenceKind = referenceKind;
            AllowNull = allowNull;
            SourceLocationId = sourceLocationId;
        }

        public string FieldPath { get; }

        public CommandPayloadFieldKind Kind { get; }

        public CommandPayloadFieldRequirement Requirement { get; }

        public CommandPayloadReferenceKind ReferenceKind { get; }

        public bool AllowNull { get; }

        public int SourceLocationId { get; }
    }

    public sealed class CommandPayloadSchema
    {
        readonly CommandPayloadFieldSchema[] fields;

        public CommandPayloadSchema(int commandId, int schemaId, CommandPayloadUnknownFieldPolicy unknownFieldPolicy, IReadOnlyList<CommandPayloadFieldSchema> fields)
        {
            if (commandId <= 0)
                throw new ArgumentException("Command payload schemas must provide a positive command id.", nameof(commandId));

            if (schemaId <= 0)
                throw new ArgumentException("Command payload schemas must provide a positive schema id.", nameof(schemaId));

            CommandId = commandId;
            SchemaId = schemaId;
            UnknownFieldPolicy = unknownFieldPolicy;
            this.fields = CopyFields(fields);
            EnsureUniqueFieldPaths(this.fields);
        }

        public int CommandId { get; }

        public int SchemaId { get; }

        public CommandPayloadUnknownFieldPolicy UnknownFieldPolicy { get; }

        public ReadOnlySpan<CommandPayloadFieldSchema> Fields => fields;

        public static CommandPayloadSchema Empty(int commandId, int schemaId, CommandPayloadUnknownFieldPolicy unknownFieldPolicy = CommandPayloadUnknownFieldPolicy.Reject)
        {
            return new CommandPayloadSchema(commandId, schemaId, unknownFieldPolicy, Array.Empty<CommandPayloadFieldSchema>());
        }

        static CommandPayloadFieldSchema[] CopyFields(IReadOnlyList<CommandPayloadFieldSchema> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var copy = new CommandPayloadFieldSchema[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index] ?? throw new ArgumentException("Command payload field schemas must not contain null entries.", nameof(source));
            }

            Array.Sort(copy, CompareFields);
            return copy;
        }

        static int CompareFields(CommandPayloadFieldSchema left, CommandPayloadFieldSchema right)
        {
            return string.CompareOrdinal(left.FieldPath, right.FieldPath);
        }

        static void EnsureUniqueFieldPaths(CommandPayloadFieldSchema[] fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fields.Length; index++)
            {
                if (!seen.Add(fields[index].FieldPath))
                    throw new ArgumentException("Command payload schemas require unique field paths.", nameof(fields));
            }
        }
    }

    public readonly struct CommandPayloadValidationResult
    {
        CommandPayloadValidationResult(bool isValid, int commandId, int schemaId, string fieldPath, CommandPayloadFieldKind expectedKind, CommandPayloadFieldKind actualKind, string message)
        {
            IsValid = isValid;
            CommandId = commandId;
            SchemaId = schemaId;
            FieldPath = fieldPath ?? string.Empty;
            ExpectedKind = expectedKind;
            ActualKind = actualKind;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }

        public int CommandId { get; }

        public int SchemaId { get; }

        public string FieldPath { get; }

        public CommandPayloadFieldKind ExpectedKind { get; }

        public CommandPayloadFieldKind ActualKind { get; }

        public string Message { get; }

        public static CommandPayloadValidationResult Valid(int commandId, int schemaId)
        {
            return new CommandPayloadValidationResult(true, commandId, schemaId, string.Empty, CommandPayloadFieldKind.Unknown, CommandPayloadFieldKind.Unknown, string.Empty);
        }

        public static CommandPayloadValidationResult Error(int commandId, int schemaId, string fieldPath, CommandPayloadFieldKind expectedKind, CommandPayloadFieldKind actualKind, string message)
        {
            return new CommandPayloadValidationResult(false, commandId, schemaId, fieldPath, expectedKind, actualKind, message);
        }
    }

    public static class CommandPayloadValidator
    {
        public static CommandPayloadValidationResult Validate(ICommandData data, ICommandPayloadSchemaCatalog catalog)
        {
            return Validate(data, new CommandPayloadValidationContext(catalog));
        }

        public static CommandPayloadValidationResult Validate(ICommandData data, CommandPayloadValidationContext context)
        {
            if (data == null)
                return CommandPayloadValidationResult.Error(0, 0, string.Empty, CommandPayloadFieldKind.Object, CommandPayloadFieldKind.Unknown, "Command data is null.");

            int commandId = data.CommandId;
            if (commandId <= 0)
                return CommandPayloadValidationResult.Error(commandId, 0, string.Empty, CommandPayloadFieldKind.Int, CommandPayloadFieldKind.Int, "CommandId is invalid.");

            if (context.SchemaCatalog == null || !context.SchemaCatalog.TryGetPayloadSchema(commandId, out CommandPayloadSchema schema) || schema == null)
                return CommandPayloadValidationResult.Error(commandId, 0, string.Empty, CommandPayloadFieldKind.Object, CommandPayloadFieldKind.Unknown, "Command payload schema is missing.");

            if (schema.CommandId != commandId)
                return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, string.Empty, CommandPayloadFieldKind.Int, CommandPayloadFieldKind.Int, "Command payload schema is bound to a different command id.");

            ReadOnlySpan<CommandPayloadFieldSchema> schemaFields = schema.Fields;
            if (!context.FieldReaderProvider.TryCreateReader(data, out ICommandPayloadFieldReader reader) || reader == null)
            {
                if (schemaFields.Length == 0)
                    return CommandPayloadValidationResult.Valid(commandId, schema.SchemaId);

                return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, string.Empty, CommandPayloadFieldKind.Object, CommandPayloadFieldKind.Unknown, "Command data does not expose payload fields for validation.");
            }

            for (int index = 0; index < schemaFields.Length; index++)
            {
                CommandPayloadFieldSchema fieldSchema = schemaFields[index];
                if (!reader.TryReadPayloadField(fieldSchema.FieldPath, out CommandPayloadFieldValue value) || !value.HasValue)
                {
                    if (fieldSchema.Requirement == CommandPayloadFieldRequirement.Required)
                        return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, fieldSchema.FieldPath, fieldSchema.Kind, CommandPayloadFieldKind.Unknown, "Required command payload field is missing.");

                    continue;
                }

                if (IsNullValue(value) && !fieldSchema.AllowNull)
                {
                    return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, fieldSchema.FieldPath, fieldSchema.Kind, value.Kind, "Command payload field is null.");
                }

                if (!KindMatches(fieldSchema.Kind, value.Kind))
                {
                    return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, fieldSchema.FieldPath, fieldSchema.Kind, value.Kind, "Command payload field kind does not match the verified schema.");
                }

                if (!ReferenceIsStructurallyValid(fieldSchema.ReferenceKind, value))
                {
                    return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, fieldSchema.FieldPath, fieldSchema.Kind, value.Kind, "Command payload reference is invalid.");
                }

                if (fieldSchema.ReferenceKind != CommandPayloadReferenceKind.None
                    && context.ReferenceValidator != null
                    && !context.ReferenceValidator.TryValidateReference(fieldSchema.ReferenceKind, value, out string referenceMessage))
                {
                    if (string.IsNullOrEmpty(referenceMessage))
                        referenceMessage = "Command payload reference is not present in the verified runtime registry.";

                    return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, fieldSchema.FieldPath, fieldSchema.Kind, value.Kind, referenceMessage);
                }
            }

            if (schema.UnknownFieldPolicy == CommandPayloadUnknownFieldPolicy.Reject)
            {
                var declaredFields = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < schemaFields.Length; index++)
                {
                    declaredFields.Add(schemaFields[index].FieldPath);
                }

                var actualFields = new List<string>();
                reader.CollectPayloadFieldPaths(actualFields);
                for (int index = 0; index < actualFields.Count; index++)
                {
                    string actualField = actualFields[index] ?? string.Empty;
                    if (actualField.Length == 0)
                        continue;

                    if (!declaredFields.Contains(actualField))
                        return CommandPayloadValidationResult.Error(commandId, schema.SchemaId, actualField, CommandPayloadFieldKind.Unknown, CommandPayloadFieldKind.Unknown, "Command payload contains an unknown field.");
                }
            }

            return CommandPayloadValidationResult.Valid(commandId, schema.SchemaId);
        }

        static bool KindMatches(CommandPayloadFieldKind expected, CommandPayloadFieldKind actual)
        {
            return expected == CommandPayloadFieldKind.Unknown || expected == actual;
        }

        static bool IsNullValue(CommandPayloadFieldValue value)
        {
            switch (value.Kind)
            {
                case CommandPayloadFieldKind.Object:
                case CommandPayloadFieldKind.TargetReference:
                case CommandPayloadFieldKind.CommandList:
                case CommandPayloadFieldKind.VarStorePayload:
                    return value.ObjectValue == null;
                case CommandPayloadFieldKind.String:
                    return value.StringValue == null;
                default:
                    return false;
            }
        }

        static bool ReferenceIsStructurallyValid(CommandPayloadReferenceKind referenceKind, CommandPayloadFieldValue value)
        {
            switch (referenceKind)
            {
                case CommandPayloadReferenceKind.None:
                    return true;
                case CommandPayloadReferenceKind.ValueKeyId:
                case CommandPayloadReferenceKind.RuntimeQueryId:
                    return value.IntValue > 0;
                case CommandPayloadReferenceKind.TargetReference:
                    return value.ObjectValue != null || value.IntValue > 0;
                default:
                    return false;
            }
        }
    }

    public interface ICommandRuntimeStateFactory
    {
        object CreateState();
    }
}
