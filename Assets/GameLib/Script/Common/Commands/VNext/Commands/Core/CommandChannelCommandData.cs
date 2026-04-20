#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum CommandChannelExecutionActorMode
    {
        UseChannelOwner = 0,
        UseCurrentActor = 10,
        UseActorSource = 20,
    }

    public enum CommandChannelBackgroundCancellationMode
    {
        FollowCaller = 0,
        DetachFromCaller = 10,
    }

    [Serializable]
    public sealed class CommandChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.CommandChannelExecute;
        public string DebugData => $"Owner={ActorSource.Kind} Scope={ExecutionScope} Exec={ExecutionActorMode} Tag={Tag} Await={AwaitMode} BgCancel={BackgroundCancellationMode}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource ActorSource;

        [LabelText("Execution Actor Mode")]
        public CommandChannelExecutionActorMode ExecutionActorMode = CommandChannelExecutionActorMode.UseChannelOwner;

        [ShowIf("@ExecutionActorMode == Game.Commands.VNext.CommandChannelExecutionActorMode.UseActorSource")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ExecutorActorSource)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource ExecutorActorSource;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        [PropertyTooltip("Inspector setting.")]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorOnly;

        [LabelText("Tag")]
        public string Tag = string.Empty;

        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [LabelText("Background Cancel Mode")]
        [ShowIf(nameof(UsesBackgroundAwaitMode))]
        [EnumToggleButtons]
        [PropertyTooltip("Inspector setting.")]
        public CommandChannelBackgroundCancellationMode BackgroundCancellationMode = CommandChannelBackgroundCancellationMode.FollowCaller;

        bool UsesBackgroundAwaitMode()
            => AwaitMode == FlowRunAwaitMode.RunInBackground;
    }
}
