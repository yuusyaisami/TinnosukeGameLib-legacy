#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using UnityEngine;
using CommonValueKind = Game.Common.ValueKind;
using KernelValueKind = Game.Kernel.IR.ValueKind;

namespace Game.Project.Bootstrap
{
    public static class KernelVerifiedValueRuntime
    {
        const string LocalBlackboardStoreRef = "local:blackboard";

        static KernelBootRuntimeSurface? s_runtimeSurface;
        static KernelVerifiedValueRuntimeSession? s_session;

        public static bool IsActive => s_session != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_runtimeSurface = null;
            s_session = null;
            VerifiedValueRuntimeBridge.Deactivate();
        }

        public static void Activate(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface is not KernelBootRuntimeSurface kernelRuntimeSurface)
                throw new InvalidOperationException("Wave D verified value authority requires a KernelBootRuntimeSurface.");

            s_runtimeSurface = kernelRuntimeSurface;
            s_session = new KernelVerifiedValueRuntimeSession(kernelRuntimeSurface.Runtime);
            VerifiedValueRuntimeBridge.Activate(s_session);
        }

        public static void Deactivate()
        {
            s_session = null;
            s_runtimeSurface = null;
            VerifiedValueRuntimeBridge.Deactivate();
        }

        public static bool TryGetRuntimeSurface(out KernelBootRuntimeSurface? runtimeSurface)
        {
            runtimeSurface = s_runtimeSurface;
            return runtimeSurface != null;
        }

        sealed class KernelVerifiedValueRuntimeSession : IVerifiedValueRuntimeSession
        {
            readonly KernelRuntime runtime;
            readonly Dictionary<string, int> keyIdByStableKey;
            readonly Dictionary<int, string> stableKeyByKeyId;

            public KernelVerifiedValueRuntimeSession(KernelRuntime runtime)
            {
                this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

                ValueSchemaPlan valueSchemaPlan = runtime.ValueSchemaPlan
                    ?? throw new InvalidOperationException("Wave D accepted path requires a verified ValueSchemaPlan.");

                keyIdByStableKey = new Dictionary<string, int>(StringComparer.Ordinal);
                stableKeyByKeyId = new Dictionary<int, string>();
                BuildKeyIndices(valueSchemaPlan, keyIdByStableKey, stableKeyByKeyId);
            }

            public bool TryResolveValueKey(string stableKey, out int valueKeyId)
            {
                if (string.IsNullOrWhiteSpace(stableKey))
                {
                    valueKeyId = 0;
                    return false;
                }

                return keyIdByStableKey.TryGetValue(stableKey, out valueKeyId);
            }

            public bool TryGetStableKey(int valueKeyId, out string stableKey)
            {
                if (valueKeyId <= 0)
                {
                    stableKey = string.Empty;
                    return false;
                }

                return stableKeyByKeyId.TryGetValue(valueKeyId, out stableKey!);
            }

            public VerifiedValueInitApplyResult ApplyLocalBlackboardInit(IScopeNode scope, IBlackboardService blackboard, VerifiedValueInitPhase phase, DynamicEvaluationRuntime evaluationRuntime)
            {
                if (scope == null)
                    throw new ArgumentNullException(nameof(scope));

                if (blackboard == null)
                    throw new ArgumentNullException(nameof(blackboard));

                _ = evaluationRuntime ?? throw new ArgumentNullException(nameof(evaluationRuntime));

                if (!KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(scope, out ScopeHandle handle))
                    return VerifiedValueInitApplyResult.NotAvailable();

                if (!runtime.RootScopeGraph.TryGetScopeValueInitPlans(handle, out IReadOnlyList<ValueInitPlanIR> valueInitPlans) || valueInitPlans.Count == 0)
                    return VerifiedValueInitApplyResult.NotAvailable();

                IVarStore vars = blackboard.LocalVars;
                if (vars == null)
                    return VerifiedValueInitApplyResult.Rejected("Wave D verified value authority requires a non-null local value store.");

                int appliedEntryCount = 0;
                bool matchedPlan = false;
                LifecyclePhase requiredPhase = MapPhase(phase);

                for (int planIndex = 0; planIndex < valueInitPlans.Count; planIndex++)
                {
                    ValueInitPlanIR valueInitPlan = valueInitPlans[planIndex];
                    if (valueInitPlan.ExecutionPhase != requiredPhase)
                        continue;

                    if (!StringComparer.Ordinal.Equals(valueInitPlan.TargetStoreRef, LocalBlackboardStoreRef))
                        continue;

                    matchedPlan = true;
                    ReadOnlySpan<ValueInitEntryIR> entries = valueInitPlan.Entries;
                    for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                    {
                        ValueInitEntryIR entry = entries[entryIndex];
                        if (!TryApplyEntry(vars, entry, out string? failureReason))
                        {
                            return VerifiedValueInitApplyResult.Rejected(
                                failureReason ?? ("Wave D verified value init rejected entry KeyId=" + entry.KeyId.Value + "."));
                        }

                        appliedEntryCount++;
                    }
                }

                return matchedPlan
                    ? VerifiedValueInitApplyResult.Applied(appliedEntryCount)
                    : VerifiedValueInitApplyResult.NotAvailable();
            }

            static void BuildKeyIndices(
                ValueSchemaPlan plan,
                Dictionary<string, int> keyIdByStableKey,
                Dictionary<int, string> stableKeyByKeyId)
            {
                ReadOnlySpan<ValueKeyIR> valueKeys = plan.ValueKeys;
                for (int index = 0; index < valueKeys.Length; index++)
                {
                    ValueKeyIR valueKey = valueKeys[index];
                    string stableKey = valueKey.StableKey;
                    int valueKeyId = valueKey.Id.Value;
                    if (valueKeyId <= 0)
                        throw new InvalidOperationException("Verified value runtime requires positive ValueKeyId values.");

                    if (!keyIdByStableKey.TryAdd(stableKey, valueKeyId))
                        throw new InvalidOperationException("Verified value runtime requires unique stable keys in ValueSchemaPlan.");

                    if (!stableKeyByKeyId.TryAdd(valueKeyId, stableKey))
                        throw new InvalidOperationException("Verified value runtime requires unique ValueKeyId values in ValueSchemaPlan.");
                }
            }

            static LifecyclePhase MapPhase(VerifiedValueInitPhase phase)
            {
                return phase switch
                {
                    VerifiedValueInitPhase.Create => LifecyclePhase.Create,
                    VerifiedValueInitPhase.Acquire => LifecyclePhase.Acquire,
                    VerifiedValueInitPhase.Reset => LifecyclePhase.Reset,
                    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported verified value init phase."),
                };
            }

            static bool TryApplyEntry(IVarStore vars, ValueInitEntryIR entry, out string? failureReason)
            {
                failureReason = null;
                int valueKeyId = entry.KeyId.Value;
                bool exists = vars.Contains(valueKeyId);

                if (!TryResolveEntryValue(entry, out DynamicVariant value, out failureReason))
                    return false;

                switch (entry.OverwritePolicy)
                {
                    case ValueInitOverwritePolicy.ErrorIfExists:
                        if (exists)
                        {
                            failureReason = "Wave D verified value init encountered ErrorIfExists for an already initialized key: " + valueKeyId + ".";
                            return false;
                        }

                        return TryWriteValue(vars, valueKeyId, in value, out failureReason);

                    case ValueInitOverwritePolicy.KeepExisting:
                        if (exists)
                            return true;

                        return TryWriteValue(vars, valueKeyId, in value, out failureReason);

                    case ValueInitOverwritePolicy.Overwrite:
                        if (exists)
                            vars.TryUnset(valueKeyId);

                        return TryWriteValue(vars, valueKeyId, in value, out failureReason);

                    case ValueInitOverwritePolicy.ClearIfNull:
                        if (value.Kind == CommonValueKind.Null)
                        {
                            if (exists)
                                vars.TryUnset(valueKeyId);

                            return true;
                        }

                        if (exists)
                            vars.TryUnset(valueKeyId);

                        return TryWriteValue(vars, valueKeyId, in value, out failureReason);

                    case ValueInitOverwritePolicy.Merge:
                        if (!exists)
                            return TryWriteValue(vars, valueKeyId, in value, out failureReason);

                        failureReason = "Wave D verified value init does not allow implicit merge semantics for key " + valueKeyId + " without an explicit merged value-store adapter.";
                        return false;

                    default:
                        failureReason = "Wave D verified value init encountered an unsupported overwrite policy: " + entry.OverwritePolicy + ".";
                        return false;
                }
            }

            static bool TryResolveEntryValue(ValueInitEntryIR entry, out DynamicVariant value, out string? failureReason)
            {
                failureReason = null;
                value = DynamicVariant.Null;

                switch (entry.SourceKind)
                {
                    case ValueInitEntrySourceKind.Literal:
                        if (entry.ValueKind == KernelValueKind.Null)
                        {
                            value = DynamicVariant.Null;
                            return true;
                        }

                        if (entry.SerializedValue == null)
                        {
                            failureReason = "Wave D verified literal init requires a serialized value for key " + entry.KeyId.Value + ".";
                            return false;
                        }

                        DynamicVariant serializedValue = DynamicVariant.FromString(entry.SerializedValue);
                        if (!TryMapValueKind(entry.ValueKind, out CommonValueKind expectedKind))
                        {
                            failureReason = "Wave D verified literal init does not support IR value kind '" + entry.ValueKind + "' for key " + entry.KeyId.Value + ".";
                            return false;
                        }

                        if (VarStore.TryCoerceVariant(expectedKind, in serializedValue, out DynamicVariant coerced, logOnFailure: false))
                        {
                            value = coerced;
                            return true;
                        }

                        failureReason = "Wave D verified literal init could not coerce serialized value '" + entry.SerializedValue + "' to " + entry.ValueKind + " for key " + entry.KeyId.Value + ".";
                        return false;

                    case ValueInitEntrySourceKind.DynamicEvaluation:
                    case ValueInitEntrySourceKind.ReactiveEvaluation:
                        failureReason = "Wave D verified value init requires explicit evaluation-plan runtime ownership before applying " + entry.SourceKind + " entries. LocalRef='" + (entry.EvaluationLocalRef ?? string.Empty) + "'.";
                        return false;

                    default:
                        failureReason = "Wave D verified value init encountered an unsupported source kind: " + entry.SourceKind + ".";
                        return false;
                }
            }

            static bool TryWriteValue(IVarStore vars, int valueKeyId, in DynamicVariant value, out string? failureReason)
            {
                failureReason = null;

                if (value.Kind == CommonValueKind.Null)
                {
                    vars.TryUnset(valueKeyId);
                    return true;
                }

                if (value.Kind == CommonValueKind.ManagedRef)
                {
                    if (value.AsManagedRef == null)
                        return true;

                    if (vars.TrySetManagedRef(valueKeyId, value.AsManagedRef))
                        return true;

                    failureReason = "Wave D verified value init could not write managed reference payload for key " + valueKeyId + ".";
                    return false;
                }

                if (vars.TrySetVariant(valueKeyId, value))
                    return true;

                failureReason = "Wave D verified value init could not write variant payload for key " + valueKeyId + ".";
                return false;
            }

            static bool TryMapValueKind(KernelValueKind sourceKind, out CommonValueKind targetKind)
            {
                switch (sourceKind)
                {
                    case KernelValueKind.Null:
                        targetKind = CommonValueKind.Null;
                        return true;
                    case KernelValueKind.Bool:
                        targetKind = CommonValueKind.Bool;
                        return true;
                    case KernelValueKind.Int:
                        targetKind = CommonValueKind.Int;
                        return true;
                    case KernelValueKind.Float:
                        targetKind = CommonValueKind.Float;
                        return true;
                    case KernelValueKind.String:
                        targetKind = CommonValueKind.String;
                        return true;
                    case KernelValueKind.Vector2:
                        targetKind = CommonValueKind.Vector2;
                        return true;
                    case KernelValueKind.Vector3:
                        targetKind = CommonValueKind.Vector3;
                        return true;
                    case KernelValueKind.Color:
                        targetKind = CommonValueKind.Color;
                        return true;
                    case KernelValueKind.ObjectRef:
                        targetKind = CommonValueKind.UnityObject;
                        return true;
                    case KernelValueKind.ManagedRef:
                        targetKind = CommonValueKind.ManagedRef;
                        return true;
                    default:
                        targetKind = CommonValueKind.Null;
                        return false;
                }
            }
        }
    }
}