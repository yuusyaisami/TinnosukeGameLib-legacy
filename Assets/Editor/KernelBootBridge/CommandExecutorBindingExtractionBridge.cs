#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using UnityEngine;

namespace Game.EditorSupport
{
    public static class CommandExecutorBindingExtractionBridge
    {
        static readonly LifetimeScopeKind[] ExtractionScopeKinds =
        {
            LifetimeScopeKind.Project,
            LifetimeScopeKind.Platform,
            LifetimeScopeKind.Global,
            LifetimeScopeKind.Scene,
            LifetimeScopeKind.Field,
            LifetimeScopeKind.Entity,
            LifetimeScopeKind.UI,
            LifetimeScopeKind.UIElement,
            LifetimeScopeKind.Runtime,
        };

        public static CommandExecutorBindingSeed[] Extract(IReadOnlyList<ScopeAuthoringRoot> roots)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            List<ScopeAuthoringRoot> orderedRoots = new List<ScopeAuthoringRoot>(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                ScopeAuthoringRoot root = roots[index];
                if (root != null)
                    orderedRoots.Add(root);
            }

            orderedRoots.Sort(CompareRoots);

            Dictionary<int, CommandExecutorBindingSeed> bindings = new Dictionary<int, CommandExecutorBindingSeed>();
            for (int rootIndex = 0; rootIndex < orderedRoots.Count; rootIndex++)
            {
                ScopeAuthoringRoot root = orderedRoots[rootIndex];
                MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
                Array.Sort(components, CompareComponents);

                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    MonoBehaviour component = components[componentIndex];
                    if (component is not IFeatureInstaller installer)
                        continue;

                    ExtractInstallerBindings(root, installer, bindings);
                }
            }

            int[] executorIds = new int[bindings.Count];
            bindings.Keys.CopyTo(executorIds, 0);
            Array.Sort(executorIds);

            CommandExecutorBindingSeed[] snapshot = new CommandExecutorBindingSeed[executorIds.Length];
            for (int index = 0; index < executorIds.Length; index++)
                snapshot[index] = bindings[executorIds[index]];

            return snapshot;
        }

        static void ExtractInstallerBindings(ScopeAuthoringRoot root, IFeatureInstaller installer, Dictionary<int, CommandExecutorBindingSeed> bindings)
        {
            Type installerType = installer.GetType();

            for (int scopeIndex = 0; scopeIndex < ExtractionScopeKinds.Length; scopeIndex++)
            {
                LifetimeScopeKind scopeKind = ExtractionScopeKinds[scopeIndex];
                ExtractionScopeNode scope = new ExtractionScopeNode(scopeKind);
                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.SetHostScope(scope);

                installer.InstallFeature(builder, scope);

                IReadOnlyList<RuntimeRegistration> registrations = builder.Registrations;
                for (int registrationIndex = 0; registrationIndex < registrations.Count; registrationIndex++)
                {
                    RuntimeRegistration registration = registrations[registrationIndex];
                    if (!IsExecutorRegistration(registration))
                        continue;

                    int commandId = ResolveCommandId(registration.ImplementationType, installerType, root, scopeKind);
                    CommandExecutorBindingSeed binding = new CommandExecutorBindingSeed(
                        new CommandExecutorId(commandId),
                        CreateBindingToken(registration.ImplementationType),
                        ToBindingKind(registration.Lifetime));

                    if (bindings.TryGetValue(commandId, out CommandExecutorBindingSeed existing))
                    {
                        if (!StringComparer.Ordinal.Equals(existing.BindingToken, binding.BindingToken)
                            || existing.BindingKind != binding.BindingKind)
                        {
                            throw new InvalidOperationException(
                                "Conflicting command executor bindings were extracted for CommandExecutorId=" + commandId
                                + " from installer '" + installerType.FullName + "' under root '" + root.name + "'.");
                        }

                        continue;
                    }

                    bindings.Add(commandId, binding);
                }
            }
        }

        static bool IsExecutorRegistration(RuntimeRegistration registration)
        {
            if (registration == null)
                return false;

            Type implementationType = registration.ImplementationType;
            if (!typeof(ICommandExecutor).IsAssignableFrom(implementationType))
                return false;

            Type[] interfaceTypes = registration.InterfaceTypes;
            for (int index = 0; index < interfaceTypes.Length; index++)
            {
                if (interfaceTypes[index] == typeof(ICommandExecutor))
                    return true;
            }

            return false;
        }

        static int ResolveCommandId(Type implementationType, Type installerType, ScopeAuthoringRoot root, LifetimeScopeKind scopeKind)
        {
            PropertyInfo? commandIdProperty = implementationType.GetProperty(nameof(ICommandExecutor.CommandId), BindingFlags.Instance | BindingFlags.Public);
            if (commandIdProperty == null || commandIdProperty.PropertyType != typeof(int) || !commandIdProperty.CanRead)
            {
                throw new InvalidOperationException(
                    "Command executor extraction requires a readable int CommandId property on '" + implementationType.FullName
                    + "' (installer='" + installerType.FullName + "', root='" + root.name + "', scope='" + scopeKind + "').");
            }

            object instance = CreateIntrospectionInstance(implementationType, installerType, root, scopeKind);

            try
            {
                object? rawValue = commandIdProperty.GetValue(instance);
                if (rawValue is int commandId && commandId > 0)
                    return commandId;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Command executor extraction failed to read CommandId from '" + implementationType.FullName
                    + "' (installer='" + installerType.FullName + "', root='" + root.name + "', scope='" + scopeKind + "').",
                    exception);
            }

            throw new InvalidOperationException(
                "Command executor extraction requires a positive CommandId on '" + implementationType.FullName
                + "' (installer='" + installerType.FullName + "', root='" + root.name + "', scope='" + scopeKind + "').");
        }

        static object CreateIntrospectionInstance(Type implementationType, Type installerType, ScopeAuthoringRoot root, LifetimeScopeKind scopeKind)
        {
            try
            {
                object? instance = Activator.CreateInstance(implementationType);
                if (instance != null)
                    return instance;
            }
            catch
            {
            }

            try
            {
                return FormatterServices.GetUninitializedObject(implementationType);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Command executor extraction could not create an inspection instance for '" + implementationType.FullName
                    + "' (installer='" + installerType.FullName + "', root='" + root.name + "', scope='" + scopeKind + "').",
                    exception);
            }
        }

        static CommandExecutorBindingKind ToBindingKind(RuntimeLifetime lifetime)
        {
            switch (lifetime)
            {
                case RuntimeLifetime.Transient:
                    return CommandExecutorBindingKind.Transient;
                case RuntimeLifetime.Scoped:
                    return CommandExecutorBindingKind.Scoped;
                case RuntimeLifetime.Singleton:
                    return CommandExecutorBindingKind.Singleton;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported runtime lifetime for command executor binding extraction.");
            }
        }

        static string CreateBindingToken(Type implementationType)
        {
            string assemblyName = implementationType.Assembly.GetName().Name ?? string.Empty;
            string typeName = implementationType.FullName ?? implementationType.Name;
            return assemblyName.Length == 0
                ? typeName
                : assemblyName + "::" + typeName;
        }

        static int CompareRoots(ScopeAuthoringRoot? left, ScopeAuthoringRoot? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int comparison = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(GetHierarchyPath(left.transform), GetHierarchyPath(right.transform));
            if (comparison != 0)
                return comparison;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static int CompareComponents(MonoBehaviour? left, MonoBehaviour? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int comparison = StringComparer.Ordinal.Compare(GetHierarchyPath(left.transform), GetHierarchyPath(right.transform));
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.GetType().FullName, right.GetType().FullName);
            if (comparison != 0)
                return comparison;

            return StringComparer.Ordinal.Compare(left.name, right.name);
        }

        static string GetHierarchyPath(Transform? transform)
        {
            if (transform == null)
                return string.Empty;

            Stack<string> segments = new Stack<string>();
            Transform? current = transform;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }

        sealed class ExtractionScopeNode : IScopeNode
        {
            public ExtractionScopeNode(LifetimeScopeKind kind)
            {
                Kind = kind;
            }

            public IScopeNode? Parent => null;

            public ILTSIdentityService? Identity => null;

            public LifetimeScopeKind Kind { get; }

            public IRuntimeResolver? Resolver => null;

            public bool IsVisible => true;

            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                return visible;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                return active;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default)
            {
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }
}