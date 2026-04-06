#nullable enable
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// 1 つの track からの寄与情報。property / compose mode / priority を持つ。
    /// </summary>
    public struct TransformPoseContribution
    {
        public TransformContributionProperty Property;
        public TransformComposeMode ComposeMode;
        public int Priority;
        public int Layer;
        public Vector4 Value;
        public bool IsValid;

        public Vector3 AsVector3() => new Vector3(Value.x, Value.y, Value.z);
        public Vector2 AsVector2() => new Vector2(Value.x, Value.y);

        public static TransformPoseContribution WorldPosition(Vector3 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.WorldPosition,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, value.z, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution LocalPosition(Vector3 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.LocalPosition,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, value.z, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution LocalRotation(Vector3 euler, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.LocalRotation,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(euler.x, euler.y, euler.z, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution LocalScale(Vector3 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.LocalScale,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, value.z, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution AnchoredPosition(Vector2 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.AnchoredPosition,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, 0f, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution SizeDelta(Vector2 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.SizeDelta,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, 0f, 0f),
                IsValid = true,
            };
        }

        public static TransformPoseContribution Pivot(Vector2 value, TransformComposeMode mode = TransformComposeMode.Replace, int priority = 0, int layer = 0)
        {
            return new TransformPoseContribution
            {
                Property = TransformContributionProperty.Pivot,
                ComposeMode = mode,
                Priority = priority,
                Layer = layer,
                Value = new Vector4(value.x, value.y, 0f, 0f),
                IsValid = true,
            };
        }
    }
}
