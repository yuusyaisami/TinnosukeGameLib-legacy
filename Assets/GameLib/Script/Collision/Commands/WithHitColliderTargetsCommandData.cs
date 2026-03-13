#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WithHitColliderTargetsCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithHitColliderTargets;
        public string DebugData
        {
            get
            {
                var bodyCount = Body?.Count ?? 0;
                var actorLabel = ActorSourceOdinLabelHelper.GetLabel("Controller", ControllerSource);
                return $"{actorLabel} Rule='{RuleName}' Body={bodyCount}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Controller\", ControllerSource)")]
        public ActorSource ControllerSource;

        [LabelText("Rule Name")]
        public string RuleName = "default";

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [LabelText("Body")]
        [CommandListFunctionName("HitCollider.WithTargets")]
        public CommandListData Body = new();
    }
}
