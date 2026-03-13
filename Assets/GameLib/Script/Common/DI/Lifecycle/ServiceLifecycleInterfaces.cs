#nullable enable

namespace Game.DI
{
    /// <summary>
    /// Service interface for objects that can reset their internal state when reused (e.g., pooled).
    /// </summary>
    public interface IResettableService
    {
        void Reset();
    }

    /// <summary>
    /// Service interface for objects that can be enabled/disabled without disposal.
    /// </summary>
    public interface IEnabledService
    {
        bool IsEnabled { get; }
        void SetEnabled(bool enabled);
    }
}

