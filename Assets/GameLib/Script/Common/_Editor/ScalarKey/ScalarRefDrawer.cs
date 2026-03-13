#if UNITY_EDITOR
using Game.Scalar;
using UnityEditor;
using UnityEngine;

namespace Game.Scalar.Editor
{
    [CustomPropertyDrawer(typeof(ScalarRef))]
    public sealed class ScalarRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var spaceProp = property.FindPropertyRelative("Space");
            var keyProp = property.FindPropertyRelative("Key");

            var labelRect = EditorGUI.PrefixLabel(position, label);
            var spaceRect = new Rect(labelRect.x, labelRect.y, Mathf.Max(70f, labelRect.width * 0.35f), labelRect.height);
            var keyRect = new Rect(
                spaceRect.xMax + 4f,
                labelRect.y,
                Mathf.Max(20f, labelRect.xMax - (spaceRect.xMax + 4f)),
                labelRect.height);

            EditorGUI.PropertyField(spaceRect, spaceProp, GUIContent.none);
            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}
#endif
