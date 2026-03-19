#nullable enable
using System.Collections.Generic;
using Game.Common;

namespace Game.Commands
{
    public readonly struct MonitorRuleDebugSnapshot
    {
        public readonly string RuleName;
        public readonly MonitorRuleKind RuleKind;
        public readonly string EventName;
        public readonly Game.Commands.VNext.ActorSourceKind EventTargetKind;
        public readonly ExecutionBehavior Behavior;
        public readonly bool CancelRunningOnConditionChange;
        public readonly bool ExecuteInitialCondition;
        public readonly string ConditionSourceType;
        public readonly IReadOnlyList<string> DependentKeys;
        public readonly MonitorValueSourceKind ValueSource;
        public readonly MonitorValueChangeMode ValueChangeMode;
        public readonly int VarStoreVarId;
        public readonly int BlackboardVarId;
        public readonly BlackboardReadScope BlackboardReadScope;
        public readonly string ScalarKey;
        public readonly float ChangeEpsilon;
        public readonly bool ExecuteInitialValueChangedEnter;
        public readonly float InitialValueChangedEnterDelaySeconds;
        public readonly int OnEnterCommandCount;
        public readonly int OnExitCommandCount;
        public readonly int WhileTrueCommandCount;
        public readonly float WhileTrueIntervalSeconds;
        public readonly int WhileFalseCommandCount;
        public readonly float WhileFalseIntervalSeconds;
        public readonly bool RuntimeRegistered;
        public readonly bool RuntimeIsTrue;
        public readonly int RuntimeRunningCount;
        public readonly string RuntimeRunningPhases;

        public MonitorRuleDebugSnapshot(
            string ruleName,
            MonitorRuleKind ruleKind,
            string eventName,
            Game.Commands.VNext.ActorSourceKind eventTargetKind,
            ExecutionBehavior behavior,
            bool cancelRunningOnConditionChange,
            bool executeInitialCondition,
            string conditionSourceType,
            IReadOnlyList<string> dependentKeys,
            MonitorValueSourceKind valueSource,
            MonitorValueChangeMode valueChangeMode,
            int varStoreVarId,
            int blackboardVarId,
            BlackboardReadScope blackboardReadScope,
            string scalarKey,
            float changeEpsilon,
            bool executeInitialValueChangedEnter,
            float initialValueChangedEnterDelaySeconds,
            int onEnterCommandCount,
            int onExitCommandCount,
            int whileTrueCommandCount,
            float whileTrueIntervalSeconds,
            int whileFalseCommandCount,
            float whileFalseIntervalSeconds,
            bool runtimeRegistered,
            bool runtimeIsTrue,
            int runtimeRunningCount,
            string runtimeRunningPhases)
        {
            RuleName = ruleName;
            RuleKind = ruleKind;
            EventName = eventName;
            EventTargetKind = eventTargetKind;
            Behavior = behavior;
            CancelRunningOnConditionChange = cancelRunningOnConditionChange;
            ExecuteInitialCondition = executeInitialCondition;
            ConditionSourceType = conditionSourceType;
            DependentKeys = dependentKeys;
            ValueSource = valueSource;
            ValueChangeMode = valueChangeMode;
            VarStoreVarId = varStoreVarId;
            BlackboardVarId = blackboardVarId;
            BlackboardReadScope = blackboardReadScope;
            ScalarKey = scalarKey;
            ChangeEpsilon = changeEpsilon;
            ExecuteInitialValueChangedEnter = executeInitialValueChangedEnter;
            InitialValueChangedEnterDelaySeconds = initialValueChangedEnterDelaySeconds;
            OnEnterCommandCount = onEnterCommandCount;
            OnExitCommandCount = onExitCommandCount;
            WhileTrueCommandCount = whileTrueCommandCount;
            WhileTrueIntervalSeconds = whileTrueIntervalSeconds;
            WhileFalseCommandCount = whileFalseCommandCount;
            WhileFalseIntervalSeconds = whileFalseIntervalSeconds;
            RuntimeRegistered = runtimeRegistered;
            RuntimeIsTrue = runtimeIsTrue;
            RuntimeRunningCount = runtimeRunningCount;
            RuntimeRunningPhases = runtimeRunningPhases;
        }
    }

    public readonly struct MonitorRuleTelemetrySnapshot
    {
        public readonly int Version;
        public readonly bool HubAvailable;
        public readonly string ScopeKind;
        public readonly string ScopeId;
        public readonly int RuleCount;
        public readonly IReadOnlyList<MonitorRuleDebugSnapshot> Rules;

        public MonitorRuleTelemetrySnapshot(
            int version,
            bool hubAvailable,
            string scopeKind,
            string scopeId,
            int ruleCount,
            IReadOnlyList<MonitorRuleDebugSnapshot> rules)
        {
            Version = version;
            HubAvailable = hubAvailable;
            ScopeKind = scopeKind;
            ScopeId = scopeId;
            RuleCount = ruleCount;
            Rules = rules;
        }
    }

    public interface IMonitorRuleTelemetry
    {
        int TelemetryVersion { get; }
        MonitorRuleTelemetrySnapshot GetSnapshot();
    }
}
