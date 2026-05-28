#nullable enable

namespace Game
{
    public enum LifetimeScopeKind
    {
        None = 0,
        Project = 1,
        Platform = 2,
        Global = 3,
        Scene = 4,
        Field = 5,
        Entity = 6,
        UI = 7,
        UIElement = 8,
        Runtime = 9,
    }
}

namespace Game.Times
{
    public enum TimeScaleBehavior
    {
        Scaled = 0,
        Unscaled = 1,
    }
}