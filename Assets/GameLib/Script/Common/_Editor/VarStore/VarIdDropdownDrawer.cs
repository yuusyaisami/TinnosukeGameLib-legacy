#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Common;
using Game.Vars.Generated;
using UnityEditor;
using UnityEngine;

namespace Game.Common.Editor
{
    [CustomPropertyDrawer(typeof(VarIdDropdownAttribute))]
    public sealed class VarIdDropdownDrawer : PropertyDrawer
    {
        static readonly IReadOnlyList<VarIdDropdownEntry> Entries = VarIdDropdownLookup.Entries;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var rect = EditorGUI.PrefixLabel(position, label);
            if (rect.height < EditorGUIUtility.singleLineHeight)
            {
                rect.height = EditorGUIUtility.singleLineHeight;
            }

            var display = FormatLabel(GetPathForValue(property.intValue));
            if (GUI.Button(rect, display, EditorStyles.popup))
            {
                ShowDropdown(rect, property);
            }

            EditorGUI.EndProperty();
        }

        void ShowDropdown(Rect rect, SerializedProperty property)
        {
            var attr = (VarIdDropdownAttribute)attribute;
            var menu = new GenericMenu();
            if (attr.AllowNone)
            {
                menu.AddItem(new GUIContent("<None>"), property.intValue == 0, () =>
                {
                    property.intValue = 0;
                    property.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator(string.Empty);
            }

            var filter = NormalizeFilter(attr.Filter);
            foreach (var entry in Entries)
            {
                if (!MatchesFilter(entry.MenuPath, filter))
                    continue;

                var isOn = property.intValue == entry.VarId;
                var path = entry.MenuPath;
                menu.AddItem(new GUIContent(path), isOn, () =>
                {
                    property.intValue = entry.VarId;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.DropDown(rect);
        }

        static string FormatLabel(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return "<None>";
            return path.Replace("/", " › ");
        }

        static string? GetPathForValue(int varId)
        {
            foreach (var entry in Entries)
            {
                if (entry.VarId == varId)
                    return entry.MenuPath;
            }
            return null;
        }

        static string NormalizeFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return string.Empty;
            var normalized = filter.Replace('\\', '/').Replace('.', '/').Trim('/');
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;
            return normalized;
        }

        static bool MatchesFilter(string path, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return true;
            if (path.Length < filter.Length)
                return false;
            if (!path.StartsWith(filter, StringComparison.Ordinal))
                return false;
            if (path.Length == filter.Length)
                return true;
            return path[filter.Length] == '/';
        }
    }

    readonly struct VarIdDropdownEntry
    {
        public string MenuPath { get; }
        public int VarId { get; }
        public VarIdDropdownEntry(string menuPath, int varId)
        {
            MenuPath = menuPath;
            VarId = varId;
        }
    }

    static class VarIdDropdownLookup
    {
        static readonly List<VarIdDropdownEntry> s_entries = BuildEntries();

        public static IReadOnlyList<VarIdDropdownEntry> Entries => s_entries;

        static List<VarIdDropdownEntry> BuildEntries()
        {
            var entries = new List<VarIdDropdownEntry>();
            var root = typeof(VarIds);
            foreach (var nested in root.GetNestedTypes(BindingFlags.Public))
            {
                AddType(nested, nested.Name, entries);
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));
            return entries;
        }

        static void AddType(Type type, string path, List<VarIdDropdownEntry> entries)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (!field.IsLiteral || field.FieldType != typeof(int))
                    continue;

                var value = field.GetRawConstantValue();
                if (value is int varId)
                {
                    entries.Add(new VarIdDropdownEntry(path + "/" + field.Name, varId));
                }
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                AddType(nested, path + "/" + nested.Name, entries);
            }
        }
    }
}
#endif