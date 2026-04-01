#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.Trait
{
    public enum TraitLotteryShortageMode
    {
        OutputLess = 0,
        AllowDuplicates = 10,
    }

    public enum TraitLotteryApplyMode
    {
        Append = 0,
        Replace = 10,
    }

    public readonly struct TraitLotteryRequest
    {
        public readonly IReadOnlyList<TraitDefinitionSO>? Candidates;
        public readonly int Count;
        public readonly bool AllowDuplicates;
        public readonly bool ExcludeExistingHolderTraits;
        public readonly TraitLotteryShortageMode ShortageMode;
        public readonly ITraitHolderService? DuplicateCheckHolder;
        public readonly IReadOnlyList<TraitDefinitionSO>? DuplicateAllowedTraits;

        public TraitLotteryRequest(
            IReadOnlyList<TraitDefinitionSO>? candidates,
            int count,
            bool allowDuplicates,
            bool excludeExistingHolderTraits,
            TraitLotteryShortageMode shortageMode,
            ITraitHolderService? duplicateCheckHolder,
            IReadOnlyList<TraitDefinitionSO>? duplicateAllowedTraits)
        {
            Candidates = candidates;
            Count = count;
            AllowDuplicates = allowDuplicates;
            ExcludeExistingHolderTraits = excludeExistingHolderTraits;
            ShortageMode = shortageMode;
            DuplicateCheckHolder = duplicateCheckHolder;
            DuplicateAllowedTraits = duplicateAllowedTraits;
        }
    }

    public sealed class TraitLotteryResult
    {
        static readonly IReadOnlyList<TraitDefinitionSO> Empty = Array.Empty<TraitDefinitionSO>();

        public static TraitLotteryResult None { get; } = new(Array.Empty<TraitDefinitionSO>(), false);

        public IReadOnlyList<TraitDefinitionSO> Selected { get; }
        public bool UsedDuplicateFallback { get; }
        public int Count => Selected.Count;

        public TraitLotteryResult(IReadOnlyList<TraitDefinitionSO>? selected, bool usedDuplicateFallback)
        {
            Selected = selected ?? Empty;
            UsedDuplicateFallback = usedDuplicateFallback;
        }
    }

    public interface ITraitLotteryService
    {
        TraitLotteryResult Draw(in TraitLotteryRequest request, ITraitHolderService? holder);
        int Apply(ITraitHolderService? holder, IReadOnlyList<TraitDefinitionSO>? selected, TraitLotteryApplyMode applyMode);
    }

    public sealed class TraitLotteryService : ITraitLotteryService
    {
        public TraitLotteryResult Draw(in TraitLotteryRequest request, ITraitHolderService? holder)
        {
            var requestedCount = Mathf.Max(0, request.Count);
            if (requestedCount <= 0 || request.Candidates == null || request.Candidates.Count == 0)
                return TraitLotteryResult.None;

            var allCandidates = BuildUniqueWeightedCandidates(request.Candidates);
            if (allCandidates.Count == 0)
                return TraitLotteryResult.None;

            var duplicateCheckHolder = request.DuplicateCheckHolder ?? holder;
            var filteredCandidates = request.ExcludeExistingHolderTraits && !request.AllowDuplicates
                ? FilterExistingHolderTraits(allCandidates, duplicateCheckHolder, request.DuplicateAllowedTraits)
                : allCandidates;

            if (filteredCandidates.Count == 0 && (!request.AllowDuplicates || allCandidates.Count == 0))
                return TraitLotteryResult.None;

            var selected = new List<TraitDefinitionSO>(requestedCount);
            var usedDuplicateFallback = false;

            if (request.AllowDuplicates)
            {
                DrawWithDuplicates(filteredCandidates.Count > 0 ? filteredCandidates : allCandidates, requestedCount, selected);
                return selected.Count == 0
                    ? TraitLotteryResult.None
                    : new TraitLotteryResult(selected, false);
            }

            DrawUnique(filteredCandidates, requestedCount, selected);

            if (selected.Count < requestedCount && request.ShortageMode == TraitLotteryShortageMode.AllowDuplicates)
            {
                usedDuplicateFallback = true;
                var remaining = requestedCount - selected.Count;
                DrawWithDuplicates(allCandidates, remaining, selected);
            }

            return selected.Count == 0
                ? TraitLotteryResult.None
                : new TraitLotteryResult(selected, usedDuplicateFallback);
        }

        public int Apply(ITraitHolderService? holder, IReadOnlyList<TraitDefinitionSO>? selected, TraitLotteryApplyMode applyMode)
        {
            if (holder == null || selected == null || selected.Count == 0)
                return 0;

            if (applyMode == TraitLotteryApplyMode.Replace)
                holder.Clear();

            var added = 0;
            for (var i = 0; i < selected.Count; i++)
            {
                var definition = selected[i];
                if (definition == null)
                    continue;

                if (holder.TryRegister(definition, out _))
                    added++;
            }

            return added;
        }

        static List<TraitDefinitionSO> BuildUniqueWeightedCandidates(IReadOnlyList<TraitDefinitionSO> source)
        {
            var results = new List<TraitDefinitionSO>(source.Count);
            var refs = new HashSet<TraitDefinitionSO>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < source.Count; i++)
            {
                var definition = source[i];
                if (definition == null || definition.Weight <= 0f)
                    continue;

                if (!refs.Add(definition))
                    continue;

                var definitionId = definition.DefinitionId;
                if (!string.IsNullOrEmpty(definitionId) && !ids.Add(definitionId))
                    continue;

                results.Add(definition);
            }

            return results;
        }

        static List<TraitDefinitionSO> FilterExistingHolderTraits(
            List<TraitDefinitionSO> source,
            ITraitHolderService? holder,
            IReadOnlyList<TraitDefinitionSO>? duplicateAllowedTraits)
        {
            var allowedRefs = new HashSet<TraitDefinitionSO>();
            var allowedIds = new HashSet<string>(StringComparer.Ordinal);
            BuildDuplicateAllowedSets(duplicateAllowedTraits, allowedRefs, allowedIds);

            if (holder == null || holder.Traits.Count == 0)
                return new List<TraitDefinitionSO>(source);

            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            var existingRefs = new HashSet<ITraitDefinition>();
            var traits = holder.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var definition = traits[i]?.Definition;
                if (definition == null)
                    continue;

                existingRefs.Add(definition);
                if (!string.IsNullOrEmpty(definition.DefinitionId))
                    existingIds.Add(definition.DefinitionId);
            }

            var filtered = new List<TraitDefinitionSO>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var definition = source[i];
                if (definition == null)
                    continue;

                if (allowedRefs.Contains(definition))
                {
                    filtered.Add(definition);
                    continue;
                }

                var definitionId = definition.DefinitionId;
                if (!string.IsNullOrEmpty(definitionId) && allowedIds.Contains(definitionId))
                {
                    filtered.Add(definition);
                    continue;
                }

                if (existingRefs.Contains(definition))
                    continue;

                if (!string.IsNullOrEmpty(definitionId) && existingIds.Contains(definitionId))
                    continue;

                filtered.Add(definition);
            }

            return filtered;
        }

        static void BuildDuplicateAllowedSets(
            IReadOnlyList<TraitDefinitionSO>? duplicateAllowedTraits,
            HashSet<TraitDefinitionSO> allowedRefs,
            HashSet<string> allowedIds)
        {
            if (duplicateAllowedTraits == null || duplicateAllowedTraits.Count == 0)
                return;

            for (var i = 0; i < duplicateAllowedTraits.Count; i++)
            {
                var definition = duplicateAllowedTraits[i];
                if (definition == null)
                    continue;

                allowedRefs.Add(definition);
                if (!string.IsNullOrEmpty(definition.DefinitionId))
                    allowedIds.Add(definition.DefinitionId);
            }
        }

        static void DrawUnique(List<TraitDefinitionSO> candidates, int count, List<TraitDefinitionSO> selected)
        {
            if (count <= 0 || candidates.Count == 0)
                return;

            var available = new List<TraitDefinitionSO>(candidates);
            while (selected.Count < count && available.Count > 0)
            {
                if (!WeightedRandomPicker.TryPickIndex(available, static definition => definition.Weight, out var pickedIndex) ||
                    pickedIndex < 0 ||
                    pickedIndex >= available.Count)
                {
                    break;
                }

                selected.Add(available[pickedIndex]);
                available.RemoveAt(pickedIndex);
            }
        }

        static void DrawWithDuplicates(List<TraitDefinitionSO> candidates, int count, List<TraitDefinitionSO> selected)
        {
            if (count <= 0 || candidates.Count == 0)
                return;

            while (count-- > 0)
            {
                if (!WeightedRandomPicker.TryPickIndex(candidates, static definition => definition.Weight, out var pickedIndex) ||
                    pickedIndex < 0 ||
                    pickedIndex >= candidates.Count)
                {
                    break;
                }

                selected.Add(candidates[pickedIndex]);
            }
        }
    }
}
