#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;

namespace Game.Channel
{
    sealed class MeshTrackEvaluationContext
    {
        public readonly IScopeNode Scope;
        public readonly IDynamicContext DynamicContext;
        public readonly float DeltaTime;
        public readonly float TimeSeconds;
        public readonly int FrameIndex;

        public MeshTrackEvaluationContext(IScopeNode scope, IDynamicContext dynamicContext, float deltaTime, float timeSeconds, int frameIndex)
        {
            Scope = scope;
            DynamicContext = dynamicContext;
            DeltaTime = deltaTime;
            TimeSeconds = timeSeconds;
            FrameIndex = frameIndex;
        }
    }

    sealed class MeshSimulationContext
    {
        public readonly float DeltaTime;
        public readonly float TimeSeconds;
        public readonly IReadOnlyList<MeshHitContactInfo> Hits;

        public MeshSimulationContext(float deltaTime, float timeSeconds, IReadOnlyList<MeshHitContactInfo> hits)
        {
            DeltaTime = deltaTime;
            TimeSeconds = timeSeconds;
            Hits = hits;
        }
    }
}
