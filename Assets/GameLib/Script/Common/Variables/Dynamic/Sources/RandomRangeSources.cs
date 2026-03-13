#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
namespace Game.Common
{
    [Serializable]
    public sealed class RandomIntRangeSource : IDynamicSource
    {
        [SerializeField]
        int minInclusive = 0;

        [SerializeField]
        int maxExclusive = 1;

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
        public string GetDebugData => $"int [{minInclusive}, {maxExclusive})";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var min = minInclusive;
            var max = maxExclusive;
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
    public sealed class RandomFloatRangeSource : IDynamicSource
    {
        [SerializeField]
        float min = 0f;

        [SerializeField]
        float max = 1f;

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
        public string GetDebugData => $"float [{min}, {max}]";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min;
            var b = max;
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
    public sealed class RandomBoolSource : IDynamicSource
    {
        [SerializeField, Range(0f, 1f)]
        float trueProbability = 0.5f;

        public string SourceTypeName => "Random";
        public string GetDebugData => $"bool p={trueProbability:0.###}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var p = Mathf.Clamp01(trueProbability);
            return DynamicVariant.FromBool(UnityEngine.Random.value < p);
        }
    }

    [Serializable]
    public sealed class RandomVector2RangeSource : IDynamicSource
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
            public bool enabled = true;

            [LabelText("Weight"), MinValue(0f)]
            public float weight = 1f;

            [LabelText("Direction")]
            public Vector2 direction = Vector2.up;

            [LabelText("Cone Half Angle (deg)"), Range(0f, 180f)]
            public float halfAngleDeg = 30f;

            [LabelText("Distance Min")]
            public float minDistance = 0f;

            [LabelText("Distance Max")]
            public float maxDistance = 1f;
        }

        [SerializeField, LabelText("Mode")]
        Vector2RandomMode mode = Vector2RandomMode.Simple;

        [SerializeField]
        [ShowIf(nameof(IsSimpleMode))]
        Vector2 min = Vector2.zero;

        [SerializeField]
        [ShowIf(nameof(IsSimpleMode))]
        Vector2 max = Vector2.one;

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
            ? $"Vector2[{mode}] [{min}..{max}]"
            : $"Vector2[{mode}] Layers={coneLayers?.Count ?? 0}";

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
            var xMin = Mathf.Min(min.x, max.x);
            var xMax = Mathf.Max(min.x, max.x);
            var yMin = Mathf.Min(min.y, max.y);
            var yMax = Mathf.Max(min.y, max.y);

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
                if (layer == null || !layer.enabled || layer.weight <= 0f)
                    continue;
                totalWeight += layer.weight;
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
                if (layer == null || !layer.enabled || layer.weight <= 0f)
                    continue;

                accum += layer.weight;
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
                    if (layer != null && layer.enabled && layer.weight > 0f)
                    {
                        selected = layer;
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selected == null)
                return Vector2.zero;

            var dir = selected.direction.sqrMagnitude > 0.0001f
                ? selected.direction.normalized
                : Vector2.up;

            var baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var spread = Mathf.Abs(selected.halfAngleDeg);
            var angleOffset = GetRandomFloat(context, $"cone:angle:{selectedIndex}", -spread, spread);
            var angleRad = (baseAngle + angleOffset) * Mathf.Deg2Rad;

            var distMin = Mathf.Min(selected.minDistance, selected.maxDistance);
            var distMax = Mathf.Max(selected.minDistance, selected.maxDistance);
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
        Vector3 min = Vector3.zero;

        [SerializeField]
        Vector3 max = Vector3.one;

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
        public string GetDebugData => $"Vector3 [{min}..{max}]";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min;
            var b = max;
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
        Vector4 min = Vector4.zero;

        [SerializeField]
        Vector4 max = Vector4.one;

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
        public string GetDebugData => $"Vector4 [{min}..{max}]";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var a = min;
            var b = max;
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
