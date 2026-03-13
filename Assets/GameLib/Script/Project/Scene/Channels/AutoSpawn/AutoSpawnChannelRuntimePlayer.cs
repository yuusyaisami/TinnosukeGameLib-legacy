#nullable enable

namespace Game.Channel
{
    internal sealed class AutoSpawnChannelRuntimePlayer
    {
        public AutoSpawnChannelDefinition Definition;
        public float NextSpawnTime;
        public bool Processing;
        public bool LoggedCondition;
        public bool LoggedMapping;
        public bool LoggedArea;
        public bool InitialSpawnDone;

        public AutoSpawnChannelRuntimePlayer(AutoSpawnChannelDefinition definition)
        {
            Definition = definition;
            NextSpawnTime = 0f;
            Processing = false;
            LoggedCondition = false;
            LoggedMapping = false;
            LoggedArea = false;
            InitialSpawnDone = false;
        }
    }
}
