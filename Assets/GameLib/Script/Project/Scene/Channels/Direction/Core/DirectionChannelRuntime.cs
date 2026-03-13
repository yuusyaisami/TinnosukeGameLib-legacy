using System;
using UnityEngine;
using Game.Common;

namespace Game.Direction
{
    public sealed class DirectionChannelRuntime : IDirectionChannelHandle
    {
        readonly string _tag;
        readonly BoolLayer _enabledLayer;
        readonly System.Action _markDirty;
        readonly System.Action _markSortDirty;

        Vector2 _direction;
        int _priority;
        DirectionBlendMode _blendMode;
        float _influence;
        float _transitionSpeedOverride;

        static readonly Vector2 VectorEpsilon = new Vector2(1e-4f, 1e-4f);

        public DirectionChannelRuntime(string tag, DirectionLayerDef def, System.Action markDirty, System.Action markSortDirty)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

            _tag = tag;
            _direction = def.InitialDirection;
            _priority = def.Priority;
            _blendMode = def.BlendMode;
            _influence = Mathf.Clamp01(def.Influence);
            _transitionSpeedOverride = def.TransitionSpeedOverride;
            _enabledLayer = new BoolLayer(BoolCompositionMode.AllTrue);
            _enabledLayer.Set("default", def.EnabledByDefault);
            _markDirty = markDirty;
            _markSortDirty = markSortDirty;
        }

        public string Tag => _tag;

        public Vector2 Direction
        {
            get => _direction;
            set => TrySetDirection(value);
        }

        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority == value) return;
                _priority = value;
                _markSortDirty?.Invoke();
                _markDirty?.Invoke();
            }
        }

        public DirectionBlendMode BlendMode
        {
            get => _blendMode;
            set
            {
                if (_blendMode == value) return;
                _blendMode = value;
                _markDirty?.Invoke();
            }
        }

        public float Influence
        {
            get => _influence;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_influence, clamped)) return;
                _influence = clamped;
                _markDirty?.Invoke();
            }
        }

        public float TransitionSpeedOverride
        {
            get => _transitionSpeedOverride;
            set
            {
                if (Mathf.Approximately(_transitionSpeedOverride, value)) return;
                _transitionSpeedOverride = value;
                _markDirty?.Invoke();
            }
        }

        public bool Enabled => _enabledLayer.Value;

        public bool TrySetDirection(Vector2 direction)
        {
            if (Approximately(_direction, direction))
                return false;

            _direction = direction;
            _markDirty?.Invoke();
            return true;
        }

        public void SetEnabled(string layerKey, bool enabled)
        {
            _enabledLayer.Set(layerKey, enabled);
            _markDirty?.Invoke();
        }

        public bool RemoveEnabled(string layerKey)
        {
            var removed = _enabledLayer.Remove(layerKey);
            if (removed)
                _markDirty?.Invoke();
            return removed;
        }

        static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) <= VectorEpsilon.x && Mathf.Abs(a.y - b.y) <= VectorEpsilon.y;
        }
    }
}
