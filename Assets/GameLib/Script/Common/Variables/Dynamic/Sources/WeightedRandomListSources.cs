#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.DI;
using Game.Commands.VNext;
using Game.MaterialFx;

namespace Game.Common
{
    static class WeightedRandomPicker
    {
        public static bool TryPickIndex<TEntry>(IReadOnlyList<TEntry> entries, Func<TEntry, float> getWeight, out int index)
        {
            index = -1;
            if (entries == null)
                return false;

            var total = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                var w = getWeight(entries[i]);
                if (w > 0f)
                    total += w;
            }

            if (total <= 0f)
                return false;

            var r = UnityEngine.Random.value * total;
            var acc = 0f;
            var lastPositive = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                var w = getWeight(entries[i]);
                if (w <= 0f)
                    continue;

                lastPositive = i;
                acc += w;
                if (r <= acc)
                {
                    index = i;
                    return true;
                }
            }

            if (lastPositive >= 0)
            {
                index = lastPositive;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class RandomWeightedStringListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row"), HideLabel]
            public DynamicValue<string> Value;

            [HorizontalGroup("Row"), Min(0f)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "string weighted (null)" : $"string weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            return DynamicVariant.FromString(entries[i].Value.GetOrDefault(context, string.Empty) ?? string.Empty);
        }
    }

    [Serializable]
    public sealed class RandomWeightedIntListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row", Width = 0.8f), HideLabel]
            public DynamicValue<int> Value;

            [HorizontalGroup("Row"), Min(0f), LabelWidth(54)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "int weighted (null)" : $"int weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            return DynamicVariant.FromInt(entries[i].Value.GetOrDefault(context, 0));
        }
    }

    [Serializable]
    public sealed class RandomWeightedFloatListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row", Width = 0.8f), HideLabel]
            public DynamicValue<float> Value;

            [HorizontalGroup("Row"), Min(0f), LabelWidth(54)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "float weighted (null)" : $"float weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            return DynamicVariant.FromFloat(entries[i].Value.GetOrDefault(context, 0f));
        }
    }

    [Serializable]
    public sealed class RandomWeightedColorListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row", Width = 0.8f), HideLabel]
            public DynamicValue<Color> Value;

            [HorizontalGroup("Row"), Min(0f), LabelWidth(54)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "Color weighted (null)" : $"Color weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            return DynamicVariant.FromColor(entries[i].Value.GetOrDefault(context, Color.white));
        }
    }

    /// <summary>
    /// Weighted random pick for Runtime Templates.
    /// - Returns as UnityObject DynamicVariant so it works with DynamicValue&lt;BaseRuntimeTemplateSO&gt;
    ///   and also DynamicValue&lt;TDerivedTemplate&gt;.
    /// </summary>
    [Serializable]
    public sealed class RandomWeightedRuntimeTemplateListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row", Width = 0.8f), HideLabel]
            public DynamicValue<BaseRuntimeTemplateSO> Value;

            [HorizontalGroup("Row"), Min(0f), LabelWidth(54)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "Template weighted (null)" : $"Template weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            return entries[i].Value.TryGet(context, out BaseRuntimeTemplateSO? template) && template != null
                ? DynamicVariant.FromUnityObject(template)
                : DynamicVariant.Null;
        }
    }

    /// <summary>
    /// Weighted random pick for Runtime Template Presets.
    /// - Returns BaseRuntimeTemplatePreset (managed ref) for DynamicValue&lt;BaseRuntimeTemplatePreset&gt;
    ///   and derived preset types.
    /// </summary>
    [Serializable]
    public sealed class RandomWeightedRuntimeTemplatePresetListSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [HorizontalGroup("Row", Width = 0.8f), HideLabel]
            public DynamicValue<BaseRuntimeTemplatePreset> Value;

            [HorizontalGroup("Row"), Min(0f), LabelWidth(54)]
            public DynamicValue<float> Weight;
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData => entries == null ? "TemplatePreset weighted (null)" : $"TemplatePreset weighted n={entries.Count}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!WeightedRandomPicker.TryPickIndex(entries, e => Mathf.Max(0f, e.Weight.GetOrDefault(context, 0f)), out var i))
                return DynamicVariant.Null;

            var preset = entries[i].Value.TryGet(context, out BaseRuntimeTemplatePreset? resolved) ? resolved : null;
            return preset != null ? DynamicVariant.FromManagedRef(preset) : DynamicVariant.Null;
        }
    }

    /// <summary>
    /// Weighted random pick for inline MaterialFx presets.
    /// - Uses inline List&lt;MaterialFxPresetEntry&gt; (no direct SO reference).
    /// - commonMaterialFxPreset is always applied first.
    /// </summary>
    [Serializable]
    public sealed class RandomMaterialFxSource : IDynamicSource
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField, Min(0f), LabelText("Weight")]
            public float Weight;

            [SerializeField, LabelText("Preset Entries")]
            [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = true, ShowIndexLabels = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
            public List<MaterialFxPresetEntry> PresetEntries;
        }

        [SerializeField, LabelText("Context Tag")]
        string contextTag = "default";

        [SerializeField, LabelText("Clear Context First")]
        bool clearContextFirst;

        [SerializeField, LabelText("Priority")]
        int priority;

        [SerializeField, LabelText("Common MaterialFx Preset")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true, ListElementLabelName = nameof(MaterialFxPresetEntry.Key))]
        List<MaterialFxPresetEntry> commonMaterialFxPreset = new();

        [SerializeField, LabelText("Weighted Presets")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        List<Entry> entries = new();

        public string SourceTypeName => "Random";
        public string GetDebugData
        {
            get
            {
                var commonCount = commonMaterialFxPreset?.Count ?? 0;
                var weightedCount = entries?.Count ?? 0;
                return $"MaterialFx common={commonCount} weighted={weightedCount}";
            }
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            _ = context;

            var commonCount = commonMaterialFxPreset?.Count ?? 0;
            var combined = new List<MaterialFxPresetEntry>(Mathf.Max(0, commonCount));
            if (commonCount > 0)
                combined.AddRange(commonMaterialFxPreset);

            if (WeightedRandomPicker.TryPickIndex(entries, static e => e.Weight, out var index))
            {
                if (index >= 0 && index < entries.Count)
                {
                    var weightedEntries = entries[index].PresetEntries;
                    if (weightedEntries != null && weightedEntries.Count > 0)
                        combined.AddRange(weightedEntries);
                }
            }

            if (!clearContextFirst && combined.Count == 0)
                return DynamicVariant.Null;

            var payload = new MaterialFxPayload
            {
                ContextTag = contextTag ?? string.Empty,
                ClearContextFirst = clearContextFirst,
                Priority = priority,
                Entries = combined,
            };

            return DynamicVariant.FromManagedRef(payload);
        }
    }
}
