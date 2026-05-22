#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Game;
using Game.Commands.VNext;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public static class KernelVerifiedCommandRuntime
    {
        static KernelBootRuntimeSurface? s_runtimeSurface;
        static KernelVerifiedCommandRuntimeSession? s_session;

        public static bool IsActive => s_session != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_runtimeSurface = null;
            s_session = null;
            VerifiedCommandRuntimeBridge.Deactivate();
        }

        public static void Activate(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface is not KernelBootRuntimeSurface kernelRuntimeSurface)
                throw new InvalidOperationException("Wave C verified command authority requires a KernelBootRuntimeSurface.");

            s_runtimeSurface = kernelRuntimeSurface;
            s_session = new KernelVerifiedCommandRuntimeSession(kernelRuntimeSurface.Runtime);
            VerifiedCommandRuntimeBridge.Activate(s_session);
        }

        public static void Deactivate()
        {
            s_session = null;
            s_runtimeSurface = null;
            VerifiedCommandRuntimeBridge.Deactivate();
        }

        public static bool TryGetRuntimeSurface(out KernelBootRuntimeSurface? runtimeSurface)
        {
            runtimeSurface = s_runtimeSurface;
            return runtimeSurface != null;
        }

        sealed class KernelVerifiedCommandRuntimeSession : IVerifiedCommandRuntimeSession
        {
            readonly VerifiedPlanCommandCatalog catalog;
            readonly VerifiedPlanCommandKeyResolver keyResolver;
            readonly KernelVerifiedCommandPayloadReferenceValidator payloadReferenceValidator;
            readonly Dictionary<int, VerifiedPlanCommandEntry> entriesByCommandId;

            public KernelVerifiedCommandRuntimeSession(KernelRuntime runtime)
            {
                if (runtime == null)
                    throw new ArgumentNullException(nameof(runtime));

                CommandCatalogPlan commandCatalogPlan = runtime.CommandCatalogPlan
                    ?? throw new InvalidOperationException("Wave C accepted path requires a verified CommandCatalogPlan.");
                ValueSchemaPlan valueSchemaPlan = runtime.ValueSchemaPlan
                    ?? throw new InvalidOperationException("Wave C accepted path requires a verified ValueSchemaPlan.");
                RuntimeQueryPlan runtimeQueryPlan = runtime.RuntimeQueryPlan
                    ?? throw new InvalidOperationException("Wave C accepted path requires a verified RuntimeQueryPlan.");

                entriesByCommandId = BuildEntriesByCommandId(commandCatalogPlan, out var payloadSchemas, out var keyIdByStableKey, out var stableKeyByKeyId);
                catalog = new VerifiedPlanCommandCatalog(payloadSchemas);
                keyResolver = new VerifiedPlanCommandKeyResolver(keyIdByStableKey, stableKeyByKeyId);
                payloadReferenceValidator = new KernelVerifiedCommandPayloadReferenceValidator(
                    new CommandPayloadReferenceRegistry(
                        CreateValueKeyIds(valueSchemaPlan),
                        CreateRuntimeQueryIds(runtimeQueryPlan)));
            }

            public ICommandCatalog Catalog => catalog;

            public ICommandKeyResolver KeyResolver => keyResolver;

            public ICommandPayloadReferenceValidator PayloadReferenceValidator => payloadReferenceValidator;

            public ICommandExecutorCatalog CreateExecutorCatalog(IRuntimeResolver resolver, IReadOnlyList<ExplicitCommandExecutorBinding> bindings)
            {
                return new VerifiedPlanCommandExecutorCatalog(resolver, bindings, entriesByCommandId);
            }

            static IEnumerable<int>? CreateValueKeyIds(ValueSchemaPlan plan)
            {
                if (plan.ValueKeys.Length == 0)
                    return null;

                int[] ids = new int[plan.ValueKeys.Length];
                for (int index = 0; index < plan.ValueKeys.Length; index++)
                    ids[index] = plan.ValueKeys[index].Id.Value;

                return ids;
            }

            static IEnumerable<int>? CreateRuntimeQueryIds(RuntimeQueryPlan plan)
            {
                if (plan.RuntimeQueries.Length == 0)
                    return null;

                int[] ids = new int[plan.RuntimeQueries.Length];
                for (int index = 0; index < plan.RuntimeQueries.Length; index++)
                    ids[index] = plan.RuntimeQueries[index].Id.Value;

                return ids;
            }

            static Dictionary<int, VerifiedPlanCommandEntry> BuildEntriesByCommandId(
                CommandCatalogPlan plan,
                out Dictionary<int, CommandPayloadSchema> payloadSchemas,
                out Dictionary<string, CommandKeyId> keyIdByStableKey,
                out Dictionary<int, string> stableKeyByKeyId)
            {
                payloadSchemas = new Dictionary<int, CommandPayloadSchema>();
                keyIdByStableKey = new Dictionary<string, CommandKeyId>(StringComparer.Ordinal);
                stableKeyByKeyId = new Dictionary<int, string>();
                Dictionary<int, VerifiedPlanCommandEntry> entries = new Dictionary<int, VerifiedPlanCommandEntry>();
                HashSet<int> seenExecutorIds = new HashSet<int>();

                for (int index = 0; index < plan.Entries.Length; index++)
                {
                    CommandEntryPlan entry = plan.Entries[index];
                    int commandId = entry.TypeId.Value;
                    if (entries.ContainsKey(commandId))
                        throw new InvalidOperationException("Verified command runtime requires unique CommandTypeId values.");

                    int executorId = entry.Executor.Id.Value;
                    if (executorId <= 0)
                        throw new InvalidOperationException("Verified command runtime requires positive CommandExecutorId values.");
                    if (!seenExecutorIds.Add(executorId))
                        throw new InvalidOperationException("Verified command runtime requires unique positive CommandExecutorId values in the accepted path.");

                    string stableKey = entry.AuthoringKey.Value;
                    CommandKeyId keyId = new CommandKeyId(commandId);
                    if (!string.IsNullOrWhiteSpace(stableKey))
                    {
                        if (keyIdByStableKey.ContainsKey(stableKey))
                            throw new InvalidOperationException("Verified command runtime requires unique authoring keys.");

                        keyIdByStableKey.Add(stableKey, keyId);
                        stableKeyByKeyId.Add(keyId.Value, stableKey);
                    }

                    payloadSchemas.Add(commandId, CreatePayloadSchema(entry));
                    entries.Add(commandId, new VerifiedPlanCommandEntry(commandId, executorId, stableKey));
                }

                return entries;
            }

            static CommandPayloadSchema CreatePayloadSchema(CommandEntryPlan entry)
            {
                CommandPayloadFieldSchema[] fields = new CommandPayloadFieldSchema[entry.PayloadSchema.Fields.Length];
                for (int index = 0; index < entry.PayloadSchema.Fields.Length; index++)
                {
                    CommandPayloadFieldPlan field = entry.PayloadSchema.Fields[index];
                    fields[index] = new CommandPayloadFieldSchema(
                        field.FieldPath,
                        ConvertFieldKind(field.Kind),
                        ConvertRequirement(field.Requirement),
                        ConvertReferenceKind(field.ReferenceKind),
                        field.AllowNull,
                        field.Source.Value);
                }

                return new CommandPayloadSchema(
                    entry.TypeId.Value,
                    entry.PayloadSchema.SchemaId.Value,
                    ConvertUnknownFieldPolicy(entry.PayloadSchema.UnknownFieldPolicy),
                    fields);
            }

            static CommandPayloadFieldKind ConvertFieldKind(CommandPayloadFieldKindIR kind)
            {
                return kind switch
                {
                    CommandPayloadFieldKindIR.Unknown => CommandPayloadFieldKind.Unknown,
                    CommandPayloadFieldKindIR.Bool => CommandPayloadFieldKind.Bool,
                    CommandPayloadFieldKindIR.Int => CommandPayloadFieldKind.Int,
                    CommandPayloadFieldKindIR.Float => CommandPayloadFieldKind.Float,
                    CommandPayloadFieldKindIR.String => CommandPayloadFieldKind.String,
                    CommandPayloadFieldKindIR.Object => CommandPayloadFieldKind.Object,
                    CommandPayloadFieldKindIR.ValueKeyId => CommandPayloadFieldKind.ValueKeyId,
                    CommandPayloadFieldKindIR.RuntimeQueryId => CommandPayloadFieldKind.RuntimeQueryId,
                    CommandPayloadFieldKindIR.TargetReference => CommandPayloadFieldKind.TargetReference,
                    CommandPayloadFieldKindIR.CommandList => CommandPayloadFieldKind.CommandList,
                    CommandPayloadFieldKindIR.VarStorePayload => CommandPayloadFieldKind.VarStorePayload,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported verified command payload field kind."),
                };
            }

            static CommandPayloadFieldRequirement ConvertRequirement(CommandPayloadFieldRequirementIR requirement)
            {
                return requirement switch
                {
                    CommandPayloadFieldRequirementIR.Optional => CommandPayloadFieldRequirement.Optional,
                    CommandPayloadFieldRequirementIR.Required => CommandPayloadFieldRequirement.Required,
                    _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unsupported verified command payload field requirement."),
                };
            }

            static CommandPayloadUnknownFieldPolicy ConvertUnknownFieldPolicy(CommandPayloadUnknownFieldPolicyIR policy)
            {
                return policy switch
                {
                    CommandPayloadUnknownFieldPolicyIR.Reject => CommandPayloadUnknownFieldPolicy.Reject,
                    CommandPayloadUnknownFieldPolicyIR.Ignore => CommandPayloadUnknownFieldPolicy.Ignore,
                    _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported verified command payload unknown-field policy."),
                };
            }

            static CommandPayloadReferenceKind ConvertReferenceKind(CommandPayloadReferenceKindIR kind)
            {
                return kind switch
                {
                    CommandPayloadReferenceKindIR.None => CommandPayloadReferenceKind.None,
                    CommandPayloadReferenceKindIR.ValueKeyId => CommandPayloadReferenceKind.ValueKeyId,
                    CommandPayloadReferenceKindIR.RuntimeQueryId => CommandPayloadReferenceKind.RuntimeQueryId,
                    CommandPayloadReferenceKindIR.TargetReference => CommandPayloadReferenceKind.TargetReference,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported verified command payload reference kind."),
                };
            }
        }

        readonly struct VerifiedPlanCommandEntry
        {
            public VerifiedPlanCommandEntry(int commandId, int executorId, string stableKey)
            {
                CommandId = commandId;
                ExecutorId = executorId;
                StableKey = stableKey ?? string.Empty;
            }

            public int CommandId { get; }

            public int ExecutorId { get; }

            public string StableKey { get; }
        }

        sealed class VerifiedPlanCommandCatalog : ICommandCatalog
        {
            readonly Dictionary<int, CommandPayloadSchema> payloadSchemas;

            public VerifiedPlanCommandCatalog(Dictionary<int, CommandPayloadSchema> payloadSchemas)
            {
                this.payloadSchemas = payloadSchemas ?? throw new ArgumentNullException(nameof(payloadSchemas));
            }

            public bool TryResolve(CommandKeyId keyId, out ICommandData data)
            {
                _ = keyId;
                data = null!;
                return false;
            }

            public bool TryResolve(CommandKeyRef key, out ICommandData data)
            {
                _ = key;
                data = null!;
                return false;
            }

            public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
            {
                _ = key;
                meta = null!;
                return false;
            }

            public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
            {
                return payloadSchemas.TryGetValue(commandId, out schema!);
            }
        }

        sealed class VerifiedPlanCommandKeyResolver : ICommandKeyResolver
        {
            readonly Dictionary<string, CommandKeyId> keyIdByStableKey;
            readonly Dictionary<int, string> stableKeyByKeyId;

            public VerifiedPlanCommandKeyResolver(Dictionary<string, CommandKeyId> keyIdByStableKey, Dictionary<int, string> stableKeyByKeyId)
            {
                this.keyIdByStableKey = keyIdByStableKey ?? throw new ArgumentNullException(nameof(keyIdByStableKey));
                this.stableKeyByKeyId = stableKeyByKeyId ?? throw new ArgumentNullException(nameof(stableKeyByKeyId));
            }

            public bool TryResolve(string stableKey, out CommandKeyId keyId)
            {
                if (string.IsNullOrWhiteSpace(stableKey))
                {
                    keyId = default;
                    return false;
                }

                return keyIdByStableKey.TryGetValue(stableKey, out keyId);
            }

            public bool TryGetStableKey(CommandKeyId keyId, out string stableKey)
            {
                return stableKeyByKeyId.TryGetValue(keyId.Value, out stableKey!);
            }
        }

        sealed class KernelVerifiedCommandPayloadReferenceValidator : ICommandPayloadReferenceValidator
        {
            readonly CommandPayloadReferenceRegistry registry;

            public KernelVerifiedCommandPayloadReferenceValidator(CommandPayloadReferenceRegistry registry)
            {
                this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            }

            public bool TryValidateReference(CommandPayloadReferenceKind referenceKind, CommandPayloadFieldValue value, out string message)
            {
                if (referenceKind == CommandPayloadReferenceKind.TargetReference)
                {
                    if (value.ObjectValue != null || value.IntValue > 0)
                    {
                        message = string.Empty;
                        return true;
                    }

                    message = "TargetReference is not structurally valid.";
                    return false;
                }

                return registry.TryValidateReference(referenceKind, value, out message);
            }
        }

        sealed class VerifiedPlanCommandExecutorCatalog : ICommandExecutorCatalog
        {
            readonly IReadOnlyDictionary<int, VerifiedPlanCommandEntry> verifiedEntriesByCommandId;
            readonly Dictionary<int, ICommandExecutor> executorsByExecutorId = new();

            public VerifiedPlanCommandExecutorCatalog(
                IRuntimeResolver resolver,
                IReadOnlyList<ExplicitCommandExecutorBinding> bindings,
                IReadOnlyDictionary<int, VerifiedPlanCommandEntry> verifiedEntries)
            {
                if (resolver == null)
                    throw new ArgumentNullException(nameof(resolver));
                if (bindings == null)
                    throw new ArgumentNullException(nameof(bindings));
                if (verifiedEntries == null)
                    throw new ArgumentNullException(nameof(verifiedEntries));

                verifiedEntriesByCommandId = verifiedEntries;

                StringBuilder errors = new StringBuilder();
                HashSet<Type> seenTypes = new HashSet<Type>();

                for (int index = 0; index < bindings.Count; index++)
                {
                    ExplicitCommandExecutorBinding binding = bindings[index];
                    if (!seenTypes.Add(binding.ExecutorType))
                    {
                        errors.AppendLine($"[{index}] Duplicate explicit executor binding type: {binding.ExecutorType.FullName}.");
                        continue;
                    }

                    object resolved;
                    try
                    {
                        resolved = resolver.Resolve(binding.ExecutorType);
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine($"[{index}] Failed to resolve explicit executor binding {binding.ExecutorType.FullName}: {ex.Message}");
                        continue;
                    }

                    if (resolved is not ICommandExecutor executor)
                    {
                        errors.AppendLine($"[{index}] Explicit executor binding {binding.ExecutorType.FullName} did not resolve an ICommandExecutor instance.");
                        continue;
                    }

                    int commandId = executor.CommandId;
                    if (commandId <= 0)
                    {
                        errors.AppendLine($"[{index}] Executor {executor.GetType().FullName} exposes an invalid CommandId {commandId}.");
                        continue;
                    }

                    if (!verifiedEntries.TryGetValue(commandId, out VerifiedPlanCommandEntry verifiedEntry))
                    {
                        errors.AppendLine($"[{index}] Executor {executor.GetType().FullName} exposes CommandId {commandId}, but no verified CommandCatalogPlan entry exists.");
                        continue;
                    }

                    if (verifiedEntry.ExecutorId <= 0)
                    {
                        errors.AppendLine($"[{index}] Verified CommandCatalogPlan entry for CommandId {commandId} exposes an invalid ExecutorId {verifiedEntry.ExecutorId}.");
                        continue;
                    }

                    if (executorsByExecutorId.TryGetValue(verifiedEntry.ExecutorId, out ICommandExecutor existing))
                    {
                        errors.AppendLine($"[{index}] Duplicate explicit executor binding for verified ExecutorId {verifiedEntry.ExecutorId}. New={executor.GetType().FullName} Existing={existing.GetType().FullName}");
                        continue;
                    }

                    executorsByExecutorId.Add(verifiedEntry.ExecutorId, executor);
                }

                if (errors.Length > 0)
                {
                    throw new InvalidOperationException(
                        "Verified command executor bindings are invalid and the accepted path cannot be built:\n"
                        + errors.ToString().TrimEnd());
                }
            }

            public bool TryGet(int commandId, out ICommandExecutor executor)
            {
                if (!verifiedEntriesByCommandId.TryGetValue(commandId, out VerifiedPlanCommandEntry verifiedEntry))
                {
                    executor = null!;
                    return false;
                }

                return executorsByExecutorId.TryGetValue(verifiedEntry.ExecutorId, out executor!);
            }
        }
    }
}