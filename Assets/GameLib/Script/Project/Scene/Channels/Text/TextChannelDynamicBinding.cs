#nullable enable
using Game.Common;
using Game.Commands.VNext;

namespace Game.Channel
{
    public enum TextDynamicBindingPlayMode
    {
        Instant = 10,
        Counter = 20,
    }

    public enum TextDynamicBindingWatchMode
    {
        EventAndPoll = 10,
        PollOnly = 20,
    }

    public static class TextDynamicBindingDefaults
    {
        public const int PollIntervalFrames = 15;
    }

    public readonly struct TextDynamicBindingRegisterRequest
    {
        public readonly string ChannelTag;
        public readonly DynamicValue<string> Source;
        public readonly TextDynamicBindingPlayMode PlayMode;
        public readonly SetTextSettings CounterSettings;
        public readonly IVarStore SnapshotVars;
        public readonly CommandContext SourceContext;
        public readonly IScopeNode OwnerActor;
        public readonly int PollIntervalFrames;

        public TextDynamicBindingRegisterRequest(
            string channelTag,
            DynamicValue<string> source,
            TextDynamicBindingPlayMode playMode,
            SetTextSettings counterSettings,
            IVarStore snapshotVars,
            CommandContext sourceContext,
            IScopeNode ownerActor,
            int pollIntervalFrames = TextDynamicBindingDefaults.PollIntervalFrames)
        {
            ChannelTag = channelTag;
            Source = source;
            PlayMode = playMode;
            CounterSettings = counterSettings;
            SnapshotVars = snapshotVars ?? NullVarStore.Instance;
            SourceContext = sourceContext;
            OwnerActor = ownerActor;
            PollIntervalFrames = pollIntervalFrames < 1 ? TextDynamicBindingDefaults.PollIntervalFrames : pollIntervalFrames;
        }
    }
}
