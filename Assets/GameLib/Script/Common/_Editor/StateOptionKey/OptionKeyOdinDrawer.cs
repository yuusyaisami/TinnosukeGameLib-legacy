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

public sealed class OptionKeyOdinDrawer : Sirenix.OdinInspector.Editor.OdinAttributeDrawer<OptionKeyPickerAttribute, string>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var registry = OptionRegistryLocator.GetOrCreate();
        if (registry == null)
        {
            CallNextDrawer(label);
            return;
        }

        var current = ValueEntry.SmartValue as string ?? string.Empty;
        var entries = new List<(string menuPath, string key)>();
        foreach (var node in registry.GetAllOptionKeys())
        {
            if (node == null) continue;
            var key = registry.GetKeyString(node);
            if (string.IsNullOrEmpty(key)) continue;
            var menuPath = registry.GetDisplayPath(node.Id);
            entries.Add((menuPath, key));
        }

        entries.Sort((a, b) => string.Compare(a.menuPath, b.menuPath, StringComparison.Ordinal));

        var labels = new GUIContent[entries.Count + 1];
        var keys = new string[entries.Count + 1];
        labels[0] = new GUIContent("<None>"); keys[0] = string.Empty;
        int idx = 1; int currentIdx = 0;
        foreach (var e in entries)
        {
            labels[idx] = new GUIContent(e.menuPath);
            keys[idx] = e.key;
            if (e.key == current) currentIdx = idx;
            idx++;
        }

        EditorGUILayout.BeginHorizontal();
        {
            if (label != null && !string.IsNullOrEmpty(label.text))
                GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

            // Dropdown button to open hierarchical GenericMenu
            string displayText = string.IsNullOrEmpty(current) ? "<None>" : current.Replace(".", " › ");
            if (GUILayout.Button(displayText, EditorStyles.popup, GUILayout.MinWidth(100f)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(current), () => { ValueEntry.SmartValue = string.Empty; });
                menu.AddSeparator(string.Empty);

                // Add the actual OptionKey menu items (folders); GenericMenu will create nested menu
                foreach (var (menuPath, key) in entries)
                {
                    if (string.IsNullOrEmpty(menuPath)) continue;
                    var isOn = string.Equals(current, key, StringComparison.Ordinal);
                    var capturedKey = key;
                    menu.AddItem(new GUIContent(menuPath), isOn, () => { ValueEntry.SmartValue = capturedKey; });
                }

                // Also show OptionValue children under each OptionKey so users can select values.
                // These entries are selectable and will set the field to the full OptionValue key (e.g. Movement.up).
                foreach (var node in registry.GetAllOptionKeys())
                {
                    if (node == null) continue;
                    var keyStr = registry.GetKeyString(node);
                    if (string.IsNullOrEmpty(keyStr)) continue;
                    var menuParent = registry.GetDisplayPath(node.Id);
                    foreach (var valNode in registry.GetOptionValues(node.Id))
                    {
                        if (valNode == null) continue;
                        var valueKey = registry.GetKeyString(valNode);
                        if (string.IsNullOrEmpty(valueKey)) continue;
                        var valueLabel = valNode.Name;
                        var path = string.IsNullOrEmpty(menuParent) ? valueLabel : menuParent + "/" + valueLabel;
                        var isOn = string.Equals(current, valueKey, StringComparison.Ordinal);
                        var capturedValueKey = valueKey;
                        menu.AddItem(new GUIContent(path), isOn, () => { ValueEntry.SmartValue = capturedValueKey; });
                    }
                }

                menu.ShowAsContext();
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FilterByLabel"), GUILayout.Width(22f)))
            {
                OptionExplorerWindow.Open((string selected) =>
                {
                    ValueEntry.SmartValue = selected ?? string.Empty;
                });
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
