namespace Game.Common
{
    /// <summary>
    /// Provides layer-based enabled/disabled control for components such as movement or direction producers.
    /// </summary>
    public interface IEnabledLayerState
    {
        bool Enabled { get; }
        void SetEnabled(string layerKey, bool enabled);
        bool RemoveEnabled(string layerKey);
    }
}
