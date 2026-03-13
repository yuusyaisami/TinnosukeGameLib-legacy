#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Specifies a literal default value for a <see cref="DynamicValue{T}"/> field.
    /// The Odin drawers respect this attribute when the literal source is selected, so the field
    /// can start from a meaningful value instead of the type's zero value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DynamicValueDefaultLiteralAttribute : Attribute
    {
        public LiteralSource.LiteralType LiteralType { get; }
        public object Value { get; }

        public DynamicValueDefaultLiteralAttribute(int value)
        {
            LiteralType = LiteralSource.LiteralType.Int;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(float value)
        {
            LiteralType = LiteralSource.LiteralType.Float;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(bool value)
        {
            LiteralType = LiteralSource.LiteralType.Bool;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(string value)
        {
            LiteralType = LiteralSource.LiteralType.String;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(Vector2 value)
        {
            LiteralType = LiteralSource.LiteralType.Vector2;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(Vector3 value)
        {
            LiteralType = LiteralSource.LiteralType.Vector3;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(Vector4 value)
        {
            LiteralType = LiteralSource.LiteralType.Vector4;
            Value = value;
        }

        public DynamicValueDefaultLiteralAttribute(Color value)
        {
            LiteralType = LiteralSource.LiteralType.Color;
            Value = value;
        }
    }

    internal static class DynamicValueDefaultLiteralHelper
    {
        static readonly Dictionary<Type, LiteralSource.LiteralType> SourceTypeMap = new()
        {
            { typeof(LiteralIntSource), LiteralSource.LiteralType.Int },
            { typeof(LiteralFloatSource), LiteralSource.LiteralType.Float },
            { typeof(LiteralBoolSource), LiteralSource.LiteralType.Bool },
            { typeof(LiteralStringSource), LiteralSource.LiteralType.String },
            { typeof(LiteralVector2Source), LiteralSource.LiteralType.Vector2 },
            { typeof(LiteralVector3Source), LiteralSource.LiteralType.Vector3 },
            { typeof(LiteralVector4Source), LiteralSource.LiteralType.Vector4 },
            { typeof(LiteralColorSource), LiteralSource.LiteralType.Color },
        };

        public static IDynamicSource? CreateFromAttribute(Type sourceType, DynamicValueDefaultLiteralAttribute? attribute)
        {
            if (attribute == null)
                return null;

            if (!SourceTypeMap.TryGetValue(sourceType, out var literalType))
                return null;

            if (literalType != attribute.LiteralType)
                return null;

            var instance = Activator.CreateInstance(sourceType) as IDynamicSource;
            if (instance == null)
                return null;

            SetValue(instance, attribute.Value);
            return instance;
        }

        static void SetValue(IDynamicSource source, object value)
        {
            var field = source.GetType().GetField("value", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                return;

            field.SetValue(source, value);
        }
    }
}