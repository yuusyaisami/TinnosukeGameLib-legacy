#if UNITY_EDITOR
#nullable enable
using UnityEditor;
using UnityEngine;

namespace Game.Direction.Editor
{
    public sealed class DirectionStateMachineAnglePreviewWindow : EditorWindow
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

        DirectionStateMachineOptionMB? _target;
        SerializedObject? _serializedTarget;

        public static void Open(DirectionStateMachineOptionMB target)
        {
            if (target == null)
                return;

            var window = GetWindow<DirectionStateMachineAnglePreviewWindow>("Direction Angle Preview");
            window.minSize = new Vector2(420f, 520f);
            window.SetTarget(target);
            window.Show();
            window.Focus();
        }

        void SetTarget(DirectionStateMachineOptionMB target)
        {
            _target = target;
            _serializedTarget = new SerializedObject(target);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4f);
            var nextTarget = EditorGUILayout.ObjectField("Target", _target, typeof(DirectionStateMachineOptionMB), true) as DirectionStateMachineOptionMB;
            if (nextTarget != _target)
            {
                if (nextTarget == null)
                {
                    _target = null;
                    _serializedTarget = null;
                }
                else
                {
                    SetTarget(nextTarget);
                }
            }

            if (_target == null || _serializedTarget == null)
            {
                EditorGUILayout.HelpBox("DirectionStateMachineOptionMB を指定すると角度範囲を表示します。", MessageType.Info);
                return;
            }

            _serializedTarget.UpdateIfRequiredOrScript();
            if (!TryReadAngles(_serializedTarget, out var angles))
            {
                EditorGUILayout.HelpBox("カスタム角度設定が見つかりません。", MessageType.Warning);
                return;
            }

            if (!angles.Enabled)
            {
                EditorGUILayout.HelpBox("Use Custom Cardinal Angles が OFF です。ON にするとプレビューされます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Angle uses +X=0deg, +Y=90deg", EditorStyles.miniBoldLabel);
            var rect = GUILayoutUtility.GetRect(380f, 380f, GUILayout.ExpandWidth(true));
            DrawPreview(rect, angles);

            EditorGUILayout.Space(8f);
            DrawLegend(angles);
        }

        static bool TryReadAngles(SerializedObject serialized, out AngleData data)
        {
            data = default;

            var enabledProp = serialized.FindProperty(UseCustomProp);
            var upCenter = serialized.FindProperty(UpCenterProp);
            var upHalf = serialized.FindProperty(UpHalfRangeProp);
            var leftCenter = serialized.FindProperty(LeftCenterProp);
            var leftHalf = serialized.FindProperty(LeftHalfRangeProp);
            var rightCenter = serialized.FindProperty(RightCenterProp);
            var rightHalf = serialized.FindProperty(RightHalfRangeProp);
            var downCenter = serialized.FindProperty(DownCenterProp);
            var downHalf = serialized.FindProperty(DownHalfRangeProp);

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

        static void DrawPreview(Rect rect, in AngleData data)
        {
            EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f, 1f));
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.36f;

            Handles.BeginGUI();
            var oldColor = Handles.color;

            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.85f);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawLine(center + new Vector2(-radius, 0f), center + new Vector2(radius, 0f));
            Handles.DrawLine(center + new Vector2(0f, -radius), center + new Vector2(0f, radius));

            DrawSector(center, radius, data.UpCenter, data.UpHalfRange, UpColor, "UP");
            DrawSector(center, radius, data.LeftCenter, data.LeftHalfRange, LeftColor, "LEFT");
            DrawSector(center, radius, data.RightCenter, data.RightHalfRange, RightColor, "RIGHT");
            DrawSector(center, radius, data.DownCenter, data.DownHalfRange, DownColor, "DOWN");

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        static void DrawSector(Vector2 center, float radius, float centerDeg, float halfRangeDeg, Color color, string label)
        {
            var fill = color;
            fill.a = 0.18f;
            Handles.color = fill;
            var sectorPolygon = BuildGuiSectorPolygon(center, radius, centerDeg, halfRangeDeg);
            Handles.DrawAAConvexPolygon(sectorPolygon);

            Handles.color = color;
            var arcPoints = BuildGuiArcPolyline(center, radius, centerDeg, halfRangeDeg);
            Handles.DrawAAPolyLine(2f, arcPoints);

            var dir = GuiDirection(centerDeg);
            Handles.DrawAAPolyLine(2.2f, center, center + dir * radius);

            var labelPos = center + dir * (radius + 14f);
            var labelRect = new Rect(labelPos.x - 46f, labelPos.y - 10f, 92f, 20f);
            var old = GUI.color;
            GUI.color = color;
            GUI.Label(labelRect, label, EditorStyles.boldLabel);
            GUI.color = old;
        }

        static Vector2 GuiDirection(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
        }

        static Vector3[] BuildGuiSectorPolygon(Vector2 center, float radius, float centerDeg, float halfRangeDeg)
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
                var p = center + GuiDirection(deg) * radius;
                points[i + 1] = p;
            }

            return points;
        }

        static Vector3[] BuildGuiArcPolyline(Vector2 center, float radius, float centerDeg, float halfRangeDeg)
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
                var p = center + GuiDirection(deg) * radius;
                points[i] = p;
            }

            return points;
        }

        static void DrawLegend(in AngleData data)
        {
            DrawLegendRow("UP", data.UpCenter, data.UpHalfRange, UpColor);
            DrawLegendRow("LEFT", data.LeftCenter, data.LeftHalfRange, LeftColor);
            DrawLegendRow("RIGHT", data.RightCenter, data.RightHalfRange, RightColor);
            DrawLegendRow("DOWN", data.DownCenter, data.DownHalfRange, DownColor);
        }

        static void DrawLegendRow(string label, float center, float halfRange, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 18f);
            var colorRect = new Rect(rect.x, rect.y + 3f, 10f, 10f);
            EditorGUI.DrawRect(colorRect, color);
            var textRect = new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height);
            EditorGUI.LabelField(textRect, $"{label}: center={center:0.##}deg, range +/-{halfRange:0.##}deg");
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
