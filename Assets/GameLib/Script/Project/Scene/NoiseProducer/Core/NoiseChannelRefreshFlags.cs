#nullable enable
using System;

namespace Game.NoiseProducer
{
    [Flags]
    public enum NoiseChannelRefreshFlags
    {
        None = 0,
        ResolveParameters = 1,
        RebuildMaterials = 2,
        RecreateTargets = 4,
        ReloadDefinition = 8,
        Full = ResolveParameters | RebuildMaterials | RecreateTargets | ReloadDefinition,
    }
}
