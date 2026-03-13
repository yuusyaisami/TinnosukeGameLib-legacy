using System.Collections.Generic;

namespace Game.StateMachine
{
    public readonly struct StateAnimationRuleTelemetryRow
    {
        public readonly string RuleHeader;
        public readonly int Priority;
        public readonly string StateKey;
        public readonly string LayerKey;
        public readonly string ChannelTag;
        public readonly bool ApplyFlipX;
        public readonly string FlipXTrueOptionValue;
        public readonly bool Matched;
        public readonly string Reason;

        public StateAnimationRuleTelemetryRow(
            string ruleHeader,
            int priority,
            string stateKey,
            string layerKey,
            string channelTag,
            bool applyFlipX,
            string flipXTrueOptionValue,
            bool matched,
            string reason)
        {
            RuleHeader = ruleHeader;
            Priority = priority;
            StateKey = stateKey;
            LayerKey = layerKey;
            ChannelTag = channelTag;
            ApplyFlipX = applyFlipX;
            FlipXTrueOptionValue = flipXTrueOptionValue;
            Matched = matched;
            Reason = reason;
        }
    }

    public readonly struct StateAnimationTelemetrySnapshot
    {
        public readonly int Version;
        public readonly uint MachineRevision;
        public readonly string CurrentState;
        public readonly string CurrentLayer;
        public readonly string ProfileName;
        public readonly string EvaluationSummary;
        public readonly string ActiveRuleHeader;
        public readonly int ActiveRulePriority;
        public readonly string ActiveRuleChannelTag;
        public readonly bool HasCurrentPlayer;
        public readonly bool HasFlipX;
        public readonly bool LastFlipX;
        public readonly bool PendingFlipXActive;
        public readonly bool PendingFlipXValue;
        public readonly float PendingFlipXElapsedSeconds;
        public readonly bool ActiveRuleApplyFlipX;
        public readonly string ActiveRuleFlipXTrueOptionValue;
        public readonly string FlipDecisionDetail;
        public readonly string FlipConfiguredOptionValue;
        public readonly string FlipDerivedOptionKey;
        public readonly string FlipResolvedByDerivedKey;
        public readonly string FlipResolvedByConfiguredKey;
        public readonly IReadOnlyList<StateAnimationRuleTelemetryRow> Rules;

        public StateAnimationTelemetrySnapshot(
            int version,
            uint machineRevision,
            string currentState,
            string currentLayer,
            string profileName,
            string evaluationSummary,
            string activeRuleHeader,
            int activeRulePriority,
            string activeRuleChannelTag,
            bool hasCurrentPlayer,
            bool hasFlipX,
            bool lastFlipX,
            bool pendingFlipXActive,
            bool pendingFlipXValue,
            float pendingFlipXElapsedSeconds,
            bool activeRuleApplyFlipX,
            string activeRuleFlipXTrueOptionValue,
            string flipDecisionDetail,
            string flipConfiguredOptionValue,
            string flipDerivedOptionKey,
            string flipResolvedByDerivedKey,
            string flipResolvedByConfiguredKey,
            IReadOnlyList<StateAnimationRuleTelemetryRow> rules)
        {
            Version = version;
            MachineRevision = machineRevision;
            CurrentState = currentState;
            CurrentLayer = currentLayer;
            ProfileName = profileName;
            EvaluationSummary = evaluationSummary;
            ActiveRuleHeader = activeRuleHeader;
            ActiveRulePriority = activeRulePriority;
            ActiveRuleChannelTag = activeRuleChannelTag;
            HasCurrentPlayer = hasCurrentPlayer;
            HasFlipX = hasFlipX;
            LastFlipX = lastFlipX;
            PendingFlipXActive = pendingFlipXActive;
            PendingFlipXValue = pendingFlipXValue;
            PendingFlipXElapsedSeconds = pendingFlipXElapsedSeconds;
            ActiveRuleApplyFlipX = activeRuleApplyFlipX;
            ActiveRuleFlipXTrueOptionValue = activeRuleFlipXTrueOptionValue;
            FlipDecisionDetail = flipDecisionDetail;
            FlipConfiguredOptionValue = flipConfiguredOptionValue;
            FlipDerivedOptionKey = flipDerivedOptionKey;
            FlipResolvedByDerivedKey = flipResolvedByDerivedKey;
            FlipResolvedByConfiguredKey = flipResolvedByConfiguredKey;
            Rules = rules;
        }
    }

    public interface IStateAnimationTelemetry
    {
        int TelemetryVersion { get; }
        StateAnimationTelemetrySnapshot GetTelemetrySnapshot();
    }
}
