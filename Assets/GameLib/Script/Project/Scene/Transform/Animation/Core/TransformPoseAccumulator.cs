#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.TransformSystem
{
    /// <summary>
    /// 全 track の寄与を集めて priority / compose mode に従い最終 pose を解決する。
    /// struct 設計。track から WriteContribution(ref accumulator) で直接書き込む。
    /// </summary>
    public struct TransformPoseAccumulator
    {
        // 各 property ごとに Replace 系の最高 priority 寄与と additive 寄与を保持
        // WorldPosition
        public bool HasWorldPosition;
        public int WorldPositionPriority;
        public Vector3 WorldPositionValue;

        // LocalPosition
        public bool HasLocalPosition;
        public int LocalPositionPriority;
        public Vector3 LocalPositionValue;
        public Vector3 LocalPositionAdditive;

        // LocalRotation (euler)
        public bool HasLocalRotation;
        public int LocalRotationPriority;
        public Vector3 LocalRotationValue;
        public Vector3 LocalRotationAdditive;

        // LocalScale
        public bool HasLocalScale;
        public int LocalScalePriority;
        public Vector3 LocalScaleValue;
        public Vector3 LocalScaleMultiply;
        public bool HasLocalScaleMultiply;

        // AnchoredPosition
        public bool HasAnchoredPosition;
        public int AnchoredPositionPriority;
        public Vector2 AnchoredPositionValue;

        // SizeDelta
        public bool HasSizeDelta;
        public int SizeDeltaPriority;
        public Vector2 SizeDeltaValue;

        // Pivot
        public bool HasPivot;
        public int PivotPriority;
        public Vector2 PivotValue;

        public static TransformPoseAccumulator Create()
        {
            return new TransformPoseAccumulator
            {
                LocalScaleValue = Vector3.one,
                LocalScaleMultiply = Vector3.one,
            };
        }

        public void Apply(in TransformPoseContribution c)
        {
            if (!c.IsValid)
                return;

            switch (c.Property)
            {
                case TransformContributionProperty.WorldPosition:
                    ApplyWorldPosition(c);
                    break;
                case TransformContributionProperty.LocalPosition:
                    ApplyLocalPosition(c);
                    break;
                case TransformContributionProperty.LocalRotation:
                    ApplyLocalRotation(c);
                    break;
                case TransformContributionProperty.LocalScale:
                    ApplyLocalScale(c);
                    break;
                case TransformContributionProperty.AnchoredPosition:
                    ApplyAnchoredPosition(c);
                    break;
                case TransformContributionProperty.SizeDelta:
                    ApplySizeDelta(c);
                    break;
                case TransformContributionProperty.Pivot:
                    ApplyPivot(c);
                    break;
            }
        }

        void ApplyWorldPosition(in TransformPoseContribution c)
        {
            var v = c.AsVector3();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasWorldPosition || c.Priority >= WorldPositionPriority)
                {
                    HasWorldPosition = true;
                    WorldPositionPriority = c.Priority;
                    WorldPositionValue = v;
                }
            }
            else if (c.ComposeMode == TransformComposeMode.Add)
            {
                // WorldPosition additive は LocalPosition additive として扱う
                LocalPositionAdditive += v;
            }
        }

        void ApplyLocalPosition(in TransformPoseContribution c)
        {
            var v = c.AsVector3();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasLocalPosition || c.Priority >= LocalPositionPriority)
                {
                    HasLocalPosition = true;
                    LocalPositionPriority = c.Priority;
                    LocalPositionValue = v;
                }
            }
            else if (c.ComposeMode == TransformComposeMode.Add)
            {
                LocalPositionAdditive += v;
            }
        }

        void ApplyLocalRotation(in TransformPoseContribution c)
        {
            var v = c.AsVector3();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasLocalRotation || c.Priority >= LocalRotationPriority)
                {
                    HasLocalRotation = true;
                    LocalRotationPriority = c.Priority;
                    LocalRotationValue = v;
                }
            }
            else if (c.ComposeMode == TransformComposeMode.Add)
            {
                LocalRotationAdditive += v;
            }
        }

        void ApplyLocalScale(in TransformPoseContribution c)
        {
            var v = c.AsVector3();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasLocalScale || c.Priority >= LocalScalePriority)
                {
                    HasLocalScale = true;
                    LocalScalePriority = c.Priority;
                    LocalScaleValue = v;
                }
            }
            else if (c.ComposeMode == TransformComposeMode.Multiply)
            {
                HasLocalScaleMultiply = true;
                LocalScaleMultiply = Vector3.Scale(LocalScaleMultiply, v);
            }
        }

        void ApplyAnchoredPosition(in TransformPoseContribution c)
        {
            var v = c.AsVector2();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasAnchoredPosition || c.Priority >= AnchoredPositionPriority)
                {
                    HasAnchoredPosition = true;
                    AnchoredPositionPriority = c.Priority;
                    AnchoredPositionValue = v;
                }
            }
            else if (c.ComposeMode == TransformComposeMode.Add)
            {
                AnchoredPositionValue += v;
            }
        }

        void ApplySizeDelta(in TransformPoseContribution c)
        {
            var v = c.AsVector2();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasSizeDelta || c.Priority >= SizeDeltaPriority)
                {
                    HasSizeDelta = true;
                    SizeDeltaPriority = c.Priority;
                    SizeDeltaValue = v;
                }
            }
        }

        void ApplyPivot(in TransformPoseContribution c)
        {
            var v = c.AsVector2();
            if (c.ComposeMode == TransformComposeMode.Replace)
            {
                if (!HasPivot || c.Priority >= PivotPriority)
                {
                    HasPivot = true;
                    PivotPriority = c.Priority;
                    PivotValue = v;
                }
            }
        }

        /// <summary>
        /// WorldPosition と LocalPosition の競合解決。
        /// WorldPosition.Replace が存在すればそちらを優先。
        /// </summary>
        public bool ResolvePositionConflict(out bool useWorld)
        {
            if (HasWorldPosition && HasLocalPosition)
            {
                useWorld = WorldPositionPriority >= LocalPositionPriority;
                return true;
            }

            useWorld = HasWorldPosition;
            return HasWorldPosition || HasLocalPosition;
        }
    }
}
