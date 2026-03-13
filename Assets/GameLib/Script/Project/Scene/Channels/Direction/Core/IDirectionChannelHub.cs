using System;
using UnityEngine;
using Game.Common;

namespace Game.Direction
{
    /// <summary>
    /// Blending modes available for direction layers.
    /// </summary>
    public enum DirectionBlendMode
    {
        Override,
        Add,
        Weighted,
        MaxMagnitude,
    }

    /// <summary>
    /// Initial configuration for a directional layer.
    /// </summary>
    [Serializable]
    public struct DirectionLayerDef
    {
        public string Tag;
        public Vector2 InitialDirection;
        public int Priority;
        public DirectionBlendMode BlendMode;
        public float Influence;
        public float TransitionSpeedOverride;
        public bool EnabledByDefault;

        public DirectionLayerDef(
            string tag,
            Vector2 initialDirection = default,
            int priority = 0,
            DirectionBlendMode blendMode = DirectionBlendMode.Add,
            float influence = 1f,
            float transitionSpeedOverride = -1f,
            bool enabledByDefault = true)
        {
            Tag = tag;
            InitialDirection = initialDirection;
            Priority = priority;
            BlendMode = blendMode;
            Influence = influence;
            TransitionSpeedOverride = transitionSpeedOverride;
            EnabledByDefault = enabledByDefault;
        }
    }

    /// <summary>
    /// Allows direction layers to expose their runtime state.
    /// </summary>
    public interface IDirectionChannelHandle : IEnabledLayerState
    {
        string Tag { get; }
        Vector2 Direction { get; set; }
        int Priority { get; set; }
        DirectionBlendMode BlendMode { get; set; }
        float Influence { get; set; }
        float TransitionSpeedOverride { get; set; }
        bool TrySetDirection(Vector2 direction);
    }

    /// <summary>
    /// Versioned output published by the direction hub.
    /// </summary>
    public interface IDirectionOutput
    {
        Vector2 OutputValue { get; }
        Vector2 TargetValue { get; }
        uint Version { get; }
        bool HasChanged(uint lastVersion);
    }

    /// <summary>
    /// Direction channel hub interface.
    /// </summary>
    public interface IDirectionChannelHub : IDisposable
    {
        IDirectionOutput Output { get; }
        Vector2 Target { get; }
        int LayerCount { get; }

        IDirectionChannelHandle RegisterLayer(string tag, DirectionLayerDef def);
        void UnregisterLayer(string tag);
        bool TryGetLayer(string tag, out IDirectionChannelHandle handle);
        bool ContainsLayer(string tag);
    }
}
