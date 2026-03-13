#nullable enable
using UnityEngine;
using UnityEditor;
using Game.MaterialFx;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxEnumValueAttribute 用の PropertyDrawer。
    /// EnumDefinition が指定されている場合は Dropdown を表示、
    /// そうでない場合は通常の int/float フィールドを表示。
    /// </summary>
    [CustomPropertyDrawer(typeof(MaterialFxEnumValueAttribute))]
    public sealed class MaterialFxEnumValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (MaterialFxEnumValueAttribute)attribute;

            // EnumDefinition を取得
            MaterialFxEnumDefinitionSO? enumDef = null;

            // 静的に指定された名前から取得を試みる
            if (!string.IsNullOrEmpty(attr.EnumDefinitionName))
            {
                enumDef = FindEnumDefinitionByName(attr.EnumDefinitionName);
            }

            // StableKeyField が指定されている場合は Registry から取得を試みる
            if (enumDef == null && !string.IsNullOrEmpty(attr.StableKeyField))
            {
                var stableKeyProp = FindSiblingProperty(property, attr.StableKeyField);
                if (stableKeyProp != null && stableKeyProp.propertyType == SerializedPropertyType.String)
                {
                    var stableKey = stableKeyProp.stringValue;
                    enumDef = GetEnumDefinitionFromRegistry(stableKey);
                }
            }

            EditorGUI.BeginProperty(position, label, property);

            if (enumDef != null && enumDef.Count > 0)
            {
                // EnumDefinition がある場合は Dropdown 表示
                DrawEnumDropdown(position, property, label, enumDef);
            }
            else
            {
                // 通常のフィールド表示
                EditorGUI.PropertyField(position, property, label);
            }

            EditorGUI.EndProperty();
        }

        void DrawEnumDropdown(Rect position, SerializedProperty property, GUIContent label, MaterialFxEnumDefinitionSO enumDef)
        {
            var options = enumDef.GetDisplayOptions();
            int currentValue = 0;

            // int または float から現在値を取得
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                currentValue = property.intValue;
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                currentValue = Mathf.RoundToInt(property.floatValue);
            }

            // 現在の値からドロップダウンのインデックスを取得
            int currentIndex = enumDef.GetIndexByValue(currentValue);
            if (currentIndex == -1) currentIndex = 0;

            // Dropdown 表示
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, options);

            if (EditorGUI.EndChangeCheck())
            {
                int newValue = enumDef.GetValue(newIndex);
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    property.intValue = newValue;
                }
                else if (property.propertyType == SerializedPropertyType.Float)
                {
                    property.floatValue = newValue;
                }
            }

            // Tooltip にエントリの説明を表示
            if (newIndex >= 0 && newIndex < enumDef.Count)
            {
                var desc = enumDef.GetEntryDescription(newIndex);
                if (!string.IsNullOrEmpty(desc))
                {
                    var tooltipRect = position;
                    tooltipRect.x += EditorGUIUtility.labelWidth;
                    tooltipRect.width -= EditorGUIUtility.labelWidth;
                    EditorGUI.LabelField(tooltipRect, new GUIContent("", desc));
                }
            }
        }

        SerializedProperty? FindSiblingProperty(SerializedProperty property, string siblingName)
        {
            var path = property.propertyPath;
            var lastDot = path.LastIndexOf('.');

            string siblingPath;
            if (lastDot >= 0)
            {
                siblingPath = path.Substring(0, lastDot + 1) + siblingName;
            }
            else
            {
                siblingPath = siblingName;
            }

            return property.serializedObject.FindProperty(siblingPath);
        }

        MaterialFxEnumDefinitionSO? FindEnumDefinitionByName(string name)
        {
            // Settings から EnumCatalog を取得して検索
            var settings = MaterialFxPropertyRegistryLocator.GetOrCreateSettings();
            if (settings == null) return null;

            var catalog = settings.EnumCatalog;
            if (catalog == null) return null;

            return catalog.FindByName(name) ?? catalog.FindByDisplayName(name);
        }

        MaterialFxEnumDefinitionSO? GetEnumDefinitionFromRegistry(string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey)) return null;

            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            if (registry == null) return null;

            foreach (var node in registry.Nodes)
            {
                if (node != null && !node.IsFolder && node.StableKey == stableKey)
                {
                    return node.EnumDefinition;
                }
            }

            return null;
        }
    }
}
