#nullable enable
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public sealed class ModalStackChannelHubService : IModalStackChannelHubService, IModalStackChannelTelemetry
    {
        struct ModalEntry
        {
            public IUIModalRoot Root;
            public ModalOptions Options;
            public ulong Sequence;
        }

        sealed class LayerState
        {
            public ModalLayerPreset Preset;
            public readonly List<ModalEntry> Entries = new();
            public IUIModalRoot? DefaultRoot;
            public ulong DefaultRootSequence;

            public LayerState(ModalLayerPreset preset)
            {
                Preset = preset;
            }
        }

        readonly Dictionary<string, LayerState> _layers = new(StringComparer.Ordinal);
        readonly List<ModalLayerResolvedState> _layerStates = new(16);
        readonly List<ModalRootResolvedState> _rootStates = new(32);

        ulong _sequence;
        IUIModalRoot? _currentInputRoot;

        public IReadOnlyList<ModalLayerResolvedState> LayerStates => _layerStates;
        public IReadOnlyList<ModalRootResolvedState> RootStates => _rootStates;
        public IUIModalRoot? CurrentInputRoot => _currentInputRoot;

        public event Action<ModalLayerStatesChangedContext>? OnLayerStatesChanged;

        public ModalStackChannelHubService()
        {
            RegisterLayer(ModalLayerPreset.Default("default"));
        }

        public void RegisterLayer(ModalLayerPreset preset)
        {
            var key = NormalizeLayerKey(preset.LayerKey);
            var normalized = preset;
            normalized.LayerKey = key;

            if (_layers.TryGetValue(key, out var existing))
            {
                existing.Preset = normalized;
                NotifyResolvedChanged(key, ModalLayerChangeKind.LayerConfigChanged, UIModalStackChangeType.Normal);
                return;
            }

            _layers.Add(key, new LayerState(normalized));
            NotifyResolvedChanged(key, ModalLayerChangeKind.RegisterLayer, UIModalStackChangeType.Normal);
        }

        public bool TryGetLayerState(string layerKey, out ModalLayerResolvedState state)
        {
            var key = NormalizeLayerKey(layerKey);
            for (int i = 0; i < _layerStates.Count; i++)
            {
                if (string.Equals(_layerStates[i].LayerKey, key, StringComparison.Ordinal))
                {
                    state = _layerStates[i];
                    return true;
                }
            }

            state = default;
            return false;
        }

        public bool TryGetRootState(IScopeNode owner, out ModalRootResolvedState state)
        {
            state = default;
            if (owner == null)
                return false;

            for (int i = 0; i < _rootStates.Count; i++)
            {
                var candidate = _rootStates[i];
                var root = candidate.Root;
                if (root == null)
                    continue;

                if (root.IsDescendant(owner))
                {
                    state = candidate;
                    return true;
                }
            }

            return false;
        }

        public void SetDefaultRoot(string layerKey, IUIModalRoot? root, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            var layer = EnsureLayer(layerKey);
            layer.DefaultRoot = root;
            layer.DefaultRootSequence = ++_sequence;
            NotifyResolvedChanged(layer.Preset.LayerKey, ModalLayerChangeKind.SetDefaultRoot, changeType);
        }

        public void PushModal(string layerKey, IUIModalRoot root, ModalOptions options = default, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            if (root == null)
                return;

            var layer = EnsureLayer(layerKey);
            for (int i = 0; i < layer.Entries.Count; i++)
            {
                if (ReferenceEquals(layer.Entries[i].Root, root))
                    return;
            }

            layer.Entries.Add(new ModalEntry
            {
                Root = root,
                Options = options,
                Sequence = ++_sequence,
            });

            NotifyResolvedChanged(layer.Preset.LayerKey, ModalLayerChangeKind.Push, changeType);
        }

        public bool PopModal(string layerKey, IUIModalRoot root, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            if (root == null)
                return false;

            var key = NormalizeLayerKey(layerKey);
            if (!_layers.TryGetValue(key, out var layer))
                return false;

            var found = -1;
            for (int i = layer.Entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(layer.Entries[i].Root, root))
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
                return false;

            for (int i = layer.Entries.Count - 1; i >= found; i--)
                layer.Entries.RemoveAt(i);

            NotifyResolvedChanged(layer.Preset.LayerKey, ModalLayerChangeKind.Pop, changeType);
            return true;
        }

        public IUIModalRoot? PopTop(string layerKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            var key = NormalizeLayerKey(layerKey);
            if (!_layers.TryGetValue(key, out var layer))
                return null;

            if (layer.Entries.Count == 0)
                return null;

            var last = layer.Entries[layer.Entries.Count - 1].Root;
            layer.Entries.RemoveAt(layer.Entries.Count - 1);
            NotifyResolvedChanged(layer.Preset.LayerKey, ModalLayerChangeKind.PopTop, changeType);
            return last;
        }

        public void ClearLayer(string layerKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            var key = NormalizeLayerKey(layerKey);
            if (!_layers.TryGetValue(key, out var layer))
                return;

            if (layer.Entries.Count == 0)
                return;

            layer.Entries.Clear();
            NotifyResolvedChanged(layer.Preset.LayerKey, ModalLayerChangeKind.ClearLayer, changeType);
        }

        public void ClearAll(UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            var changed = false;
            foreach (var pair in _layers)
            {
                if (pair.Value.Entries.Count == 0)
                    continue;

                pair.Value.Entries.Clear();
                changed = true;
            }

            if (!changed)
                return;

            NotifyResolvedChanged("*", ModalLayerChangeKind.ClearAll, changeType);
        }

        LayerState EnsureLayer(string? layerKey)
        {
            var key = NormalizeLayerKey(layerKey);
            if (_layers.TryGetValue(key, out var existing))
                return existing;

            var preset = ModalLayerPreset.Default(key);
            var created = new LayerState(preset);
            _layers.Add(key, created);
            return created;
        }

        static string NormalizeLayerKey(string? layerKey)
        {
            if (string.IsNullOrWhiteSpace(layerKey))
                return "default";
            return layerKey.Trim();
        }

        static IUIModalRoot? GetActiveRoot(LayerState layer)
        {
            if (layer.Entries.Count > 0)
                return layer.Entries[layer.Entries.Count - 1].Root;
            return layer.DefaultRoot;
        }

        static ulong GetActiveSequence(LayerState layer)
        {
            if (layer.Entries.Count > 0)
                return layer.Entries[layer.Entries.Count - 1].Sequence;
            return layer.DefaultRootSequence;
        }

        void NotifyResolvedChanged(string causeLayerKey, ModalLayerChangeKind kind, UIModalStackChangeType changeType)
        {
            var prevLayers = new List<ModalLayerResolvedState>(_layerStates);
            var prevRoots = new List<ModalRootResolvedState>(_rootStates);

            BuildResolvedStates();

            if (!AreLayerStatesEqual(prevLayers, _layerStates) || !AreRootStatesEqual(prevRoots, _rootStates))
            {
                var context = new ModalLayerStatesChangedContext(
                    prevLayers,
                    _layerStates,
                    prevRoots,
                    _rootStates,
                    causeLayerKey,
                    kind,
                    changeType);

                OnLayerStatesChanged?.Invoke(context);
            }
        }

        void BuildResolvedStates()
        {
            _layerStates.Clear();
            _rootStates.Clear();
            _currentInputRoot = null;

            var temp = new List<TempLayer>(Math.Max(4, _layers.Count));
            foreach (var pair in _layers)
            {
                var key = pair.Key;
                var layer = pair.Value;
                var active = GetActiveRoot(layer);
                var hasAny = active != null;

                var t = new TempLayer();
                t.Key = key;
                t.Preset = layer.Preset;
                t.Layer = layer;
                t.ActiveRoot = active;
                t.ActiveSequence = GetActiveSequence(layer);
                t.HasAnyUI = hasAny;
                t.Visible = hasAny;
                t.InputActive = hasAny;
                t.IsTopOrderGroup = false;
                t.IsPrimaryInOrder = false;
                t.SuppressedByLayerKey = string.Empty;
                temp.Add(t);
            }

            if (temp.Count == 0)
                return;

            var maxOrder = int.MinValue;
            var hasAnyLayer = false;
            for (int i = 0; i < temp.Count; i++)
            {
                if (!temp[i].HasAnyUI)
                    continue;

                hasAnyLayer = true;
                if (temp[i].Preset.Order > maxOrder)
                    maxOrder = temp[i].Preset.Order;
            }

            var topIndices = new List<int>(8);
            if (hasAnyLayer)
            {
                for (int i = 0; i < temp.Count; i++)
                {
                    if (!temp[i].HasAnyUI)
                        continue;

                    if (temp[i].Preset.Order == maxOrder)
                    {
                        var t = temp[i];
                        t.IsTopOrderGroup = true;
                        temp[i] = t;
                        topIndices.Add(i);
                    }
                }
            }

            if (topIndices.Count > 0)
            {
                var primaryIndex = topIndices[0];
                var primarySequence = temp[primaryIndex].ActiveSequence;
                for (int i = 1; i < topIndices.Count; i++)
                {
                    var idx = topIndices[i];
                    var candidateSeq = temp[idx].ActiveSequence;
                    if (candidateSeq < primarySequence)
                    {
                        primaryIndex = idx;
                        primarySequence = candidateSeq;
                    }
                }

                for (int i = 0; i < topIndices.Count; i++)
                {
                    var idx = topIndices[i];
                    var t = temp[idx];
                    var isPrimary = idx == primaryIndex;
                    var allowSim = t.Preset.AllowSimultaneousInputInSameOrder ||
                                   t.Preset.TiePolicy == ModalLayerTiePolicy.SimultaneousAllowedLayers;

                    var inputActive = isPrimary || allowSim;
                    t.IsPrimaryInOrder = isPrimary;
                    t.InputActive = inputActive && t.Visible;
                    temp[idx] = t;
                }

                var forceVisibleFalse = false;
                var forceInputInactive = false;
                var suppressorKey = string.Empty;
                for (int i = 0; i < topIndices.Count; i++)
                {
                    var top = temp[topIndices[i]];
                    if (top.Preset.TopOrderEffect == ModalLayerTopOrderEffect.ForceLowerLayerVisibleFalse)
                    {
                        forceVisibleFalse = true;
                        suppressorKey = top.Key;
                    }
                    else if (top.Preset.TopOrderEffect == ModalLayerTopOrderEffect.ForceLowerLayerInputInactive)
                    {
                        forceInputInactive = true;
                        if (string.IsNullOrEmpty(suppressorKey))
                            suppressorKey = top.Key;
                    }
                }

                if (forceVisibleFalse || forceInputInactive)
                {
                    for (int i = 0; i < temp.Count; i++)
                    {
                        var t = temp[i];
                        if (!t.HasAnyUI)
                            continue;
                        if (t.Preset.Order >= maxOrder)
                            continue;

                        if (forceVisibleFalse)
                        {
                            t.Visible = false;
                            t.InputActive = false;
                            t.SuppressedByLayerKey = suppressorKey;
                            temp[i] = t;
                        }
                        else if (forceInputInactive)
                        {
                            t.InputActive = false;
                            t.SuppressedByLayerKey = suppressorKey;
                            temp[i] = t;
                        }
                    }
                }
            }

            temp.Sort((a, b) =>
            {
                var cmp = b.Preset.Order.CompareTo(a.Preset.Order);
                if (cmp != 0)
                    return cmp;
                return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });

            for (int i = 0; i < temp.Count; i++)
            {
                var t = temp[i];
                _layerStates.Add(new ModalLayerResolvedState(
                    t.Key,
                    t.Preset.Order,
                    t.HasAnyUI,
                    t.ActiveRoot,
                    t.Visible,
                    t.InputActive,
                    t.IsTopOrderGroup,
                    t.IsPrimaryInOrder,
                    t.SuppressedByLayerKey));

                if (_currentInputRoot == null && t.InputActive && t.ActiveRoot != null)
                    _currentInputRoot = t.ActiveRoot;

                BuildRootStatesForLayer(t, _rootStates);
            }
        }

        static void BuildRootStatesForLayer(TempLayer layer, List<ModalRootResolvedState> dest)
        {
            if (layer.Layer == null)
                return;

            var activeRoot = layer.ActiveRoot;
            var activeSet = new HashSet<IUIModalRoot>(ReferenceEqualityComparer<IUIModalRoot>.Instance);

            for (int i = 0; i < layer.Layer.Entries.Count; i++)
            {
                var root = layer.Layer.Entries[i].Root;
                if (root == null || !activeSet.Add(root))
                    continue;

                AppendRootState(layer, root, activeRoot, dest);
            }

            if (layer.Layer.DefaultRoot != null && activeSet.Add(layer.Layer.DefaultRoot))
                AppendRootState(layer, layer.Layer.DefaultRoot, activeRoot, dest);
        }

        static void AppendRootState(TempLayer layer, IUIModalRoot root, IUIModalRoot? activeRoot, List<ModalRootResolvedState> dest)
        {
            var isActiveInLayer = ReferenceEquals(root, activeRoot);
            var visible = layer.Visible && (isActiveInLayer || layer.Preset.KeepNonActiveInLayerVisible);
            var input = layer.InputActive && (isActiveInLayer || layer.Preset.KeepNonActiveInLayerInputActive);

            var reason = ModalLayerRootInactiveReason.None;
            if (!isActiveInLayer)
                reason = ModalLayerRootInactiveReason.NotActiveInLayer;
            if (!string.IsNullOrEmpty(layer.SuppressedByLayerKey))
                reason = ModalLayerRootInactiveReason.LayerSuppressedByOtherLayer;

            dest.Add(new ModalRootResolvedState(layer.Key, root, isActiveInLayer, visible, input, reason));
        }

        static bool AreLayerStatesEqual(List<ModalLayerResolvedState> a, List<ModalLayerResolvedState> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i];
                var y = b[i];
                if (!string.Equals(x.LayerKey, y.LayerKey, StringComparison.Ordinal))
                    return false;
                if (x.Order != y.Order || x.HasAnyUI != y.HasAnyUI || x.Visible != y.Visible || x.InputActive != y.InputActive ||
                    x.IsTopOrderGroup != y.IsTopOrderGroup || x.IsPrimaryInOrder != y.IsPrimaryInOrder)
                    return false;
                if (!ReferenceEquals(x.ActiveRoot, y.ActiveRoot))
                    return false;
                if (!string.Equals(x.SuppressedByLayerKey, y.SuppressedByLayerKey, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        static bool AreRootStatesEqual(List<ModalRootResolvedState> a, List<ModalRootResolvedState> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i];
                var y = b[i];
                if (!string.Equals(x.LayerKey, y.LayerKey, StringComparison.Ordinal))
                    return false;
                if (!ReferenceEquals(x.Root, y.Root))
                    return false;
                if (x.IsActiveInLayer != y.IsActiveInLayer || x.Visible != y.Visible || x.InputActive != y.InputActive || x.InactiveReason != y.InactiveReason)
                    return false;
            }

            return true;
        }

        struct TempLayer
        {
            public string Key;
            public ModalLayerPreset Preset;
            public LayerState? Layer;
            public IUIModalRoot? ActiveRoot;
            public ulong ActiveSequence;
            public bool HasAnyUI;
            public bool Visible;
            public bool InputActive;
            public bool IsTopOrderGroup;
            public bool IsPrimaryInOrder;
            public string SuppressedByLayerKey;
        }

        sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new();

            public bool Equals(T? x, T? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
