#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace Game.Common.Editor
{
    internal readonly struct ManagedReferenceTypePickerItem
    {
        public ManagedReferenceTypePickerItem(Type type, string label, string menuPath)
        {
            Type = type;
            Label = label;
            MenuPath = menuPath;
        }

        public Type Type { get; }
        public string Label { get; }
        public string MenuPath { get; }
    }

    internal static class ManagedReferenceTypePickerUtility
    {
        static readonly Dictionary<Type, ManagedReferenceTypePickerItem[]> s_cache = new();

        public static IReadOnlyList<ManagedReferenceTypePickerItem> GetTypeItems(Type baseType, Func<Type, bool>? filter = null)
        {
            if (baseType == null)
                return Array.Empty<ManagedReferenceTypePickerItem>();

            if (filter == null && s_cache.TryGetValue(baseType, out var cached))
                return cached;

            var candidates = CollectCandidates(baseType, filter);
            var items = new ManagedReferenceTypePickerItem[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                items[i] = new ManagedReferenceTypePickerItem(
                    candidate,
                    BuildLabel(candidate),
                    BuildMenuPath(candidate));
            }

            Array.Sort(items, static (a, b) => string.Compare(a.MenuPath, b.MenuPath, StringComparison.Ordinal));

            if (filter == null)
                s_cache[baseType] = items;

            return items;
        }

        public static bool TryCreateInstance(Type concreteType, out object? instance)
        {
            instance = null;

            if (!IsPickableType(concreteType, concreteType))
                return false;

            instance = Activator.CreateInstance(concreteType);
            return instance != null;
        }

        static List<Type> CollectCandidates(Type baseType, Func<Type, bool>? filter)
        {
            var result = new List<Type>();
            var seen = new HashSet<Type>();

            void TryAppend(Type candidate)
            {
                if (!IsPickableType(baseType, candidate))
                    return;

                if (filter != null && !filter(candidate))
                    return;

                if (seen.Add(candidate))
                    result.Add(candidate);
            }

            // Include base type itself when it is concrete and constructible.
            TryAppend(baseType);

            var derivedTypes = TypeCache.GetTypesDerivedFrom(baseType);
            for (var i = 0; i < derivedTypes.Count; i++)
            {
                var candidate = derivedTypes[i];
                if (candidate == null)
                    continue;

                TryAppend(candidate);
            }

            return result;
        }

        static bool IsPickableType(Type baseType, Type candidate)
        {
            if (candidate == null)
                return false;

            if (!baseType.IsAssignableFrom(candidate))
                return false;

            if (candidate.IsAbstract || candidate.IsInterface)
                return false;

            if (candidate.IsGenericTypeDefinition || candidate.ContainsGenericParameters)
                return false;

            if (candidate.IsValueType)
                return false;

            return candidate.GetConstructor(Type.EmptyTypes) != null;
        }

        static string BuildLabel(Type type)
        {
            if (type == null)
                return string.Empty;

            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
                name = name.Substring(0, tickIndex);

            return name;
        }

        static string BuildMenuPath(Type type)
        {
            var label = BuildLabel(type);
            var ns = type.Namespace;
            if (string.IsNullOrEmpty(ns))
                return label;

            const string gamePrefix = "Game.";
            if (ns.StartsWith(gamePrefix, StringComparison.Ordinal))
                ns = ns.Substring(gamePrefix.Length);

            return $"{ns.Replace('.', '/')}/{label}";
        }
    }
}
#endif