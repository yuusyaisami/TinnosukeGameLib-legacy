#nullable enable
using System;

namespace Game.RoomMap
{
    [Flags]
    public enum RoomMapTileTagFlags
    {
        None = 0,
        Floor = 1 << 0,
        Wall = 1 << 1,
        Water = 1 << 2,
        Door = 1 << 3,
        Decoration = 1 << 4,
    }
}
