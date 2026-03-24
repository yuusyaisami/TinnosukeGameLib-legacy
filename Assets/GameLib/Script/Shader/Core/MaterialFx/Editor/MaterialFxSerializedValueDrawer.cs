#if UNITY_EDITOR
#nullable enable
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Game.Common;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxSerializedValue 用の Odin drawer。
    /// Registry 側に EnumDefinition がある場合は、Float/Int の通常入力ではなく enum popup を優先して描画する。
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    public sealed class MaterialFxSerializedValueDrawer : OdinValueDrawer<MaterialFxSerializedValue>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var enumDefinition = TryResolveEnumDefinition();
            if (enumDefinition != null && enumDefinition.Count > 0 && TryDrawEnumValue(enumDefinition))
                return;

            CallNextDrawer(label);
        }

        bool TryDrawEnumValue(MaterialFxEnumDefinitionSO enumDefinition)
        {
            var value = ValueEntry.SmartValue;
            if (value.Type != ValueKind.Float && value.Type != ValueKind.Int)
                return false;

            var currentValue = value.Type == ValueKind.Float
                ? Mathf.RoundToInt(value.ResolveFloat(null))
                : value.Int;

            var currentIndex = enumDefinition.GetIndexByValue(currentValue);
            if (currentIndex < 0)
                currentIndex = 0;

            var displayName = GetEnumDisplayName(enumDefinition, value.Type);
            var options = enumDefinition.GetDisplayOptions();

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(new GUIContent(displayName), currentIndex, options);
            if (!EditorGUI.EndChangeCheck())
                return true;

            var nextValue = enumDefinition.GetValue(nextIndex);
            if (value.Type == ValueKind.Float)
            {
                value.Float = DynamicValueExtensions.FromLiteral((float)nextValue);
            }
            else
            {
                value.Int = nextValue;
            }

            ValueEntry.SmartValue = value;
            return true;
        }

        static string GetEnumDisplayName(MaterialFxEnumDefinitionSO enumDefinition, ValueKind valueKind)
        {
            if (!string.IsNullOrEmpty(enumDefinition.DisplayName))
                return enumDefinition.DisplayName;

            if (!string.IsNullOrEmpty(enumDefinition.name))
                return enumDefinition.name;

            return valueKind == ValueKind.Int ? "Value (Int)" : "Value (Float)";
        }

        MaterialFxEnumDefinitionSO? TryResolveEnumDefinition()
        {
            var entryProp = FindMaterialFxPresetEntryProperty(Property);
            if (entryProp == null)
                return null;

            var keyProp = FindChild(entryProp, "Key");
            if (keyProp?.ValueEntry == null)
                return null;

            var stableKey = keyProp.ValueEntry.WeakSmartValue as string;
            if (string.IsNullOrEmpty(stableKey))
                return null;

            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            if (registry == null)
                return null;

            foreach (var node in registry.Nodes)
            {
                if (node == null || node.IsFolder)
                    continue;

                if (!string.Equals(node.StableKey, stableKey, StringComparison.Ordinal))
                    continue;

                if (node.EnumDefinition == null || node.EnumDefinition.Count == 0)
                    return null;

                return node.EnumDefinition;
            }

            return null;
        }

        static InspectorProperty? FindMaterialFxPresetEntryProperty(InspectorProperty property)
        {
            for (var current = property; current != null; current = current.Parent)
            {
                if (FindChild(current, "Key") != null)
                    return current;
            }

            return null;
        }

        static InspectorProperty? FindChild(InspectorProperty parent, string name)
        {
            if (parent == null)
                return null;

            for (var i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                if (child != null && child.Name == name)
                    return child;
            }

            return null;
        }
    }
}
#endif
