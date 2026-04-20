#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;

namespace Game.Commands
{
    [DisallowMultipleComponent]
    public sealed class MonitorRuleMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Monitor Rules")]
        [LabelText("Monitor Rules")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        MonitorRule[] _monitorRules = Array.Empty<MonitorRule>();

        [BoxGroup("Monitor Rules")]
        [LabelText("Shared Expression Variables")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        List<ExpressionVariable> _sharedExpressionVariables = new();

        void Awake()
        {
            EnsureRuleDefaults();
            BindDebugOwners();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureRuleDefaults();
            BindDebugOwners();
        }
#endif

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // Register a scope-aware handler that will add/remove rules when the scope acquires/releases.
            builder.Register<MonitorRuleService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(_monitorRules)
                .WithParameter(_sharedExpressionVariables)
                .WithParameter(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void BindDebugOwners()
        {
            if (_monitorRules == null || _monitorRules.Length == 0)
                return;

            for (int i = 0; i < _monitorRules.Length; i++)
            {
                var rule = _monitorRules[i];
                if (rule.OnEnterCommands != null)
                {
                    rule.OnEnterCommands.BindDebugOwner(this, $"_monitorRules[{i}].OnEnterCommands");
                    rule.OnEnterCommands.SetFunctionName("MonitorRule.OnEnter");
                }
                if (rule.OnExitCommands != null)
                {
                    rule.OnExitCommands.BindDebugOwner(this, $"_monitorRules[{i}].OnExitCommands");
                    rule.OnExitCommands.SetFunctionName("MonitorRule.OnExit");
                }
                if (rule.WhileTrueCommands.Commands != null)
                {
                    rule.WhileTrueCommands.Commands.BindDebugOwner(this, $"_monitorRules[{i}].WhileTrueCommands.Commands");
                    rule.WhileTrueCommands.Commands.SetFunctionName("MonitorRule.WhileTrue", overwrite: true);
                }
                if (rule.WhileFalseCommands.Commands != null)
                {
                    rule.WhileFalseCommands.Commands.BindDebugOwner(this, $"_monitorRules[{i}].WhileFalseCommands.Commands");
                    rule.WhileFalseCommands.Commands.SetFunctionName("MonitorRule.WhileFalse", overwrite: true);
                }
            }
        }

        void EnsureRuleDefaults()
        {
            if (_monitorRules == null || _monitorRules.Length == 0)
                return;

            for (int i = 0; i < _monitorRules.Length; i++)
            {
                var rule = _monitorRules[i];
                rule.EnsureDefaults();
                _monitorRules[i] = rule;
            }
        }
    }
    sealed class MonitorRuleService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        sealed class BlackboardBridgeBinding
        {
            public IBlackboardService Blackboard = null!;
            public Action<int> Handler = null!;
        }

        readonly MonitorRule[] _rules;
        readonly IReadOnlyList<ExpressionVariable> _sharedExpressionVariables;
        readonly MonitorRuleMB _owner;
        readonly string[] _effectiveRuleNames;
        readonly HashSet<int> _blackboardBridgeVarIds = new();
        readonly List<BlackboardBridgeBinding> _blackboardBridgeBindings = new();
        VarStore? _vars;
        bool _ownsVarStore;
        IMonitorChannelHub? _hub;
        IScopeNode? _effectiveScope;
        IScopeNode featureScope;

        public MonitorRuleService(IScopeNode scope, MonitorRule[] rules, IReadOnlyList<ExpressionVariable> sharedExpressionVariables, MonitorRuleMB owner)
        {
            _rules = rules ?? Array.Empty<MonitorRule>();
            _sharedExpressionVariables = sharedExpressionVariables ?? (IReadOnlyList<ExpressionVariable>)Array.Empty<ExpressionVariable>();
            _owner = owner;
            _effectiveRuleNames = new string[_rules.Length];
            featureScope = scope;
        }

        string GetOrCreateRuleName(in IScopeNode scope, int index, in MonitorRule rule)
        {
            if (!string.IsNullOrEmpty(rule.RuleName))
                return rule.RuleName;

            var cached = _effectiveRuleNames[index];
            if (!string.IsNullOrEmpty(cached))
                return cached;

            // Auto-generate a stable key within this session so rules still work even if RuleName is left empty.
            // This avoids silent no-op (RulesCount>0 but runtimes stay 0).
            var scopeId = scope.Identity?.Id ?? "(none)";
            var ownerId = _owner != null ? _owner.GetInstanceID() : 0;
            var name = $"__auto__:{scope.Kind}:{scopeId}:MonitorRuleMB#{ownerId}:{index}";
            _effectiveRuleNames[index] = name;
            return name;
        }

        static bool TryResolveLocalHub(IScopeNode scope, out IMonitorChannelHub? hub)
        {
            hub = null;
            if (scope == null || scope.Resolver == null)
                return false;

            var resolver = scope.Resolver;
            if (resolver.TryResolve<IScopeMultiRegistry>(out var registry) && registry != null)
            {
                if (registry.TryGetSingle<IMonitorChannelHub>(out var localHub) && localHub != null)
                {
                    hub = localHub;
                    return true;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[MonitorRuleMB] Local IMonitorChannelHub not found. Scope={DescribeScope(scope)} Count={registry.Count<IMonitorChannelHub>()}");
#endif
                return false;
            }

            if (resolver.TryResolve<IMonitorChannelHub>(out var resolvedHub) && resolvedHub != null)
            {
                hub = resolvedHub;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[MonitorRuleMB] IMonitorChannelHub not found. Scope={DescribeScope(scope)}");
#endif
            return false;
        }

        static IScopeNode ResolveEffectiveScope(IScopeNode scope, MonitorRuleMB owner)
        {
            if (owner is Component comp &&
                ScopeFeatureInstallerUtility.TryGetNearestScopeNode(comp, includeInactive: true, out var nearest) &&
                nearest != null)
            {
                return nearest;
            }

            return scope;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (scope == null || scope.Resolver == null)
                return;

            if (!ReferenceEquals(scope, featureScope))
                return;

            try
            {
                var effectiveScope = ResolveEffectiveScope(scope, _owner);
                _effectiveScope = effectiveScope;

                //Debug.LogError($"[MonitorRuleMB] OnAcquire called. ScopeKind={scope.Kind}, ScopeId={scope.Identity?.Id ?? "(none)"}");
                if (!TryResolveLocalHub(_effectiveScope, out var hub) || hub == null)
                    return;
                _hub = hub;

                //Debug.LogError($"[MonitorRuleMB] OnAcquire. ScopeKind={scope.Kind}, ScopeId={scope.Identity?.Id ?? "(none)"}, HubType={hub.GetType().Name}, HubVars={(hub.CurrentVarStore != null ? "OK" : "NULL")}");

                //Debug.LogError($"[MonitorRuleMB] RulesCount={_rules.Length}");

                if (hub.CurrentVarStore == null)
                {
                    _vars = new VarStore();
                    hub.AttachToVars(_vars);
                    _ownsVarStore = true;
                }
                else
                {
                    _vars = null;
                    _ownsVarStore = false;
                }

                for (int i = 0; i < _rules.Length; i++)
                {
                    var r = _rules[i];
                    var ruleName = GetOrCreateRuleName(_effectiveScope, i, r);

                    try
                    {
                        if (_sharedExpressionVariables.Count > 0)
                        {
                            r.Condition.TrySetExternalExpressionVariables(_sharedExpressionVariables);
                        }
                        var effectiveRule = r;
                        effectiveRule.RuleName = ruleName;

                        hub.RemoveRule(ruleName);
                        hub.AddRule(effectiveRule);

                        //Debug.LogError($"[MonitorRuleMB] Added rule '{ruleName}'. Kind={r.RuleKind}, ValueChangedMode={r.ValueChangedMode}, Targets={r.GetValueChangedTargetCount()}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                TrySetupBlackboardBridge(_effectiveScope, hub);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (scope == null || scope.Resolver == null)
                return;

            if (!ReferenceEquals(scope, featureScope))
                return;

            try
            {
                var effectiveScope = _effectiveScope ?? ResolveEffectiveScope(scope, _owner);
                var hub = _hub;
                if (hub == null)
                {
                    if (!TryResolveLocalHub(effectiveScope, out hub) || hub == null)
                        return;
                }

                if (_ownsVarStore && _vars != null && ReferenceEquals(hub.CurrentVarStore, _vars))
                {
                    hub.DetachFromVars(_vars);
                }
                UnsubscribeBlackboardBridge();
                _vars = null;
                _ownsVarStore = false;

                for (int i = 0; i < _rules.Length; i++)
                {
                    var r = _rules[i];
                    var ruleName = GetOrCreateRuleName(effectiveScope, i, r);

                    try
                    {
                        r.Condition.TryClearExternalExpressionVariables();
                        hub.RemoveRule(ruleName);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                _hub = null;
                _effectiveScope = null;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void TrySetupBlackboardBridge(IScopeNode? scope, IMonitorChannelHub hub)
        {
            _blackboardBridgeVarIds.Clear();
            UnsubscribeBlackboardBridge();
            if (scope == null || hub == null)
                return;

            CollectBlackboardBridgeVarIds();
            if (_blackboardBridgeVarIds.Count == 0)
                return;

            for (IScopeNode? node = scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null || !resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                    continue;

                var localVars = blackboard.LocalVars;
                if (localVars == null)
                    continue;

                var binding = new BlackboardBridgeBinding
                {
                    Blackboard = blackboard,
                    Handler = varId => OnBlackboardVarChanged(blackboard, varId),
                };

                localVars.OnVarChanged += binding.Handler;
                _blackboardBridgeBindings.Add(binding);
                SyncBlackboardBridge(localVars, hub.CurrentVarStore);
            }
        }

        void CollectBlackboardBridgeVarIds()
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (!rule.Condition.HasSource)
                    continue;

                var keys = rule.Condition.GetDependentKeys();
                if (keys == null || keys.Count == 0)
                    continue;

                for (int k = 0; k < keys.Count; k++)
                {
                    var key = keys[k];
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                        continue;

                    _blackboardBridgeVarIds.Add(varId);
                }
            }
        }

        void SyncBlackboardBridge(IVarStore? source, IVarStore? destination)
        {
            if (source == null || destination == null || _blackboardBridgeVarIds.Count == 0)
                return;

            var ids = source.EnumerateVarIds();
            foreach (var varId in ids)
            {
                if (!_blackboardBridgeVarIds.Contains(varId))
                    continue;

                if (source.TryGetVariant(varId, out var value))
                    destination.TrySetVariant(varId, value);
            }
        }

        void OnBlackboardVarChanged(IBlackboardService blackboard, int varId)
        {
            if (varId == 0 || !_blackboardBridgeVarIds.Contains(varId))
                return;

            var monitorVars = _vars ?? _hub?.CurrentVarStore;
            if (blackboard == null || monitorVars == null)
                return;

            if (blackboard.LocalVars.TryGetVariant(varId, out var value))
                monitorVars.TrySetVariant(varId, value);
        }

        void UnsubscribeBlackboardBridge()
        {
            for (int i = _blackboardBridgeBindings.Count - 1; i >= 0; i--)
            {
                try
                {
                    var binding = _blackboardBridgeBindings[i];
                    var blackboard = binding.Blackboard;
                    if (blackboard != null && blackboard.LocalVars != null)
                        blackboard.LocalVars.OnVarChanged -= binding.Handler;
                }
                catch
                {
                }
            }

            _blackboardBridgeBindings.Clear();
            _blackboardBridgeVarIds.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }
#endif
    }
}
