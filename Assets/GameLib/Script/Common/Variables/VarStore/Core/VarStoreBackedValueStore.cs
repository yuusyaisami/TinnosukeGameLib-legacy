#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.IR;
using Game.Kernel.Value;
using DynamicValueKind = Game.Common.ValueKind;
using KernelValueKind = Game.Kernel.IR.ValueKind;

namespace Game.Common
{
    interface ISceneKernelValueInitHost
    {
        bool TryDispatchValueInit(string targetStoreRef, LifecyclePhase phase, out string failureReason);
    }

    public sealed class VarStoreBackedValueStore : IValueStore
    {
        readonly IVarStore varStore;
        readonly IReadOnlyDictionary<ValueKeyId, ValueKeyMetadata>? metadataByKeyId;

        public VarStoreBackedValueStore(
            IVarStore varStore,
            ValueStoreScopeKind scopeKind,
            IReadOnlyDictionary<ValueKeyId, ValueKeyMetadata>? metadataByKeyId = null)
        {
            this.varStore = varStore ?? throw new ArgumentNullException(nameof(varStore));
            this.metadataByKeyId = metadataByKeyId;
            ScopeKind = scopeKind;
        }

        public ValueStoreScopeKind ScopeKind { get; }

        public bool TryRead(ValueKeyId keyId, out ValueVariant value)
        {
            if (keyId.Value <= 0)
            {
                value = default;
                return false;
            }

            if (!varStore.TryGetVariant(keyId.Value, out DynamicVariant dynamicValue))
            {
                value = default;
                return false;
            }

            return TryConvertToValueVariant(dynamicValue, out value);
        }

        public uint GetRevision(ValueKeyId keyId)
        {
            if (keyId.Value <= 0)
                return 0u;

            int revision = varStore.GetVarVersion(keyId.Value);
            return revision <= 0 ? 0u : (uint)revision;
        }

        public bool TryGetMetadata(ValueKeyId keyId, out ValueKeyMetadata metadata)
        {
            if (metadataByKeyId != null && metadataByKeyId.TryGetValue(keyId, out metadata))
                return true;

            metadata = default;
            return false;
        }

        public bool TryWrite(ValueKeyId keyId, in ValueVariant value)
        {
            if (keyId.Value <= 0)
                return false;

            if (!TryConvertToDynamicVariant(value, out DynamicVariant dynamicValue))
                return false;

            return varStore.TrySetVariant(keyId.Value, in dynamicValue);
        }

        static bool TryConvertToDynamicVariant(in ValueVariant value, out DynamicVariant dynamicValue)
        {
            switch (value.Kind)
            {
                case KernelValueKind.Null:
                    dynamicValue = DynamicVariant.Null;
                    return true;
                case KernelValueKind.Bool:
                    if (value.TryGetBool(out bool boolValue))
                    {
                        dynamicValue = DynamicVariant.FromBool(boolValue);
                        return true;
                    }
                    break;
                case KernelValueKind.Int:
                    if (value.TryGetInt(out int intValue))
                    {
                        dynamicValue = DynamicVariant.FromInt(intValue);
                        return true;
                    }
                    break;
                case KernelValueKind.Float:
                    if (value.TryGetFloat(out float floatValue))
                    {
                        dynamicValue = DynamicVariant.FromFloat(floatValue);
                        return true;
                    }
                    break;
                case KernelValueKind.String:
                    if (value.TryGetString(out string? stringValue) && stringValue != null)
                    {
                        dynamicValue = DynamicVariant.FromString(stringValue);
                        return true;
                    }
                    break;
            }

            dynamicValue = default;
            return false;
        }

        static bool TryConvertToValueVariant(in DynamicVariant dynamicValue, out ValueVariant value)
        {
            switch (dynamicValue.Kind)
            {
                case DynamicValueKind.Null:
                    value = ValueVariant.Null;
                    return true;
                case DynamicValueKind.Bool:
                    value = ValueVariant.FromBool(dynamicValue.AsBool);
                    return true;
                case DynamicValueKind.Int:
                    value = ValueVariant.FromInt(dynamicValue.AsInt);
                    return true;
                case DynamicValueKind.Float:
                    value = ValueVariant.FromFloat(dynamicValue.AsFloat);
                    return true;
                case DynamicValueKind.String:
                    value = ValueVariant.FromString(dynamicValue.AsString);
                    return true;
                default:
                    value = default;
                    return false;
            }
        }
    }
}