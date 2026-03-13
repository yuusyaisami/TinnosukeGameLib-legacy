#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.UI.TraitList;
using UnityEngine;

namespace Game.UI
{
    public interface IUITransformListLayoutService<TItem, TSlot, in TProfile>
    {
        int GetCapacity(TProfile profile);

        bool TryBuildSlots(
            IReadOnlyList<TItem> items,
            UITraitListRange range,
            TProfile profile,
            out List<TSlot> slots,
            out UITraitListRange normalizedRange,
            out string? error);
    }

    public interface IUITransformListVisualizerService<TSlot, TInstance, in TLayoutProfile, in TVisualProfile>
        where TInstance : class
    {
        UniTask<TInstance?> SpawnAsync(
            TSlot slot,
            TVisualProfile profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct);

        UniTask RelayoutAsync(
            TInstance instance,
            TSlot slot,
            TLayoutProfile layoutProfile,
            CancellationToken ct);

        UniTask DespawnAsync(TInstance instance, CancellationToken ct);
    }

    public interface IUITransformListBuilderService<TItem, TSlot, TInstance, in TLayoutProfile, in TVisualProfile>
        where TInstance : class
    {
        int GetCapacity(TLayoutProfile profile);

        bool TryBuildSlots(
            IReadOnlyList<TItem> items,
            UITraitListRange range,
            TLayoutProfile profile,
            out List<TSlot> slots,
            out UITraitListRange normalizedRange,
            out string? error);

        UniTask<List<TInstance?>> SpawnAsync(
            IReadOnlyList<TSlot> slots,
            TVisualProfile profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct);

        UniTask RelayoutAsync(
            TInstance instance,
            TSlot slot,
            TLayoutProfile layoutProfile,
            CancellationToken ct);

        UniTask DespawnAsync(IReadOnlyList<TInstance> instances, CancellationToken ct);
    }

    public sealed class UITransformListBuilderService<TItem, TSlot, TInstance, TLayoutProfile, TVisualProfile> :
        IUITransformListBuilderService<TItem, TSlot, TInstance, TLayoutProfile, TVisualProfile>,
        IScopeAcquireHandler,
        IScopeReleaseHandler
        where TInstance : class
    {
        readonly IUITransformListLayoutService<TItem, TSlot, TLayoutProfile> _layout;
        readonly IUITransformListVisualizerService<TSlot, TInstance, TLayoutProfile, TVisualProfile> _visualizer;

        public UITransformListBuilderService(
            IUITransformListLayoutService<TItem, TSlot, TLayoutProfile> layout,
            IUITransformListVisualizerService<TSlot, TInstance, TLayoutProfile, TVisualProfile> visualizer)
        {
            _layout = layout;
            _visualizer = visualizer;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
        }

        public int GetCapacity(TLayoutProfile profile)
        {
            return _layout.GetCapacity(profile);
        }

        public bool TryBuildSlots(
            IReadOnlyList<TItem> items,
            UITraitListRange range,
            TLayoutProfile profile,
            out List<TSlot> slots,
            out UITraitListRange normalizedRange,
            out string? error)
        {
            return _layout.TryBuildSlots(items, range, profile, out slots, out normalizedRange, out error);
        }

        public async UniTask<List<TInstance?>> SpawnAsync(
            IReadOnlyList<TSlot> slots,
            TVisualProfile profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct)
        {
            var results = new List<TInstance?>(slots != null ? slots.Count : 0);
            if (slots == null || slots.Count == 0)
                return results;

            for (int i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var instance = await _visualizer.SpawnAsync(slots[i], profile, parent, scopeParent, runner, ct);
                results.Add(instance);
            }

            return results;
        }

        public UniTask RelayoutAsync(
            TInstance instance,
            TSlot slot,
            TLayoutProfile layoutProfile,
            CancellationToken ct)
        {
            return _visualizer.RelayoutAsync(instance, slot, layoutProfile, ct);
        }

        public async UniTask DespawnAsync(IReadOnlyList<TInstance> instances, CancellationToken ct)
        {
            if (instances == null || instances.Count == 0)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var instance = instances[i];
                if (instance == null)
                    continue;
                await _visualizer.DespawnAsync(instance, ct);
            }
        }
    }
}
