#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    [Serializable]
    public sealed class DynamicCounterIntSource : IDynamicSource
    {
        [SerializeField]
        int startValue = 0;

        [SerializeField]
        int step = 1;

        [SerializeField, LabelText("Counter Key")]
        string counterKey = "";

        string _localKey = "";

        int _fallbackValue;
        bool _fallbackInitialized;

        public string SourceTypeName => "Counter";
        public string GetDebugData => $"int start={startValue} step={step}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!DynamicCounterSourceUtility.TryResolveController(context, out var controller))
                return DynamicVariant.FromInt(GetNextFallback());

            var key = DynamicCounterSourceUtility.ResolveKey(counterKey, ref _localKey, context, DynamicCounterSourceUtility.SourceKeyInt);
            var value = controller.GetNextInt(key, startValue, step);
            return DynamicVariant.FromInt(value);
        }

        int GetNextFallback()
        {
            if (!_fallbackInitialized)
            {
                _fallbackValue = startValue;
                _fallbackInitialized = true;
            }

            var value = _fallbackValue;
            _fallbackValue += step;
            return value;
        }
    }

    [Serializable]
    public sealed class DynamicCounterFloatSource : IDynamicSource
    {
        [SerializeField]
        float startValue = 0f;

        [SerializeField]
        float step = 1f;

        [SerializeField, LabelText("Counter Key")]
        string counterKey = "";

        string _localKey = "";

        float _fallbackValue;
        bool _fallbackInitialized;

        public string SourceTypeName => "Counter";
        public string GetDebugData => $"float start={startValue} step={step}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!DynamicCounterSourceUtility.TryResolveController(context, out var controller))
                return DynamicVariant.FromFloat(GetNextFallback());

            var key = DynamicCounterSourceUtility.ResolveKey(counterKey, ref _localKey, context, DynamicCounterSourceUtility.SourceKeyFloat);
            var value = controller.GetNextFloat(key, startValue, step);
            return DynamicVariant.FromFloat(value);
        }

        float GetNextFallback()
        {
            if (!_fallbackInitialized)
            {
                _fallbackValue = startValue;
                _fallbackInitialized = true;
            }

            var value = _fallbackValue;
            _fallbackValue += step;
            return value;
        }
    }

    static class DynamicCounterSourceUtility
    {
        public const string SourceKeyInt = "DynamicCounterInt";
        public const string SourceKeyFloat = "DynamicCounterFloat";

        public static bool TryResolveController(IDynamicContext context, out IDynamicCounterController controller)
        {
            controller = null!;
            var resolver = context?.Scope?.Resolver;
            return resolver != null && resolver.TryResolve(out controller) && controller != null;
        }

        public static string ResolveKey(string explicitKey, ref string localKey, IDynamicContext context, string sourceType)
        {
            if (!string.IsNullOrWhiteSpace(explicitKey))
                return explicitKey;

            if (string.IsNullOrWhiteSpace(localKey))
                localKey = Guid.NewGuid().ToString("N");

            var scopeId = context?.Scope?.Identity?.Id;
            if (string.IsNullOrWhiteSpace(scopeId))
                scopeId = context?.Scope?.Kind.ToString() ?? "none";

            return $"{scopeId}:{sourceType}:{localKey}";
        }
    }
}
