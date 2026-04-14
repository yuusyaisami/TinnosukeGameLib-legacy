#if UNITY_EDITOR
#nullable enable

using System;
using System.Reflection;
using Game.DI;
using Game.Fire;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Fire.Editor
{
    internal static class FirePatternInspectorLabelUtility
    {
        public static string Build(FireDefinition? definition)
        {
            if (definition == null)
                return "FireDefinition (null)";

            return definition switch
            {
                CircleFireDefinition value => BuildCircle(value),
                BurstFireDefinition value => BuildBurst(value),
                SpreadFireDefinition value => BuildSpread(value),
                SpiralFireDefinition value => BuildSpiral(value),
                FlowerFireDefinition value => BuildFlower(value),
                StarBurstFireDefinition value => BuildStarBurst(value),
                _ => definition.GetType().Name,
            };
        }

        public static string Build(BaseFirePattern? pattern)
        {
            if (pattern == null)
                return "BaseFirePattern (null)";

            var patternId = string.IsNullOrWhiteSpace(pattern.PatternId) ? pattern.DebugName : pattern.PatternId;
            var spawner = string.IsNullOrWhiteSpace(pattern.SpawnerTag)
                ? pattern.SpawnerKind.ToString()
                : $"{pattern.SpawnerKind}:{pattern.SpawnerTag}";
            var definition = Build(pattern.FireDefinition);
            return $"{patternId} | {spawner} | {definition}";
        }

        public static string Build(FirePatternRuntimeTemplatePreset? preset)
        {
            if (preset == null)
                return "FirePatternRuntimeTemplatePreset (null)";

            var templateId = string.IsNullOrEmpty(preset.TemplateId) ? preset.GetType().Name : preset.TemplateId;
            var prefab = preset.Prefab != null ? preset.Prefab.name : "null";
            var overridePattern = GetFieldValue<BaseFirePattern>(preset, "overrideFirePattern");
            var patternLabel = overridePattern != null ? Build(overridePattern) : string.Empty;

            var baseLabel = $"{templateId} | {prefab} | pool:{(preset.UsePooling ? "on" : "off")} | cat:{preset.Category}";
            if (string.IsNullOrEmpty(patternLabel))
                return baseLabel;

            return $"{baseLabel} | {patternLabel}";
        }

        static string BuildCircle(CircleFireDefinition value)
        {
            return $"Circle | count={DescribeDynamicValue(value, "_count")} radius={DescribeDynamicValue(value, "_radius")} start={DescribeDynamicValue(value, "_startAngle")} end={DescribeDynamicValue(value, "_endAngle")} includeEnd={DescribeBool(value, "_includeEndAngle")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string BuildBurst(BurstFireDefinition value)
        {
            return $"Burst | count={DescribeDynamicValue(value, "_burstCount")} interval={DescribeDynamicValue(value, "_burstInterval")} mode={DescribeEnum(value, "_angleMode")} total={DescribeDynamicValue(value, "_totalSpread")} step={DescribeDynamicValue(value, "_perShotDegrees")} center={DescribeBool(value, "_centerAligned")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string BuildSpread(SpreadFireDefinition value)
        {
            return $"Spread | count={DescribeDynamicValue(value, "_count")} spread={DescribeDynamicValue(value, "_spreadAngle")} center={DescribeBool(value, "_centerAligned")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string BuildSpiral(SpiralFireDefinition value)
        {
            return $"Spiral | count={DescribeDynamicValue(value, "_count")} turns={DescribeDynamicValue(value, "_revolutions")} radius={DescribeDynamicValue(value, "_startRadius")}→{DescribeDynamicValue(value, "_endRadius")} tangent={DescribeBool(value, "_tangentDirection")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string BuildFlower(FlowerFireDefinition value)
        {
            return $"Flower | count={DescribeDynamicValue(value, "_count")} petals={DescribeDynamicValue(value, "_petalCount")} radius={DescribeDynamicValue(value, "_radius")} amp={DescribeDynamicValue(value, "_petalAmplitude")} phase={DescribeDynamicValue(value, "_phaseOffset")} tangent={DescribeBool(value, "_tangentDirection")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string BuildStarBurst(StarBurstFireDefinition value)
        {
            return $"StarBurst | spikes={DescribeDynamicValue(value, "_spikeCount")} outer={DescribeDynamicValue(value, "_outerRadius")} inner={DescribeDynamicValue(value, "_innerRadius")} rot={DescribeDynamicValue(value, "_rotationOffset")} tangent={DescribeBool(value, "_tangentDirection")} target={DescribeEnum(value, "_targetMode")}";
        }

        static string DescribeDynamicValue(object owner, string fieldName)
        {
            var raw = GetFieldValue<object>(owner, fieldName);
            if (raw == null)
                return "null";

            var rawType = raw.GetType();
            var hasSource = GetPropertyValue<bool>(raw, "HasSource");
            var debugData = GetPropertyValue<string>(raw, "SourceDebugData");
            var sourceTypeName = GetPropertyValue<string>(raw, "SourceTypeName");

            if (!string.IsNullOrEmpty(debugData))
                return Trim(debugData, 18);

            if (hasSource && !string.IsNullOrEmpty(sourceTypeName))
                return sourceTypeName;

            return rawType.Name;
        }

        static string DescribeBool(object owner, string fieldName)
        {
            return GetFieldValue<bool>(owner, fieldName) ? "on" : "off";
        }

        static string DescribeEnum(object owner, string fieldName)
        {
            var value = GetFieldValue<object>(owner, fieldName);
            return value != null ? value.ToString() ?? string.Empty : string.Empty;
        }

        static T? GetFieldValue<T>(object owner, string fieldName)
        {
            if (owner == null)
                return default;

            var field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                return default;

            var raw = field.GetValue(owner);
            if (raw is T typed)
                return typed;

            return default;
        }

        static TValue? GetPropertyValue<TValue>(object value, string propertyName)
        {
            if (value == null)
                return default;

            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return default;

            var raw = property.GetValue(value);
            if (raw is TValue typed)
                return typed;

            return default;
        }

        static string Trim(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var singleLine = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine.Substring(0, maxLength - 3) + "...";
        }
    }
}
#endif