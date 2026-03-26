#nullable enable
using System;
using Game.Common;
using UnityEngine;

namespace Game.VariableLayer
{
    [Serializable]
    public readonly struct VariableLayerValue : IEquatable<VariableLayerValue>
    {
        readonly float _floatValue;
        readonly int _intValue;
        readonly Vector4 _vectorValue;

        public ValueKind Kind { get; }

        public float FloatValue => _floatValue;
        public int IntValue => _intValue;
        public bool BoolValue => _intValue != 0;
        public Vector2 Vector2Value => new(_vectorValue.x, _vectorValue.y);
        public Vector3 Vector3Value => new(_vectorValue.x, _vectorValue.y, _vectorValue.z);
        public Vector4 Vector4Value => _vectorValue;
        public Color ColorValue => new(_vectorValue.x, _vectorValue.y, _vectorValue.z, _vectorValue.w);

        VariableLayerValue(ValueKind kind, float floatValue, int intValue, Vector4 vectorValue)
        {
            Kind = kind;
            _floatValue = floatValue;
            _intValue = intValue;
            _vectorValue = vectorValue;
        }

        public static VariableLayerValue FromFloat(float value) => new(ValueKind.Float, value, default, default);
        public static VariableLayerValue FromInt(int value) => new(ValueKind.Int, default, value, default);
        public static VariableLayerValue FromBool(bool value) => new(ValueKind.Bool, default, value ? 1 : 0, default);
        public static VariableLayerValue FromVector2(Vector2 value) => new(ValueKind.Vector2, default, default, new Vector4(value.x, value.y, 0f, 0f));
        public static VariableLayerValue FromVector3(Vector3 value) => new(ValueKind.Vector3, default, default, new Vector4(value.x, value.y, value.z, 0f));
        public static VariableLayerValue FromVector4(Vector4 value) => new(ValueKind.Vector4, default, default, value);
        public static VariableLayerValue FromColor(Color value) => new(ValueKind.Color, default, default, new Vector4(value.r, value.g, value.b, value.a));

        public static VariableLayerValue GetDefault(ValueKind kind)
        {
            return kind switch
            {
                ValueKind.Bool => FromBool(false),
                ValueKind.Int => FromInt(0),
                ValueKind.Float => FromFloat(0f),
                ValueKind.Vector2 => FromVector2(Vector2.zero),
                ValueKind.Vector3 => FromVector3(Vector3.zero),
                ValueKind.Vector4 => FromVector4(Vector4.zero),
                ValueKind.Color => FromColor(new Color(0f, 0f, 0f, 0f)),
                _ => default,
            };
        }

        public bool Equals(VariableLayerValue other)
        {
            return VariableLayerValueUtility.Approximately(this, other);
        }

        public override bool Equals(object? obj)
        {
            return obj is VariableLayerValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, _floatValue, _intValue, _vectorValue);
        }

        public override string ToString()
        {
            return Kind switch
            {
                ValueKind.Bool => BoolValue.ToString(),
                ValueKind.Int => IntValue.ToString(),
                ValueKind.Float => FloatValue.ToString("0.###"),
                ValueKind.Vector2 => Vector2Value.ToString(),
                ValueKind.Vector3 => Vector3Value.ToString(),
                ValueKind.Vector4 => Vector4Value.ToString(),
                ValueKind.Color => ColorValue.ToString(),
                _ => $"Unsupported({Kind})",
            };
        }
    }

    public static class VariableLayerValueUtility
    {
        public static VariableLayerValue Add(in VariableLayerValue baseValue, in VariableLayerValue additiveValue)
        {
            if (baseValue.Kind != additiveValue.Kind)
                return additiveValue;

            return baseValue.Kind switch
            {
                ValueKind.Bool => VariableLayerValue.FromBool(baseValue.BoolValue || additiveValue.BoolValue),
                ValueKind.Int => VariableLayerValue.FromInt(baseValue.IntValue + additiveValue.IntValue),
                ValueKind.Float => VariableLayerValue.FromFloat(baseValue.FloatValue + additiveValue.FloatValue),
                ValueKind.Vector2 => VariableLayerValue.FromVector2(baseValue.Vector2Value + additiveValue.Vector2Value),
                ValueKind.Vector3 => VariableLayerValue.FromVector3(baseValue.Vector3Value + additiveValue.Vector3Value),
                ValueKind.Vector4 => VariableLayerValue.FromVector4(baseValue.Vector4Value + additiveValue.Vector4Value),
                ValueKind.Color => VariableLayerValue.FromColor(baseValue.ColorValue + additiveValue.ColorValue),
                _ => additiveValue,
            };
        }

        public static VariableLayerValue Lerp(in VariableLayerValue from, in VariableLayerValue to, float t)
        {
            if (from.Kind != to.Kind)
                return to;

            t = Mathf.Clamp01(t);
            return from.Kind switch
            {
                ValueKind.Bool => t < 1f ? from : to,
                ValueKind.Int => VariableLayerValue.FromInt(Mathf.RoundToInt(Mathf.Lerp(from.IntValue, to.IntValue, t))),
                ValueKind.Float => VariableLayerValue.FromFloat(Mathf.Lerp(from.FloatValue, to.FloatValue, t)),
                ValueKind.Vector2 => VariableLayerValue.FromVector2(Vector2.Lerp(from.Vector2Value, to.Vector2Value, t)),
                ValueKind.Vector3 => VariableLayerValue.FromVector3(Vector3.Lerp(from.Vector3Value, to.Vector3Value, t)),
                ValueKind.Vector4 => VariableLayerValue.FromVector4(Vector4.Lerp(from.Vector4Value, to.Vector4Value, t)),
                ValueKind.Color => VariableLayerValue.FromColor(Color.Lerp(from.ColorValue, to.ColorValue, t)),
                _ => to,
            };
        }

        public static bool Approximately(in VariableLayerValue a, in VariableLayerValue b)
        {
            if (a.Kind != b.Kind)
                return false;

            return a.Kind switch
            {
                ValueKind.Bool => a.BoolValue == b.BoolValue,
                ValueKind.Int => a.IntValue == b.IntValue,
                ValueKind.Float => Mathf.Abs(a.FloatValue - b.FloatValue) <= 0.0001f,
                ValueKind.Vector2 => (a.Vector2Value - b.Vector2Value).sqrMagnitude <= 0.000001f,
                ValueKind.Vector3 => (a.Vector3Value - b.Vector3Value).sqrMagnitude <= 0.000001f,
                ValueKind.Vector4 => (a.Vector4Value - b.Vector4Value).sqrMagnitude <= 0.000001f,
                ValueKind.Color => (a.ColorValue - b.ColorValue).grayscale <= 0.0001f &&
                                   Mathf.Abs(a.ColorValue.a - b.ColorValue.a) <= 0.0001f,
                _ => false,
            };
        }
    }
}
