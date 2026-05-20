#nullable enable
using System;
using Game.Commands.VNext;
using Game.Times;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    [Serializable]
    public sealed class TimerValueSource : IDynamicSource, IDynamicTrackedEvaluationPolicyProvider
    {
        [SerializeField, LabelText("Timer Key")]
        string timerKey = "default";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField] ActorSource actorSource;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "TimerValue";
        public string GetDebugData => timerKey ?? "null";
        public bool AllowTrackedEvaluation => false;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null || string.IsNullOrWhiteSpace(timerKey))
                return DynamicVariant.Null;

            var scope = ActorSourceFastResolver.ResolveCached(context, actorSource, ref _cache);
            if (scope?.Resolver == null)
                return DynamicVariant.Null;

            if (!scope.Resolver.TryResolve<ITimerHubService>(out var hub) || hub == null)
                return DynamicVariant.Null;

            if (!hub.TryGetRuntime(timerKey.Trim(), out var runtime) || runtime == null)
                return DynamicVariant.Null;

            return DynamicVariant.FromFloat(runtime.CurrentTime);
        }
    }
}
