#nullable enable
using UnityEngine;

namespace Game.RoomMap
{
    /// <summary>
    /// RoomMapTileRegistry の leaf（TileId）を階層ドロップダウンで選択する。
    /// int フィールド専用。
    /// </summary>
    public sealed class RoomMapTileIdDropdownAttribute : PropertyAttribute
    {
        public string Filter { get; }
        public bool AllowNone { get; }

        public RoomMapTileIdDropdownAttribute(string filter = "", bool allowNone = true)
        {
            Filter = filter ?? string.Empty;
            AllowNone = allowNone;
        }
    }
}
