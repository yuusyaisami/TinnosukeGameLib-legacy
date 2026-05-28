#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Game.Kernel.Generation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Commands.VNext
{
    public interface ICommandExecutorCatalog
    {
        bool TryGet(int commandId, out ICommandExecutor executor);
    }

    public sealed class CommandExecutorCatalog : ICommandExecutorCatalog
    {
        readonly Dictionary<int, ICommandExecutor> _map = new();
        readonly Dictionary<int, ICommandExecutor>? _verifiedMap;

        public CommandExecutorCatalog(IReadOnlyList<ICommandExecutor> executors)
            : this(executors, null)
        {
        }

        public CommandExecutorCatalog(IReadOnlyList<ICommandExecutor> executors, CommandExecutorTablePlan? verifiedTable)
        {
            if (executors == null)
                throw new ArgumentNullException(nameof(executors));

            var errors = new StringBuilder();
            Dictionary<string, ICommandExecutor> executorsByBindingToken = new Dictionary<string, ICommandExecutor>(executors.Count, StringComparer.Ordinal);
            for (int i = 0; i < executors.Count; i++)
            {
                var executor = executors[i];
                if (executor == null)
                {
                    errors.AppendLine($"[{i}] Executor entry is null.");
                    continue;
                }

                var commandId = executor.CommandId;
                if (commandId <= 0)
                {
                    errors.AppendLine($"[{i}] Invalid CommandId on executor {executor.GetType().Name}: {commandId}");
                    continue;
                }

                string bindingToken = CreateBindingToken(executor.GetType());
                if (executorsByBindingToken.TryGetValue(bindingToken, out var existingBinding))
                {
                    errors.AppendLine($"[{i}] Duplicate binding token {bindingToken} on executor {executor.GetType().Name} (existing: {existingBinding.GetType().Name}).");
                    continue;
                }

                if (_map.TryGetValue(commandId, out var existing))
                {
                    string existingType = existing == null ? "<null>" : existing.GetType().Name;
                    errors.AppendLine($"[{i}] Duplicate CommandId {commandId} on executor {executor.GetType().Name} (existing: {existingType}).");
                    continue;
                }

                _map.Add(commandId, executor);
                executorsByBindingToken.Add(bindingToken, executor);
            }

            if (verifiedTable != null)
                _verifiedMap = BuildVerifiedMap(verifiedTable, executorsByBindingToken, errors);

            if (errors.Length > 0)
            {
                string message = "Command executor catalog binding is invalid and cannot be used:\n" + errors.ToString().TrimEnd();
                Debug.LogError($"[CommandExecutorCatalog] {message}");
                throw new ArgumentException(message, nameof(executors));
            }
        }

        public bool TryGet(int commandId, out ICommandExecutor executor)
        {
            if (_verifiedMap != null)
                return _verifiedMap.TryGetValue(commandId, out executor!);

            return _map.TryGetValue(commandId, out executor!);
        }

        static Dictionary<int, ICommandExecutor> BuildVerifiedMap(
            CommandExecutorTablePlan verifiedTable,
            Dictionary<string, ICommandExecutor> executorsByBindingToken,
            StringBuilder errors)
        {
            Dictionary<int, ICommandExecutor> verifiedMap = new Dictionary<int, ICommandExecutor>(verifiedTable.Entries.Length);
            ReadOnlySpan<CommandExecutorEntryPlan> entries = verifiedTable.Entries;

            for (int index = 0; index < entries.Length; index++)
            {
                CommandExecutorEntryPlan entry = entries[index];
                if (!executorsByBindingToken.TryGetValue(entry.BindingToken, out ICommandExecutor executor))
                {
                    errors.AppendLine($"[verified:{index}] Missing runtime executor for binding token {entry.BindingToken} (CommandExecutorId={entry.ExecutorId.Value}).");
                    continue;
                }

                if (executor.CommandId != entry.ExecutorId.Value)
                {
                    errors.AppendLine($"[verified:{index}] Runtime executor {executor.GetType().Name} resolved from binding token {entry.BindingToken} reports CommandId={executor.CommandId}, expected CommandExecutorId={entry.ExecutorId.Value}.");
                    continue;
                }

                if (!verifiedMap.TryAdd(entry.ExecutorId.Value, executor))
                {
                    errors.AppendLine($"[verified:{index}] Duplicate verified CommandExecutorId {entry.ExecutorId.Value} in CommandExecutorTablePlan.");
                }
            }

            return verifiedMap;
        }

        static string CreateBindingToken(Type implementationType)
        {
            string assemblyName = implementationType.Assembly.GetName().Name ?? string.Empty;
            string typeName = implementationType.FullName ?? implementationType.Name;
            return assemblyName.Length == 0
                ? typeName
                : assemblyName + "::" + typeName;
        }
    }

    public static class CommandExecutorCatalogFactory
    {
        public static ICommandExecutorCatalog Create(global::Game.IRuntimeResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            IReadOnlyList<ICommandExecutor> executors = resolver.TryResolve<IReadOnlyList<ICommandExecutor>>(out IReadOnlyList<ICommandExecutor>? resolvedExecutors)
                && resolvedExecutors != null
                ? resolvedExecutors
                : Array.Empty<ICommandExecutor>();

            CommandExecutorTablePlan? verifiedTable = null;
            if (resolver.TryResolve<global::Game.IScopeNode>(out global::Game.IScopeNode? ownerScope) && ownerScope != null)
            {
                bool hostDiscovered = false;
                if (CommandExecutorTableRuntimeBridge.TryGetForScope(ownerScope, out verifiedTable, out hostDiscovered))
                    return new CommandExecutorCatalog(executors, verifiedTable);

                if (hostDiscovered)
                    throw new InvalidOperationException("Command executor catalog requires a verified CommandExecutorTablePlan when a scene kernel host is present.");
            }

            return new CommandExecutorCatalog(executors, verifiedTable);
        }
    }

    static class CommandExecutorTableRuntimeBridge
    {
        static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public;
        static Type? s_sceneKernelHostType;
        static PropertyInfo? s_runtimeKernelProperty;
        static PropertyInfo? s_compositionProperty;
        static PropertyInfo? s_hasRuntimeBindingProperty;
        static PropertyInfo? s_runtimeSurfaceProperty;
        static PropertyInfo? s_commandExecutorTablePlanProperty;
        static bool s_bridgeInitialized;

        public static bool TryGetForScope(global::Game.IScopeNode ownerScope, out CommandExecutorTablePlan? verifiedTable, out bool hostDiscovered)
        {
            if (ownerScope == null)
                throw new ArgumentNullException(nameof(ownerScope));

            hostDiscovered = false;
            UnityEngine.SceneManagement.Scene scopeScene = default;

            if (TryGetScopeScene(ownerScope, out scopeScene)
                && TryFindSceneKernelHost(scopeScene, out Component? sceneKernelHost))
            {
                hostDiscovered = true;
                if (TryGetCommandExecutorTable(sceneKernelHost, out verifiedTable))
                    return true;
            }

            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid()
                && activeScene.isLoaded
                && (!scopeScene.IsValid() || activeScene.handle != scopeScene.handle)
                && TryFindSceneKernelHost(activeScene, out sceneKernelHost))
            {
                hostDiscovered = true;
                if (TryGetCommandExecutorTable(sceneKernelHost, out verifiedTable))
                    return true;
            }

            verifiedTable = null;
            return false;
        }

        static bool TryGetScopeScene(global::Game.IScopeNode ownerScope, out UnityEngine.SceneManagement.Scene scene)
        {
            Transform? transform = ownerScope.Identity?.SelfTransform;
            if (transform == null && ownerScope is Component component)
                transform = component.transform;

            if (transform != null)
            {
                scene = transform.gameObject.scene;
                return scene.IsValid() && scene.isLoaded;
            }

            scene = default;
            return false;
        }

        static bool TryFindSceneKernelHost(UnityEngine.SceneManagement.Scene scene, out Component? sceneKernelHost)
        {
            if (!TryInitializeBridge())
            {
                sceneKernelHost = null;
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                sceneKernelHost = null;
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            Component? resolvedHost = null;
            for (int index = 0; index < roots.Length; index++)
            {
                Component? candidate = roots[index].GetComponent(s_sceneKernelHostType!);
                if (candidate == null)
                    continue;

                if (resolvedHost != null && resolvedHost != candidate)
                    throw new InvalidOperationException("Only one root SceneKernelHostMB may exist per loaded scene.");

                resolvedHost = candidate;
            }

            sceneKernelHost = resolvedHost;
            return sceneKernelHost != null;
        }

        static bool TryGetCommandExecutorTable(Component sceneKernelHost, out CommandExecutorTablePlan? verifiedTable)
        {
            if (!TryInitializeBridge())
            {
                verifiedTable = null;
                return false;
            }

            object? runtimeKernel = s_runtimeKernelProperty!.GetValue(sceneKernelHost);
            object? composition = runtimeKernel == null ? null : s_compositionProperty!.GetValue(runtimeKernel);
            if (composition == null)
            {
                verifiedTable = null;
                return false;
            }

            if (s_hasRuntimeBindingProperty!.GetValue(composition) is not bool hasRuntimeBinding || !hasRuntimeBinding)
            {
                verifiedTable = null;
                return false;
            }

            object? runtimeSurface = s_runtimeSurfaceProperty!.GetValue(composition);
            verifiedTable = runtimeSurface == null
                ? null
                : s_commandExecutorTablePlanProperty!.GetValue(runtimeSurface) as CommandExecutorTablePlan;

            return verifiedTable != null;

        }

        static bool TryInitializeBridge()
        {
            if (s_bridgeInitialized)
            {
                return s_sceneKernelHostType != null
                    && s_runtimeKernelProperty != null
                    && s_compositionProperty != null
                    && s_hasRuntimeBindingProperty != null
                    && s_runtimeSurfaceProperty != null
                    && s_commandExecutorTablePlanProperty != null;
            }

            s_bridgeInitialized = true;
            s_sceneKernelHostType = ResolveType("Game.Kernel.Layers.Unity.SceneKernelHostMB");
            s_runtimeKernelProperty = s_sceneKernelHostType?.GetProperty("RuntimeKernel", InstanceFlags);
            Type? runtimeKernelType = s_runtimeKernelProperty?.PropertyType;
            s_compositionProperty = runtimeKernelType?.GetProperty("Composition", InstanceFlags);
            Type? compositionType = s_compositionProperty?.PropertyType;
            s_hasRuntimeBindingProperty = compositionType?.GetProperty("HasRuntimeBinding", InstanceFlags);
            s_runtimeSurfaceProperty = compositionType?.GetProperty("RuntimeSurface", InstanceFlags);
            Type? runtimeSurfaceType = s_runtimeSurfaceProperty?.PropertyType;
            s_commandExecutorTablePlanProperty = runtimeSurfaceType?.GetProperty("CommandExecutorTablePlan", InstanceFlags);

            return s_sceneKernelHostType != null
                && s_runtimeKernelProperty != null
                && s_compositionProperty != null
                && s_hasRuntimeBindingProperty != null
                && s_runtimeSurfaceProperty != null
                && s_commandExecutorTablePlanProperty != null;
        }

        static Type? ResolveType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type? resolvedType = assemblies[index].GetType(fullName, throwOnError: false);
                if (resolvedType != null)
                    return resolvedType;
            }

            return null;
        }
    }

    public sealed class CommandExecutorRegistry
    {
        readonly ICommandExecutorCatalog _catalog;

        public CommandExecutorRegistry(IReadOnlyList<ICommandExecutor> executors)
        {
            _catalog = new CommandExecutorCatalog(executors);
        }

        public CommandExecutorRegistry(IReadOnlyList<ICommandExecutor> executors, CommandExecutorTablePlan? verifiedTable)
        {
            _catalog = new CommandExecutorCatalog(executors, verifiedTable);
        }

        public bool TryGet(int commandId, out ICommandExecutor executor)
        {
            return _catalog.TryGet(commandId, out executor);
        }
    }
}
