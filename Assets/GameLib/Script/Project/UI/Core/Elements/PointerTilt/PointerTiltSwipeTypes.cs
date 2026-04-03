#nullable enable

namespace Game.UI
{
    public enum PointerTiltEnvironmentMode
    {
        Auto = 10,
        ScreenUI = 20,
        World = 30,
    }

    public enum PointerTiltSwipeState
    {
        Idle = 10,
        PressedCandidate = 20,
        ThresholdReached = 30,
    }
}
