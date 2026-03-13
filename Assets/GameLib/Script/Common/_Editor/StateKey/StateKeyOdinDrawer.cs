#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;
using Game.StateMachine.Editor;
using Game.StateMachine;

public sealed class StateKeyOdinDrawer : Sirenix.OdinInspector.Editor.OdinAttributeDrawer<StateKeyPickerAttribute, string>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var registry = StateKeyRegistryLocator.GetOrCreate();
        if (registry == null)
        {
            CallNextDrawer(label);
            return;
        }

        var current = ValueEntry.SmartValue as string ?? string.Empty;
        var entries = new List<(string menuPath, string key)>();
        foreach (var node in registry.Nodes)
        {
            if (node == null || node.IsFolder) continue;
            var key = registry.GetKeyString(node);
            if (string.IsNullOrEmpty(key)) continue;
            var menuPath = registry.GetDisplayPath(node.Id);
            entries.Add((menuPath, key));
        }
        entries.Sort((a, b) => string.Compare(a.menuPath, b.menuPath, StringComparison.Ordinal));

        EditorGUILayout.BeginHorizontal();
        {
            if (label != null && !string.IsNullOrEmpty(label.text))
                GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

            // Dropdown button that opens a hierarchical GenericMenu
            string displayText = string.IsNullOrEmpty(current) ? "<None>" : current.Replace(".", " › ");
            if (GUILayout.Button(displayText, EditorStyles.popup, GUILayout.MinWidth(100f)))
            {
                var menu = new GenericMenu();
                // None
                menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(current), () => { ValueEntry.SmartValue = string.Empty; });
                menu.AddSeparator(string.Empty);

                // Add all leaf items (with nested menu paths). GenericMenu will create sub-menus for us.
                foreach (var (menuPath, key) in entries)
                {
                    if (string.IsNullOrEmpty(menuPath)) continue;
                    var isOn = string.Equals(current, key, StringComparison.Ordinal);
                    var capturedKey = key;
                    menu.AddItem(new GUIContent(menuPath), isOn, () => { ValueEntry.SmartValue = capturedKey; });
                }

                // Use ShowAsContext to open the menu at the cursor; this avoids DropDown placement issues in Odin.
                menu.ShowAsContext();
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FilterByLabel"), GUILayout.Width(22f)))
            {
                StateKeyExplorerWindow.Open((string selected) =>
                {
                    ValueEntry.SmartValue = selected ?? string.Empty;
                });
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
