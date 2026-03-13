#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ApplyDisplayNameButtonAttribute))]
public sealed class ApplyDisplayNameButtonDrawer : PropertyDrawer
{
    const float ButtonWidth = 64f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var attr = (ApplyDisplayNameButtonAttribute)attribute;
        var mode = attr?.Mode ?? ApplyDisplayNameMode.Button;
        bool showButton = mode == ApplyDisplayNameMode.Button;

        var fieldRect = position;
        Rect buttonRect = default;

        if (showButton)
        {
            fieldRect.width -= ButtonWidth + 4f;
            buttonRect = new Rect(position.x + position.width - ButtonWidth, position.y, ButtonWidth, position.height);
        }

        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(fieldRect, property, label);
        bool changed = EditorGUI.EndChangeCheck();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (showButton)
            {
                var text = string.IsNullOrEmpty(attr?.ButtonText) ? "Apply" : attr.ButtonText;
                if (GUI.Button(buttonRect, text))
                {
                    ApplyDisplayName(property);
                }
            }
            else if (changed)
            {
                // auto mode: apply immediately when the value changes
                property.serializedObject.ApplyModifiedProperties();
                ApplyDisplayName(property);
                property.serializedObject.Update();
            }
        }
    }

    void ApplyDisplayName(SerializedProperty property)
    {
        // Try call __ApplyDisplayNameToAsset on the target object if it exists
        var target = property.serializedObject.targetObject;
        if (target is ScriptableObject so)
        {
            var method = so.GetType().GetMethod("__ApplyDisplayNameToAsset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(so, null);
                return;
            }

            var val = property.stringValue;
            if (string.IsNullOrEmpty(val))
                return;

            if (HasSameNameInDirectory(so, val, out var conflictPath))
            {
                Debug.LogWarning($"[ApplyDisplayName] Skip update because an asset with the same name exists in the directory: {conflictPath}");
                return;
            }

            Undo.RecordObject(so, "Apply displayName to asset name");
            so.name = val;
            EditorUtility.SetDirty(so);
            var path = AssetDatabase.GetAssetPath(so);
            if (!string.IsNullOrEmpty(path))
            {
                // Rename asset file to match object name to avoid Unity's main object name mismatch warning.
                RenameAssetFileIfNeeded(path, val);
            }
            return;
        }

        ApplyToObject(property);
    }

    void ApplyToObject(SerializedProperty property)
    {
        var obj = property.serializedObject.targetObject as UnityEngine.Object;
        if (obj == null)
            return;

        var val = property.stringValue;
        if (string.IsNullOrEmpty(val))
            return;

        if (HasSameNameInDirectory(obj, val, out var conflictPath))
        {
            Debug.LogWarning($"[ApplyDisplayName] Skip update because an asset with the same name exists in the directory: {conflictPath}");
            return;
        }

        Undo.RecordObject(obj, "Apply display name");
        obj.name = val;
        EditorUtility.SetDirty(obj);

        // If this object is an asset in the project, rename its file to match the new object name.
        var path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
        {
            RenameAssetFileIfNeeded(path, val);
        }
    }

    bool HasSameNameInDirectory(UnityEngine.Object obj, string newName, out string conflictPath)
    {
        conflictPath = null;
        if (string.IsNullOrEmpty(newName))
            return false;

        var path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path))
            return false; // Non-asset: directory check is not applicable

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
            return false;

        var guids = AssetDatabase.FindAssets(string.Empty, new[] { directory });
        for (int i = 0; i < guids.Length; i++)
        {
            var otherPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(otherPath) || string.Equals(otherPath, path, StringComparison.Ordinal))
                continue;

            if (!string.Equals(Path.GetDirectoryName(otherPath), directory, StringComparison.Ordinal))
                continue;

            var otherName = Path.GetFileNameWithoutExtension(otherPath);
            if (string.Equals(otherName, newName, StringComparison.OrdinalIgnoreCase))
            {
                conflictPath = otherPath;
                return true;
            }
        }

        return false;
    }

    void RenameAssetFileIfNeeded(string assetPath, string newName)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(newName))
            return;

        var directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(directory))
            return;

        var ext = Path.GetExtension(assetPath);
        var newFileName = newName + ext;
        var newPath = Path.Combine(directory, newFileName).Replace("\\", "/");

        var currentFileName = Path.GetFileName(assetPath);
        if (string.Equals(currentFileName, newFileName, StringComparison.Ordinal))
            return; // No rename needed

        var error = AssetDatabase.RenameAsset(assetPath, newName);
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning($"[ApplyDisplayName] Failed to rename asset file: {error}");
            return;
        }

        // Import the new path to ensure Unity updates the asset database
        AssetDatabase.ImportAsset(newPath);
        AssetDatabase.SaveAssets();
    }
}
#endif
