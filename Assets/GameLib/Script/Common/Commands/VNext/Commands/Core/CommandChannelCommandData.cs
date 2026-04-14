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
        [PropertyTooltip("CommandChannel を取得する所有者スコープ。既存挙動ではこのスコープが実行Actorにもなります。")]
        public ActorSource ActorSource;

        [LabelText("Execution Actor Mode")]
        public CommandChannelExecutionActorMode ExecutionActorMode = CommandChannelExecutionActorMode.UseChannelOwner;

        [ShowIf("@ExecutionActorMode == Game.Commands.VNext.CommandChannelExecutionActorMode.UseActorSource")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ExecutorActorSource)")]
        [PropertyTooltip("Execution Actor Mode が UseActorSource のとき、CommandList の実行Actorとして使用するスコープ。")]
        public ActorSource ExecutorActorSource;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        [PropertyTooltip("ActorSource の対象だけ実行するか、子孫にも同じ Tag を実行するかを指定します。")]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorOnly;

        [LabelText("Tag")]
        public string Tag = string.Empty;

        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [LabelText("Background Cancel Mode")]
        [ShowIf(nameof(UsesBackgroundAwaitMode))]
        [EnumToggleButtons]
        [PropertyTooltip("Run In Background 時に、呼び出し元のキャンセルへ追従するかを指定します。DetachFromCaller を選ぶと、呼び出し元がキャンセルされても CommandChannel の実行を継続します。")]
        public CommandChannelBackgroundCancellationMode BackgroundCancellationMode = CommandChannelBackgroundCancellationMode.FollowCaller;

        bool UsesBackgroundAwaitMode()
            => AwaitMode == FlowRunAwaitMode.RunInBackground;
    }
}
