#nullable enable
using System;
using Game.Channel;
using Game.Commands.VNext;
using Sirenix.OdinInspector;

namespace Game.RoomMap
{
    [Serializable]
    public sealed class RoomTileVisualData
    {
        [LabelText("Default Preset")]
        public AnimationSpritePreset? DefaultPreset;

        [LabelText("Tile Command")]
        public CommandListData TileCommand = new();

        [LabelText("Override Preset")]
        public bool OverridePreset = true;

        [LabelText("Override Command")]
        public bool OverrideCommand = true;

        [LabelText("Append Command")]
        [ShowIf(nameof(OverrideCommand))]
        public bool AppendCommand;

        public bool HasAnyCommand()
        {
            return TileCommand != null && TileCommand.Count > 0;
        }

        public void CopyFrom(RoomTileVisualData other)
        {
            if (other == null)
                return;

            DefaultPreset = other.DefaultPreset;
            TileCommand = other.TileCommand;
            OverridePreset = other.OverridePreset;
            OverrideCommand = other.OverrideCommand;
            AppendCommand = other.AppendCommand;
        }
    }
}
