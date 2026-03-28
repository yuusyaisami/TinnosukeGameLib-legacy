#nullable enable

namespace Game.UI
{
    internal interface ISliderVisualBackend
    {
        void OnAcquire(IScopeNode scope, bool isReset);
        void OnRelease(IScopeNode scope, bool isReset);
        void Tick();
    }
}
