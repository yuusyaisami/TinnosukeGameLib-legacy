#nullable enable
using System.Collections.Generic;

namespace Game.Channel
{
    internal sealed class GridObjectChannelVisualCollection
    {
        readonly Dictionary<GridObjectChannelItemKey, GridObjectChannelVisualInstance> _lookup = new();
        readonly List<GridObjectChannelVisualInstance> _instances = new();

        public int Count => _instances.Count;
        public IReadOnlyList<GridObjectChannelVisualInstance> Items => _instances;

        public bool ContainsKey(GridObjectChannelItemKey key) => _lookup.ContainsKey(key);

        public bool TryGetValue(GridObjectChannelItemKey key, out GridObjectChannelVisualInstance? instance)
        {
            if (_lookup.TryGetValue(key, out var resolved) && resolved != null)
            {
                instance = resolved;
                return true;
            }

            instance = null;
            return false;
        }

        public GridObjectChannelVisualInstance this[int index] => _instances[index];

        public void Add(GridObjectChannelVisualInstance instance)
        {
            _instances.Add(instance);
            _lookup[instance.Key] = instance;
        }

        public void RemoveAt(int index)
        {
            var instance = _instances[index];
            _instances.RemoveAt(index);
            if (instance != null)
                _lookup.Remove(instance.Key);
        }

        public bool RemoveByKey(GridObjectChannelItemKey key)
        {
            if (!_lookup.TryGetValue(key, out var instance) || instance == null)
                return false;

            _lookup.Remove(key);
            return _instances.Remove(instance);
        }

        public void Reindex(GridObjectChannelItemKey oldKey, GridObjectChannelVisualInstance instance)
        {
            if (oldKey.Equals(instance.Key))
                return;

            _lookup.Remove(oldKey);
            _lookup[instance.Key] = instance;
        }

        public void Clear()
        {
            _instances.Clear();
            _lookup.Clear();
        }

        public void SortByListIndex()
        {
            if (_instances.Count <= 1)
                return;

            _instances.Sort(static (a, b) => a.ListIndex.CompareTo(b.ListIndex));
        }
    }
}
