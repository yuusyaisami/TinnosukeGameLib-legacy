#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.IR;
using Game.Kernel.Layers;
using Game.Kernel.Layers.Composition;
using Game.Kernel.Layers.Unity;
using Game.Kernel.Value;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    sealed class SceneKernelValueStoreBoundary : ISceneKernelValueStoreBoundary
    {
        readonly Dictionary<EntityRef, IValueStore> storesByEntityRef = new Dictionary<EntityRef, IValueStore>();
        readonly Dictionary<int, ISceneKernelValueInitHost> valueInitHostsByRuntimeInstanceId = new Dictionary<int, ISceneKernelValueInitHost>();

        public int Count => storesByEntityRef.Count;

        public int ValueInitHostCount => valueInitHostsByRuntimeInstanceId.Count;

        public bool TryBind(EntityRef entityRef, IValueStore valueStore)
        {
            if (entityRef.IsEmpty)
                throw new ArgumentException("ValueStore boundaries require a non-empty EntityRef.", nameof(entityRef));

            if (valueStore == null)
                throw new ArgumentNullException(nameof(valueStore));

            if (storesByEntityRef.TryGetValue(entityRef, out IValueStore? existingStore) && !ReferenceEquals(existingStore, valueStore))
                return false;

            storesByEntityRef[entityRef] = valueStore;
            return true;
        }

        public bool TryUnbind(EntityRef entityRef, IValueStore valueStore)
        {
            if (!storesByEntityRef.TryGetValue(entityRef, out IValueStore? existingStore))
                return false;

            if (!ReferenceEquals(existingStore, valueStore))
                return false;

            return storesByEntityRef.Remove(entityRef);
        }

        public bool TryGetValueStore(EntityRef entityRef, out IValueStore valueStore)
        {
            return storesByEntityRef.TryGetValue(entityRef, out valueStore!);
        }

        public bool TryBindValueInitHost(int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost)
        {
            if (scopeRuntimeInstanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(scopeRuntimeInstanceId), scopeRuntimeInstanceId, "Value-init hosts require a positive runtime instance id.");

            if (valueInitHost == null)
                throw new ArgumentNullException(nameof(valueInitHost));

            if (valueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? existingHost) && !ReferenceEquals(existingHost, valueInitHost))
                return false;

            valueInitHostsByRuntimeInstanceId[scopeRuntimeInstanceId] = valueInitHost;
            return true;
        }

        public bool TryUnbindValueInitHost(int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost)
        {
            if (!valueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? existingHost))
                return false;

            if (!ReferenceEquals(existingHost, valueInitHost))
                return false;

            return valueInitHostsByRuntimeInstanceId.Remove(scopeRuntimeInstanceId);
        }

        public bool TryDispatchValueInit(int scopeRuntimeInstanceId, string targetStoreRef, LifecyclePhase phase, out string failureReason)
        {
            if (scopeRuntimeInstanceId <= 0)
            {
                failureReason = "SceneKernel value-init dispatch requires a positive runtime instance id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetStoreRef))
            {
                failureReason = "SceneKernel value-init dispatch requires a non-empty target store ref.";
                return false;
            }

            if (!valueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? valueInitHost))
            {
                failureReason = "SceneKernel value-init boundary could not find a registered host for runtime instance id '" + scopeRuntimeInstanceId + "'.";
                return false;
            }

            return valueInitHost.TryDispatchValueInit(targetStoreRef, phase, out failureReason);
        }
    }

    static class SceneKernelValueStoreBindingHub
    {
        public static bool TryRegister(Scene scene, EntityRef entityRef, IValueStore valueStore, out string failureReason)
        {
            if (!TryGetOrCreateBoundary(scene, out SceneKernelComposition? composition, out SceneKernelValueStoreBoundary? boundary, out failureReason))
                return false;

            if (!boundary.TryBind(entityRef, valueStore))
            {
                failureReason = "SceneKernel value-store boundary already has a different store bound for EntityRef '" + entityRef.Value + "'.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public static bool TryRegisterValueInitHost(Scene scene, int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost, out string failureReason)
        {
            if (!TryGetOrCreateBoundary(scene, out _, out SceneKernelValueStoreBoundary? boundary, out failureReason))
                return false;

            if (!boundary.TryBindValueInitHost(scopeRuntimeInstanceId, valueInitHost))
            {
                failureReason = "SceneKernel value-store boundary already has a different value-init host bound for runtime instance id '" + scopeRuntimeInstanceId + "'.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public static bool TryUnregister(Scene scene, EntityRef entityRef, IValueStore valueStore)
        {
            if (!TryGetExistingBoundary(scene, out SceneKernelComposition? composition, out SceneKernelValueStoreBoundary? boundary))
                return false;

            bool removed = boundary.TryUnbind(entityRef, valueStore);
            if (removed && boundary.Count == 0 && boundary.ValueInitHostCount == 0)
                composition.ClearValueStoreBoundary();

            return removed;
        }

        public static bool TryUnregisterValueInitHost(Scene scene, int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost)
        {
            if (!TryGetExistingBoundary(scene, out SceneKernelComposition? composition, out SceneKernelValueStoreBoundary? boundary))
                return false;

            bool removed = boundary.TryUnbindValueInitHost(scopeRuntimeInstanceId, valueInitHost);
            if (removed && boundary.Count == 0 && boundary.ValueInitHostCount == 0)
                composition.ClearValueStoreBoundary();

            return removed;
        }

        static bool TryGetOrCreateBoundary(
            Scene scene,
            out SceneKernelComposition composition,
            out SceneKernelValueStoreBoundary boundary,
            out string failureReason)
        {
            composition = null!;
            boundary = null!;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                failureReason = "SceneKernel ValueStore binding requires a valid loaded scene.";
                return false;
            }

            SceneKernelHostMB sceneKernelHost = SceneKernelHostMB.EnsureForScene(scene);
            if (sceneKernelHost.RuntimeKernel.Composition is not SceneKernelComposition typedComposition)
            {
                failureReason = "SceneKernel ValueStore binding requires the runtime kernel to own a SceneKernelComposition.";
                return false;
            }

            composition = typedComposition;
            if (composition.ValueStoreBoundary == null)
            {
                boundary = new SceneKernelValueStoreBoundary();
                composition.BindValueStoreBoundary(boundary);
                failureReason = string.Empty;
                return true;
            }

            if (composition.ValueStoreBoundary is SceneKernelValueStoreBoundary typedBoundary)
            {
                boundary = typedBoundary;
                failureReason = string.Empty;
                return true;
            }

            failureReason = "SceneKernel ValueStore binding found an incompatible value-store boundary implementation.";
            return false;
        }

        static bool TryGetExistingBoundary(Scene scene, out SceneKernelComposition composition, out SceneKernelValueStoreBoundary boundary)
        {
            composition = null!;
            boundary = null!;

            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            if (!TryFindSceneKernelHost(scene, out SceneKernelHostMB? sceneKernelHost))
                return false;

            if (sceneKernelHost.RuntimeKernel.Composition is not SceneKernelComposition typedComposition)
                return false;

            if (typedComposition.ValueStoreBoundary is not SceneKernelValueStoreBoundary typedBoundary)
                return false;

            composition = typedComposition;
            boundary = typedBoundary;
            return true;
        }

        static bool TryFindSceneKernelHost(Scene scene, out SceneKernelHostMB sceneKernelHost)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                SceneKernelHostMB? candidate = roots[index].GetComponent<SceneKernelHostMB>();
                if (candidate != null)
                {
                    sceneKernelHost = candidate;
                    return true;
                }
            }

            sceneKernelHost = null!;
            return false;
        }
    }
}