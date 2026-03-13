#nullable enable
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.CameraSystem
{
    public sealed class CameraZoomService : ICameraZoomService
    {
        readonly LayeredFloat _layers = new();
        readonly List<CameraZoomLayer> _layerView = new();
        readonly List<LayeredFloat.LayerSnapshot> _layerSnapshots = new();

        float _baseZoom;
        float _minSize;
        float _maxSize;
        float _current;
        float _target;

        public CameraZoomService(float baseZoom, float minSize, float maxSize)
        {
            _baseZoom = baseZoom;
            _minSize = minSize;
            _maxSize = maxSize;
            _current = baseZoom;
            _target = baseZoom;
        }

        public float Current => _current;
        public float Target => _target;
        public float BaseZoom => _baseZoom;
        public float MinSize => _minSize;
        public float MaxSize => _maxSize;
        public IReadOnlyList<CameraZoomLayer> Layers => _layerView;

        public void ResetBase(float baseZoom)
        {
            _baseZoom = baseZoom;
            _current = ClampValue(_baseZoom + _layers.CurrentSum);
            _target = ClampValue(_baseZoom + _layers.TargetSum);
        }

        public void SetClamp(float minSize, float maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
        }

        public void SetLayer(string layerTag, float value)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            _layers.SetLayer(layerTag, value);
        }

        public void SetLayer(string layerTag, float value, float duration, Ease ease)
        {
            if (string.IsNullOrEmpty(layerTag))
                return;

            _layers.SetLayer(layerTag, value, duration, ease);
        }

        public void ClearLayer(string layerTag)
        {
            _layers.ClearLayer(layerTag);
        }

        public void ClearAllLayers()
        {
            _layers.ClearAll();
        }

        public void ResetToBase(bool immediate)
        {
            ClearAllLayers();
            if (immediate)
            {
                _current = ClampValue(_baseZoom);
                _target = _current;
            }
        }

        public void Tick(float dt)
        {
            _ = dt;
            _current = ClampValue(_baseZoom + _layers.CurrentSum);
            _target = ClampValue(_baseZoom + _layers.TargetSum);
            BuildLayerView();
        }

        void BuildLayerView()
        {
            _layerView.Clear();
            _layers.AppendSnapshots(_layerSnapshots);
            for (int i = 0; i < _layerSnapshots.Count; i++)
            {
                var layer = _layerSnapshots[i];
                _layerView.Add(new CameraZoomLayer(
                    layer.Tag,
                    layer.Value,
                    layer.Target,
                    layer.Duration,
                    layer.Ease,
                    layer.IsAnimating));
            }
        }

        float ClampValue(float value)
        {
            return Mathf.Clamp(value, _minSize, _maxSize);
        }
    }
}
