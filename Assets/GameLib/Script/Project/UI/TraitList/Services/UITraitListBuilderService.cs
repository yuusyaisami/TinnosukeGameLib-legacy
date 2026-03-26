#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Trait;
using Game.UI;
using UnityEngine;

namespace Game.UI.TraitList
{
    public interface IUITraitListBuilderService
    {
        UITraitListRuntime? CurrentRuntime { get; }

        UniTask<UITraitListRuntime?> BuildAsync(
            UITraitListProfileSO profile,
            ITraitHolderService holder,
            string holderKey,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits,
            CancellationToken ct);

        UniTask RefreshAsync(UITraitListRefreshMode mode, CancellationToken ct);
        UniTask SetRangeAsync(UITraitListRange range, bool rebuild, CancellationToken ct);
        UniTask ClearAsync(CancellationToken ct);
    }

    public sealed class UITraitListBuilderService :
        IUITraitListBuilderService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IUITransformListBuilderService<ITraitInstance, UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO> _listBuilder;
        readonly IUITraitListLayoutService _layoutService;
        readonly IUITraitListVisualizerService _visualizerService;
        readonly ICommandRunner _runner;

        readonly SemaphoreSlim _mutex = new(1, 1);
        CancellationTokenSource? _buildCts;
        UITraitListRuntime? _runtime;

        public UITraitListRuntime? CurrentRuntime => _runtime;

        public UITraitListBuilderService(
            IUITransformListBuilderService<ITraitInstance, UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO> listBuilder,
            IUITraitListLayoutService layoutService,
            IUITraitListVisualizerService visualizerService,
            ICommandRunner runner)
        {
            _listBuilder = listBuilder;
            _layoutService = layoutService;
            _visualizerService = visualizerService;
            _runner = runner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            CancelBuild();
            UniTask.Void(async () =>
            {
                try
                {
                    await ClearAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UITraitListBuilder] OnRelease clear failed: {ex.Message}");
                }
            });
        }

        public async UniTask<UITraitListRuntime?> BuildAsync(
            UITraitListProfileSO profile,
            ITraitHolderService holder,
            string holderKey,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits,
            CancellationToken ct)
        {
            if (profile == null || holder == null || parent == null || scopeParent == null)
                return null;

            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                _buildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var buildCt = _buildCts.Token;

                await ClearInternalAsync();
                return await BuildInternalAsync(profile, holder, holderKey, range, parent, scopeParent, placementService, hideVisiblePlacedTraits, buildCt);
            }
            catch (OperationCanceledException)
            {
                await ClearInternalAsync();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UITraitListBuilder] BuildAsync failed: {ex.Message}");
                return _runtime;
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask RefreshAsync(UITraitListRefreshMode mode, CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                _buildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var buildCt = _buildCts.Token;
                await RefreshInternalAsync(mode, null, buildCt);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask SetRangeAsync(UITraitListRange range, bool rebuild, CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                _buildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var buildCt = _buildCts.Token;

                if (rebuild)
                {
                    var runtime = _runtime;
                    if (runtime == null)
                        return;

                    await ClearInternalAsync();
                    await BuildInternalAsync(
                        runtime.Profile,
                        runtime.Holder,
                        runtime.HolderKey,
                        range,
                        runtime.Parent,
                        runtime.ScopeParent,
                        runtime.PlacementService,
                        runtime.HideVisiblePlacedTraits,
                        buildCt);
                }
                else
                {
                    await RefreshInternalAsync(UITraitListRefreshMode.Incremental, range, buildCt);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask ClearAsync(CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                await ClearInternalAsync();
            }
            finally
            {
                _mutex.Release();
            }
        }

        async UniTask<UITraitListRuntime?> BuildInternalAsync(
            UITraitListProfileSO profile,
            ITraitHolderService holder,
            string holderKey,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits,
            CancellationToken ct)
        {
            if (profile == null || holder == null)
                return null;

            var layoutProfile = profile.LayoutProfile;
            var visualProfile = profile.VisualizerProfile;
            if (layoutProfile == null || visualProfile == null)
            {
                Debug.LogError($"[UITraitListBuilder] Build aborted: profile is invalid. LayoutProfile={(layoutProfile != null)} VisualizerProfile={(visualProfile != null)}");
                return null;
            }

            var traits = CollectFilteredTraits(holder, holderKey, placementService, hideVisiblePlacedTraits);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var sourceCount = holder.Traits?.Count ?? 0;
            //Debug.Log(
            //    $"[UITraitListBuilder] Build start holderKey='{holderKey}' sourceCount={sourceCount} filteredCount={traits.Count} " +
            //    $"range=({range.StartIndex},{range.Count}) hideVisiblePlacedTraits={hideVisiblePlacedTraits}");
#endif
            if (!_listBuilder.TryBuildSlots(traits, range, layoutProfile, out var slots, out var normalizedRange, out var error))
            {
                Debug.LogError($"[UITraitListBuilder] Build slots failed: {error}");
                return null;
            }

            ApplyContextToSlots(slots, holderKey, normalizedRange);

            var spawned = await _listBuilder.SpawnAsync(slots, visualProfile, parent, scopeParent, _runner, ct);
            var instances = new List<UITraitListVisualInstance>(slots.Count);
            var lookup = new Dictionary<ITraitInstance, UITraitListVisualInstance>(ReferenceEqualityComparer<ITraitInstance>.Instance);

            for (int i = 0; i < spawned.Count; i++)
            {
                var instance = spawned[i];
                if (instance == null)
                    continue;

                instances.Add(instance);
                lookup[instance.Trait] = instance;
            }

            RecalculateSlotPositions(slots, instances, layoutProfile, visualProfile, ResolveLayoutRect(parent));
            for (int i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null)
                    continue;

                if (lookup.TryGetValue(slot.Trait, out var instance) && instance != null)
                    await _listBuilder.RelayoutAsync(instance, slot, layoutProfile, ct);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log(
            //    $"[UITraitListBuilder] Build complete holderKey='{holderKey}' slots={slots.Count} " +
            //    $"spawned={instances.Count} normalizedRange=({normalizedRange.StartIndex},{normalizedRange.Count})");
#endif

            var runtime = new UITraitListRuntime(
                holder,
                holderKey,
                profile,
                normalizedRange,
                parent,
                scopeParent,
                placementService,
                hideVisiblePlacedTraits,
                instances,
                lookup);
            _runtime = runtime;
            SortInstancesByListIndex(instances);
            return runtime;
        }

        async UniTask RefreshInternalAsync(
            UITraitListRefreshMode mode,
            UITraitListRange? rangeOverride,
            CancellationToken ct)
        {
            var runtime = _runtime;
            if (runtime == null)
                return;

            var profile = runtime.Profile;
            var layoutProfile = profile.LayoutProfile;
            var visualProfile = profile.VisualizerProfile;
            if (layoutProfile == null || visualProfile == null)
                return;

            var range = rangeOverride ?? runtime.Range;
            var traits = CollectFilteredTraits(runtime.Holder, runtime.HolderKey, runtime.PlacementService, runtime.HideVisiblePlacedTraits);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var sourceCount = runtime.Holder.Traits?.Count ?? 0;
            //Debug.Log(
            //    $"[UITraitListBuilder] Refresh start mode={mode} holderKey='{runtime.HolderKey}' sourceCount={sourceCount} " +
            //    $"filteredCount={traits.Count} range=({range.StartIndex},{range.Count})");
#endif
            if (!_listBuilder.TryBuildSlots(traits, range, layoutProfile, out var slots, out var normalizedRange, out var error))
            {
                Debug.LogError($"[UITraitListBuilder] Refresh slots failed: {error}");
                return;
            }

            ApplyContextToSlots(slots, runtime.HolderKey, normalizedRange);

            if (mode == UITraitListRefreshMode.FullRebuild)
            {
                await ClearInternalAsync();
                await BuildInternalAsync(
                    profile,
                    runtime.Holder,
                    runtime.HolderKey,
                    normalizedRange,
                    runtime.Parent,
                    runtime.ScopeParent,
                    runtime.PlacementService,
                    runtime.HideVisiblePlacedTraits,
                    ct);
                return;
            }

            var slotLookup = new Dictionary<ITraitInstance, UITraitListSlot>(ReferenceEqualityComparer<ITraitInstance>.Instance);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Trait != null && !slotLookup.ContainsKey(slot.Trait))
                    slotLookup.Add(slot.Trait, slot);
            }

            if (mode != UITraitListRefreshMode.LayoutOnly)
            {
                for (int i = runtime.Instances.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    var instance = runtime.Instances[i];
                    if (instance == null)
                    {
                        runtime.Instances.RemoveAt(i);
                        continue;
                    }

                    if (!slotLookup.ContainsKey(instance.Trait))
                    {
                        await _listBuilder.DespawnAsync(new[] { instance }, ct);
                        runtime.Instances.RemoveAt(i);
                        runtime.Lookup.Remove(instance.Trait);
                    }
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var slot = slots[i];
                    if (slot.Trait == null)
                        continue;

                    if (!runtime.Lookup.TryGetValue(slot.Trait, out var existing) || existing == null)
                    {
                        var spawned = await SpawnSingleAsync(slot, visualProfile, runtime.Parent, runtime.ScopeParent, ct);
                        if (spawned != null)
                        {
                            runtime.Instances.Add(spawned);
                            runtime.Lookup[slot.Trait] = spawned;
                        }
                    }
                }
            }

            RecalculateSlotPositions(slots, runtime.Instances, layoutProfile, visualProfile, ResolveLayoutRect(runtime.Parent));

            for (int i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null)
                    continue;

                if (runtime.Lookup.TryGetValue(slot.Trait, out var instance) && instance != null)
                    await _listBuilder.RelayoutAsync(instance, slot, layoutProfile, ct);
            }

            runtime.SetRange(normalizedRange);
            SortInstancesByListIndex(runtime.Instances);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log(
            //    $"[UITraitListBuilder] Refresh complete mode={mode} holderKey='{runtime.HolderKey}' slots={slots.Count} " +
            //    $"activeInstances={runtime.Instances.Count} normalizedRange=({normalizedRange.StartIndex},{normalizedRange.Count})");
#endif
        }

        async UniTask<UITraitListVisualInstance?> SpawnSingleAsync(
            UITraitListSlot slot,
            UITraitListVisualizerProfileSO visualProfile,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct)
        {
            var slots = new List<UITraitListSlot>(1) { slot };
            var spawned = await _listBuilder.SpawnAsync(slots, visualProfile, parent, scopeParent, _runner, ct);
            return spawned.Count > 0 ? spawned[0] : null;
        }

        async UniTask ClearInternalAsync()
        {
            var runtime = _runtime;
            if (runtime == null)
            {
                _runtime = null;
                return;
            }

            await _listBuilder.DespawnAsync(runtime.Instances, CancellationToken.None);
            runtime.Instances.Clear();
            runtime.Lookup.Clear();
            _runtime = null;
        }

        void CancelBuild()
        {
            _buildCts?.Cancel();
            _buildCts?.Dispose();
            _buildCts = null;
        }

        static IReadOnlyList<ITraitInstance> CollectFilteredTraits(
            ITraitHolderService holder,
            string holderKey,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits)
        {
            if (!hideVisiblePlacedTraits || placementService == null)
                return holder.Traits;

            var traits = holder.Traits;
            if (traits == null || traits.Count == 0)
                return Array.Empty<ITraitInstance>();

            var results = new List<ITraitInstance>(traits.Count);
            for (int i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null)
                    continue;

                if (placementService.TryGetPresentationState(holderKey, trait.InstanceId, out var state) &&
                    state == TraitRuntimePresentationState.Visible)
                {
                    continue;
                }

                results.Add(trait);
            }

            return results;
        }

        static void ApplyContextToSlots(List<UITraitListSlot> slots, string holderKey, UITraitListRange range)
        {
            if (slots == null || slots.Count == 0)
                return;

            var key = holderKey ?? string.Empty;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.HolderKey = key;
                slot.RangeStart = range.StartIndex;
                slot.RangeCount = range.Count;
                slots[i] = slot;
            }
        }

        static void SortInstancesByListIndex(List<UITraitListVisualInstance> instances)
        {
            if (instances == null || instances.Count <= 1)
                return;

            instances.Sort((a, b) => a.ListIndex.CompareTo(b.ListIndex));
        }

        void RecalculateSlotPositions(
            List<UITraitListSlot> slots,
            IReadOnlyList<UITraitListVisualInstance> instances,
            UITraitListLayoutProfileSO layoutProfile,
            UITraitListVisualizerProfileSO visualProfile,
            RectTransform? layoutRect)
        {
            if (slots == null || slots.Count == 0 || layoutProfile == null || visualProfile == null)
                return;

            var itemSize = ResolveLayoutItemSize(instances, visualProfile);
            _layoutService.RecalculateAnchoredPositions(slots, layoutProfile, layoutRect, itemSize);
        }

        Vector2 ResolveLayoutItemSize(
            IReadOnlyList<UITraitListVisualInstance> instances,
            UITraitListVisualizerProfileSO visualProfile)
        {
            var resolved = Vector2.zero;
            if (visualProfile == null)
                return resolved;

            if (visualProfile.OverrideSize)
            {
                resolved = new Vector2(Mathf.Max(0f, visualProfile.Width), Mathf.Max(0f, visualProfile.Height));
                if (resolved.x > 0f || resolved.y > 0f)
                    return resolved;
            }

            if (instances == null || instances.Count == 0)
                return resolved;

            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                    continue;

                if (!_visualizerService.TryResolveLayoutElementSize(instance, visualProfile, out var size))
                    continue;

                resolved.x = Mathf.Max(resolved.x, size.x);
                resolved.y = Mathf.Max(resolved.y, size.y);
            }

            return resolved;
        }

        static RectTransform? ResolveLayoutRect(Transform parent)
        {
            return parent as RectTransform;
        }
    }
}
