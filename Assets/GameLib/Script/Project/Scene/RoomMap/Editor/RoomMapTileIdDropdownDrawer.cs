#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using Game.RoomMap;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CustomPropertyDrawer(typeof(RoomMapTileIdDropdownAttribute))]
    public sealed class RoomMapTileIdDropdownDrawer : PropertyDrawer
    {
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
                rect.height = EditorGUIUtility.singleLineHeight;

            var registry = RoomMapTileRegistryLocator.GetOrCreate();
            var entries = RoomMapTileDropdownLookup.GetEntries(registry);

            var display = FormatLabel(GetPathForValue(entries, property.intValue));
            if (GUI.Button(rect, display, EditorStyles.popup))
            {
                ShowDropdown(rect, property, entries);
            }

            EditorGUI.EndProperty();
        }

        void ShowDropdown(Rect rect, SerializedProperty property, IReadOnlyList<RoomMapTileDropdownEntry> entries)
        {
            var attr = (RoomMapTileIdDropdownAttribute)attribute;
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
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!MatchesFilter(entry.MenuPath, filter))
                    continue;

                var isOn = property.intValue == entry.TileId;
                var path = entry.MenuPath;
                menu.AddItem(new GUIContent(path), isOn, () =>
                {
                    property.intValue = entry.TileId;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.DropDown(rect);
        }

        static string FormatLabel(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return "<None>";
            return path!.Replace("/", " › ");
        }

        static string? GetPathForValue(IReadOnlyList<RoomMapTileDropdownEntry> entries, int tileId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.TileId == tileId)
                    return entry.MenuPath;
            }
            return null;
        }

        static string NormalizeFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return string.Empty;
            var normalized = filter.Replace('\\', '/').Replace('.', '/').Trim('/');
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

    readonly struct RoomMapTileDropdownEntry
    {
        public string MenuPath { get; }
        public int TileId { get; }

        public RoomMapTileDropdownEntry(string menuPath, int tileId)
        {
            MenuPath = menuPath;
            TileId = tileId;
        }
    }

    static class RoomMapTileDropdownLookup
    {
        static readonly List<RoomMapTileDropdownEntry> s_entries = new();
        static int s_cachedRegistryInstanceId;
        static int s_cachedNodeCount;

        public static IReadOnlyList<RoomMapTileDropdownEntry> GetEntries(RoomMapTileRegistry registry)
        {
            if (registry == null)
                return Array.Empty<RoomMapTileDropdownEntry>();

            var id = registry.GetInstanceID();
            var count = registry.Count;
            if (s_entries.Count == 0 || s_cachedRegistryInstanceId != id || s_cachedNodeCount != count)
            {
                s_cachedRegistryInstanceId = id;
                s_cachedNodeCount = count;
                Rebuild(registry);
            }

            return s_entries;
        }

        static void Rebuild(RoomMapTileRegistry registry)
        {
            s_entries.Clear();
            var nodes = registry.Nodes;
            if (nodes == null)
                return;

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null || n.IsFolder)
                    continue;
                if (n.TileId <= 0)
                    continue;

                var path = registry.GetDisplayPath(n.Id);
                if (string.IsNullOrEmpty(path))
                    continue;

                s_entries.Add(new RoomMapTileDropdownEntry(path, n.TileId));
            }

            s_entries.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));
        }
    }
}
#endif
