#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.StateMachine.Editor;
using Game.StateMachine;

[Obsolete("Use Odin StateKey drawer (StateKeyOdinDrawer) instead; this legacy drawer is disabled.")]
public sealed class StateKeyPickerDrawer : PropertyDrawer
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

        var labelRect = position;
        labelRect.width = EditorGUIUtility.labelWidth;

        var buttonRect = position;
        buttonRect.x = position.xMax - ExplorerButtonWidth;
        buttonRect.width = ExplorerButtonWidth;

        var dropdownRect = position;
        dropdownRect.x = labelRect.xMax;
        dropdownRect.width = position.width - labelRect.width - ExplorerButtonWidth - 2f;

        EditorGUI.LabelField(labelRect, label);

        string currentKey = property.stringValue ?? string.Empty;
        string displayText = string.IsNullOrEmpty(currentKey) ? "<None>" : currentKey.Replace(".", " › ");

        if (GUI.Button(dropdownRect, displayText, EditorStyles.popup))
        {
            ShowDropdownMenu(dropdownRect, property, currentKey);
        }

        if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("d_FilterByLabel"), EditorStyles.iconButton))
        {
            StateKeyExplorerWindow.Open(property);
        }

        EditorGUI.EndProperty();
    }

    void ShowDropdownMenu(Rect rect, SerializedProperty property, string currentKey)
    {
        var registry = StateKeyRegistryLocator.GetOrCreate();
        if (registry == null)
        {
            Debug.LogWarning("[StateKeyPicker] Registry not found.");
            return;
        }

        var menu = new GenericMenu();

        // None
        menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(currentKey), () =>
        {
            property.stringValue = string.Empty;
            property.serializedObject.ApplyModifiedProperties();
        });

        menu.AddSeparator(string.Empty);

        var visited = new HashSet<string>(StringComparer.Ordinal);
        // Build sorted list of leaf nodes by path
        var entries = new List<(string path, string key)>();
        foreach (var node in registry.Nodes)
        {
            if (node == null || node.IsFolder) continue;
            var keyStr = registry.GetKeyString(node);
            if (string.IsNullOrEmpty(keyStr)) continue;
            var menuPath = registry.GetDisplayPath(node.Id);
            entries.Add((menuPath, keyStr));
        }

        entries.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
        foreach (var (menuPath, keyStr) in entries)
        {
            if (visited.Add(menuPath))
            {
                var isOn = string.Equals(currentKey, keyStr, StringComparison.Ordinal);
                var capturedKey = keyStr;
                menu.AddItem(new GUIContent(menuPath), isOn, () =>
                {
                    property.stringValue = capturedKey;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        menu.DropDown(rect);
    }
}
#endif
