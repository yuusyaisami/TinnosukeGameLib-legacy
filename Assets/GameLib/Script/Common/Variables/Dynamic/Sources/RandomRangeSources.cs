#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
namespace Game.Common
{
    [Serializable]
    public sealed class RandomIntRangeSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        [SerializeField]
        DynamicValue<int> minInclusive = DynamicValueExtensions.FromLiteral(0);

        [SerializeField]
        DynamicValue<int> maxExclusive = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Variance")]
        [SerializeField, LabelText("Use Variance Controller")]
        bool useVarianceController;

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), LabelText("Variance Key")]
        string varianceKey = "";

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), InlineProperty, HideLabel]
        VarianceSettings varianceSettings = VarianceSettings.Default;

        public string SourceTypeName => "Random";
        public string GetDebugData => $"int [{minInclusive.SourceDebugData}, {maxExclusive.SourceDebugData})";
        public bool AllowTrackedEvaluation => false;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var min = minInclusive.GetOrDefault(context, 0);
            var max = maxExclusive.GetOrDefault(context, 1);
            if (max < min)
                (min, max) = (max, min);

            if (max == min)
                return DynamicVariant.FromInt(min);

            if (!useVarianceController)
                return DynamicVariant.FromInt(UnityEngine.Random.Range(min, max));

            if (!RandomVarianceSourceUtility.TryResolveController(context, out var controller))
                return DynamicVariant.FromInt(UnityEngine.Random.Range(min, max));

            var key = RandomVarianceSourceUtility.ResolveKey(
                varianceKey,
                context,
                RandomVarianceSourceUtility.SourceKeyInt,
                RandomVarianceSourceUtility.RangeHash(min, max));

            return DynamicVariant.FromInt(controller.GetNextInt(key, min, max, varianceSettings));
        }
    }

    [Serializable]
    public sealed class RandomFloatRangeSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        [SerializeField]
        DynamicValue<float> min = DynamicValueExtensions.FromLiteral(0f);

        [SerializeField]
        DynamicValue<float> max = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Variance")]
        [SerializeField, LabelText("Use Variance Controller")]
        bool useVarianceController;

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), LabelText("Variance Key")]
        string varianceKey = "";

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), InlineProperty, HideLabel]
        VarianceSettings varianceSettings = VarianceSettings.Default;

        public string SourceTypeName => "Random";
        public string GetDebugData => $"float [{min.SourceDebugData}, {max.SourceDebugData}]";
        public bool AllowTrackedEvaluation => false;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min.GetOrDefault(context, 0f);
            var b = max.GetOrDefault(context, 1f);
            if (b < a)
                (a, b) = (b, a);

            if (!useVarianceController)
                return DynamicVariant.FromFloat(UnityEngine.Random.Range(a, b));

            if (!RandomVarianceSourceUtility.TryResolveController(context, out var controller))
                return DynamicVariant.FromFloat(UnityEngine.Random.Range(a, b));

            var key = RandomVarianceSourceUtility.ResolveKey(
                varianceKey,
                context,
                RandomVarianceSourceUtility.SourceKeyFloat,
                RandomVarianceSourceUtility.RangeHash(a, b));

            return DynamicVariant.FromFloat(controller.GetNextFloat(key, a, b, varianceSettings));
        }
    }

    [Serializable]
    public sealed class RandomBoolSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        [SerializeField, Range(0f, 1f)]
        DynamicValue<float> trueProbability = DynamicValueExtensions.FromLiteral(0.5f);

        public string SourceTypeName => "Random";
        public string GetDebugData => $"bool p={trueProbability.SourceDebugData}";
        public bool AllowTrackedEvaluation => false;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var p = Mathf.Clamp01(trueProbability.GetOrDefault(context, 0.5f));
            return DynamicVariant.FromBool(UnityEngine.Random.value < p);
        }
    }

    [Serializable]
    public sealed class RandomVector2RangeSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        public enum Vector2RandomMode
        {
            Simple = 0,
            ConeLayered = 1,
        }

        [Serializable]
        public sealed class ConeLayer
        {
            [LabelText("Enabled")]
            public DynamicValue<bool> enabled = DynamicValueExtensions.FromLiteral(true);

            [LabelText("Weight"), MinValue(0f)]
            public DynamicValue<float> weight = DynamicValueExtensions.FromLiteral(1f);

            [LabelText("Direction")]
            public DynamicValue<Vector2> direction = DynamicValueExtensions.FromLiteral(Vector2.up);

            [LabelText("Cone Half Angle (deg)"), Range(0f, 180f)]
            public DynamicValue<float> halfAngleDeg = DynamicValueExtensions.FromLiteral(30f);

            [LabelText("Distance Min")]
            public DynamicValue<float> minDistance = DynamicValueExtensions.FromLiteral(0f);

            [LabelText("Distance Max")]
            public DynamicValue<float> maxDistance = DynamicValueExtensions.FromLiteral(1f);
        }

        [SerializeField, LabelText("Mode")]
        Vector2RandomMode mode = Vector2RandomMode.Simple;

        [SerializeField]
        [ShowIf(nameof(IsSimpleMode))]
        DynamicValue<Vector2> min = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [SerializeField]
        [ShowIf(nameof(IsSimpleMode))]
        DynamicValue<Vector2> max = DynamicValueExtensions.FromLiteral(Vector2.one);

        [SerializeField]
        [ShowIf(nameof(IsConeMode))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        List<ConeLayer> coneLayers = new() { new ConeLayer() };

        [BoxGroup("Variance")]
        [SerializeField, LabelText("Use Variance Controller")]
        bool useVarianceController;

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), LabelText("Variance Key")]
        string varianceKey = "";

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), InlineProperty, HideLabel]
        VarianceSettings varianceSettings = VarianceSettings.Default;

        public string SourceTypeName => "Random";
        public string GetDebugData => mode == Vector2RandomMode.Simple
            ? $"Vector2[{mode}] [{min.SourceDebugData}..{max.SourceDebugData}]"
            : $"Vector2[{mode}] Layers={coneLayers?.Count ?? 0}";
        public bool AllowTrackedEvaluation => false;

        bool IsSimpleMode => mode == Vector2RandomMode.Simple;
        bool IsConeMode => mode == Vector2RandomMode.ConeLayered;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (mode == Vector2RandomMode.ConeLayered)
            {
                return DynamicVariant.FromVector2(EvaluateConeLayered(context));
            }

            return DynamicVariant.FromVector2(EvaluateSimple(context));
        }

        Vector2 EvaluateSimple(IDynamicContext context)
        {
            var minValueRaw = min.GetOrDefault(context, Vector2.zero);
            var maxValueRaw = max.GetOrDefault(context, Vector2.one);
            var xMin = Mathf.Min(minValueRaw.x, maxValueRaw.x);
            var xMax = Mathf.Max(minValueRaw.x, maxValueRaw.x);
            var yMin = Mathf.Min(minValueRaw.y, maxValueRaw.y);
            var yMax = Mathf.Max(minValueRaw.y, maxValueRaw.y);

            if (!TryGetController(context, out var controller))
            {
                return new Vector2(
                    UnityEngine.Random.Range(xMin, xMax),
                    UnityEngine.Random.Range(yMin, yMax));
            }

            var minValue = new Vector2(xMin, yMin);
            var maxValue = new Vector2(xMax, yMax);
            var key = RandomVarianceSourceUtility.ResolveKey(
                varianceKey,
                context,
                RandomVarianceSourceUtility.SourceKeyVector2,
                RandomVarianceSourceUtility.RangeHash(minValue, maxValue));

            return controller.GetNextVector2(key, minValue, maxValue, varianceSettings);
        }

        Vector2 EvaluateConeLayered(IDynamicContext context)
        {
            var layers = coneLayers;
            if (layers == null || layers.Count == 0)
                return Vector2.zero;

            var totalWeight = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null)
                    continue;

                var enabled = layer.enabled.GetOrDefault(context, true);
                var weight = layer.weight.GetOrDefault(context, 1f);
                if (!enabled || weight <= 0f)
                    continue;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return Vector2.zero;

            var layerPick = GetRandomFloat(context, "cone:layerPick", 0f, totalWeight);
            ConeLayer? selected = null;
            var selectedIndex = -1;
            var accum = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null)
                    continue;

                var enabled = layer.enabled.GetOrDefault(context, true);
                var weight = layer.weight.GetOrDefault(context, 1f);
                if (!enabled || weight <= 0f)
                    continue;

                accum += weight;
                if (layerPick <= accum)
                {
                    selected = layer;
                    selectedIndex = i;
                    break;
                }
            }

            if (selected == null)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer != null &&
                        layer.enabled.GetOrDefault(context, true) &&
                        layer.weight.GetOrDefault(context, 1f) > 0f)
                    {
                        selected = layer;
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selected == null)
                return Vector2.zero;

            var selectedDirection = selected.direction.GetOrDefault(context, Vector2.up);
            var dir = selectedDirection.sqrMagnitude > 0.0001f
                ? selectedDirection.normalized
                : Vector2.up;

            var baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var spread = Mathf.Abs(selected.halfAngleDeg.GetOrDefault(context, 30f));
            var angleOffset = GetRandomFloat(context, $"cone:angle:{selectedIndex}", -spread, spread);
            var angleRad = (baseAngle + angleOffset) * Mathf.Deg2Rad;

            var minDistance = selected.minDistance.GetOrDefault(context, 0f);
            var maxDistance = selected.maxDistance.GetOrDefault(context, 1f);
            var distMin = Mathf.Min(minDistance, maxDistance);
            var distMax = Mathf.Max(minDistance, maxDistance);
            var distance = GetRandomFloat(context, $"cone:distance:{selectedIndex}", distMin, distMax);

            return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * distance;
        }

        bool TryGetController(IDynamicContext context, out IRandomVarianceController controller)
        {
            if (!useVarianceController)
            {
                controller = null!;
                return false;
            }

            return RandomVarianceSourceUtility.TryResolveController(context, out controller);
        }

        float GetRandomFloat(IDynamicContext context, string suffix, float minValue, float maxValue)
        {
            var a = Mathf.Min(minValue, maxValue);
            var b = Mathf.Max(minValue, maxValue);

            if (!TryGetController(context, out var controller))
                return UnityEngine.Random.Range(a, b);

            var hash = RandomVarianceSourceUtility.RangeHash(a, b);
            var sourceKey = $"{RandomVarianceSourceUtility.SourceKeyVector2}:{mode}:{suffix}";
            var baseKey = RandomVarianceSourceUtility.ResolveKey(varianceKey, context, sourceKey, hash);
            return controller.GetNextFloat(baseKey, a, b, varianceSettings);
        }
    }

    [Serializable]
    public sealed class RandomVector3RangeSource : IDynamicSource
    {
        [SerializeField]
        DynamicValue<Vector3> min = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField]
        DynamicValue<Vector3> max = DynamicValueExtensions.FromLiteral(Vector3.one);

        [BoxGroup("Variance")]
        [SerializeField, LabelText("Use Variance Controller")]
        bool useVarianceController;

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), LabelText("Variance Key")]
        string varianceKey = "";

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), InlineProperty, HideLabel]
        VarianceSettings varianceSettings = VarianceSettings.Default;

        public string SourceTypeName => "Random";
        public string GetDebugData => $"Vector3 [{min.SourceDebugData}..{max.SourceDebugData}]";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min.GetOrDefault(context, Vector3.zero);
            var b = max.GetOrDefault(context, Vector3.one);
            var xMin = Mathf.Min(a.x, b.x);
            var xMax = Mathf.Max(a.x, b.x);
            var yMin = Mathf.Min(a.y, b.y);
            var yMax = Mathf.Max(a.y, b.y);
            var zMin = Mathf.Min(a.z, b.z);
            var zMax = Mathf.Max(a.z, b.z);

            if (!useVarianceController)
            {
                return DynamicVariant.FromVector3(new Vector3(
                    UnityEngine.Random.Range(xMin, xMax),
                    UnityEngine.Random.Range(yMin, yMax),
                    UnityEngine.Random.Range(zMin, zMax)));
            }

            if (!RandomVarianceSourceUtility.TryResolveController(context, out var controller))
            {
                return DynamicVariant.FromVector3(new Vector3(
                    UnityEngine.Random.Range(xMin, xMax),
                    UnityEngine.Random.Range(yMin, yMax),
                    UnityEngine.Random.Range(zMin, zMax)));
            }

            var minValue = new Vector3(xMin, yMin, zMin);
            var maxValue = new Vector3(xMax, yMax, zMax);
            var key = RandomVarianceSourceUtility.ResolveKey(
                varianceKey,
                context,
                RandomVarianceSourceUtility.SourceKeyVector3,
                RandomVarianceSourceUtility.RangeHash(minValue, maxValue));

            return DynamicVariant.FromVector3(controller.GetNextVector3(key, minValue, maxValue, varianceSettings));
        }
    }

    [Serializable]
    public sealed class RandomVector4RangeSource : IDynamicSource
    {
        [SerializeField]
        DynamicValue<Vector4> min = DynamicValue<Vector4>.FromSource(new LiteralVector4Source(Vector4.zero));

        [SerializeField]
        DynamicValue<Vector4> max = DynamicValue<Vector4>.FromSource(new LiteralVector4Source(Vector4.one));

        [BoxGroup("Variance")]
        [SerializeField, LabelText("Use Variance Controller")]
        bool useVarianceController;

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), LabelText("Variance Key")]
        string varianceKey = "";

        [BoxGroup("Variance")]
        [SerializeField, ShowIf(nameof(useVarianceController)), InlineProperty, HideLabel]
        VarianceSettings varianceSettings = VarianceSettings.Default;

        public string SourceTypeName => "Random";
        public string GetDebugData => $"Vector4 [{min.SourceDebugData}..{max.SourceDebugData}]";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min.GetOrDefault(context, Vector4.zero);
            var b = max.GetOrDefault(context, Vector4.one);
            var xMin = Mathf.Min(a.x, b.x);
            var xMax = Mathf.Max(a.x, b.x);
            var yMin = Mathf.Min(a.y, b.y);
            var yMax = Mathf.Max(a.y, b.y);
            var zMin = Mathf.Min(a.z, b.z);
            var zMax = Mathf.Max(a.z, b.z);
            var wMin = Mathf.Min(a.w, b.w);
            var wMax = Mathf.Max(a.w, b.w);

            if (!useVarianceController)
            {
                return DynamicVariant.FromVector4(new Vector4(
                    UnityEngine.Random.Range(xMin, xMax),
                    UnityEngine.Random.Range(yMin, yMax),
                    UnityEngine.Random.Range(zMin, zMax),
                    UnityEngine.Random.Range(wMin, wMax)));
            }

            if (!RandomVarianceSourceUtility.TryResolveController(context, out var controller))
            {
                return DynamicVariant.FromVector4(new Vector4(
                    UnityEngine.Random.Range(xMin, xMax),
                    UnityEngine.Random.Range(yMin, yMax),
                    UnityEngine.Random.Range(zMin, zMax),
                    UnityEngine.Random.Range(wMin, wMax)));
            }

            var minValue = new Vector4(xMin, yMin, zMin, wMin);
            var maxValue = new Vector4(xMax, yMax, zMax, wMax);
            var key = RandomVarianceSourceUtility.ResolveKey(
                varianceKey,
                context,
                RandomVarianceSourceUtility.SourceKeyVector4,
                RandomVarianceSourceUtility.RangeHash(minValue, maxValue));

            return DynamicVariant.FromVector4(controller.GetNextVector4(key, minValue, maxValue, varianceSettings));
        }
    }

    static class RandomVarianceSourceUtility
    {
        public const string SourceKeyInt = "RandomIntRange";
        public const string SourceKeyFloat = "RandomFloatRange";
        public const string SourceKeyVector2 = "RandomVector2Range";
        public const string SourceKeyVector3 = "RandomVector3Range";
        public const string SourceKeyVector4 = "RandomVector4Range";

        public static bool TryResolveController(IDynamicContext context, out IRandomVarianceController controller)
        {
            controller = null!;
            var resolver = context?.Scope?.Resolver;
            return resolver != null && resolver.TryResolve(out controller) && controller != null;
        }

        public static string ResolveKey(string explicitKey, IDynamicContext context, string sourceType, int rangeHash)
        {
            if (!string.IsNullOrWhiteSpace(explicitKey))
                return explicitKey;

            var scopeId = context?.Scope?.Identity?.Id;
            if (string.IsNullOrWhiteSpace(scopeId))
                scopeId = context?.Scope?.Kind.ToString() ?? "none";

            return $"{scopeId}:{sourceType}:{rangeHash}";
        }

        public static int RangeHash(int min, int max)
        {
            var hash = 17;
            hash = CombineHash(hash, min);
            hash = CombineHash(hash, max);
            return hash;
        }

        public static int RangeHash(float min, float max)
        {
            var hash = 17;
            hash = CombineHash(hash, min.GetHashCode());
            hash = CombineHash(hash, max.GetHashCode());
            return hash;
        }

        public static int RangeHash(Vector2 min, Vector2 max)
        {
            var hash = 17;
            hash = CombineHash(hash, min.x.GetHashCode());
            hash = CombineHash(hash, min.y.GetHashCode());
            hash = CombineHash(hash, max.x.GetHashCode());
            hash = CombineHash(hash, max.y.GetHashCode());
            return hash;
        }

        public static int RangeHash(Vector3 min, Vector3 max)
        {
            var hash = 17;
            hash = CombineHash(hash, min.x.GetHashCode());
            hash = CombineHash(hash, min.y.GetHashCode());
            hash = CombineHash(hash, min.z.GetHashCode());
            hash = CombineHash(hash, max.x.GetHashCode());
            hash = CombineHash(hash, max.y.GetHashCode());
            hash = CombineHash(hash, max.z.GetHashCode());
            return hash;
        }

        public static int RangeHash(Vector4 min, Vector4 max)
        {
            var hash = 17;
            hash = CombineHash(hash, min.x.GetHashCode());
            hash = CombineHash(hash, min.y.GetHashCode());
            hash = CombineHash(hash, min.z.GetHashCode());
            hash = CombineHash(hash, min.w.GetHashCode());
            hash = CombineHash(hash, max.x.GetHashCode());
            hash = CombineHash(hash, max.y.GetHashCode());
            hash = CombineHash(hash, max.z.GetHashCode());
            hash = CombineHash(hash, max.w.GetHashCode());
            return hash;
        }

        static int CombineHash(int seed, int value)
            => unchecked(seed * 31 + value);
    }
}
