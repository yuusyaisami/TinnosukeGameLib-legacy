#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.VarStoreKeys;
using UnityEditor;
using UnityEngine;

namespace Game.VarStoreKeys.Editor
{
    /// <summary>
    /// VarKeyRef のカスタムドロワー。
    /// VarKeyRegistry から stable key を選択でき、選択に応じて varId を設定する。
    /// </summary>
    [CustomPropertyDrawer(typeof(Game.Common.VarKeyRef))]
    public class VarKeyRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var stableProp = property.FindPropertyRelative("stableKey");
            var idProp = property.FindPropertyRelative("varId");

            var rect = EditorGUI.PrefixLabel(position, label);
            if (rect.height < EditorGUIUtility.singleLineHeight)
                rect.height = EditorGUIUtility.singleLineHeight;

            var current = stableProp.stringValue ?? string.Empty;
            var display = string.IsNullOrEmpty(current) ? "<None>" : current.Replace(".", " › ");

            if (GUI.Button(rect, display, EditorStyles.popup))
            {
                ShowDropdown(rect, stableProp, idProp, current);
            }

            EditorGUI.EndProperty();
        }

        void ShowDropdown(Rect rect, SerializedProperty stableProp, SerializedProperty idProp, string currentPath)
        {
            var registry = VarKeyRegistryLocator.GetOrCreate();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(currentPath), () =>
            {
                stableProp.stringValue = string.Empty;
                idProp.intValue = 0;
                stableProp.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator(string.Empty);

            AddMenuItemsFromRegistry(menu, registry, stableProp, idProp, currentPath, visited);

            menu.DropDown(rect);
        }

        void AddMenuItemsFromRegistry(
            GenericMenu menu,
            VarKeyRegistry registry,
            SerializedProperty stableProp,
            SerializedProperty idProp,
            string currentPath,
            HashSet<string> visited)
        {
            if (registry == null) return;

            foreach (var node in registry.Nodes)
            {
                if (node == null || node.IsFolder) continue;

                var keyString = registry.GetKeyString(node);
                if (string.IsNullOrEmpty(keyString)) continue;

                // Use registry display path (folder structure) for menu nesting when possible.
                var menuPath = registry.GetDisplayPath(node.Id);
                if (string.IsNullOrEmpty(menuPath))
                    menuPath = keyString.Replace(".", "/");

                if (visited.Add(menuPath))
                {
                    var isOn = string.Equals(currentPath, keyString, StringComparison.Ordinal);
                    var capturedKey = keyString;
                    var capturedId = node.VarId;
                    var label = menuPath;
                    menu.AddItem(new GUIContent(label), isOn, () =>
                    {
                        stableProp.stringValue = capturedKey;
                        idProp.intValue = capturedId;
                        stableProp.serializedObject.ApplyModifiedProperties();
                    });
                }
            }
        }
    }
}
#endif
