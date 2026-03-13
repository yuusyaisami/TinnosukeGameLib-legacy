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
        readonly ICommandRunner _runner;

        readonly SemaphoreSlim _mutex = new(1, 1);
        CancellationTokenSource? _buildCts;
        UITraitListRuntime? _runtime;

        public UITraitListRuntime? CurrentRuntime => _runtime;

        public UITraitListBuilderService(
            IUITransformListBuilderService<ITraitInstance, UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO> listBuilder,
            ICommandRunner runner)
        {
            _listBuilder = listBuilder;
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
                return await BuildInternalAsync(profile, holder, holderKey, range, parent, scopeParent, buildCt);
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
            CancellationToken ct)
        {
            if (profile == null || holder == null)
                return null;

            var layoutProfile = profile.LayoutProfile;
            var visualProfile = profile.VisualizerProfile;
            if (layoutProfile == null || visualProfile == null)
                return null;

            if (!_listBuilder.TryBuildSlots(holder.Traits, range, layoutProfile, out var slots, out var normalizedRange, out var error))
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

            var runtime = new UITraitListRuntime(holder, holderKey, profile, normalizedRange, parent, scopeParent, instances, lookup);
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
            if (!_listBuilder.TryBuildSlots(runtime.Holder.Traits, range, layoutProfile, out var slots, out var normalizedRange, out var error))
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
    }
}
