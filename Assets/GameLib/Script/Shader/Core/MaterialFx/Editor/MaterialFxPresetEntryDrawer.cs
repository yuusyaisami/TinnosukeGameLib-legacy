#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxPresetEntry 用 PropertyDrawer。
    /// Key 選択時に Registry から ValueType を取得して Value.Type を自動設定する。
    /// </summary>
    [CustomPropertyDrawer(typeof(MaterialFxPresetEntry))]
    public sealed class MaterialFxPresetEntryDrawer : PropertyDrawer
    {
        const float ExplorerButtonWidth = 22f;
        const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // If collapsed, only show header line
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var keyProp = property.FindPropertyRelative("Key");
            var valueProp = property.FindPropertyRelative("Value");
            var typeProp = valueProp?.FindPropertyRelative("Type");

            var lifetimeProp = property.FindPropertyRelative("LifetimeSeconds");
            var applyFadeProp = property.FindPropertyRelative("ApplyWeightFade");

            float height = EditorGUIUtility.singleLineHeight; // Header line

            // When expanded, include the detailed rows: Key row + Value row etc.
            // Add Key row height
            height += Spacing + EditorGUIUtility.singleLineHeight;

            if (typeProp != null)
            {
                var valueType = (ValueKind)typeProp.enumValueIndex;
                height += Spacing + GetValueFieldHeight(valueType);
            }

            height += Spacing + EditorGUIUtility.singleLineHeight; // BlendMode

            // Lifetime
            if (lifetimeProp != null)
                height += Spacing + EditorGUIUtility.singleLineHeight;

            // Fade (optional)
            if (applyFadeProp != null)
            {
                height += Spacing + EditorGUIUtility.singleLineHeight; // ApplyWeightFade
                if (applyFadeProp.boolValue)
                {
                    height += Spacing + EditorGUIUtility.singleLineHeight; // TargetWeight
                    height += Spacing + EditorGUIUtility.singleLineHeight; // FadeDuration
                    height += Spacing + EditorGUIUtility.singleLineHeight; // FadeEase
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keyProp = property.FindPropertyRelative("Key");
            var valueProp = property.FindPropertyRelative("Value");
            var blendModeProp = property.FindPropertyRelative("BlendMode");
            var typeProp = valueProp?.FindPropertyRelative("Type");

            var lifetimeProp = property.FindPropertyRelative("LifetimeSeconds");
            var applyFadeProp = property.FindPropertyRelative("ApplyWeightFade");
            var targetWeightProp = property.FindPropertyRelative("TargetWeight");
            var fadeDurationProp = property.FindPropertyRelative("FadeDuration");
            var fadeEaseProp = property.FindPropertyRelative("FadeEase");

            float y = position.y;

            // ---- Header (Foldout) ----
            bool expanded = property.isExpanded;

            // Build display text from Key
            string currentKey = keyProp.stringValue ?? string.Empty;
            string displayText = string.IsNullOrEmpty(currentKey) ? "<None>" : currentKey.Replace("/", " › ");

            // Foldout label only (display Key summary)
            var foldoutRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, expanded, new GUIContent(displayText), true);

            y += EditorGUIUtility.singleLineHeight + Spacing;

            // If collapsed, stop here
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            // ---- Key Row (editable when expanded) ----
            var keyRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            DrawKeyField(keyRect, keyProp, typeProp);
            y += EditorGUIUtility.singleLineHeight + Spacing;

            // ---- Value Row ----
            if (typeProp != null)
            {
                var valueType = (ValueKind)typeProp.enumValueIndex;
                float valueHeight = GetValueFieldHeight(valueType);
                var valueRect = new Rect(position.x, y, position.width, valueHeight);
                DrawValueField(valueRect, valueProp, valueType);
                y += valueHeight + Spacing;
            }

            // ---- BlendMode Row ----
            var blendRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(blendRect, blendModeProp);
            y += EditorGUIUtility.singleLineHeight + Spacing;

            // ---- Lifetime Row ----
            if (lifetimeProp != null)
            {
                var lifeRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(lifeRect, lifetimeProp, new GUIContent("Lifetime Seconds (-1 = Infinite)"));
                y += EditorGUIUtility.singleLineHeight + Spacing;
            }

            // ---- Fade Rows ----
            if (applyFadeProp != null)
            {
                var applyRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(applyRect, applyFadeProp, new GUIContent("Apply Weight Fade"));
                y += EditorGUIUtility.singleLineHeight + Spacing;

                if (applyFadeProp.boolValue)
                {
                    if (targetWeightProp != null)
                    {
                        var wRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(wRect, targetWeightProp, new GUIContent("Target Weight"));
                        y += EditorGUIUtility.singleLineHeight + Spacing;
                    }

                    if (fadeDurationProp != null)
                    {
                        var dRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(dRect, fadeDurationProp, new GUIContent("Fade Duration"));
                        y += EditorGUIUtility.singleLineHeight + Spacing;
                    }

                    if (fadeEaseProp != null)
                    {
                        var eRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(eRect, fadeEaseProp, new GUIContent("Fade Ease"));
                        y += EditorGUIUtility.singleLineHeight + Spacing;
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        void DrawKeyField(Rect position, SerializedProperty keyProp, SerializedProperty typeProp)
        {
            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;

            var buttonRect = position;
            buttonRect.x = position.xMax - ExplorerButtonWidth;
            buttonRect.width = ExplorerButtonWidth;

            var dropdownRect = position;
            dropdownRect.x = labelRect.xMax;
            dropdownRect.width = position.width - labelRect.width - ExplorerButtonWidth - 2f;

            EditorGUI.LabelField(labelRect, "Key");

            string currentKey = keyProp.stringValue ?? string.Empty;
            string displayText = string.IsNullOrEmpty(currentKey) ? "<None>" : currentKey.Replace("/", " › ");

            if (GUI.Button(dropdownRect, displayText, EditorStyles.popup))
            {
                ShowKeyDropdown(dropdownRect, keyProp, typeProp, currentKey);
            }

            if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("d_FilterByLabel"), EditorStyles.iconButton))
            {
                MaterialFxPropertyExplorerWindow.Open(keyProp);
            }
        }

        void ShowKeyDropdown(Rect rect, SerializedProperty keyProp, SerializedProperty typeProp, string currentKey)
        {
            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            if (registry == null)
            {
                Debug.LogWarning("[MaterialFxPresetEntry] Registry not found.");
                return;
            }

            var tree = MaterialFxPropertyTree.Build(registry);
            var menu = new GenericMenu();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(currentKey), () =>
            {
                keyProp.stringValue = string.Empty;
                keyProp.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator(string.Empty);
            AddMenuItemsRecursive(menu, tree, keyProp, typeProp, currentKey, visited);
            menu.DropDown(rect);
        }

        void AddMenuItemsRecursive(
            GenericMenu menu,
            MaterialFxPropertyTree.Node node,
            SerializedProperty keyProp,
            SerializedProperty typeProp,
            string currentKey,
            HashSet<string> visited)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsFolder && !string.IsNullOrEmpty(child.StableKey))
                {
                    var menuPath = child.FullPath;
                    if (visited.Add(menuPath))
                    {
                        var isOn = string.Equals(currentKey, child.StableKey, StringComparison.Ordinal);
                        var stableKey = child.StableKey;
                        var valueType = child.ValueType;

                        menu.AddItem(new GUIContent(menuPath), isOn, () =>
                        {
                            keyProp.stringValue = stableKey;
                            if (typeProp != null)
                            {
                                typeProp.enumValueIndex = (int)valueType;
                            }
                            keyProp.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }

                AddMenuItemsRecursive(menu, child, keyProp, typeProp, currentKey, visited);
            }
        }

        float GetValueFieldHeight(ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => EditorGUIUtility.singleLineHeight,
                ValueKind.Int => EditorGUIUtility.singleLineHeight,
                ValueKind.Bool => EditorGUIUtility.singleLineHeight,
                ValueKind.Float2 => EditorGUIUtility.singleLineHeight,
                ValueKind.Float3 => EditorGUIUtility.singleLineHeight,
                ValueKind.Float4 => EditorGUIUtility.singleLineHeight * 2,
                ValueKind.Color => EditorGUIUtility.singleLineHeight,
                ValueKind.Texture => EditorGUIUtility.singleLineHeight,
                ValueKind.TextureArray => EditorGUIUtility.singleLineHeight,
                _ => EditorGUIUtility.singleLineHeight
            };
        }

        void DrawValueField(Rect position, SerializedProperty valueProp, ValueKind type)
        {
            var label = new GUIContent($"Value ({type})");

            switch (type)
            {
                case ValueKind.Float:
                    var floatProp = valueProp.FindPropertyRelative("Float");
                    DrawFloatOrEnumField(position, floatProp, valueProp, label);
                    break;

                case ValueKind.Int:
                    var intProp = valueProp.FindPropertyRelative("Int");
                    DrawIntOrEnumField(position, intProp, valueProp, label);
                    break;

                case ValueKind.Bool:
                    var boolIntProp = valueProp.FindPropertyRelative("Int");
                    var boolRect = EditorGUI.PrefixLabel(position, label);
                    bool currentBool = boolIntProp.intValue != 0;
                    bool newBool = EditorGUI.Toggle(boolRect, currentBool);
                    if (newBool != currentBool)
                        boolIntProp.intValue = newBool ? 1 : 0;
                    break;

                case ValueKind.Float2:
                    var f2Prop = valueProp.FindPropertyRelative("Float2");
                    EditorGUI.PropertyField(position, f2Prop, label);
                    break;

                case ValueKind.Float3:
                    var f3Prop = valueProp.FindPropertyRelative("Float3");
                    EditorGUI.PropertyField(position, f3Prop, label);
                    break;

                case ValueKind.Float4:
                    var f4Prop = valueProp.FindPropertyRelative("Float4");
                    EditorGUI.PropertyField(position, f4Prop, label, true);
                    break;

                case ValueKind.Color:
                    var colorProp = valueProp.FindPropertyRelative("Color");
                    EditorGUI.PropertyField(position, colorProp, label);
                    break;

                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    var texProp = valueProp.FindPropertyRelative("Texture");
                    EditorGUI.PropertyField(position, texProp, label);
                    break;

                default:
                    EditorGUI.LabelField(position, label, new GUIContent("(unsupported type)"));
                    break;
            }
        }

        /// <summary>
        /// Float フィールドを EnumDefinition 対応で描画
        /// </summary>
        void DrawFloatOrEnumField(Rect position, SerializedProperty floatProp, SerializedProperty valueProp, GUIContent label)
        {
            var enumDef = GetEnumDefinitionFromValue(valueProp);
            if (enumDef != null && enumDef.Count > 0)
            {
                DrawEnumDropdown(position, floatProp, enumDef, label, isFloat: true);
            }
            else
            {
                EditorGUI.PropertyField(position, floatProp, label);
            }
        }

        /// <summary>
        /// Int フィールドを EnumDefinition 対応で描画
        /// </summary>
        void DrawIntOrEnumField(Rect position, SerializedProperty intProp, SerializedProperty valueProp, GUIContent label)
        {
            var enumDef = GetEnumDefinitionFromValue(valueProp);
            if (enumDef != null && enumDef.Count > 0)
            {
                DrawEnumDropdown(position, intProp, enumDef, label, isFloat: false);
            }
            else
            {
                EditorGUI.PropertyField(position, intProp, label);
            }
        }

        /// <summary>
        /// EnumDefinition をドロップダウンとして描画
        /// </summary>
        void DrawEnumDropdown(Rect position, SerializedProperty prop, MaterialFxEnumDefinitionSO enumDef, GUIContent label, bool isFloat)
        {
            var options = enumDef.GetDisplayOptions();
            int currentValue = isFloat ? Mathf.RoundToInt(prop.floatValue) : prop.intValue;

            int currentIndex = enumDef.GetIndexByValue(currentValue);
            if (currentIndex == -1) currentIndex = 0;

            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;
            var popupRect = position;
            popupRect.x += EditorGUIUtility.labelWidth + 2;
            popupRect.width -= EditorGUIUtility.labelWidth + 2;

            EditorGUI.LabelField(labelRect, label);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, currentIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                int newValue = enumDef.GetValue(newIndex);
                if (isFloat)
                    prop.floatValue = newValue;
                else
                    prop.intValue = newValue;
            }

            // Tooltip にエントリの説明を表示
            if (newIndex >= 0 && newIndex < enumDef.Count)
            {
                var desc = enumDef.GetEntryDescription(newIndex);
                if (!string.IsNullOrEmpty(desc))
                {
                    GUI.Label(popupRect, new GUIContent("", desc));
                }
            }
        }

        /// <summary>
        /// 親の MaterialFxPresetEntry から Key を取得し、対応する EnumDefinition を探す
        /// </summary>
        MaterialFxEnumDefinitionSO GetEnumDefinitionFromValue(SerializedProperty valueProp)
        {
            // valueProp は "Value" プロパティなので、親の Entry から Key を取得
            var entryProp = valueProp.serializedObject.FindProperty(
                valueProp.propertyPath.Replace(".Value", string.Empty));

            if (entryProp == null)
            {
                // 別のアプローチ: propertyPath から Key を探す
                var path = valueProp.propertyPath;
                var keyPath = path.Replace(".Value", ".Key");
                var keyProp = valueProp.serializedObject.FindProperty(keyPath);
                if (keyProp != null)
                {
                    return GetEnumDefinitionForKey(keyProp.stringValue);
                }
                return null;
            }

            var keyProperty = entryProp.FindPropertyRelative("Key");
            if (keyProperty == null) return null;

            return GetEnumDefinitionForKey(keyProperty.stringValue);
        }

        /// <summary>
        /// StableKey から EnumDefinition を取得
        /// </summary>
        MaterialFxEnumDefinitionSO GetEnumDefinitionForKey(string stableKey)
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
#endif
