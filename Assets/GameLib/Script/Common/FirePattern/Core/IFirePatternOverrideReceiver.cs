#nullable enable

namespace Game.Fire
{
    /// <summary>
    /// Receives runtime overrides for fire pattern inputs.
    /// Intended to be set by runtime templates during OnAcquire, before spawn notifications.
    /// </summary>
    public interface IFirePatternOverrideReceiver
    {
        void SetOverridePattern(BaseFirePattern? pattern);
    }
}
