#nullable enable
using System.Collections.Generic;
using Game;

namespace Game.Common
{
    public interface IDynamicCounterController
    {
        int GetNextInt(string key, int startValue, int step);
        float GetNextFloat(string key, float startValue, float step);
    }

    public sealed class DynamicCounterController : IDynamicCounterController, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly Dictionary<string, int> _intCounters = new();
        readonly Dictionary<string, float> _floatCounters = new();

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ClearAll();
        }

        public int GetNextInt(string key, int startValue, int step)
        {
            if (string.IsNullOrWhiteSpace(key))
                return startValue;

            if (!_intCounters.TryGetValue(key, out var current))
                current = startValue;

            _intCounters[key] = current + step;
            return current;
        }

        public float GetNextFloat(string key, float startValue, float step)
        {
            if (string.IsNullOrWhiteSpace(key))
                return startValue;

            if (!_floatCounters.TryGetValue(key, out var current))
                current = startValue;

            _floatCounters[key] = current + step;
            return current;
        }

        void ClearAll()
        {
            _intCounters.Clear();
            _floatCounters.Clear();
        }
    }
}
