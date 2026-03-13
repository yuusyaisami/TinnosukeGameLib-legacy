#nullable enable
using System.Collections.Generic;

namespace Game.CameraSystem
{
    public sealed class LayeredBool
    {
        sealed class Layer
        {
            public readonly string Tag;
            public bool Value;
            public int Order;

            public Layer(string tag)
            {
                Tag = tag;
                Value = false;
                Order = 0;
            }
        }

        public readonly struct LayerSnapshot
        {
            public readonly string Tag;
            public readonly bool Value;
            public readonly int Order;

            public LayerSnapshot(string tag, bool value, int order)
            {
                Tag = tag;
                Value = value;
                Order = order;
            }
        }

        readonly Dictionary<string, Layer> _layers = new();
        int _order;

        public bool CurrentValue => ResolveCurrent();
        public bool HasLayers => _layers.Count > 0;

        public bool Contains(string tag)
        {
            return !string.IsNullOrEmpty(tag) && _layers.ContainsKey(tag);
        }

        public void SetLayer(string tag, bool value)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            if (!_layers.TryGetValue(tag, out var layer))
            {
                layer = new Layer(tag);
                _layers[tag] = layer;
            }

            layer.Value = value;
            layer.Order = ++_order;
        }

        public void ClearLayer(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            _layers.Remove(tag);
        }

        public void ClearAll()
        {
            _layers.Clear();
        }

        public void ClearAllExcept(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                ClearAll();
                return;
            }

            if (!_layers.TryGetValue(tag, out var keep))
            {
                ClearAll();
                return;
            }

            _layers.Clear();
            _layers[tag] = keep;
        }

        public void AppendSnapshots(List<LayerSnapshot> dest)
        {
            dest.Clear();
            foreach (var layer in _layers.Values)
            {
                dest.Add(new LayerSnapshot(layer.Tag, layer.Value, layer.Order));
            }
        }

        bool ResolveCurrent()
        {
            if (_layers.Count == 0)
                return false;

            Layer? best = null;
            foreach (var layer in _layers.Values)
            {
                if (best == null || layer.Order > best.Order)
                    best = layer;
            }

            return best != null && best.Value;
        }
    }
}
