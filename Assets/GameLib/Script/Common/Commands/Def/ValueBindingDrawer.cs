#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Commands
{
    [CustomPropertyDrawer(typeof(FlexibleValue<>), true)]
    public sealed class FlexibleValueDrawer : PropertyDrawer
    {
        const float DropdownWidth = 120f;
        const float Spacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var sourceProp = property.FindPropertyRelative("source");
            var source = (ValueSource)sourceProp.enumValueIndex;
            float line = EditorGUIUtility.singleLineHeight;
            if (source == ValueSource.LiteralAddVariable)
                return line * 2f + EditorGUIUtility.standardVerticalSpacing;
            return line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Draw label and get remaining rect
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var sourceProp = property.FindPropertyRelative("source");
            var variableKeyProp = property.FindPropertyRelative("variableKey");
            var addVariableKeyProp = property.FindPropertyRelative("addVariableKey");
            var literalProp = property.FindPropertyRelative("literal");

            var source = (ValueSource)sourceProp.enumValueIndex;

            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Source dropdown
            var dropdownRect = new Rect(lineRect.x, lineRect.y, DropdownWidth, lineRect.height);
            EditorGUI.PropertyField(dropdownRect, sourceProp, GUIContent.none);

            var fieldRect = new Rect(
                dropdownRect.xMax + Spacing,
                lineRect.y,
                lineRect.width - DropdownWidth - Spacing,
                lineRect.height);

            switch (source)
            {
                case ValueSource.VariableKey:
                    EditorGUI.PropertyField(fieldRect, variableKeyProp, GUIContent.none);
                    break;

                case ValueSource.Literal:
                    DrawValueField(fieldRect, literalProp, "Value");
                    break;

                case ValueSource.LiteralAddVariable:
                    // 1行目: key
                    EditorGUI.PropertyField(fieldRect, addVariableKeyProp, GUIContent.none);

                    // 2行目: value
                    var valueRect = new Rect(
                        position.x,
                        lineRect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                        position.width,
                        EditorGUIUtility.singleLineHeight);

                    // indent value line to align with dropdown
                    var labelRect = new Rect(valueRect.x + DropdownWidth + Spacing, valueRect.y, 50f, valueRect.height);
                    var valueFieldRect = new Rect(labelRect.xMax + Spacing, valueRect.y,
                        position.width - (labelRect.xMax + Spacing - position.x), valueRect.height);

                    EditorGUI.LabelField(labelRect, "Value");
                    EditorGUI.PropertyField(valueFieldRect, literalProp, GUIContent.none);
                    break;
            }

            EditorGUI.indentLevel = oldIndent;
            EditorGUI.EndProperty();
        }

        void DrawValueField(Rect rect, SerializedProperty prop, string label)
        {
            var labelWidth = 50f;
            var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var valueRect = new Rect(rect.x + labelWidth + Spacing, rect.y, rect.width - labelWidth - Spacing, rect.height);
            EditorGUI.LabelField(labelRect, label);
            EditorGUI.PropertyField(valueRect, prop, GUIContent.none);
        }
    }

    // Wrappers forward to inner FlexibleValue<T>
    [CustomPropertyDrawer(typeof(FlexibleString))]
    [CustomPropertyDrawer(typeof(FlexibleBool))]
    [CustomPropertyDrawer(typeof(FlexibleFloat))]
    [CustomPropertyDrawer(typeof(FlexibleInt))]
    [CustomPropertyDrawer(typeof(FlexibleVector2))]
    [CustomPropertyDrawer(typeof(FlexibleVector3))]
    [CustomPropertyDrawer(typeof(FlexibleVector4))]
    [CustomPropertyDrawer(typeof(FlexibleColor))]
    [CustomPropertyDrawer(typeof(FlexibleQuaternion))]
    [CustomPropertyDrawer(typeof(FlexibleTransform))]
    [CustomPropertyDrawer(typeof(FlexibleAnimationCurve))]
    [CustomPropertyDrawer(typeof(FlexibleTexture))]
    [CustomPropertyDrawer(typeof(FlexibleAnimationData))]
    public sealed class FlexibleWrapperDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var inner = property.FindPropertyRelative("value");
            return EditorGUI.GetPropertyHeight(inner, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var inner = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, inner, label, true);
        }
    }
}
#endif
