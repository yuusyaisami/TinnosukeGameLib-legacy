#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

#nullable enable
namespace Game.Commands.VNext.Editor
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class CommandDataTypeSelectorDrawer : OdinValueDrawer<ICommandData>
    {
        static readonly Dictionary<Type, string> _typePathCache = new();
        static Type[]? _cachedTypes;

        static void DrawChildSafely(InspectorProperty child)
        {
            if (child == null)
                return;

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            try
            {
                child.Draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
            }
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var current = ValueEntry.SmartValue;
            var currentType = current?.GetType();
            var typeTitle = currentType != null ? currentType.Name : "<None>";
            var debugData = current?.DebugData;
            var title = string.IsNullOrEmpty(debugData) ? typeTitle : debugData;

            const int MaxTitleChars = 80;
            var displayTitle = title;
            if (displayTitle != null && displayTitle.Length > MaxTitleChars)
                displayTitle = displayTitle.Substring(0, MaxTitleChars - 1) + "…";
            var tooltip = typeTitle;
            if (!string.IsNullOrEmpty(debugData) && !string.Equals(debugData, typeTitle, StringComparison.Ordinal))
                tooltip = $"{typeTitle} | {debugData}";
            var titleContent = GUIHelper.TempContent(displayTitle ?? "<None>", tooltip);

            var expanded = Property.State.Expanded;
            var rowRect = EditorGUILayout.GetControlRect();
            var contentRect = EditorGUI.IndentedRect(rowRect);
            var foldoutRect = new Rect(contentRect.x, contentRect.y, 14f, contentRect.height);
            var fieldRect = new Rect(foldoutRect.xMax + 4f, contentRect.y, Mathf.Max(0f, contentRect.width - (foldoutRect.width + 4f)), contentRect.height);

            expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, false);
            Property.State.Expanded = expanded;
            if (GUI.Button(fieldRect, titleContent, EditorStyles.popup))
            {
                ShowSelector();
            }

            if (current != null && Property.State.Expanded)
            {
                var snapshot = new List<InspectorProperty>(Property.Children.Count);
                for (int i = 0; i < Property.Children.Count; i++)
                    snapshot.Add(Property.Children[i]);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    var child = snapshot[i];
                    if (child == null)
                        continue;
                    if (!child.State.Visible)
                        continue;
                    if (!string.IsNullOrEmpty(child.Name) && child.Name[0] == '$')
                        continue;
                    if (!string.IsNullOrEmpty(child.Name) && child.Name[0] == '#')
                        child.State.Expanded = true;
                    if (child.GetAttribute<InlinePropertyAttribute>() != null && child.Children.Count > 0)
                        child.State.Expanded = true;
                    DrawChildSafely(child);
                }
            }
        }

        void ShowSelector()
        {
            var types = GetCommandDataTypes();
            var selector = new GenericSelector<Type>("Command Data", false, GetPathForType, types);
            selector.FlattenedTree = false;
            selector.SelectionConfirmed += selection =>
            {
                var selected = selection.FirstOrDefault();
                if (selected == null)
                    return;

                if (selected.GetConstructor(Type.EmptyTypes) == null)
                    return;

                ValueEntry.SmartValue = (ICommandData)Activator.CreateInstance(selected);
            };

            selector.ShowInPopup();
        }

        static IEnumerable<Type> GetCommandDataTypes()
        {
            if (_cachedTypes != null)
                return _cachedTypes;

            BuildTypePathCache();

            var types = TypeCache.GetTypesDerivedFrom<ICommandData>();
            var list = new List<Type>();
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                list.Add(t);
            }

            _cachedTypes = list.ToArray();
            return _cachedTypes;
        }

        static string GetPathForType(Type type)
        {
            if (_typePathCache.TryGetValue(type, out var cached))
                return cached;

            var typeName = type.Name;
            var simpleName = typeName;
            if (simpleName.EndsWith("CommandData", StringComparison.Ordinal))
                simpleName = simpleName.Substring(0, simpleName.Length - "CommandData".Length);

            var category = "Other";

            var result = string.IsNullOrEmpty(category) ? simpleName : $"{category}/{simpleName}";
            _typePathCache[type] = result;
            return result;
        }

        static void BuildTypePathCache()
        {
            if (_typePathCache.Count > 0)
                return;

            var roots = new[] { "Assets/GameLib/Script/Common/Commands/VNext/Commands" };
            var nameToType = new Dictionary<string, Type>(StringComparer.Ordinal);
            var commandTypes = TypeCache.GetTypesDerivedFrom<ICommandData>();
            for (int i = 0; i < commandTypes.Count; i++)
            {
                var t = commandTypes[i];
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                nameToType[t.Name] = t;
            }

            var guids = AssetDatabase.FindAssets("t:script", roots);
            var classRegex = new Regex(@"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!File.Exists(path))
                    continue;

                var marker = "/Commands/VNext/Commands/";
                var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;

                var rel = path.Substring(idx + marker.Length);
                var folder = System.IO.Path.GetDirectoryName(rel)?.Replace("\\", "/");
                if (string.IsNullOrEmpty(folder))
                    folder = "Other";

                var text = File.ReadAllText(path);
                var matches = classRegex.Matches(text);
                for (int m = 0; m < matches.Count; m++)
                {
                    var className = matches[m].Groups["name"].Value;
                    if (!nameToType.TryGetValue(className, out var type))
                        continue;
                    if (_typePathCache.ContainsKey(type))
                        continue;

                    var simpleName = className;
                    if (simpleName.EndsWith("CommandData", StringComparison.Ordinal))
                        simpleName = simpleName.Substring(0, simpleName.Length - "CommandData".Length);

                    _typePathCache[type] = string.IsNullOrEmpty(folder)
                        ? simpleName
                        : $"{folder}/{simpleName}";
                }
            }
        }
    }
}
#endif
