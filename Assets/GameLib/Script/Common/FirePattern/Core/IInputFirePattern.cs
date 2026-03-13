#nullable enable
using Game.Entity;
using Game.Spawn;

namespace Game.Fire
{
    public interface IInputFirePattern : ISpawnContextConsumer
    {
        string TargetChannelTag { get; }
        BaseFirePattern[] Patterns { get; }
    }
}
