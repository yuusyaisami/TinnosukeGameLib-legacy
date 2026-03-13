using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Common
{
    public static class BlackboardVariantExtensions
    {
        public static DynamicVariant GlobalGetVariant<T>(this IBlackboardService blackboard, int varId)
        {
            if (blackboard == null)
                return DynamicVariant.Null;

            if (!blackboard.TryGlobalGetVariant(varId, out var value))
                return DynamicVariant.Null;

            if (TryCoerceVariant<T>(value, out var coerced))
                return coerced;

            return DynamicVariant.Null;
        }

        static bool TryCoerceVariant<T>(in DynamicVariant value, out DynamicVariant coerced)
        {
            var targetType = typeof(T);
            if (typeof(Object).IsAssignableFrom(targetType))
            {
                if (value.Kind == ValueKind.UnityObject)
                {
                    var unityObject = value.AsUnityObject;
                    if (unityObject != null && targetType.IsInstanceOfType(unityObject))
                    {
                        coerced = value;
                        return true;
                    }
                }

                coerced = DynamicVariant.Null;
                return false;
            }

            if (TryGetExpectedKind(targetType, out var expectedKind))
                return VarStore.TryCoerceVariant(expectedKind, value, out coerced, logOnFailure: false);

            coerced = DynamicVariant.Null;
            return false;
        }

        static bool TryGetExpectedKind(Type targetType, out ValueKind expectedKind)
        {
            if (targetType == typeof(bool))
            {
                expectedKind = ValueKind.Bool;
                return true;
            }
            if (targetType == typeof(int))
            {
                expectedKind = ValueKind.Int;
                return true;
            }
            if (targetType == typeof(float))
            {
                expectedKind = ValueKind.Float;
                return true;
            }
            if (targetType == typeof(string))
            {
                expectedKind = ValueKind.String;
                return true;
            }
            if (targetType == typeof(Vector2))
            {
                expectedKind = ValueKind.Vector2;
                return true;
            }
            if (targetType == typeof(Vector3))
            {
                expectedKind = ValueKind.Vector3;
                return true;
            }
            if (targetType == typeof(Vector4))
            {
                expectedKind = ValueKind.Vector4;
                return true;
            }
            if (targetType == typeof(Color))
            {
                expectedKind = ValueKind.Color;
                return true;
            }

            expectedKind = ValueKind.Null;
            return false;
        }
    }
}
