// Game.Channel.TransformAnimationHubMB.cs

using System;
using Game;
using Game.Common;
using Game.TransformSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class TransformAnimationHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        TransformChannelDef[] channels = Array.Empty<TransformChannelDef>();

        [Title("Path Gizmo")]
        [SerializeField] bool showCurveGizmo = true;
        [SerializeField, ShowIf(nameof(showCurveGizmo))] bool curveSelectedOnly = true;
        [SerializeField, ShowIf(nameof(showCurveGizmo)), MinValue(0.01f)] float curvePointRadius = 0.08f;
        [SerializeField, ShowIf(nameof(showCurveGizmo))] bool enableSceneHandles = true;
        [SerializeField, ShowIf(nameof(enableSceneHandles))] bool moveDestinationHandle = true;
        [SerializeField, ShowIf(nameof(enableSceneHandles))] bool moveCurveControlHandle = true;

        [Title("Debug")]
        [SerializeField]
        bool enableDebugLog = false;

        [SerializeField, InlineProperty, HideLabel]
        TransformAnimationHubDebugViewer debugViewer = new();

        /// <summary>
        /// Awake 縺ｧ蛻晄悄 Transform 險ｭ螳壹ｒ蜊ｳ蠎ｧ縺ｫ驕ｩ逕ｨ縲・
        /// DI 螳御ｺ・燕縺ｫ螳溯｡後＆繧後ｋ縺溘ａ縲・ frame 縺ｮ驕・ｻｶ縺ｪ縺・Transform 繧定ｨｭ螳壹〒縺阪ｋ縲・
        /// </summary>
        void Awake()
        {
            if (channels == null) return;

            for (int i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                if (channel == null)
                    continue;

                channel.EnsureIntegrity(this);
                channel.ApplyInitialTransform();
            }
        }


        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            if (channels != null)
            {
                for (int i = 0; i < channels.Length; i++)
                {
                    channels[i]?.EnsureIntegrity(this);
                }
            }

            builder.Register<TransformAnimationOutputRegistryService>(RuntimeLifetime.Singleton)
                .As<ITransformAnimationOutputRegistry>()
                .AsSelf();

            builder.Register<TransformAnimationTargetRegistryService>(RuntimeLifetime.Singleton)
                .As<ITransformAnimationTargetRegistry>()
                .As<IScopeReleaseHandler>();

            builder.Register<TransformAnimationHubService>(RuntimeLifetime.Singleton)
                .As<ITransformAnimationHubService>()
                .AsSelf()
                                .As<IScopeTickHandler>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
              .WithParameter<IScopeNode>(scope)
                            .WithParameter(enableDebugLog)
              .WithParameter(channels);

            builder.RegisterBuildCallback(container =>
            {
                if (debugViewer != null && container.TryResolve<ITransformAnimationHubService>(out var hub) && hub != null)
                {
                    debugViewer.Bind(hub);
                }
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<TransformChannelDef>();
        }
#endif

        void OnDrawGizmos()
        {
            if (curveSelectedOnly)
                return;

            DrawCurveGizmosCore();
        }

        void OnDrawGizmosSelected()
        {
            if (!curveSelectedOnly)
                return;

            DrawCurveGizmosCore();
        }

        void DrawCurveGizmosCore()
        {
            if (!showCurveGizmo || channels == null)
                return;

            var prevColor = Gizmos.color;
            var radius = Mathf.Max(0.01f, curvePointRadius);

            for (int i = 0; i < channels.Length; i++)
            {
                var def = channels[i];
                if (def == null)
                    continue;

                var preset = def.TransformPreset;
                if (preset == null || preset.Steps == null || preset.Steps.Count == 0)
                    continue;

                var target = def.RectTransform != null ? (Transform)def.RectTransform : def.Transform;
                if (!target)
                    continue;

                var currentWorld = target.position;
                var currentLocal = target.localPosition;

                for (int stepIndex = 0; stepIndex < preset.Steps.Count; stepIndex++)
                {
                    var step = preset.Steps[stepIndex];
                    if (step == null)
                        continue;

                    if (step.Operation != TransformAnimationOperation.WorldPosition &&
                        step.Operation != TransformAnimationOperation.LocalPosition)
                    {
                        continue;
                    }

                    var start = step.Operation == TransformAnimationOperation.WorldPosition
                        ? currentWorld
                        : currentLocal;

                    var value = step.Vector3Value.Get(null, NullVarStore.Instance, step.Relative ? Vector3.zero : start);
                    var end = step.PositionPathMode == TransformPositionPathMode.Poly
                        ? start + value
                        : (step.Relative ? start + value : value);

                    if (step.Operation == TransformAnimationOperation.WorldPosition)
                        currentWorld = end;
                    else
                        currentLocal = end;

                    if (step.PositionPathMode == TransformPositionPathMode.Linear)
                        continue;

                    DrawCurveStepGizmo(target, step, start, end, radius);
                }
            }

            Gizmos.color = prevColor;
        }

        static void DrawCurveStepGizmo(Transform target, ITransformAnimationStep step, in Vector3 start, in Vector3 end, float pointRadius)
        {
            var isLocal = step.Operation == TransformAnimationOperation.LocalPosition;

            var path = TransformAnimationChannelPlayer.BuildPositionPath(start, end, step);
            var points = path.Points;
            if (points == null || points.Length == 0)
                return;

            for (int i = 1; i < points.Length; i++)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.15f, 1f);
                var prevWorldLine = isLocal ? target.TransformPoint(points[i - 1]) : points[i - 1];
                var nextWorldLine = isLocal ? target.TransformPoint(points[i]) : points[i];
                Gizmos.DrawLine(prevWorldLine, nextWorldLine);
            }

            var startWorld = isLocal ? target.TransformPoint(start) : start;
            var endWorld = isLocal ? target.TransformPoint(end) : end;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(startWorld, pointRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(endWorld, pointRadius);

            if (step.PositionPathMode == TransformPositionPathMode.Curve)
            {
                var control = TransformAnimationChannelPlayer.ResolveCurveControlPoint(start, end, step);
                var controlWorld = isLocal ? target.TransformPoint(control) : control;
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(controlWorld, pointRadius);
            }
            else if (step.PositionPathMode == TransformPositionPathMode.Poly)
            {
                Gizmos.color = new Color(1f, 0.3f, 1f, 1f);
                for (int i = 1; i < points.Length - 1; i++)
                {
                    var waypointWorld = isLocal ? target.TransformPoint(points[i]) : points[i];
                    Gizmos.DrawSphere(waypointWorld, pointRadius * 0.7f);
                }
            }
        }

#if UNITY_EDITOR
        public void DrawSceneHandles()
        {
            if (!showCurveGizmo || !enableSceneHandles || channels == null)
                return;

            var serialized = new SerializedObject(this);
            serialized.Update();

            for (int i = 0; i < channels.Length; i++)
            {
                var def = channels[i];
                if (def == null)
                    continue;

                var preset = def.TransformPreset;
                if (preset == null || preset.Steps == null || preset.Steps.Count == 0)
                    continue;

                var target = def.RectTransform != null ? (Transform)def.RectTransform : def.Transform;
                if (!target)
                    continue;

                var currentWorld = target.position;
                var currentLocal = target.localPosition;

                for (int stepIndex = 0; stepIndex < preset.Steps.Count; stepIndex++)
                {
                    var step = preset.Steps[stepIndex];
                    if (step == null)
                        continue;

                    if (step.Operation != TransformAnimationOperation.WorldPosition &&
                        step.Operation != TransformAnimationOperation.LocalPosition)
                    {
                        continue;
                    }

                    var start = step.Operation == TransformAnimationOperation.WorldPosition
                        ? currentWorld
                        : currentLocal;

                    var value = step.Vector3Value.Get(null, NullVarStore.Instance, step.Relative ? Vector3.zero : start);
                    var end = step.PositionPathMode == TransformPositionPathMode.Poly
                        ? start + value
                        : (step.Relative ? start + value : value);

                    if (step.Operation == TransformAnimationOperation.WorldPosition)
                        currentWorld = end;
                    else
                        currentLocal = end;

                    TryHandleStepEdit(serialized, i, stepIndex, target, step, start, end);
                }
            }

            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(this);
            }
        }

        void TryHandleStepEdit(
            SerializedObject serialized,
            int channelIndex,
            int stepIndex,
            Transform targetTransform,
            ITransformAnimationStep step,
            in Vector3 start,
            in Vector3 end)
        {
            var channelsProp = serialized.FindProperty(nameof(channels));
            if (channelsProp == null || !channelsProp.isArray || channelIndex < 0 || channelIndex >= channelsProp.arraySize)
                return;

            var channelProp = channelsProp.GetArrayElementAtIndex(channelIndex);
            var presetProp = channelProp.FindPropertyRelative("transformPreset");
            var stepsProp = presetProp?.FindPropertyRelative("steps");
            if (stepsProp == null || !stepsProp.isArray || stepIndex < 0 || stepIndex >= stepsProp.arraySize)
                return;

            var stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);
            var vectorProp = stepProp.FindPropertyRelative("vector3");
            var sourceProp = vectorProp?.FindPropertyRelative("_source");
            if (sourceProp == null || sourceProp.propertyType != SerializedPropertyType.ManagedReference)
                return;

            var sourceTypeName = sourceProp.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(sourceTypeName) || !sourceTypeName.EndsWith(".LiteralVector3Source", StringComparison.Ordinal))
                return;

            var literalValueProp = sourceProp.FindPropertyRelative("value");
            if (literalValueProp == null)
                return;

            var isLocal = step.Operation == TransformAnimationOperation.LocalPosition;
            var endWorld = isLocal ? targetTransform.TransformPoint(end) : end;

            if (moveDestinationHandle)
            {
                EditorGUI.BeginChangeCheck();
                var newEndWorld = Handles.PositionHandle(endWorld, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Transform Path Destination");

                    var newEnd = isLocal ? targetTransform.InverseTransformPoint(newEndWorld) : newEndWorld;
                    var storedValue = step.PositionPathMode == TransformPositionPathMode.Poly
                        ? (newEnd - start)
                        : (step.Relative ? (newEnd - start) : newEnd);
                    literalValueProp.vector3Value = storedValue;
                    endWorld = newEndWorld;
                }
            }

            if (step.PositionPathMode != TransformPositionPathMode.Curve || !moveCurveControlHandle)
                return;

            var endForControl = isLocal ? targetTransform.InverseTransformPoint(endWorld) : endWorld;
            var control = TransformAnimationChannelPlayer.ResolveCurveControlPoint(start, endForControl, step);
            var controlWorld = isLocal ? targetTransform.TransformPoint(control) : control;

            EditorGUI.BeginChangeCheck();
            var newControlWorld = Handles.PositionHandle(controlWorld, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Transform Path Control");

                var newControl = isLocal ? targetTransform.InverseTransformPoint(newControlWorld) : newControlWorld;
                var baseControl = TransformAnimationChannelPlayer.ResolveCurveBaseControlPoint(start, endForControl, step);
                var offsetProp = stepProp.FindPropertyRelative("curveControlOffset");
                if (offsetProp != null)
                    offsetProp.vector3Value = newControl - baseControl;
            }
        }
#endif
    }
}
