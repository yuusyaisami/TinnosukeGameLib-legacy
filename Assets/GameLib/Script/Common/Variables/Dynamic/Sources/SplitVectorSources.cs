#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Builds Vector2 from separate DynamicValue&lt;float&gt; sources (X/Y).
    /// Supports partial registration by falling back to a default value per axis.
    /// </summary>
    [Serializable]
    public sealed class SplitVector2Source : IDynamicSource
    {
        [SerializeField, LabelText("Use X")]
        bool useX = true;

        [SerializeField, ShowIf(nameof(useX)), LabelText("X")]
        [DynamicValueDefaultLiteral(0f)]
        DynamicValue<float> x;

        [SerializeField, LabelText("Use Y")]
        bool useY = true;

        [SerializeField, ShowIf(nameof(useY)), LabelText("Y")]
        [DynamicValueDefaultLiteral(0f)]
        DynamicValue<float> y;

        [SerializeField, LabelText("Fallback XY")]
        Vector2 fallback = Vector2.zero;

        public string SourceTypeName => "SplitVec2";
        public string GetDebugData => $"useX={useX}, useY={useY}, fallback=({fallback.x},{fallback.y})";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var vx = useX ? x.GetOrDefault(context, fallback.x) : fallback.x;
            var vy = useY ? y.GetOrDefault(context, fallback.y) : fallback.y;
            return DynamicVariant.FromVector2(new Vector2(vx, vy));
        }
    }

    /// <summary>
    /// Builds Vector3 from separate DynamicValue&lt;float&gt; sources (X/Y/Z).
    /// Supports partial registration by falling back to a default value per axis.
    /// </summary>
    [Serializable]
    public sealed class SplitVector3Source : IDynamicSource
    {
        [SerializeField, LabelText("Use X")]
        bool useX = true;

        [SerializeField, ShowIf(nameof(useX)), LabelText("X")]
        [DynamicValueDefaultLiteral(0f)]
        DynamicValue<float> x;

        [SerializeField, LabelText("Use Y")]
        bool useY = true;

        [SerializeField, ShowIf(nameof(useY)), LabelText("Y")]
        [DynamicValueDefaultLiteral(0f)]
        DynamicValue<float> y;

        [SerializeField, LabelText("Use Z")]
        bool useZ = true;

        [SerializeField, ShowIf(nameof(useZ)), LabelText("Z")]
        [DynamicValueDefaultLiteral(0f)]
        DynamicValue<float> z;

        [SerializeField, LabelText("Fallback XYZ")]
        Vector3 fallback = Vector3.zero;

        public string SourceTypeName => "SplitVec3";
        public string GetDebugData => $"useX={useX}, useY={useY}, useZ={useZ}, fallback=({fallback.x},{fallback.y},{fallback.z})";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var vx = useX ? x.GetOrDefault(context, fallback.x) : fallback.x;
            var vy = useY ? y.GetOrDefault(context, fallback.y) : fallback.y;
            var vz = useZ ? z.GetOrDefault(context, fallback.z) : fallback.z;
            return DynamicVariant.FromVector3(new Vector3(vx, vy, vz));
        }
    }
}
