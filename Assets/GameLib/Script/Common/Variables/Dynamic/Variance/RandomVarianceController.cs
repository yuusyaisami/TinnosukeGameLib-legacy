#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game;

namespace Game.Common
{
    public enum VarianceStrategy
    {
        None = 0,
        RejectIfClose = 1,
        LowDiscrepancyWithJitter = 2,
        PushAwayBias = 3,
    }

    [Serializable]
    public struct VarianceSettings
    {
        [LabelText("Strategy")]
        public VarianceStrategy Strategy;

        [LabelText("History Size"), MinValue(1)]
        public int HistorySize;

        [LabelText("Min Distance"), MinValue(0f)]
        public float MinDistance;

        [LabelText("Min Distance Ratio"), MinValue(0f)]
        public float MinDistanceRatio;

        [LabelText("Max Retry"), MinValue(0)]
        public int MaxRetry;

        [LabelText("Jitter"), MinValue(0f)]
        public float Jitter;

        public static VarianceSettings Default => new()
        {
            Strategy = VarianceStrategy.RejectIfClose,
            HistorySize = 4,
            MinDistance = 0f,
            MinDistanceRatio = 0.15f,
            MaxRetry = 6,
            Jitter = 0.05f,
        };
    }

    public readonly struct RandomVarianceControllerOptions
    {
        public readonly int MaxKeysPerType;
        public readonly VarianceSettings DefaultSettings;

        public RandomVarianceControllerOptions(int maxKeysPerType, VarianceSettings defaultSettings)
        {
            MaxKeysPerType = maxKeysPerType;
            DefaultSettings = defaultSettings;
        }
    }

    public interface IRandomVarianceController
    {
        float GetNextFloat(string key, float min, float max, in VarianceSettings settings);
        int GetNextInt(string key, int minInclusive, int maxExclusive, in VarianceSettings settings);
        Vector2 GetNextVector2(string key, Vector2 min, Vector2 max, in VarianceSettings settings);
        Vector3 GetNextVector3(string key, Vector3 min, Vector3 max, in VarianceSettings settings);
        Vector4 GetNextVector4(string key, Vector4 min, Vector4 max, in VarianceSettings settings);
    }

    public sealed class RandomVarianceController : IRandomVarianceController, IScopeAcquireHandler, IScopeReleaseHandler
    {
        const int DefaultMaxKeysPerType = 512;

        readonly VarianceSettings _defaultSettings;
        readonly HistoryStore<float> _floatStore;
        readonly HistoryStore<int> _intStore;
        readonly HistoryStore<Vector2> _vector2Store;
        readonly HistoryStore<Vector3> _vector3Store;
        readonly HistoryStore<Vector4> _vector4Store;

        public RandomVarianceController()
            : this(new RandomVarianceControllerOptions(DefaultMaxKeysPerType, VarianceSettings.Default))
        {
        }

        public RandomVarianceController(RandomVarianceControllerOptions options)
        {
            _defaultSettings = options.DefaultSettings;
            var maxKeys = Mathf.Max(1, options.MaxKeysPerType);
            _floatStore = new HistoryStore<float>(maxKeys);
            _intStore = new HistoryStore<int>(maxKeys);
            _vector2Store = new HistoryStore<Vector2>(maxKeys);
            _vector3Store = new HistoryStore<Vector3>(maxKeys);
            _vector4Store = new HistoryStore<Vector4>(maxKeys);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ClearAll();
        }

        public float GetNextFloat(string key, float min, float max, in VarianceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return UnityEngine.Random.Range(min, max);

            if (max <= min)
                return min;

            if (settings.Strategy == VarianceStrategy.None)
                return UnityEngine.Random.Range(min, max);

            var normalized = NormalizeSettings(settings);
            var entry = _floatStore.GetOrCreate(key, normalized.HistorySize);
            return SampleFloat(min, max, normalized, entry);
        }

        public int GetNextInt(string key, int minInclusive, int maxExclusive, in VarianceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return UnityEngine.Random.Range(minInclusive, maxExclusive);

            if (maxExclusive <= minInclusive)
                return minInclusive;

            if (settings.Strategy == VarianceStrategy.None)
                return UnityEngine.Random.Range(minInclusive, maxExclusive);

            var normalized = NormalizeSettings(settings);
            var entry = _intStore.GetOrCreate(key, normalized.HistorySize);
            return SampleInt(minInclusive, maxExclusive, normalized, entry);
        }

        public Vector2 GetNextVector2(string key, Vector2 min, Vector2 max, in VarianceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return RandomVector2(min, max);

            if (min == max)
                return min;

            if (settings.Strategy == VarianceStrategy.None)
                return RandomVector2(min, max);

            var normalized = NormalizeSettings(settings);
            var entry = _vector2Store.GetOrCreate(key, normalized.HistorySize);
            return SampleVector2(min, max, normalized, entry);
        }

        public Vector3 GetNextVector3(string key, Vector3 min, Vector3 max, in VarianceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return RandomVector3(min, max);

            if (min == max)
                return min;

            if (settings.Strategy == VarianceStrategy.None)
                return RandomVector3(min, max);

            var normalized = NormalizeSettings(settings);
            var entry = _vector3Store.GetOrCreate(key, normalized.HistorySize);
            return SampleVector3(min, max, normalized, entry);
        }

        public Vector4 GetNextVector4(string key, Vector4 min, Vector4 max, in VarianceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return RandomVector4(min, max);

            if (min == max)
                return min;

            if (settings.Strategy == VarianceStrategy.None)
                return RandomVector4(min, max);

            var normalized = NormalizeSettings(settings);
            var entry = _vector4Store.GetOrCreate(key, normalized.HistorySize);
            return SampleVector4(min, max, normalized, entry);
        }

        void ClearAll()
        {
            _floatStore.Clear();
            _intStore.Clear();
            _vector2Store.Clear();
            _vector3Store.Clear();
            _vector4Store.Clear();
        }

        VarianceSettings NormalizeSettings(in VarianceSettings settings)
        {
            var normalized = settings;
            if (normalized.HistorySize <= 0)
                normalized.HistorySize = Mathf.Max(1, _defaultSettings.HistorySize);
            if (normalized.MinDistance < 0f)
                normalized.MinDistance = 0f;
            if (normalized.MinDistanceRatio < 0f)
                normalized.MinDistanceRatio = 0f;
            if (normalized.MaxRetry < 0)
                normalized.MaxRetry = 0;
            if (normalized.Jitter < 0f)
                normalized.Jitter = 0f;
            return normalized;
        }

        float SampleFloat(float min, float max, in VarianceSettings settings, HistoryStore<float>.Entry entry)
        {
            switch (settings.Strategy)
            {
                case VarianceStrategy.RejectIfClose:
                    return SampleFloatReject(min, max, settings, entry);
                case VarianceStrategy.LowDiscrepancyWithJitter:
                    return SampleFloatLowDiscrepancy(min, max, settings, entry);
                case VarianceStrategy.PushAwayBias:
                    return SampleFloatPushAway(min, max, settings, entry);
                default:
                    return UnityEngine.Random.Range(min, max);
            }
        }

        int SampleInt(int minInclusive, int maxExclusive, in VarianceSettings settings, HistoryStore<int>.Entry entry)
        {
            switch (settings.Strategy)
            {
                case VarianceStrategy.RejectIfClose:
                    return SampleIntReject(minInclusive, maxExclusive, settings, entry);
                case VarianceStrategy.LowDiscrepancyWithJitter:
                    return SampleIntLowDiscrepancy(minInclusive, maxExclusive, settings, entry);
                case VarianceStrategy.PushAwayBias:
                    return SampleIntPushAway(minInclusive, maxExclusive, settings, entry);
                default:
                    return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }
        }

        Vector2 SampleVector2(Vector2 min, Vector2 max, in VarianceSettings settings, HistoryStore<Vector2>.Entry entry)
        {
            switch (settings.Strategy)
            {
                case VarianceStrategy.RejectIfClose:
                    return SampleVector2Reject(min, max, settings, entry);
                case VarianceStrategy.LowDiscrepancyWithJitter:
                    return SampleVector2LowDiscrepancy(min, max, settings, entry);
                case VarianceStrategy.PushAwayBias:
                    return SampleVector2PushAway(min, max, settings, entry);
                default:
                    return RandomVector2(min, max);
            }
        }

        Vector3 SampleVector3(Vector3 min, Vector3 max, in VarianceSettings settings, HistoryStore<Vector3>.Entry entry)
        {
            switch (settings.Strategy)
            {
                case VarianceStrategy.RejectIfClose:
                    return SampleVector3Reject(min, max, settings, entry);
                case VarianceStrategy.LowDiscrepancyWithJitter:
                    return SampleVector3LowDiscrepancy(min, max, settings, entry);
                case VarianceStrategy.PushAwayBias:
                    return SampleVector3PushAway(min, max, settings, entry);
                default:
                    return RandomVector3(min, max);
            }
        }

        Vector4 SampleVector4(Vector4 min, Vector4 max, in VarianceSettings settings, HistoryStore<Vector4>.Entry entry)
        {
            switch (settings.Strategy)
            {
                case VarianceStrategy.RejectIfClose:
                    return SampleVector4Reject(min, max, settings, entry);
                case VarianceStrategy.LowDiscrepancyWithJitter:
                    return SampleVector4LowDiscrepancy(min, max, settings, entry);
                case VarianceStrategy.PushAwayBias:
                    return SampleVector4PushAway(min, max, settings, entry);
                default:
                    return RandomVector4(min, max);
            }
        }

        float SampleFloatReject(float min, float max, in VarianceSettings settings, HistoryStore<float>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var retries = settings.MaxRetry;
            var best = UnityEngine.Random.Range(min, max);
            var bestScore = MinDistanceToHistory(best, history);

            for (int i = 0; i < retries; i++)
            {
                var candidate = UnityEngine.Random.Range(min, max);
                var score = MinDistanceToHistory(candidate, history);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    if (bestScore >= threshold)
                        break;
                }
            }

            history.Add(best);
            return best;
        }

        int SampleIntReject(int minInclusive, int maxExclusive, in VarianceSettings settings, HistoryStore<int>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(minInclusive, maxExclusive, settings);
            var retries = settings.MaxRetry;
            var best = UnityEngine.Random.Range(minInclusive, maxExclusive);
            var bestScore = MinDistanceToHistory(best, history);

            for (int i = 0; i < retries; i++)
            {
                var candidate = UnityEngine.Random.Range(minInclusive, maxExclusive);
                var score = MinDistanceToHistory(candidate, history);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    if (bestScore >= threshold)
                        break;
                }
            }

            history.Add(best);
            return best;
        }

        Vector2 SampleVector2Reject(Vector2 min, Vector2 max, in VarianceSettings settings, HistoryStore<Vector2>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var retries = settings.MaxRetry;
            var best = RandomVector2(min, max);
            var bestScore = MinDistanceToHistory(best, history);

            for (int i = 0; i < retries; i++)
            {
                var candidate = RandomVector2(min, max);
                var score = MinDistanceToHistory(candidate, history);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    if (bestScore >= threshold)
                        break;
                }
            }

            history.Add(best);
            return best;
        }

        Vector3 SampleVector3Reject(Vector3 min, Vector3 max, in VarianceSettings settings, HistoryStore<Vector3>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var retries = settings.MaxRetry;
            var best = RandomVector3(min, max);
            var bestScore = MinDistanceToHistory(best, history);

            for (int i = 0; i < retries; i++)
            {
                var candidate = RandomVector3(min, max);
                var score = MinDistanceToHistory(candidate, history);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    if (bestScore >= threshold)
                        break;
                }
            }

            history.Add(best);
            return best;
        }

        Vector4 SampleVector4Reject(Vector4 min, Vector4 max, in VarianceSettings settings, HistoryStore<Vector4>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var retries = settings.MaxRetry;
            var best = RandomVector4(min, max);
            var bestScore = MinDistanceToHistory(best, history);

            for (int i = 0; i < retries; i++)
            {
                var candidate = RandomVector4(min, max);
                var score = MinDistanceToHistory(candidate, history);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    if (bestScore >= threshold)
                        break;
                }
            }

            history.Add(best);
            return best;
        }

        float SampleFloatLowDiscrepancy(float min, float max, in VarianceSettings settings, HistoryStore<float>.Entry entry)
        {
            var t = NextLowDiscrepancy01(entry.SequenceIndex++, 0f);
            t = ApplyJitter(t, settings.Jitter);
            var value = Mathf.Lerp(min, max, t);
            entry.History.Add(value);
            return value;
        }

        int SampleIntLowDiscrepancy(int minInclusive, int maxExclusive, in VarianceSettings settings, HistoryStore<int>.Entry entry)
        {
            var range = maxExclusive - minInclusive;
            if (range <= 0)
                return minInclusive;

            var t = NextLowDiscrepancy01(entry.SequenceIndex++, 0f);
            t = ApplyJitter(t, settings.Jitter);
            var value = minInclusive + Mathf.FloorToInt(t * range);
            if (value >= maxExclusive)
                value = maxExclusive - 1;
            entry.History.Add(value);
            return value;
        }

        Vector2 SampleVector2LowDiscrepancy(Vector2 min, Vector2 max, in VarianceSettings settings, HistoryStore<Vector2>.Entry entry)
        {
            var index = entry.SequenceIndex++;
            var x = NextLowDiscrepancy01(index, 0f);
            var y = NextLowDiscrepancy01(index, 0.5f);
            x = ApplyJitter(x, settings.Jitter);
            y = ApplyJitter(y, settings.Jitter);
            var value = new Vector2(
                Mathf.Lerp(min.x, max.x, x),
                Mathf.Lerp(min.y, max.y, y));
            entry.History.Add(value);
            return value;
        }

        Vector3 SampleVector3LowDiscrepancy(Vector3 min, Vector3 max, in VarianceSettings settings, HistoryStore<Vector3>.Entry entry)
        {
            var index = entry.SequenceIndex++;
            var x = NextLowDiscrepancy01(index, 0f);
            var y = NextLowDiscrepancy01(index, 0.33f);
            var z = NextLowDiscrepancy01(index, 0.66f);
            x = ApplyJitter(x, settings.Jitter);
            y = ApplyJitter(y, settings.Jitter);
            z = ApplyJitter(z, settings.Jitter);
            var value = new Vector3(
                Mathf.Lerp(min.x, max.x, x),
                Mathf.Lerp(min.y, max.y, y),
                Mathf.Lerp(min.z, max.z, z));
            entry.History.Add(value);
            return value;
        }

        Vector4 SampleVector4LowDiscrepancy(Vector4 min, Vector4 max, in VarianceSettings settings, HistoryStore<Vector4>.Entry entry)
        {
            var index = entry.SequenceIndex++;
            var x = NextLowDiscrepancy01(index, 0f);
            var y = NextLowDiscrepancy01(index, 0.25f);
            var z = NextLowDiscrepancy01(index, 0.5f);
            var w = NextLowDiscrepancy01(index, 0.75f);
            x = ApplyJitter(x, settings.Jitter);
            y = ApplyJitter(y, settings.Jitter);
            z = ApplyJitter(z, settings.Jitter);
            w = ApplyJitter(w, settings.Jitter);
            var value = new Vector4(
                Mathf.Lerp(min.x, max.x, x),
                Mathf.Lerp(min.y, max.y, y),
                Mathf.Lerp(min.z, max.z, z),
                Mathf.Lerp(min.w, max.w, w));
            entry.History.Add(value);
            return value;
        }

        float SampleFloatPushAway(float min, float max, in VarianceSettings settings, HistoryStore<float>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var value = UnityEngine.Random.Range(min, max);

            if (history.TryGetLast(out var last) && threshold > 0f)
            {
                var leftMax = last - threshold;
                var rightMin = last + threshold;
                var hasLeft = leftMax > min;
                var hasRight = rightMin < max;
                if (hasLeft && hasRight)
                {
                    var leftSpan = leftMax - min;
                    var rightSpan = max - rightMin;
                    if (leftSpan > rightSpan)
                        value = UnityEngine.Random.Range(min, leftMax);
                    else if (rightSpan > leftSpan)
                        value = UnityEngine.Random.Range(rightMin, max);
                    else
                        value = UnityEngine.Random.value < 0.5f ? UnityEngine.Random.Range(min, leftMax) : UnityEngine.Random.Range(rightMin, max);
                }
                else if (hasLeft)
                {
                    value = UnityEngine.Random.Range(min, leftMax);
                }
                else if (hasRight)
                {
                    value = UnityEngine.Random.Range(rightMin, max);
                }
            }

            history.Add(value);
            return value;
        }

        int SampleIntPushAway(int minInclusive, int maxExclusive, in VarianceSettings settings, HistoryStore<int>.Entry entry)
        {
            var history = entry.History;
            var threshold = Mathf.CeilToInt(GetDistanceThreshold(minInclusive, maxExclusive, settings));
            var value = UnityEngine.Random.Range(minInclusive, maxExclusive);

            if (history.TryGetLast(out var last) && threshold > 0)
            {
                var leftMaxExclusive = last - threshold + 1;
                var rightMinInclusive = last + threshold;
                var hasLeft = leftMaxExclusive > minInclusive;
                var hasRight = rightMinInclusive < maxExclusive;
                if (hasLeft && hasRight)
                {
                    var leftSpan = leftMaxExclusive - minInclusive;
                    var rightSpan = maxExclusive - rightMinInclusive;
                    if (leftSpan > rightSpan)
                        value = UnityEngine.Random.Range(minInclusive, leftMaxExclusive);
                    else if (rightSpan > leftSpan)
                        value = UnityEngine.Random.Range(rightMinInclusive, maxExclusive);
                    else
                        value = UnityEngine.Random.value < 0.5f
                            ? UnityEngine.Random.Range(minInclusive, leftMaxExclusive)
                            : UnityEngine.Random.Range(rightMinInclusive, maxExclusive);
                }
                else if (hasLeft)
                {
                    value = UnityEngine.Random.Range(minInclusive, leftMaxExclusive);
                }
                else if (hasRight)
                {
                    value = UnityEngine.Random.Range(rightMinInclusive, maxExclusive);
                }
            }

            history.Add(value);
            return value;
        }

        Vector2 SampleVector2PushAway(Vector2 min, Vector2 max, in VarianceSettings settings, HistoryStore<Vector2>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var value = RandomVector2(min, max);

            if (history.TryGetLast(out var last) && threshold > 0f)
            {
                var delta = value - last;
                var dist = delta.magnitude;
                if (dist < threshold)
                {
                    if (dist <= Mathf.Epsilon)
                        delta = max - min;
                    var dir = delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
                    value = ClampVector2(last + dir * threshold, min, max);
                }
            }

            history.Add(value);
            return value;
        }

        Vector3 SampleVector3PushAway(Vector3 min, Vector3 max, in VarianceSettings settings, HistoryStore<Vector3>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var value = RandomVector3(min, max);

            if (history.TryGetLast(out var last) && threshold > 0f)
            {
                var delta = value - last;
                var dist = delta.magnitude;
                if (dist < threshold)
                {
                    if (dist <= Mathf.Epsilon)
                        delta = max - min;
                    var dir = delta.sqrMagnitude > 0f ? delta.normalized : Vector3.right;
                    value = ClampVector3(last + dir * threshold, min, max);
                }
            }

            history.Add(value);
            return value;
        }

        Vector4 SampleVector4PushAway(Vector4 min, Vector4 max, in VarianceSettings settings, HistoryStore<Vector4>.Entry entry)
        {
            var history = entry.History;
            var threshold = GetDistanceThreshold(min, max, settings);
            var value = RandomVector4(min, max);

            if (history.TryGetLast(out var last) && threshold > 0f)
            {
                var delta = value - last;
                var dist = delta.magnitude;
                if (dist < threshold)
                {
                    if (dist <= Mathf.Epsilon)
                        delta = max - min;
                    var dir = delta.sqrMagnitude > 0f ? delta.normalized : new Vector4(1f, 0f, 0f, 0f);
                    value = ClampVector4(last + dir * threshold, min, max);
                }
            }

            history.Add(value);
            return value;
        }

        static float GetDistanceThreshold(float min, float max, in VarianceSettings settings)
        {
            var range = Mathf.Abs(max - min);
            var byRatio = range * settings.MinDistanceRatio;
            return Mathf.Max(settings.MinDistance, byRatio);
        }

        static float GetDistanceThreshold(int minInclusive, int maxExclusive, in VarianceSettings settings)
        {
            var range = Mathf.Max(0, maxExclusive - minInclusive);
            var byRatio = range * settings.MinDistanceRatio;
            return Mathf.Max(settings.MinDistance, byRatio);
        }

        static float GetDistanceThreshold(Vector2 min, Vector2 max, in VarianceSettings settings)
        {
            var range = (max - min).magnitude;
            var byRatio = range * settings.MinDistanceRatio;
            return Mathf.Max(settings.MinDistance, byRatio);
        }

        static float GetDistanceThreshold(Vector3 min, Vector3 max, in VarianceSettings settings)
        {
            var range = (max - min).magnitude;
            var byRatio = range * settings.MinDistanceRatio;
            return Mathf.Max(settings.MinDistance, byRatio);
        }

        static float GetDistanceThreshold(Vector4 min, Vector4 max, in VarianceSettings settings)
        {
            var range = (max - min).magnitude;
            var byRatio = range * settings.MinDistanceRatio;
            return Mathf.Max(settings.MinDistance, byRatio);
        }

        static float MinDistanceToHistory(float value, VarianceHistory<float> history)
        {
            if (history.Count == 0)
                return float.PositiveInfinity;

            var minDistance = float.MaxValue;
            for (int i = 0; i < history.Count; i++)
            {
                var distance = Mathf.Abs(value - history.GetValue(i));
                if (distance < minDistance)
                    minDistance = distance;
            }
            return minDistance;
        }

        static float MinDistanceToHistory(int value, VarianceHistory<int> history)
        {
            if (history.Count == 0)
                return float.PositiveInfinity;

            var minDistance = float.MaxValue;
            for (int i = 0; i < history.Count; i++)
            {
                var distance = Mathf.Abs(value - history.GetValue(i));
                if (distance < minDistance)
                    minDistance = distance;
            }
            return minDistance;
        }

        static float MinDistanceToHistory(Vector2 value, VarianceHistory<Vector2> history)
        {
            if (history.Count == 0)
                return float.PositiveInfinity;

            var minDistance = float.MaxValue;
            for (int i = 0; i < history.Count; i++)
            {
                var distance = Vector2.Distance(value, history.GetValue(i));
                if (distance < minDistance)
                    minDistance = distance;
            }
            return minDistance;
        }

        static float MinDistanceToHistory(Vector3 value, VarianceHistory<Vector3> history)
        {
            if (history.Count == 0)
                return float.PositiveInfinity;

            var minDistance = float.MaxValue;
            for (int i = 0; i < history.Count; i++)
            {
                var distance = Vector3.Distance(value, history.GetValue(i));
                if (distance < minDistance)
                    minDistance = distance;
            }
            return minDistance;
        }

        static float MinDistanceToHistory(Vector4 value, VarianceHistory<Vector4> history)
        {
            if (history.Count == 0)
                return float.PositiveInfinity;

            var minDistance = float.MaxValue;
            for (int i = 0; i < history.Count; i++)
            {
                var distance = Vector4.Distance(value, history.GetValue(i));
                if (distance < minDistance)
                    minDistance = distance;
            }
            return minDistance;
        }

        static float NextLowDiscrepancy01(int index, float offset)
        {
            const float phi = 0.61803398875f;
            var t = (index + 1) * phi + offset;
            return t - Mathf.Floor(t);
        }

        static float ApplyJitter(float t, float jitter)
        {
            if (jitter <= 0f)
                return t;

            var offset = UnityEngine.Random.Range(-jitter, jitter);
            return Mathf.Repeat(t + offset, 1f);
        }

        static Vector2 RandomVector2(Vector2 min, Vector2 max)
            => new(UnityEngine.Random.Range(min.x, max.x), UnityEngine.Random.Range(min.y, max.y));

        static Vector3 RandomVector3(Vector3 min, Vector3 max)
            => new(UnityEngine.Random.Range(min.x, max.x), UnityEngine.Random.Range(min.y, max.y), UnityEngine.Random.Range(min.z, max.z));

        static Vector4 RandomVector4(Vector4 min, Vector4 max)
            => new(UnityEngine.Random.Range(min.x, max.x), UnityEngine.Random.Range(min.y, max.y), UnityEngine.Random.Range(min.z, max.z), UnityEngine.Random.Range(min.w, max.w));

        static Vector2 ClampVector2(Vector2 value, Vector2 min, Vector2 max)
            => new(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y));

        static Vector3 ClampVector3(Vector3 value, Vector3 min, Vector3 max)
            => new(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y), Mathf.Clamp(value.z, min.z, max.z));

        static Vector4 ClampVector4(Vector4 value, Vector4 min, Vector4 max)
            => new(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y), Mathf.Clamp(value.z, min.z, max.z), Mathf.Clamp(value.w, min.w, max.w));

        sealed class VarianceHistory<T>
        {
            T[] _values = Array.Empty<T>();
            int _count;
            int _nextIndex;

            public int Count => _count;

            public VarianceHistory(int capacity)
            {
                EnsureCapacity(capacity);
            }

            public void EnsureCapacity(int capacity)
            {
                capacity = Mathf.Max(1, capacity);
                if (_values.Length == capacity)
                    return;

                _values = new T[capacity];
                _count = 0;
                _nextIndex = 0;
            }

            public void Add(T value)
            {
                if (_values.Length == 0)
                    return;

                _values[_nextIndex] = value;
                _nextIndex = (_nextIndex + 1) % _values.Length;
                if (_count < _values.Length)
                    _count++;
            }

            public bool TryGetLast(out T value)
            {
                if (_count <= 0)
                {
                    value = default!;
                    return false;
                }

                var index = _nextIndex - 1;
                if (index < 0)
                    index = _values.Length - 1;
                value = _values[index];
                return true;
            }

            public T GetValue(int index) => _values[index];
        }

        sealed class HistoryStore<T>
        {
            readonly int _maxKeys;
            readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
            readonly LinkedList<string> _lru = new();

            public HistoryStore(int maxKeys)
            {
                _maxKeys = Mathf.Max(1, maxKeys);
            }

            public Entry GetOrCreate(string key, int capacity)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    Touch(entry);
                    entry.History.EnsureCapacity(capacity);
                    return entry;
                }

                var history = new VarianceHistory<T>(capacity);
                var node = _lru.AddLast(key);
                entry = new Entry(history, node);
                _entries.Add(key, entry);
                Trim();
                return entry;
            }

            public void Clear()
            {
                _entries.Clear();
                _lru.Clear();
            }

            void Touch(Entry entry)
            {
                _lru.Remove(entry.Node);
                _lru.AddLast(entry.Node);
            }

            void Trim()
            {
                while (_entries.Count > _maxKeys)
                {
                    var oldest = _lru.First;
                    if (oldest == null)
                        return;
                    _lru.RemoveFirst();
                    _entries.Remove(oldest.Value);
                }
            }

            public sealed class Entry
            {
                public VarianceHistory<T> History { get; }
                public LinkedListNode<string> Node { get; }
                public int SequenceIndex;

                public Entry(VarianceHistory<T> history, LinkedListNode<string> node)
                {
                    History = history;
                    Node = node;
                    SequenceIndex = 0;
                }
            }
        }
    }
}
