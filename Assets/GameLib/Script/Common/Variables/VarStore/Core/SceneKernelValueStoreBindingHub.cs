#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.IR;
using Game.Kernel.Value;
namespace Game.Common
{
    static class SceneKernelValueStoreBindingHub
    {
        sealed class SceneBindingState
        {
            public readonly Dictionary<EntityRef, IValueStore> StoresByEntityRef = new Dictionary<EntityRef, IValueStore>();
            public readonly Dictionary<int, ISceneKernelValueInitHost> ValueInitHostsByRuntimeInstanceId = new Dictionary<int, ISceneKernelValueInitHost>();
        }

        static readonly Dictionary<int, SceneBindingState> StatesBySceneHandle = new Dictionary<int, SceneBindingState>();

        public static bool TryRegister(UnityEngine.SceneManagement.Scene scene, EntityRef entityRef, IValueStore valueStore, out string failureReason)
        {
            if (!TryGetOrCreateState(scene, out SceneBindingState state, out failureReason))
                return false;

            if (entityRef.IsEmpty)
            {
                failureReason = "SceneKernel value-store binding requires a non-empty EntityRef.";
                return false;
            }

            if (valueStore == null)
            {
                failureReason = "SceneKernel value-store binding requires a non-null IValueStore.";
                return false;
            }

            if (state.StoresByEntityRef.TryGetValue(entityRef, out IValueStore? existingStore) && !ReferenceEquals(existingStore, valueStore))
            {
                failureReason = "SceneKernel value-store binding already has a different store bound for EntityRef '" + entityRef.Value + "'.";
                return false;
            }

            state.StoresByEntityRef[entityRef] = valueStore;
            failureReason = string.Empty;
            return true;
        }

        public static bool TryRegisterValueInitHost(UnityEngine.SceneManagement.Scene scene, int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost, out string failureReason)
        {
            if (!TryGetOrCreateState(scene, out SceneBindingState state, out failureReason))
                return false;

            if (scopeRuntimeInstanceId <= 0)
            {
                failureReason = "SceneKernel value-init binding requires a positive runtime instance id.";
                return false;
            }

            if (valueInitHost == null)
            {
                failureReason = "SceneKernel value-init binding requires a non-null host.";
                return false;
            }

            if (state.ValueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? existingHost) && !ReferenceEquals(existingHost, valueInitHost))
            {
                failureReason = "SceneKernel value-init binding already has a different host bound for runtime instance id '" + scopeRuntimeInstanceId + "'.";
                return false;
            }

            state.ValueInitHostsByRuntimeInstanceId[scopeRuntimeInstanceId] = valueInitHost;
            failureReason = string.Empty;
            return true;
        }

        public static bool TryUnregister(UnityEngine.SceneManagement.Scene scene, EntityRef entityRef, IValueStore valueStore)
        {
            if (!TryGetExistingState(scene, out SceneBindingState state))
                return false;

            if (!state.StoresByEntityRef.TryGetValue(entityRef, out IValueStore? existingStore) || !ReferenceEquals(existingStore, valueStore))
                return false;

            state.StoresByEntityRef.Remove(entityRef);
            CleanupState(scene, state);
            return true;
        }

        public static bool TryUnregisterValueInitHost(UnityEngine.SceneManagement.Scene scene, int scopeRuntimeInstanceId, ISceneKernelValueInitHost valueInitHost)
        {
            if (!TryGetExistingState(scene, out SceneBindingState state))
                return false;

            if (!state.ValueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? existingHost) || !ReferenceEquals(existingHost, valueInitHost))
                return false;

            state.ValueInitHostsByRuntimeInstanceId.Remove(scopeRuntimeInstanceId);
            CleanupState(scene, state);
            return true;
        }

        public static bool TryGetValueStore(UnityEngine.SceneManagement.Scene scene, EntityRef entityRef, out IValueStore valueStore)
        {
            if (TryGetExistingState(scene, out SceneBindingState state) && state.StoresByEntityRef.TryGetValue(entityRef, out IValueStore? resolvedStore))
            {
                valueStore = resolvedStore;
                return true;
            }

            valueStore = null!;
            return false;
        }

        public static bool TryDispatchValueInit(UnityEngine.SceneManagement.Scene scene, int scopeRuntimeInstanceId, string targetStoreRef, LifecyclePhase phase, out string failureReason)
        {
            if (!TryGetExistingState(scene, out SceneBindingState state))
            {
                failureReason = "SceneKernel value-init binding requires a registered scene state.";
                return false;
            }

            if (!state.ValueInitHostsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out ISceneKernelValueInitHost? valueInitHost) || valueInitHost == null)
            {
                failureReason = "SceneKernel value-init binding could not resolve a host for runtime instance id '" + scopeRuntimeInstanceId + "'.";
                return false;
            }

            return valueInitHost.TryDispatchValueInit(targetStoreRef, phase, out failureReason);
        }

        static bool TryGetOrCreateState(UnityEngine.SceneManagement.Scene scene, out SceneBindingState state, out string failureReason)
        {
            state = null!;
            if (!scene.IsValid())
            {
                failureReason = "SceneKernel value-store binding requires a valid scene.";
                return false;
            }

            int sceneHandle = scene.handle;
            if (sceneHandle == 0)
            {
                failureReason = "SceneKernel value-store binding requires a scene with a non-zero handle.";
                return false;
            }

            if (!StatesBySceneHandle.TryGetValue(sceneHandle, out state!))
            {
                state = new SceneBindingState();
                StatesBySceneHandle.Add(sceneHandle, state);
            }

            failureReason = string.Empty;
            return true;
        }

        static bool TryGetExistingState(UnityEngine.SceneManagement.Scene scene, out SceneBindingState state)
        {
            state = null!;
            return scene.IsValid() && scene.handle != 0 && StatesBySceneHandle.TryGetValue(scene.handle, out state!);
        }

        static void CleanupState(UnityEngine.SceneManagement.Scene scene, SceneBindingState state)
        {
            if (state.StoresByEntityRef.Count == 0 && state.ValueInitHostsByRuntimeInstanceId.Count == 0 && scene.IsValid() && scene.handle != 0)
                StatesBySceneHandle.Remove(scene.handle);
        }
    }
}