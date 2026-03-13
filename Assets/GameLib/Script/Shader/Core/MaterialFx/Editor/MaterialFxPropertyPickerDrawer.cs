#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// [MaterialFxPropertyPicker] 属性付き string フィールド用 Drawer。
    /// ドロップダウンで StableKey を選択できる。ボタンで Explorer も開ける。
    /// </summary>
    [CustomPropertyDrawer(typeof(MaterialFxPropertyPickerAttribute))]
    public sealed class MaterialFxPropertyPickerDrawer : PropertyDrawer
    {
        const float ExplorerButtonWidth = 22f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // Layout: [Label] [Dropdown][Explorer Button]
            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;

            var buttonRect = position;
            buttonRect.x = position.xMax - ExplorerButtonWidth;
            buttonRect.width = ExplorerButtonWidth;

            var dropdownRect = position;
            dropdownRect.x = labelRect.xMax;
            dropdownRect.width = position.width - labelRect.width - ExplorerButtonWidth - 2f;

            EditorGUI.LabelField(labelRect, label);

            // Current value display
            string currentKey = property.stringValue ?? string.Empty;
            string displayText = string.IsNullOrEmpty(currentKey) ? "<None>" : currentKey.Replace("/", " › ");

            if (GUI.Button(dropdownRect, displayText, EditorStyles.popup))
            {
                ShowDropdownMenu(dropdownRect, property, currentKey);
            }

            // Explorer button
            if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("d_FilterByLabel"), EditorStyles.iconButton))
            {
                MaterialFxPropertyExplorerWindow.Open(property);
            }

            EditorGUI.EndProperty();
        }

        void ShowDropdownMenu(Rect rect, SerializedProperty property, string currentKey)
        {
            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            if (registry == null)
            {
                Debug.LogWarning("[MaterialFxPropertyPicker] Registry not found.");
                return;
            }

            var tree = MaterialFxPropertyTree.Build(registry);
            var menu = new GenericMenu();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            // None option
            menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(currentKey), () =>
            {
                property.stringValue = string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator(string.Empty);

            // Build menu from tree
            AddMenuItemsRecursive(menu, tree, property, currentKey, visited);

            menu.DropDown(rect);
        }

        void AddMenuItemsRecursive(
            GenericMenu menu,
            MaterialFxPropertyTree.Node node,
            SerializedProperty property,
            string currentKey,
            HashSet<string> visited)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsFolder && !string.IsNullOrEmpty(child.StableKey))
                {
                    // Leaf: add menu item
                    var menuPath = child.FullPath; // "Flash/Amount" becomes submenu
                    if (visited.Add(menuPath))
                    {
                        var isOn = string.Equals(currentKey, child.StableKey, StringComparison.Ordinal);
                        var stableKey = child.StableKey;
                        menu.AddItem(new GUIContent(menuPath), isOn, () =>
                        {
                            property.stringValue = stableKey;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }

                // Recurse into children
                AddMenuItemsRecursive(menu, child, property, currentKey, visited);
            }
        }
    }
}
#endif
