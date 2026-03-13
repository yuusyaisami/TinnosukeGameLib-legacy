#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.ActionBlock.Keys;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public sealed class ActionBlockKeyOdinDrawer : OdinAttributeDrawer<ActionBlockKeyDropdownAttribute, string>
{
    static readonly List<(string menuPath, string key)> Entries = new();
    static bool _built;

    protected override void DrawPropertyLayout(GUIContent label)
    {
        EnsureEntries();

        var current = ValueEntry.SmartValue as string ?? string.Empty;

        EditorGUILayout.BeginHorizontal();
        {
            if (label != null && !string.IsNullOrEmpty(label.text))
                GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

            var displayText = string.IsNullOrEmpty(current) ? "<None>" : current.Replace(".", " › ");
            if (GUILayout.Button(displayText, EditorStyles.popup, GUILayout.MinWidth(100f)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(current), () => { ValueEntry.SmartValue = string.Empty; });
                menu.AddSeparator(string.Empty);

                for (int i = 0; i < Entries.Count; i++)
                {
                    var entry = Entries[i];
                    var isOn = string.Equals(current, entry.key, StringComparison.Ordinal);
                    var capturedKey = entry.key;
                    menu.AddItem(new GUIContent(entry.menuPath), isOn, () => { ValueEntry.SmartValue = capturedKey; });
                }

                menu.ShowAsContext();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    static void EnsureEntries()
    {
        if (_built)
            return;

        _built = true;
        Entries.Clear();

        CollectFromType(typeof(ActionBlockKeys));
        Entries.Sort((a, b) => string.Compare(a.menuPath, b.menuPath, StringComparison.Ordinal));
    }

    static void CollectFromType(Type type)
    {
        if (type == null)
            return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var fields = type.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field.FieldType != typeof(string))
                continue;

            if (!field.IsLiteral || field.IsInitOnly)
                continue;

            var value = field.GetRawConstantValue() as string;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var menuPath = value.Replace(".", "/");
            Entries.Add((menuPath, value));
        }

        var nestedTypes = type.GetNestedTypes(flags);
        for (int i = 0; i < nestedTypes.Length; i++)
        {
            CollectFromType(nestedTypes[i]);
        }
    }
}
#endif
