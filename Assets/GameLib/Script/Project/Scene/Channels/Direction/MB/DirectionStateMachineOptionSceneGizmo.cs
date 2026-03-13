#if UNITY_EDITOR
#nullable enable
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Direction.Editor
{
    static class DirectionStateMachineOptionSceneGizmo
    {
        const string UseCustomProp = "useCustomCardinalAngles";
        const string UpCenterProp = "upCenterDeg";
        const string UpHalfRangeProp = "upHalfRangeDeg";
        const string LeftCenterProp = "leftCenterDeg";
        const string LeftHalfRangeProp = "leftHalfRangeDeg";
        const string RightCenterProp = "rightCenterDeg";
        const string RightHalfRangeProp = "rightHalfRangeDeg";
        const string DownCenterProp = "downCenterDeg";
        const string DownHalfRangeProp = "downHalfRangeDeg";

        static readonly Color UpColor = new(0.25f, 0.8f, 0.45f, 1f);
        static readonly Color LeftColor = new(0.95f, 0.65f, 0.25f, 1f);
        static readonly Color RightColor = new(0.25f, 0.55f, 0.95f, 1f);
        static readonly Color DownColor = new(0.95f, 0.35f, 0.35f, 1f);

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void Draw(DirectionStateMachineOptionMB target, GizmoType gizmoType)
        {
            if (target == null)
                return;

            if (!TryReadAngles(target, out var angleData) || !angleData.Enabled)
                return;

            var center = target.transform.position;
            var radius = HandleUtility.GetHandleSize(center) * 1.2f;
            DrawAxis(center, radius);

            DrawSector(center, radius, angleData.UpCenter, angleData.UpHalfRange, UpColor, "UP");
            DrawSector(center, radius, angleData.LeftCenter, angleData.LeftHalfRange, LeftColor, "LEFT");
            DrawSector(center, radius, angleData.RightCenter, angleData.RightHalfRange, RightColor, "RIGHT");
            DrawSector(center, radius, angleData.DownCenter, angleData.DownHalfRange, DownColor, "DOWN");
        }

        static bool TryReadAngles(DirectionStateMachineOptionMB target, out AngleData data)
        {
            data = default;
            if (target == null)
                return false;

            var so = new SerializedObject(target);
            var enabledProp = so.FindProperty(UseCustomProp);
            var upCenter = so.FindProperty(UpCenterProp);
            var upHalf = so.FindProperty(UpHalfRangeProp);
            var leftCenter = so.FindProperty(LeftCenterProp);
            var leftHalf = so.FindProperty(LeftHalfRangeProp);
            var rightCenter = so.FindProperty(RightCenterProp);
            var rightHalf = so.FindProperty(RightHalfRangeProp);
            var downCenter = so.FindProperty(DownCenterProp);
            var downHalf = so.FindProperty(DownHalfRangeProp);

            if (enabledProp == null ||
                upCenter == null || upHalf == null ||
                leftCenter == null || leftHalf == null ||
                rightCenter == null || rightHalf == null ||
                downCenter == null || downHalf == null)
            {
                return false;
            }

            data = new AngleData(
                enabledProp.boolValue,
                upCenter.floatValue,
                upHalf.floatValue,
                leftCenter.floatValue,
                leftHalf.floatValue,
                rightCenter.floatValue,
                rightHalf.floatValue,
                downCenter.floatValue,
                downHalf.floatValue);
            return true;
        }

        static void DrawAxis(Vector3 center, float radius)
        {
            var oldColor = Handles.color;
            var oldZ = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.85f);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius);
            Handles.DrawLine(center + Vector3.down * radius, center + Vector3.up * radius);

            Handles.color = oldColor;
            Handles.zTest = oldZ;
        }

        static void DrawSector(Vector3 center, float radius, float centerDeg, float halfRangeDeg, Color color, string label)
        {
            var oldColor = Handles.color;
            var oldZ = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            var fill = color;
            fill.a = 0.15f;
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(BuildSceneSectorPolygon(center, radius, centerDeg, halfRangeDeg));

            Handles.color = color;
            Handles.DrawAAPolyLine(2f, BuildSceneArcPolyline(center, radius, centerDeg, halfRangeDeg));

            var dir = SceneDirection(centerDeg);
            Handles.DrawAAPolyLine(2f, center, center + dir * radius);

            Handles.Label(center + dir * (radius + HandleUtility.GetHandleSize(center) * 0.2f), label);

            Handles.color = oldColor;
            Handles.zTest = oldZ;
        }

        static Vector3[] BuildSceneSectorPolygon(Vector3 center, float radius, float centerDeg, float halfRangeDeg)
        {
            var clampedHalf = Mathf.Clamp(halfRangeDeg, 0f, 180f);
            var start = centerDeg - clampedHalf;
            var end = centerDeg + clampedHalf;
            const int segmentCount = 40;

            var points = new Vector3[segmentCount + 2];
            points[0] = center;
            for (var i = 0; i <= segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                var deg = Mathf.Lerp(start, end, t);
                points[i + 1] = center + SceneDirection(deg) * radius;
            }

            return points;
        }

        static Vector3[] BuildSceneArcPolyline(Vector3 center, float radius, float centerDeg, float halfRangeDeg)
        {
            var clampedHalf = Mathf.Clamp(halfRangeDeg, 0f, 180f);
            var start = centerDeg - clampedHalf;
            var end = centerDeg + clampedHalf;
            const int segmentCount = 40;

            var points = new Vector3[segmentCount + 1];
            for (var i = 0; i <= segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                var deg = Mathf.Lerp(start, end, t);
                points[i] = center + SceneDirection(deg) * radius;
            }

            return points;
        }

        static Vector3 SceneDirection(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        }

        readonly struct AngleData
        {
            public readonly bool Enabled;
            public readonly float UpCenter;
            public readonly float UpHalfRange;
            public readonly float LeftCenter;
            public readonly float LeftHalfRange;
            public readonly float RightCenter;
            public readonly float RightHalfRange;
            public readonly float DownCenter;
            public readonly float DownHalfRange;

            public AngleData(
                bool enabled,
                float upCenter,
                float upHalfRange,
                float leftCenter,
                float leftHalfRange,
                float rightCenter,
                float rightHalfRange,
                float downCenter,
                float downHalfRange)
            {
                Enabled = enabled;
                UpCenter = upCenter;
                UpHalfRange = Mathf.Clamp(upHalfRange, 0f, 180f);
                LeftCenter = leftCenter;
                LeftHalfRange = Mathf.Clamp(leftHalfRange, 0f, 180f);
                RightCenter = rightCenter;
                RightHalfRange = Mathf.Clamp(rightHalfRange, 0f, 180f);
                DownCenter = downCenter;
                DownHalfRange = Mathf.Clamp(downHalfRange, 0f, 180f);
            }
        }
    }
}
#endif
