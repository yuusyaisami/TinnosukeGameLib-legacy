#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public struct ScopeFlagOption
    {
        [LabelText("Apply")]
        public bool Apply;

        [ShowIf(nameof(Apply))]
        [LabelText("Value")]
        public bool Value;

        public bool TryGetValue(out bool value)
        {
            value = Value;
            return Apply;
        }
    }

    [Serializable]
    public sealed class LifetimeScopeStateCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetLifetimeScopeState;
        public string DebugData
        {
            get
            {
                var actorLabel = ActorSourceOdinLabelHelper.GetLabel("Actor", ActorSource);
                var active = Active.Apply ? Active.Value.ToString() : "<none>";
                var visible = Visible.Apply ? Visible.Value.ToString() : "<none>";
                return $"{actorLabel} Scope={ExecutionScope} Active={active} Visible={visible}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;

        [LabelText("Execution Scope")]
        [EnumToggleButtons]
        public WithActorExecutionScope ExecutionScope = WithActorExecutionScope.ActorOnly;

        [LabelText("Active")]
        public ScopeFlagOption Active = new();

        [LabelText("Visible")]
        public ScopeFlagOption Visible = new();
    }
}
