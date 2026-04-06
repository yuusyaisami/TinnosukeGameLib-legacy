// Game.Profile.ProfileBindingInspectorLabelUtility.cs
//
// Shared inspector label builder for IProfileValueBinding list rows.

#nullable enable

using System;
using System.Reflection;
using Game.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Profile
{
    static class ProfileBindingInspectorLabelUtility
    {
        public static string Build(IProfileValueBinding? binding)
        {
            if (binding == null)
                return "Binding (null)";

            var typeName = binding.GetType().Name;
            var valueLabel = BuildValueLabel(binding);
            return BuildLabel(typeName, valueLabel, binding.ScalarKey.Name, binding.BlackboardKey);
        }

        public static string BuildLabel(string typeName, object? value, string? scalarKeyName, int blackboardVarId)
        {
            return BuildLabel(typeName, FormatRawValue(value), scalarKeyName, blackboardVarId);
        }

        public static string BuildLabel(string typeName, string? valueLabel, string? scalarKeyName, int blackboardVarId)
        {
            var safeTypeName = string.IsNullOrWhiteSpace(typeName) ? "Binding" : typeName;
            var scalarLabel = GetLeafLabel(scalarKeyName);
            var blackboardLabel = GetBlackboardLabel(blackboardVarId);

            if (!string.IsNullOrEmpty(scalarLabel) && !string.IsNullOrEmpty(blackboardLabel))
            {
                if (string.IsNullOrEmpty(valueLabel))
                    return $"{safeTypeName} | {scalarLabel}/{blackboardLabel}";

                return $"{safeTypeName} | {valueLabel} | {scalarLabel}/{blackboardLabel}";
            }

            if (!string.IsNullOrEmpty(scalarLabel))
            {
                if (string.IsNullOrEmpty(valueLabel))
                    return $"{safeTypeName} | {scalarLabel}";

                return $"{safeTypeName} | {valueLabel} | {scalarLabel}";
            }

            if (!string.IsNullOrEmpty(blackboardLabel))
            {
                if (string.IsNullOrEmpty(valueLabel))
                    return $"{safeTypeName} | {blackboardLabel}";

                return $"{safeTypeName} | {valueLabel} | {blackboardLabel}";
            }

            if (string.IsNullOrEmpty(valueLabel))
                return safeTypeName;

            return $"{safeTypeName} | {valueLabel}";
        }

        static string BuildValueLabel(IProfileValueBinding binding)
        {
            if (binding == null)
                return string.Empty;

            switch (binding)
            {
                case ProfileFloatValue v:
                    return v.Value.ToString("0.###");
                case ProfileIntValue v:
                    return v.Value.ToString();
                case ProfileBoolValue v:
                    return v.Value ? "true" : "false";
                case ProfileStringValue v:
                    return QuoteAndTrim(v.Value, 24);
                case ProfileVector2Value v:
                    return v.Value.ToString();
                case ProfileVector3Value v:
                    return v.Value.ToString();
                case ProfileVector4Value v:
                    return v.Value.ToString();
                case ProfileColorValue v:
                    return v.Value.ToString();
                case ProfileUnityObjectValue v:
                    return v.Value != null ? v.Value.name : "null";
                case ProfileDynamicValue v:
                    return v.ToString();
            }

            var type = binding.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ProfileValue<>))
            {
                var valueField = type.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                if (valueField != null)
                    return FormatRawValue(valueField.GetValue(binding));
            }

            return string.Empty;
        }

        static string FormatRawValue(object? raw)
        {
            if (raw == null)
                return "null";

            if (raw is bool b)
                return b ? "true" : "false";

            if (raw is string s)
                return QuoteAndTrim(s, 24);

            if (raw is Object obj)
                return obj != null ? obj.name : "null";

            return raw.ToString() ?? string.Empty;
        }

        static string QuoteAndTrim(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            var singleLine = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length > maxLength)
                singleLine = singleLine.Substring(0, maxLength - 3) + "...";

            return $"\"{singleLine}\"";
        }

        static string GetBlackboardLabel(int varId)
        {
            if (varId == 0)
                return string.Empty;

            if (VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
                return GetLeafLabel(stableKey);

            return $"varId:{varId}";
        }

        static string GetLeafLabel(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var lastDot = key.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < key.Length)
                return key.Substring(lastDot + 1);

            var lastSlash = key.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < key.Length)
                return key.Substring(lastSlash + 1);

            return key;
        }
    }
}