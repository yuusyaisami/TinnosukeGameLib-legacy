#if UNITY_EDITOR
using Game.EventKey.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EventKeyPickerAttribute))]
public sealed class EventKeyPickerDrawer : PropertyDrawer
{
    const float ButtonWidth = 22f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var fieldRect = position;
        fieldRect.width -= ButtonWidth;

        var buttonRect = position;
        buttonRect.x += position.width - ButtonWidth;
        buttonRect.width = ButtonWidth;

        EditorGUI.PropertyField(fieldRect, property, label);

        if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("d_FilterByLabel")))
        {
            EventKeyExplorerWindow.Open(property);
        }
    }
}
#endif
