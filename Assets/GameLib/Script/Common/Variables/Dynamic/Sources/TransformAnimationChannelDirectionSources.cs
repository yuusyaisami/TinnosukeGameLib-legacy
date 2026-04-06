#nullable enable

using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    public enum TransformAnimationChannelDirectionAxis
    {
        Right = 0,
        Up = 1,
        Forward = 2,
    }

    public enum TransformAnimationChannelVector2Plane
    {
        XY = 0,
        XZ = 1,
        YZ = 2,
    }

    public enum TransformAnimationChannelAngleUnit
    {
        Degrees = 0,
        Radians = 1,
    }

    [Serializable]
    public sealed class TransformAnimationChannelDirection2Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TransformAnimationChannelTargetSelectMode targetSelectMode = TransformAnimationChannelTargetSelectMode.Self;

        [SerializeField, LabelText("Child Path")]
        [ShowIf(nameof(ShowChildPathField))]
        string childPath = "";

        [SerializeField, LabelText("Child Name")]
        [ShowIf(nameof(ShowChildNameField))]
        string childName = "";

        [SerializeField, LabelText("Search Recursive")]
        [ShowIf(nameof(ShowChildNameField))]
        bool childNameRecursive = true;

        [SerializeField, LabelText("Space")]
        TransformAnimationChannelPositionSpace space = TransformAnimationChannelPositionSpace.World;

        [SerializeField, LabelText("Direction Axis")]
        TransformAnimationChannelDirectionAxis directionAxis = TransformAnimationChannelDirectionAxis.Right;

        [SerializeField, LabelText("Output Plane")]
        TransformAnimationChannelVector2Plane outputPlane = TransformAnimationChannelVector2Plane.XY;

        [SerializeField, LabelText("Fallback")]
        Vector2 fallback = Vector2.right;

        [SerializeField, LabelText("Debug Log")]
        bool debugLogEnabled;

        [SerializeField, LabelText("Debug Every N Frames"), MinValue(1)]
        [ShowIf(nameof(debugLogEnabled))]
        int debugLogEveryNFrames = 30;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] TransformAnimationChannelTargetResolveCache _targetCache;
        [NonSerialized] int _lastDebugLogFrame = -1;

        public string SourceTypeName => "TransformChannelDir";
        public string GetDebugData =>
            $"{actorSource.Kind}:{channelTag} [{targetSelectMode}] ({space},{directionAxis},{outputPlane},Vector2)";

        bool ShowChildPathField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildPath;
        bool ShowChildNameField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TransformAnimationChannelPositionSourceHelper.TryGetTargetTransform(
                    context,
                    actorSource,
                    channelTag,
                    targetSelectMode,
                    childPath,
                    childName,
                    childNameRecursive,
                    ref _actorCache,
                    ref _targetCache,
                    out var resolved,
                    out var root))
            {
                TryLog($"Resolve failed. actor={actorSource.Kind}, tag={channelTag}, mode={targetSelectMode}, path={childPath}, name={childName}");
                return DynamicVariant.FromVector2(TransformAnimationChannelDirectionSourceMath.NormalizeOrDefault(fallback, Vector2.right));
            }

            var direction3 = TransformAnimationChannelDirectionSourceMath.ResolveDirection(resolved, space, directionAxis);
            var projected = TransformAnimationChannelDirectionSourceMath.ProjectToPlane(direction3, outputPlane);
            var output = TransformAnimationChannelDirectionSourceMath.NormalizeOrDefault(projected, fallback);

            TryLog(
                $"tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, " +
                $"resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, space={space}, axis={directionAxis}, plane={outputPlane}, " +
                $"dir3={direction3}, out={output}");

            return DynamicVariant.FromVector2(output);
        }

        void TryLog(string message)
        {
            if (!debugLogEnabled)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, debugLogEveryNFrames);
            if (_lastDebugLogFrame >= 0 && frame - _lastDebugLogFrame < interval)
                return;

            _lastDebugLogFrame = frame;
            Debug.Log($"[TransformAnimationChannelDirection2Source] {message}");
        }
    }

    [Serializable]
    public sealed class TransformAnimationChannelDirection3Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TransformAnimationChannelTargetSelectMode targetSelectMode = TransformAnimationChannelTargetSelectMode.Self;

        [SerializeField, LabelText("Child Path")]
        [ShowIf(nameof(ShowChildPathField))]
        string childPath = "";

        [SerializeField, LabelText("Child Name")]
        [ShowIf(nameof(ShowChildNameField))]
        string childName = "";

        [SerializeField, LabelText("Search Recursive")]
        [ShowIf(nameof(ShowChildNameField))]
        bool childNameRecursive = true;

        [SerializeField, LabelText("Space")]
        TransformAnimationChannelPositionSpace space = TransformAnimationChannelPositionSpace.World;

        [SerializeField, LabelText("Direction Axis")]
        TransformAnimationChannelDirectionAxis directionAxis = TransformAnimationChannelDirectionAxis.Forward;

        [SerializeField, LabelText("Fallback")]
        Vector3 fallback = Vector3.forward;

        [SerializeField, LabelText("Debug Log")]
        bool debugLogEnabled;

        [SerializeField, LabelText("Debug Every N Frames"), MinValue(1)]
        [ShowIf(nameof(debugLogEnabled))]
        int debugLogEveryNFrames = 30;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] TransformAnimationChannelTargetResolveCache _targetCache;
        [NonSerialized] int _lastDebugLogFrame = -1;

        public string SourceTypeName => "TransformChannelDir";
        public string GetDebugData =>
            $"{actorSource.Kind}:{channelTag} [{targetSelectMode}] ({space},{directionAxis},Vector3)";

        bool ShowChildPathField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildPath;
        bool ShowChildNameField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TransformAnimationChannelPositionSourceHelper.TryGetTargetTransform(
                    context,
                    actorSource,
                    channelTag,
                    targetSelectMode,
                    childPath,
                    childName,
                    childNameRecursive,
                    ref _actorCache,
                    ref _targetCache,
                    out var resolved,
                    out var root))
            {
                TryLog($"Resolve failed. actor={actorSource.Kind}, tag={channelTag}, mode={targetSelectMode}, path={childPath}, name={childName}");
                return DynamicVariant.FromVector3(TransformAnimationChannelDirectionSourceMath.NormalizeOrDefault(fallback, Vector3.forward));
            }

            var direction = TransformAnimationChannelDirectionSourceMath.ResolveDirection(resolved, space, directionAxis);
            var output = TransformAnimationChannelDirectionSourceMath.NormalizeOrDefault(direction, fallback);

            TryLog(
                $"tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, " +
                $"resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, space={space}, axis={directionAxis}, " +
                $"dir3={direction}, out={output}");

            return DynamicVariant.FromVector3(output);
        }

        void TryLog(string message)
        {
            if (!debugLogEnabled)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, debugLogEveryNFrames);
            if (_lastDebugLogFrame >= 0 && frame - _lastDebugLogFrame < interval)
                return;

            _lastDebugLogFrame = frame;
            Debug.Log($"[TransformAnimationChannelDirection3Source] {message}");
        }
    }

    [Serializable]
    public sealed class TransformAnimationChannelAngleSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TransformAnimationChannelTargetSelectMode targetSelectMode = TransformAnimationChannelTargetSelectMode.Self;

        [SerializeField, LabelText("Child Path")]
        [ShowIf(nameof(ShowChildPathField))]
        string childPath = "";

        [SerializeField, LabelText("Child Name")]
        [ShowIf(nameof(ShowChildNameField))]
        string childName = "";

        [SerializeField, LabelText("Search Recursive")]
        [ShowIf(nameof(ShowChildNameField))]
        bool childNameRecursive = true;

        [SerializeField, LabelText("Space")]
        TransformAnimationChannelPositionSpace space = TransformAnimationChannelPositionSpace.World;

        [SerializeField, LabelText("Direction Axis")]
        TransformAnimationChannelDirectionAxis directionAxis = TransformAnimationChannelDirectionAxis.Right;

        [SerializeField, LabelText("Projection Plane")]
        TransformAnimationChannelVector2Plane projectionPlane = TransformAnimationChannelVector2Plane.XY;

        [SerializeField, LabelText("Angle Unit")]
        TransformAnimationChannelAngleUnit angleUnit = TransformAnimationChannelAngleUnit.Degrees;

        [SerializeField, LabelText("Fallback")]
        float fallback;

        [SerializeField, LabelText("Debug Log")]
        bool debugLogEnabled;

        [SerializeField, LabelText("Debug Every N Frames"), MinValue(1)]
        [ShowIf(nameof(debugLogEnabled))]
        int debugLogEveryNFrames = 30;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] TransformAnimationChannelTargetResolveCache _targetCache;
        [NonSerialized] int _lastDebugLogFrame = -1;

        public string SourceTypeName => "TransformChannelAngle";
        public string GetDebugData =>
            $"{actorSource.Kind}:{channelTag} [{targetSelectMode}] ({space},{directionAxis},{projectionPlane},{angleUnit})";

        bool ShowChildPathField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildPath;
        bool ShowChildNameField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TransformAnimationChannelPositionSourceHelper.TryGetTargetTransform(
                    context,
                    actorSource,
                    channelTag,
                    targetSelectMode,
                    childPath,
                    childName,
                    childNameRecursive,
                    ref _actorCache,
                    ref _targetCache,
                    out var resolved,
                    out var root))
            {
                TryLog($"Resolve failed. actor={actorSource.Kind}, tag={channelTag}, mode={targetSelectMode}, path={childPath}, name={childName}");
                return DynamicVariant.FromFloat(fallback);
            }

            var direction3 = TransformAnimationChannelDirectionSourceMath.ResolveDirection(resolved, space, directionAxis);
            var projected = TransformAnimationChannelDirectionSourceMath.ProjectToPlane(direction3, projectionPlane);

            if (!TransformAnimationChannelDirectionSourceMath.TryNormalize(projected, out var normalized))
            {
                TryLog(
                    $"Projected direction was zero. tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, " +
                    $"root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, " +
                    $"space={space}, axis={directionAxis}, plane={projectionPlane}");
                return DynamicVariant.FromFloat(fallback);
            }

            var radians = Mathf.Atan2(normalized.y, normalized.x);
            var output = angleUnit == TransformAnimationChannelAngleUnit.Radians
                ? radians
                : radians * Mathf.Rad2Deg;

            TryLog(
                $"tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, " +
                $"resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, space={space}, axis={directionAxis}, plane={projectionPlane}, " +
                $"dir3={direction3}, projected={normalized}, unit={angleUnit}, out={output}");

            return DynamicVariant.FromFloat(output);
        }

        void TryLog(string message)
        {
            if (!debugLogEnabled)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, debugLogEveryNFrames);
            if (_lastDebugLogFrame >= 0 && frame - _lastDebugLogFrame < interval)
                return;

            _lastDebugLogFrame = frame;
            Debug.Log($"[TransformAnimationChannelAngleSource] {message}");
        }
    }

    static class TransformAnimationChannelDirectionSourceMath
    {
        const float Epsilon = 0.000001f;

        public static Vector3 ResolveDirection(
            Transform transform,
            TransformAnimationChannelPositionSpace space,
            TransformAnimationChannelDirectionAxis axis)
        {
            var rotation = space == TransformAnimationChannelPositionSpace.World
                ? transform.rotation
                : transform.localRotation;

            return axis switch
            {
                TransformAnimationChannelDirectionAxis.Right => rotation * Vector3.right,
                TransformAnimationChannelDirectionAxis.Up => rotation * Vector3.up,
                _ => rotation * Vector3.forward,
            };
        }

        public static Vector2 ProjectToPlane(Vector3 direction, TransformAnimationChannelVector2Plane plane)
        {
            return plane switch
            {
                TransformAnimationChannelVector2Plane.XZ => new Vector2(direction.x, direction.z),
                TransformAnimationChannelVector2Plane.YZ => new Vector2(direction.y, direction.z),
                _ => new Vector2(direction.x, direction.y),
            };
        }

        public static bool TryNormalize(Vector2 value, out Vector2 normalized)
        {
            if (value.sqrMagnitude <= Epsilon)
            {
                normalized = Vector2.zero;
                return false;
            }

            normalized = value.normalized;
            return true;
        }

        public static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            if (value.sqrMagnitude <= Epsilon)
            {
                normalized = Vector3.zero;
                return false;
            }

            normalized = value.normalized;
            return true;
        }

        public static Vector2 NormalizeOrDefault(Vector2 value, Vector2 fallback)
        {
            if (TryNormalize(value, out var normalized))
                return normalized;

            if (TryNormalize(fallback, out var fallbackNormalized))
                return fallbackNormalized;

            return Vector2.zero;
        }

        public static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
        {
            if (TryNormalize(value, out var normalized))
                return normalized;

            if (TryNormalize(fallback, out var fallbackNormalized))
                return fallbackNormalized;

            return Vector3.zero;
        }
    }
}