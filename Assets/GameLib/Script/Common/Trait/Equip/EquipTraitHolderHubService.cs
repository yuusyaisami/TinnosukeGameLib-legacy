#nullable enable
using System;
using VContainer;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.Trait
{
    /// <summary>
    /// 複数の EquipTraitSlotRuntime をキーで管理するハブ。
    /// 各スロットは対応する ITraitHolderService を監視し、
    /// 装備中の Trait が Holder から除去されたとき自動で Unequip する。
    /// </summary>
    public interface IEquipTraitHolderHubService
    {
        IReadOnlyList<string> SlotKeys { get; }
        bool TryGetSlot(string slotKey, out EquipTraitSlotRuntime? slot);
    }

    public sealed class EquipTraitHolderHubService :
        IEquipTraitHolderHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly Dictionary<string, EquipTraitSlotRuntime> _slots = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> _slotToHolderKey = new(StringComparer.Ordinal);
        readonly List<EquipTraitSlotRuntime> _slotList = new();
        readonly List<string> _slotKeys = new();

        readonly IScopeNode? _scope;
        ITraitHolderHubService? _holderHub;

        // TraitHolder の変更イベントを購読するためのバインディング
        readonly Dictionary<string, (ITraitHolderService holder, Action<IReadOnlyList<ITraitInstance>> handler)> _holderBindings = new();

        public EquipTraitHolderHubService(
            IScopeNode? scope,
            IReadOnlyList<EquipTraitSlotSettings>? settings)
        {
            _scope = scope;

            if (settings == null || settings.Count == 0)
                return;

            for (int i = 0; i < settings.Count; i++)
            {
                var setting = settings[i];
                if (setting == null)
                    continue;

                var key = setting.NormalizedSlotKey;
                if (string.IsNullOrEmpty(key))
                    continue;

                if (_slots.ContainsKey(key))
                    continue;

                var slot = new EquipTraitSlotRuntime(scope, key);
                setting.ApplyTo(slot);

                _slots.Add(key, slot);
                _slotList.Add(slot);
                _slotKeys.Add(key);

                // スロットと対応する HolderKey を記録
                if (!string.IsNullOrWhiteSpace(setting.HolderKey))
                    _slotToHolderKey[key] = setting.HolderKey.Trim();
            }
        }

        public IReadOnlyList<string> SlotKeys => _slotKeys;

        public bool TryGetSlot(string slotKey, out EquipTraitSlotRuntime? slot)
        {
            slot = null;
            if (string.IsNullOrWhiteSpace(slotKey))
                return false;

            var normalized = slotKey.Trim();
            if (!_slots.TryGetValue(normalized, out var s))
                return false;

            slot = s;
            return true;
        }

        // ───────────────── Lifecycle ─────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // TraitHolderHubService を取得
            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<ITraitHolderHubService>(out var hub) && hub != null)
            {
                _holderHub = hub;
            }

            // 各スロットの対応 HolderKey の TraitHolder を購読
            BindHolderEvents();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnbindHolderEvents();

            for (int i = 0; i < _slotList.Count; i++)
                _slotList[i].Reset();

            _holderHub = null;
        }

        // ───────────────── Holder Event Binding ─────────────────

        void BindHolderEvents()
        {
            if (_holderHub == null) return;

            foreach (var kv in _slotToHolderKey)
            {
                var slotKey = kv.Key;
                var holderKey = kv.Value;

                if (!_holderHub.TryGetHolder(holderKey, out var holder) || holder == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[EquipTraitHub] Slot '{slotKey}' references holder '{holderKey}' which was not found.");
#endif
                    continue;
                }

                if (_holderBindings.ContainsKey(slotKey))
                    continue;

                // Slot のキーをキャプチャ
                var capturedSlotKey = slotKey;
                Action<IReadOnlyList<ITraitInstance>> handler = _ => OnHolderTraitsChanged(capturedSlotKey);
                holder.OnTraitsChanged += handler;

                _holderBindings[slotKey] = (holder, handler);
            }
        }

        void UnbindHolderEvents()
        {
            foreach (var kv in _holderBindings)
            {
                var (holder, handler) = kv.Value;
                holder.OnTraitsChanged -= handler;
            }

            _holderBindings.Clear();
        }

        /// <summary>
        /// 装備中の Trait が Holder から除去されたとき自動 Unequip。
        /// </summary>
        void OnHolderTraitsChanged(string slotKey)
        {
            if (!_slots.TryGetValue(slotKey, out var slot))
                return;

            if (!slot.IsOccupied) return;

            var equipped = slot.Equipped;
            if (equipped == null) return;

            // 対応する HolderKey を取得
            if (!_slotToHolderKey.TryGetValue(slotKey, out var holderKey))
                return;

            if (_holderHub == null) return;
            if (!_holderHub.TryGetHolder(holderKey, out var holder) || holder == null)
                return;

            // Holder の Traits 一覧の中に装備中の Trait がまだ存在するかチェック
            var traits = holder.Traits;
            bool found = false;
            for (int i = 0; i < traits.Count; i++)
            {
                if (ReferenceEquals(traits[i], equipped))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Trait が除去された → 自動 Unequip
                slot.UnequipImmediate();
            }
        }

        // ───────────────── Utility ─────────────────

        /// <summary>
        /// 指定スロットに対応する TraitHolder 内の Trait を解決する。
        /// </summary>
        internal bool TryResolveTraitFromHolder(
            string slotKey,
            EquipTraitTargetKind targetKind,
            ITraitDefinition? targetDefinition,
            string? targetDefinitionId,
            int targetIndex,
            out ITraitInstance? instance)
        {
            instance = null;

            if (!_slotToHolderKey.TryGetValue(slotKey, out var holderKey))
                return false;

            if (_holderHub == null) return false;
            if (!_holderHub.TryGetHolder(holderKey, out var holder) || holder == null)
                return false;

            var traits = holder.Traits;
            if (traits == null || traits.Count == 0)
                return false;

            switch (targetKind)
            {
                case EquipTraitTargetKind.ByDefinition:
                    if (targetDefinition == null) return false;
                    return holder.TryGetInstance(targetDefinition, out instance);

                case EquipTraitTargetKind.First:
                    instance = traits[0];
                    return instance != null;

                case EquipTraitTargetKind.Last:
                    instance = traits[traits.Count - 1];
                    return instance != null;

                case EquipTraitTargetKind.ByIndex:
                    if (targetIndex < 0 || targetIndex >= traits.Count)
                        return false;
                    instance = traits[targetIndex];
                    return instance != null;

                case EquipTraitTargetKind.ByDefinitionId:
                    if (string.IsNullOrWhiteSpace(targetDefinitionId))
                        return false;
                    for (int i = 0; i < traits.Count; i++)
                    {
                        if (string.Equals(traits[i].Definition?.DefinitionId, targetDefinitionId, StringComparison.Ordinal))
                        {
                            instance = traits[i];
                            return true;
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }
    }
}
