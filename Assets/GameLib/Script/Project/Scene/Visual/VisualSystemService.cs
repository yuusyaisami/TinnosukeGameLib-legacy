#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Common;
using Game.Commands.VNext;
using Game.MaterialFx;
using UnityEngine;
using VContainer;

namespace Game.Visual
{
    /// <summary>
    /// VisualSystem v1.
    /// - Hub 登録/解除
    /// - selector ごとの StateSlot を保持
    /// - SetState は保持＆新規Hubへ同期
    /// - Broadcast は保持しない
    /// </summary>
    public sealed class VisualSystemService : IVisualSystem, IScopeAcquireHandler, IScopeReleaseHandler
    {
        sealed class RefEqComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly RefEqComparer<T> Instance = new();
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        sealed class StateSlot
        {
            public MaterialFxPresetEntry[] Entries = Array.Empty<MaterialFxPresetEntry>();
            public bool ClearMissingKeys;
            public int BasePriority;
            public int Hash;
        }

        readonly HashSet<IVisualHub> _hubs = new(RefEqComparer<IVisualHub>.Instance);

        // selector -> snapshot
        readonly Dictionary<VisualTargetSelector, StateSlot> _stateSlots = new();

        readonly List<KeyValuePair<VisualTargetSelector, StateSlot>> _sortedSlotsScratch = new();
        readonly DynamicValue<MaterialFxPayload> _defaultMaterialFxSource;

        public VisualSystemService(DynamicValue<MaterialFxPayload> defaultMaterialFxSource = default)
        {
            _defaultMaterialFxSource = defaultMaterialFxSource;
        }

        // ----------------------------------------------------------------------------
        // IVisualSystem
        // ----------------------------------------------------------------------------

        public void RegisterHub(IVisualHub hub)
        {
            if (hub == null) return;
            if (!_hubs.Add(hub)) return;

            if (_stateSlots.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[VisualSystem] RegisterHub (no state slots) hubKind={hub.Kind} hubTag={hub.HubTag} hubId={hub.HubInstanceId}");
#endif
                return;
            }

            // Apply all stored slots to the new hub.
            // More specific selectors are applied later (last write wins).
            _sortedSlotsScratch.Clear();
            foreach (var kv in _stateSlots)
            {
                _sortedSlotsScratch.Add(kv);
            }

            _sortedSlotsScratch.Sort(static (a, b) => a.Key.SpecificityOrder.CompareTo(b.Key.SpecificityOrder));

            int appliedSlots = 0;
            for (int i = 0; i < _sortedSlotsScratch.Count; i++)
            {
                var selector = _sortedSlotsScratch[i].Key;
                if (!selector.Matches(hub))
                    continue;

                var slot = _sortedSlotsScratch[i].Value;
                hub.SetHubState(slot.Entries, slot.ClearMissingKeys, slot.BasePriority);
                appliedSlots++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[VisualSystem] RegisterHub applied slots={appliedSlots} hubKind={hub.Kind} hubTag={hub.HubTag} hubId={hub.HubInstanceId}");
#endif
        }

        public void UnregisterHub(IVisualHub hub)
        {
            if (hub == null) return;
            _hubs.Remove(hub);
        }

        public void SetState(
            VisualTargetSelector selector,
            IReadOnlyList<MaterialFxPresetEntry> entries,
            bool clearMissingKeys = true,
            int basePriority = 0)
        {
            // State は「保持して新規Hubへ同期」する。
            // 原則として時間依存入力は Broadcast に回すが、
            // ApplyWeightFade のみ（LifetimeSeconds なし）は最終値を State にも保持する。
            SplitEntriesForSetState(entries, out var stateEntries, out var broadcastEntries);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[VisualSystem] SetState selector={selector} entries={entries?.Count ?? 0} state={stateEntries.Length} broadcast={broadcastEntries.Length} clearMissing={clearMissingKeys} basePrio={basePriority}");
#endif

            // Broadcast only (when caller provided only time-dependent entries)
            if ((entries != null && entries.Count > 0) && stateEntries.Length == 0)
            {
                if (broadcastEntries.Length > 0)
                    Broadcast(selector, broadcastEntries, basePriority);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[VisualSystem] SetState broadcast-only selector={selector} hubs={_hubs.Count} broadcast={broadcastEntries.Length}");
#endif
                return;
            }

            var nextHash = ComputeSnapshotHash(selector, stateEntries, clearMissingKeys, basePriority);
            if (_stateSlots.TryGetValue(selector, out var prev) && prev != null && prev.Hash == nextHash)
            {
                // same snapshot → suppress state resend
                // but timed broadcast entries must still be replayed
                if (broadcastEntries.Length > 0)
                    Broadcast(selector, broadcastEntries, basePriority);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ////Debug.Log($"[VisualSystem] SetState suppressed (same snapshot) selector={selector} hash={nextHash}");
#endif
                return;
            }

            var slot = prev ?? new StateSlot();
            slot.ClearMissingKeys = clearMissingKeys;
            slot.BasePriority = basePriority;
            slot.Hash = nextHash;
            slot.Entries = stateEntries;
            _stateSlots[selector] = slot;

            if (_hubs.Count == 0)
            {
                if (broadcastEntries.Length > 0)
                    Broadcast(selector, broadcastEntries, basePriority);
                return;
            }

            foreach (var hub in _hubs)
            {
                if (!selector.Matches(hub))
                    continue;

                hub.SetHubState(slot.Entries, clearMissingKeys, basePriority);
                if (broadcastEntries.Length > 0)
                    hub.BroadcastMaterialFx(broadcastEntries, basePriority);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[VisualSystem] SetState applied to hubs={_hubs.Count} selector={selector}");
#endif
        }

        public void Broadcast(
            VisualTargetSelector selector,
            IReadOnlyList<MaterialFxPresetEntry> entries,
            int basePriority = 0)
        {
            if (_hubs.Count == 0) return;
            if (entries == null || entries.Count == 0) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int matched = 0;
#endif
            foreach (var hub in _hubs)
            {
                if (!selector.Matches(hub))
                    continue;

                hub.BroadcastMaterialFx(entries, basePriority);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                matched++;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[VisualSystem] Broadcast selector={selector} entries={entries.Count} hubs={_hubs.Count} matched={matched}");
#endif
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _stateSlots.Clear();

            var payload = ResolveDefaultPayload(scope);
            if (payload == null || payload.Entries == null || payload.Entries.Count == 0)
                return;

            SetState(VisualTargetSelector.All(), payload.Entries, clearMissingKeys: true, basePriority: payload.Priority);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _stateSlots.Clear();
            _sortedSlotsScratch.Clear();
            _hubs.Clear();
        }

        // ----------------------------------------------------------------------------
        // Internal
        // ----------------------------------------------------------------------------

        MaterialFxPayload? ResolveDefaultPayload(IScopeNode scope)
        {
            if (!_defaultMaterialFxSource.HasSource)
                return null;

            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                var context = new SimpleDynamicContext(vars, scope);
                return _defaultMaterialFxSource.GetOrDefault(context);
            }

            return _defaultMaterialFxSource.GetOrDefaultWithoutContext();
        }

        static bool IsTimeDependent(in MaterialFxPresetEntry e)
        {
            // FadeDuration alone should not force broadcast path.
            // Time dependency is controlled by lifetime or explicit fade usage.
            return e.LifetimeSeconds > 0f || e.ApplyWeightFade;
        }

        static bool ShouldKeepAsPersistentFinalState(in MaterialFxPresetEntry e)
        {
            // ApplyWeightFade only (without finite lifetime) should keep the final value as persistent state.
            // This is equivalent to storing an entry with "ApplyWeightFade OFF" and "LifetimeSeconds OFF".
            return e.ApplyWeightFade && e.LifetimeSeconds <= 0f;
        }

        static MaterialFxPresetEntry ToPersistentStateEntry(in MaterialFxPresetEntry source)
        {
            var normalized = source;
            normalized.ApplyWeightFade = false;
            normalized.LifetimeSeconds = -1f;
            normalized.TargetWeight = Game.Common.DynamicValueExtensions.FromLiteral(1f);
            normalized.FadeDuration = Game.Common.DynamicValueExtensions.FromLiteral(0f);
            return normalized;
        }

        static void SplitEntriesForSetState(
            IReadOnlyList<MaterialFxPresetEntry> entries,
            out MaterialFxPresetEntry[] stateEntries,
            out MaterialFxPresetEntry[] broadcastEntries)
        {
            if (entries == null || entries.Count == 0)
            {
                stateEntries = Array.Empty<MaterialFxPresetEntry>();
                broadcastEntries = Array.Empty<MaterialFxPresetEntry>();
                return;
            }

            var hasAnyTimeDependent = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsTimeDependent(entries[i]))
                {
                    hasAnyTimeDependent = true;
                    break;
                }
            }

            if (!hasAnyTimeDependent)
            {
                // copy as-is to keep snapshot immutable
                var arr = new MaterialFxPresetEntry[entries.Count];
                for (int i = 0; i < entries.Count; i++) arr[i] = entries[i];
                stateEntries = arr;
                broadcastEntries = Array.Empty<MaterialFxPresetEntry>();
                return;
            }

            var stateList = new List<MaterialFxPresetEntry>(entries.Count);
            var broadcastList = new List<MaterialFxPresetEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (IsTimeDependent(e))
                {
                    if (ShouldKeepAsPersistentFinalState(e))
                    {
                        stateList.Add(ToPersistentStateEntry(e));
                    }

                    broadcastList.Add(e);
                    continue;
                }

                stateList.Add(e);
            }

            stateEntries = stateList.Count == 0 ? Array.Empty<MaterialFxPresetEntry>() : stateList.ToArray();
            broadcastEntries = broadcastList.Count == 0 ? Array.Empty<MaterialFxPresetEntry>() : broadcastList.ToArray();
        }

        static int ComputeSnapshotHash(
            VisualTargetSelector selector,
            MaterialFxPresetEntry[] entries,
            bool clearMissingKeys,
            int basePriority)
        {
            unchecked
            {
                var h = 17;
                h = (h * 31) ^ selector.GetHashCode();
                h = (h * 31) ^ (clearMissingKeys ? 1 : 0);
                h = (h * 31) ^ basePriority;
                h = (h * 31) ^ (entries?.Length ?? 0);

                if (entries != null)
                {
                    for (int i = 0; i < entries.Length; i++)
                    {
                        h = (h * 31) ^ HashEntry(entries[i]);
                    }
                }

                return h;
            }
        }

        static int HashEntry(in MaterialFxPresetEntry e)
        {
            unchecked
            {
                var h = 17;
                h = (h * 31) ^ (e.Key != null ? StringComparer.Ordinal.GetHashCode(e.Key) : 0);
                h = (h * 31) ^ (int)e.BlendMode;

                // State は時間依存をフィルタ済みだが、念のため値も含める
                h = (h * 31) ^ (int)e.Value.Type;
                h = (h * 31) ^ HashValue(e.Value);

                return h;
            }
        }

        static int HashValue(in MaterialFxSerializedValue v)
        {
            unchecked
            {
                var h = 17;
                h = (h * 31) ^ (int)v.Type;

                // Note: UnityEngine.Object hash is not stable across sessions, but snapshot hash is runtime-only.
                switch (v.Type)
                {
                    case Game.MaterialFx.ValueKind.Float:
                        h = (h * 31) ^ HashDynamicFloat(v.Float);
                        break;
                    case Game.MaterialFx.ValueKind.Int:
                    case Game.MaterialFx.ValueKind.Bool:
                        h = (h * 31) ^ v.Int;
                        break;
                    case Game.MaterialFx.ValueKind.Float2:
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float2.x);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float2.y);
                        break;
                    case Game.MaterialFx.ValueKind.Float3:
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float3.x);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float3.y);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float3.z);
                        break;
                    case Game.MaterialFx.ValueKind.Float4:
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float4.x);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float4.y);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float4.z);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Float4.w);
                        break;
                    case Game.MaterialFx.ValueKind.Color:
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Color.r);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Color.g);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Color.b);
                        h = (h * 31) ^ BitConverter.SingleToInt32Bits(v.Color.a);
                        break;
                    case Game.MaterialFx.ValueKind.Texture:
                    case Game.MaterialFx.ValueKind.TextureArray:
                        h = (h * 31) ^ (v.Texture != null ? v.Texture.GetInstanceID() : 0);
                        break;
                }

                return h;
            }
        }

        static int HashDynamicFloat(in Game.Common.DynamicValue<float> value)
        {
            unchecked
            {
                if (!value.HasSource)
                    return BitConverter.SingleToInt32Bits(value.GetOrDefault(default!, 0f));

                var h = 17;
                h = (h * 31) ^ StringComparer.Ordinal.GetHashCode(value.SourceTypeName ?? string.Empty);
                h = (h * 31) ^ StringComparer.Ordinal.GetHashCode(value.SourceDebugData ?? string.Empty);
                return h;
            }
        }
    }
}
