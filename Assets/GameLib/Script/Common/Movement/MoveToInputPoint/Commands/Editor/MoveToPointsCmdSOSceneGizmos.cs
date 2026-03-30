#if UNITY_EDITOR
#nullable enable
using Game.Common;
using UnityEditor;
using UnityEngine;
using System;
using VNext = Game.Commands.VNext;

namespace Game.Movement.Editor
{
    [InitializeOnLoad]
    public static class MoveToPointsCmdSOSceneGizmos
    {
        static readonly System.Collections.Generic.Dictionary<int, string[]> CachedSourcePathsByTargetId = new();

        static MoveToPointsCmdSOSceneGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView view)
        {
            var eventType = Event.current.type;
            if (eventType != EventType.Layout &&
                eventType != EventType.Repaint &&
                eventType != EventType.MouseDown &&
                eventType != EventType.MouseDrag &&
                eventType != EventType.MouseUp &&
                eventType != EventType.MouseMove)
                return;

            var target = Selection.activeObject;
            if (target == null)
                return;

            try
            {
                var serialized = new SerializedObject(target);
                serialized.Update();

                if (!TryDrawInlineMoveToPoints(serialized, target))
                    return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static bool TryDrawInlineMoveToPoints(SerializedObject serialized, UnityEngine.Object target)
        {
            var targetId = target.GetInstanceID();
            if (!CachedSourcePathsByTargetId.TryGetValue(targetId, out var sourcePaths) || sourcePaths == null)
            {
                sourcePaths = BuildSourcePaths(serialized);
                CachedSourcePathsByTargetId[targetId] = sourcePaths;
            }

            var hasAny = false;
            var needsRescan = false;

            for (int i = 0; i < sourcePaths.Length; i++)
            {
                var sourceProp = serialized.FindProperty(sourcePaths[i]);
                if (sourceProp == null)
                {
                    needsRescan = true;
                    continue;
                }

                if (DrawInlineMoveToPoints(serialized, target, sourceProp))
                    hasAny = true;
            }

            if (needsRescan)
                CachedSourcePathsByTargetId[targetId] = BuildSourcePaths(serialized);

            return hasAny;
        }

        static string[] BuildSourcePaths(SerializedObject serialized)
        {
            var paths = new System.Collections.Generic.List<string>();
            var iterator = serialized.GetIterator();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                if (!IsManagedRefType(iterator, "InlineCommandSource"))
                    continue;

                var dataProp = iterator.FindPropertyRelative("data");
                if (dataProp == null || dataProp.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                if (!IsManagedRefType(dataProp, "MoveToPointsCommandData"))
                    continue;

                paths.Add(iterator.propertyPath);
            }

            return paths.ToArray();
        }

        static bool DrawInlineMoveToPoints(SerializedObject serialized, UnityEngine.Object target, SerializedProperty sourceProp)
        {
            if (!sourceProp.isExpanded)
                return false;

            var dataProp = sourceProp.FindPropertyRelative("data");
            if (dataProp == null || dataProp.propertyType != SerializedPropertyType.ManagedReference)
                return false;

            if (!IsManagedRefType(dataProp, "MoveToPointsCommandData"))
                return false;

            var pointsProp = dataProp.FindPropertyRelative("Points");
            var originProp = dataProp.FindPropertyRelative("PreviewOrigin");
            if (originProp == null || pointsProp == null || !pointsProp.isArray || pointsProp.arraySize == 0)
                return false;

            if (!IsExpanded(sourceProp, dataProp, pointsProp))
                return false;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var origin2 = originProp.vector2Value;
            var origin = new Vector3(origin2.x, origin2.y, 0f);

            EditorGUI.BeginChangeCheck();
            var newOrigin = Handles.PositionHandle(origin, Quaternion.identity);
            newOrigin.z = 0f;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "MoveTo Preview Origin");
                originProp.vector2Value = new Vector2(newOrigin.x, newOrigin.y);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                serialized.Update();
                origin = newOrigin;
            }

            var hasAny = true;
            var prevWorld = origin;
            var distAcc = 0f;

            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                var entryProp = pointsProp.GetArrayElementAtIndex(i);
                if (entryProp == null)
                    break;

                var spaceProp = entryProp.FindPropertyRelative("Space");
                var pointProp = entryProp.FindPropertyRelative("Point");
                if (spaceProp == null || pointProp == null)
                    break;

                if (!TryGetLiteralVector2(pointProp, out var literalProp, out var literalValue))
                    break;

                var space = (VNext.MoveToPointSpace)spaceProp.enumValueIndex;
                var world = space == VNext.MoveToPointSpace.World
                    ? new Vector3(literalValue.x, literalValue.y, 0f)
                    : prevWorld + new Vector3(literalValue.x, literalValue.y, 0f);

                var segLen = Vector3.Distance(prevWorld, world);
                var midDist = distAcc + segLen * 0.5f;
                Handles.color = HueByDistance(midDist, 0.85f);
                Handles.DrawAAPolyLine(3f, prevWorld, world);

                distAcc += segLen;

                Handles.color = HueByDistance(distAcc, 0.9f);
                Handles.DrawSolidDisc(world, Vector3.forward, HandleUtility.GetHandleSize(world) * 0.05f);
                Handles.Label(world + Vector3.up * HandleUtility.GetHandleSize(world) * 0.1f, $"P{i + 1}");

                EditorGUI.BeginChangeCheck();
                var newWorld = Handles.PositionHandle(world, Quaternion.identity);
                newWorld.z = 0f;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "MoveTo Point");

                    var v2 = space == VNext.MoveToPointSpace.World
                        ? new Vector2(newWorld.x, newWorld.y)
                        : new Vector2(newWorld.x - prevWorld.x, newWorld.y - prevWorld.y);

                    literalProp.vector2Value = v2;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    serialized.Update();

                    world = newWorld;
                }

                prevWorld = world;
            }

            return hasAny;
        }

        static bool IsManagedRefType(SerializedProperty prop, string typeName)
        {
            var fullName = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullName))
                return false;
            return fullName.EndsWith("." + typeName, StringComparison.Ordinal);
        }

        static bool IsExpanded(SerializedProperty sourceProp, SerializedProperty dataProp, SerializedProperty pointsProp)
        {
            if (sourceProp.isExpanded || dataProp.isExpanded || pointsProp.isExpanded)
                return true;
            return false;
        }

        static Color HueByDistance(float distance, float alpha)
        {
            var hue = Mathf.Repeat(distance / 10f, 1f);
            var c = Color.HSVToRGB(hue, 0.9f, 1f);
            c.a = alpha;
            return c;
        }

        static bool TryGetLiteralVector2(SerializedProperty dynamicValueProp, out SerializedProperty literalValueProp, out Vector2 value)
        {
            literalValueProp = null!;
            value = default;

            var sourceProp = dynamicValueProp.FindPropertyRelative("_source");
            if (sourceProp == null || sourceProp.propertyType != SerializedPropertyType.ManagedReference)
                return false;

            var typeName = sourceProp.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typeName) || !typeName.EndsWith(".LiteralVector2Source", StringComparison.Ordinal))
                return false;

            literalValueProp = sourceProp.FindPropertyRelative("value");
            if (literalValueProp == null)
                return false;

            value = literalValueProp.vector2Value;
            return true;
        }
    }
}
#endif
