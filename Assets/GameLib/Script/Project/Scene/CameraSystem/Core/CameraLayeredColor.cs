#nullable enable
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.CameraSystem
{
    public sealed class LayeredColor
    {
        sealed class Layer
        {
            public readonly string Tag;
            public Color Value;
            public Color Target;
            public float Duration;
            public Ease Ease;
            public Tween? Tween;

            public Layer(string tag)
            {
                Tag = tag;
                Value = new Color(0f, 0f, 0f, 0f);
                Target = new Color(0f, 0f, 0f, 0f);
                Duration = 0f;
                Ease = Ease.Linear;
                Tween = null;
            }
        }

        public readonly struct LayerSnapshot
        {
            public readonly string Tag;
            public readonly Color Value;
            public readonly Color Target;
            public readonly float Duration;
            public readonly Ease Ease;
            public readonly bool IsAnimating;

            public LayerSnapshot(string tag, Color value, Color target, float duration, Ease ease, bool isAnimating)
            {
                Tag = tag;
                Value = value;
                Target = target;
                Duration = duration;
                Ease = ease;
                IsAnimating = isAnimating;
            }
        }

        readonly Dictionary<string, Layer> _layers = new();

        public Color CurrentSum => SumCurrent();
        public Color TargetSum => SumTarget();
        public bool HasLayers => _layers.Count > 0;

        public bool Contains(string tag)
        {
            return !string.IsNullOrEmpty(tag) && _layers.ContainsKey(tag);
        }

        public void SetLayer(string tag, Color value)
        {
            SetLayer(tag, value, 0f, Ease.Linear);
        }

        public void SetLayer(string tag, Color value, float duration, Ease ease)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            if (!_layers.TryGetValue(tag, out var layer))
            {
                layer = new Layer(tag);
                _layers[tag] = layer;
            }

            if (layer.Tween != null)
            {
                layer.Tween.Kill();
                layer.Tween = null;
            }

            layer.Target = value;
            layer.Duration = Mathf.Max(0f, duration);
            layer.Ease = ease == Ease.Unset ? Ease.Linear : ease;

            if (layer.Duration <= 0f)
            {
                layer.Value = value;
                return;
            }

            layer.Tween = DOTween
                .To(() => layer.Value, v => layer.Value = v, value, layer.Duration)
                .SetEase(layer.Ease)
                .OnComplete(() => layer.Tween = null);
        }

        public void ClearLayer(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            if (!_layers.TryGetValue(tag, out var layer))
                return;

            if (layer.Tween != null)
            {
                layer.Tween.Kill();
                layer.Tween = null;
            }

            _layers.Remove(tag);
        }

        public void ClearAll()
        {
            foreach (var layer in _layers.Values)
            {
                if (layer.Tween != null)
                {
                    layer.Tween.Kill();
                    layer.Tween = null;
                }
            }

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

            foreach (var layer in _layers.Values)
            {
                if (layer == keep)
                    continue;

                if (layer.Tween != null)
                {
                    layer.Tween.Kill();
                    layer.Tween = null;
                }
            }

            _layers.Clear();
            _layers[tag] = keep;
        }

        public void AppendSnapshots(List<LayerSnapshot> dest)
        {
            dest.Clear();
            foreach (var layer in _layers.Values)
            {
                dest.Add(new LayerSnapshot(
                    layer.Tag,
                    layer.Value,
                    layer.Target,
                    layer.Duration,
                    layer.Ease,
                    layer.Tween != null));
            }
        }

        Color SumCurrent()
        {
            Color sum = new Color(0f, 0f, 0f, 0f);
            foreach (var layer in _layers.Values)
            {
                sum += layer.Value;
            }
            return sum;
        }

        Color SumTarget()
        {
            Color sum = new Color(0f, 0f, 0f, 0f);
            foreach (var layer in _layers.Values)
            {
                sum += layer.Target;
            }
            return sum;
        }
    }
}
