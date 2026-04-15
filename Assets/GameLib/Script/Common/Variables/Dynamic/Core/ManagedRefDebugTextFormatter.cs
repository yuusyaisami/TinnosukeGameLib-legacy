#nullable enable
using System;
using System.Reflection;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// ManagedRef のデバッグ表示を型ごとに上書きするための契約。
    /// </summary>
    public interface IDynamicManagedRefDebugText
    {
        string GetManagedRefDebugText();
    }

    /// <summary>
    /// CommandDebug / Runtimeログ向けの ManagedRef 文字列化ユーティリティ。
    /// </summary>
    public static class ManagedRefDebugTextFormatter
    {
        static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

        public static string Format(object? value)
        {
            if (value == null)
                return "null";

            if (value is IDynamicManagedRefDebugText contract)
            {
                var text = contract.GetManagedRefDebugText();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            if (TryGetDefinitionId(value, out var definitionId))
                return definitionId;

            if (value is Table table)
                return $"Table(rows={table.RowCount})";

            if (value is UnityEngine.Object unityObject)
                return string.IsNullOrWhiteSpace(unityObject.name) ? unityObject.GetType().Name : unityObject.name;

            var textFromToString = value.ToString();
            if (!string.IsNullOrWhiteSpace(textFromToString))
                return textFromToString;

            return value.GetType().Name;
        }

        static bool TryGetDefinitionId(object value, out string definitionId)
        {
            definitionId = string.Empty;

            var property = value.GetType().GetProperty("DefinitionId", PublicInstance);
            if (property == null || property.PropertyType != typeof(string) || !property.CanRead)
                return false;

            var raw = property.GetValue(value) as string;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            definitionId = raw;
            return true;
        }
    }
}