using System;
using System.Collections.Generic;
using System.Text;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Game.Common
{
    public enum RichTextSourceMode
    {
        Template = 0,
        RefService = 1,
    }

    [Serializable]
    public sealed class RichTextSource : IDynamicSource, IExpressionSource, IExternalExpressionVariablesReceiver, IDynamicSourceConfigurationRevisionProvider, IDynamicSourceDependencyRevisionProvider
    {
        [LabelText("Source Mode")]
        [SerializeField]
        [OnValueChanged(nameof(MarkDirty), true)]
        RichTextSourceMode _sourceMode = RichTextSourceMode.Template;

        [LabelText("Ref Key")]
        [SerializeField]
        [ShowIf(nameof(IsRefServiceMode))]
        DynamicValue<string> _refKey;

        [LabelText("Log Actor Stores On Warn")]
        [SerializeField]
        [ShowIf(nameof(IsRefServiceMode))]
        [FormerlySerializedAs("_logActorBlackboardOnWarn")]
        bool _logActorStoresOnWarn;

        [LabelText("Max Actor Store Entries")]
        [SerializeField]
        [MinValue(0)]
        [ShowIf(nameof(ShouldShowActorStoreEntryLimit))]
        [FormerlySerializedAs("_maxActorBlackboardEntries")]
        int _maxActorStoreEntries = 16;

        [LabelText("Allow Implicit Keys")]
        [SerializeField]
        [ShowIf(nameof(IsTemplateMode))]
        bool _allowImplicitKeys = true;

        [LabelText("@GetExpressionVariablesDebugData()")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        [OnValueChanged(nameof(MarkDirty), true)]
        [ShowIf(nameof(IsTemplateMode))]
        List<ExpressionVariable> _variables = new();

        [NonSerialized]
        IReadOnlyList<ExpressionVariable> _externalVariables;

        [NonSerialized]
        bool _includeLocalVariablesWithExternal;

        [LabelText("Template")]
        [PropertyTooltip("$ExpressionFunctionTooltip")]
        [SerializeField]
        [OnValueChanged(nameof(Validate))]
        [MultiLineProperty(3)]
        [ShowIf(nameof(IsTemplateMode))]
        string _template = string.Empty;

        [ShowInInspector]
        [ReadOnly]
        [LabelText("Validation")]
        [GUIColor(nameof(GetValidationColor))]
        [ShowIf(nameof(IsTemplateMode))]
        string _validationMessage;

        // caches
        Dictionary<string, DynamicValue> _scopeMap;
        Dictionary<string, ValueKind> _typeMap;
        List<RichTextNode> _compiledNodes;
        HashSet<string> _usedIdentifiers;
        List<string> _usedIdentifiersList;
        RichTextEvalScope _evalScope;
        RichTextValueProxySource _valueProxy;
        StringBuilder _builder;

        bool _dirty = true;
        bool _validationIsError;
        int _lastEmptyWarnFrame = -1;
        int _configurationRevision;

        bool IsTemplateMode => _sourceMode == RichTextSourceMode.Template;
        bool IsRefServiceMode => _sourceMode == RichTextSourceMode.RefService;
        bool ShouldShowActorStoreEntryLimit => IsRefServiceMode && _logActorStoresOnWarn;
        string ExpressionFunctionTooltip => ExpressionFunctionRegistry.GetInspectorFunctionTooltip();
        public RichTextSourceMode SourceMode => _sourceMode;

        public string SourceTypeName => _sourceMode == RichTextSourceMode.RefService ? "RichTextRef" : "RichText";
        public string GetDebugData => _sourceMode == RichTextSourceMode.RefService
            ? (_refKey.HasSource ? "(ref key set)" : "(ref key empty)")
            : (string.IsNullOrEmpty(_template) ? "(empty)" : _template);
        public int GetSourceConfigurationRevision() => _configurationRevision;

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = 0;

            if (_refKey.HasSource)
                revision = unchecked((revision * 397) ^ _refKey.GetSourceDependencyRevision(context));

            if (_externalVariables != null)
            {
                foreach (var variable in _externalVariables)
                {
                    if (variable == null)
                        continue;

                    revision = unchecked((revision * 397) ^ variable.GetSourceDependencyRevision(context));
                }
            }

            if (_variables != null && (_includeLocalVariablesWithExternal || _externalVariables == null))
            {
                foreach (var variable in _variables)
                {
                    if (variable == null)
                        continue;

                    revision = unchecked((revision * 397) ^ variable.GetSourceDependencyRevision(context));
                }
            }

            return revision;
        }

        public string Template
        {
            get => _template;
            set
            {
                if (_template == value)
                    return;
                _template = value ?? string.Empty;
                MarkDirty();
            }
        }

        public bool AllowImplicitKeys
        {
            get => _allowImplicitKeys;
            set
            {
                if (_allowImplicitKeys == value)
                    return;
                _allowImplicitKeys = value;
                MarkDirty();
            }
        }

        public IReadOnlyList<ExpressionVariable> Variables => _variables;

        public void SetVariables(IReadOnlyList<ExpressionVariable> variables)
        {
            _variables.Clear();
            if (variables != null)
                _variables.AddRange(variables);
            MarkDirty();
        }

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_sourceMode == RichTextSourceMode.RefService)
            {
                return EvaluateRefService(context);
            }

            var ctx = context ?? DummyDynamicContext.Instance;

            if (_dirty || _compiledNodes == null)
            {
                if (!TryCompile(out _validationMessage))
                {
                    _validationIsError = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    RichTextRuntimeLogger.Error(
                        "RTS-COMPILE-FAILED",
                        "Template compilation failed.",
                        BuildRuntimeLogContext(
                            ctx,
                            "Compile",
                            detail: _validationMessage,
                            variables: BuildVariableDefinitionSummary()));
#endif
                    return DynamicVariant.FromString(string.Empty);
                }
            }

            if (_compiledNodes == null || _compiledNodes.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(_template))
                {
                    RichTextRuntimeLogger.Warn(
                        "RTS-COMPILED-NODES-EMPTY",
                        "Compiled node list is empty.",
                        BuildRuntimeLogContext(
                            ctx,
                            "Evaluate",
                            detail: "Template resolved to zero nodes.",
                            variables: BuildVariableDefinitionSummary()));
                }
#endif
                return DynamicVariant.FromString(string.Empty);
            }

            if (_evalScope == null)
                _evalScope = new RichTextEvalScope(ctx, _scopeMap, _valueProxy);
            else
                _evalScope.UpdateContext(ctx);

            if (_builder == null)
                _builder = new StringBuilder(64);
            else
                _builder.Clear();

            for (int i = 0; i < _compiledNodes.Count; i++)
                _builder.Append(_compiledNodes[i].Evaluate(_evalScope));

            var result = _builder.ToString();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(_template) && string.IsNullOrEmpty(result) && _lastEmptyWarnFrame != Time.frameCount)
            {
                _lastEmptyWarnFrame = Time.frameCount;
                RichTextRuntimeLogger.Warn(
                    "RTS-EVALUATED-EMPTY",
                    "Template evaluated to an empty string.",
                    BuildRuntimeLogContext(
                        ctx,
                        "Evaluate",
                        detail: $"nodes={_compiledNodes.Count}",
                        variables: BuildVariableEvaluationDebug(ctx)));
            }
#endif
            return DynamicVariant.FromString(result);
        }

        public IReadOnlyList<string> GetDependentKeys()
        {
            if (_sourceMode == RichTextSourceMode.RefService)
                return null;

            if (_dirty || _usedIdentifiers == null)
                TryCompile(out _);

            if (_usedIdentifiers == null || _usedIdentifiers.Count == 0)
                return null;

            if (_usedIdentifiersList == null)
                _usedIdentifiersList = new List<string>(_usedIdentifiers.Count);
            else
                _usedIdentifiersList.Clear();

            foreach (var key in _usedIdentifiers)
                _usedIdentifiersList.Add(key);

            return _usedIdentifiersList;
        }

        DynamicVariant EvaluateRefService(IDynamicContext context)
        {
            if (context == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                RichTextRuntimeLogger.Warn(
                    "RTS-REF-CONTEXT-NULL",
                    "RefService mode requires a non-null dynamic context.",
                    BuildRuntimeLogContext(
                        null,
                        "RefService",
                        detail: "Context is null.",
                        refKey: TryResolveRefKeyPreview(null)));
#endif
                return DynamicVariant.FromString(string.Empty);
            }

            if (!TryResolveRefKey(context, out var key))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                RichTextRuntimeLogger.Warn(
                    "RTS-REF-KEY-MISSING",
                    "RefService key could not be resolved.",
                    BuildRuntimeLogContext(
                        context,
                        "RefService",
                        detail: "Ref key is missing or empty.",
                        refKey: TryResolveRefKeyPreview(context)));
#endif
                return DynamicVariant.FromString(string.Empty);
            }

            if (!TryEvaluateRefServiceAcrossScopes(context, key, out var text, out var foundService, out var serviceDetail))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!foundService)
                {
                    RichTextRuntimeLogger.Warn(
                        "RTS-REF-SERVICE-MISSING",
                        "IRichTextRefService is not available in scope chain.",
                        BuildRuntimeLogContext(
                            context,
                            "RefService",
                            detail: serviceDetail,
                            refKey: key));
                }
                else
                {
                    RichTextRuntimeLogger.Warn(
                        "RTS-REF-EVALUATE-FAILED",
                        "IRichTextRefService evaluation returned false.",
                        BuildRuntimeLogContext(
                            context,
                            "RefService",
                            detail: serviceDetail,
                            refKey: key));
                }
#endif
                return DynamicVariant.FromString(string.Empty);
            }

            return DynamicVariant.FromString(text ?? string.Empty);
        }

        bool TryEvaluateRefServiceAcrossScopes(
            IDynamicContext context,
            string key,
            out string text,
            out bool foundService,
            out string detail)
        {
            text = string.Empty;
            foundService = false;
            detail = string.Empty;

            var visited = new HashSet<IScopeNode>();
            var serviceScopes = new List<string>(4);
            var scannedScopes = 0;

            foreach (var root in EnumerateRefServiceRoots(context))
            {
                for (var node = root; node != null; node = node.Parent)
                {
                    if (!visited.Add(node))
                        continue;

                    scannedScopes++;
                    var resolver = node.Resolver;
                    if (resolver == null || !resolver.TryResolve<IRichTextRefService>(out var refService) || refService == null)
                        continue;

                    foundService = true;
                    var label = DynamicRuntimeLogUtility.DescribeScope(node);
                    if (refService.TryEvaluate(key, context, out text))
                    {
                        detail = $"Resolved refKey from scope='{label}' after scanning {scannedScopes} scopes.";
                        return true;
                    }

                    if (serviceScopes.Count < 8)
                        serviceScopes.Add(label);
                }
            }

            if (!foundService)
            {
                detail = $"No IRichTextRefService was found while scanning {scannedScopes} scopes.";
                return false;
            }

            detail = serviceScopes.Count == 0
                ? $"TryEvaluate returned false while scanning {scannedScopes} scopes."
                : $"TryEvaluate returned false. serviceScopes={string.Join(" -> ", serviceScopes)}";
            return false;
        }

        static IEnumerable<IScopeNode> EnumerateRefServiceRoots(IDynamicContext context)
        {
            yield return context.Scope;

            if (context is CommandContext commandContext)
            {
                if (commandContext.Actor != null)
                    yield return commandContext.Actor;
                if (commandContext.CommandRootScope != null)
                    yield return commandContext.CommandRootScope;
                if (commandContext.RootActor != null)
                    yield return commandContext.RootActor;
                if (commandContext.CallerActor != null)
                    yield return commandContext.CallerActor;
            }
        }

        bool TryResolveRefKey(IDynamicContext context, out string key)
        {
            key = string.Empty;
            if (!_refKey.HasSource)
                return false;

            var variant = _refKey.Evaluate(context);
            if (variant.Kind == ValueKind.Null)
                return false;

            key = variant.AsString ?? string.Empty;
            return !string.IsNullOrEmpty(key);
        }

        bool TryCompile(out string message)
        {
            message = null;
            _dirty = false;
            _validationIsError = false;

            try
            {
                if (!BuildCaches(out message))
                {
                    _validationIsError = true;
                    _compiledNodes = null;
                    _usedIdentifiers = null;
                    return false;
                }

                if (!RichTextTemplateCompiler.TryCompile(
                        _template,
                        _scopeMap,
                        _typeMap,
                        _allowImplicitKeys,
                        out var nodes,
                        out var usedIdentifiers,
                        out message))
                {
                    _compiledNodes = null;
                    _usedIdentifiers = null;
                    _validationIsError = true;
                    return false;
                }

                _compiledNodes = nodes;
                _usedIdentifiers = usedIdentifiers;
                message = "OK";
                _validationIsError = false;
                return true;
            }
            catch (Exception ex)
            {
                _compiledNodes = null;
                _usedIdentifiers = null;
                message = $"Compile error: {ex.Message}";
                _validationIsError = true;
                return false;
            }

        }

        bool BuildCaches(out string error)
        {
            error = null;

            if (_scopeMap == null)
                _scopeMap = new Dictionary<string, DynamicValue>(StringComparer.Ordinal);
            else
                _scopeMap.Clear();

            if (_typeMap == null)
                _typeMap = new Dictionary<string, ValueKind>(StringComparer.Ordinal);
            else
                _typeMap.Clear();

            var keys = new HashSet<string>(StringComparer.Ordinal);

            bool AddVars(IReadOnlyList<ExpressionVariable> vars, string label, bool allowOverrideExisting, out string localError)
            {
                localError = null;
                if (vars == null)
                    return true;

                for (int i = 0; i < vars.Count; i++)
                {
                    var v = vars[i];
                    if (v == null)
                        continue;

                    var key = v.ExpressionKey;
                    if (string.IsNullOrEmpty(key))
                    {
                        localError = $"{label} [{i}] has no key (source not set)";
                        return false;
                    }

                    if (string.Equals(key, RichTextConstants.ValueIdentifier, StringComparison.Ordinal))
                    {
                        localError = "Identifier 'value' is reserved";
                        return false;
                    }

                    if (!keys.Add(key))
                    {
                        if (!allowOverrideExisting)
                        {
                            localError = $"Duplicate variable key: {key}";
                            return false;
                        }
                    }

                    var expectedKind = ResolveExpectedKind(v, fallback: ValueKind.String);
                    if (expectedKind == ValueKind.Null)
                    {
                        localError = $"Variable '{key}' expected type is Null";
                        return false;
                    }

                    if (!v.HasSource)
                    {
                        localError = $"Variable '{key}' has no DynamicValue source";
                        return false;
                    }

                    _scopeMap[key] = v.Value;
                    _typeMap[key] = expectedKind;
                }

                return true;
            }

            if (_externalVariables != null)
            {
                if (!AddVars(_externalVariables, "ExternalVariable", allowOverrideExisting: false, out var e0))
                {
                    error = e0;
                    return false;
                }

                if (_includeLocalVariablesWithExternal)
                {
                    if (!AddVars(_variables, "LocalVariable", allowOverrideExisting: true, out var e1))
                    {
                        error = e1;
                        return false;
                    }
                }
            }
            else
            {
                if (!AddVars(_variables, "Variable", allowOverrideExisting: false, out var e2))
                {
                    error = e2;
                    return false;
                }
            }

            if (_valueProxy == null)
                _valueProxy = new RichTextValueProxySource();
            _scopeMap[RichTextConstants.ValueIdentifier] = DynamicValue.FromSource(_valueProxy);

            return true;
        }

        static ValueKind ResolveExpectedKind(ExpressionVariable variable, ValueKind fallback)
        {
            var expected = variable.ExpectedKind;
            if (expected != ValueKind.Auto)
                return expected;

            try
            {
                var inferred = variable.Value.Evaluate(DummyDynamicContext.Instance).Kind;
                return inferred == ValueKind.Null || inferred == ValueKind.ManagedRef || inferred == ValueKind.Auto
                    ? fallback
                    : inferred;
            }
            catch
            {
                return fallback;
            }
        }

        string BuildVariableEvaluationDebug(IDynamicContext context)
        {
            if (_scopeMap == null || _scopeMap.Count == 0)
                return "(none)";

            var sb = new StringBuilder(128);
            var count = 0;
            foreach (var pair in _scopeMap)
            {
                if (string.Equals(pair.Key, RichTextConstants.ValueIdentifier, StringComparison.Ordinal))
                    continue;

                if (count > 0)
                    sb.Append(", ");
                if (count >= 8)
                {
                    sb.Append("...");
                    break;
                }

                sb.Append(pair.Key);
                sb.Append(':');
                try
                {
                    var variant = pair.Value.Evaluate(context);
                    sb.Append(variant.Kind);
                    sb.Append('=');
                    sb.Append(variant.ToString());
                }
                catch (Exception ex)
                {
                    sb.Append("EX(");
                    sb.Append(ex.Message);
                    sb.Append(')');
                }

                count++;
            }

            if (count == 0)
                return "(none)";

            return sb.ToString();
        }

        RichTextRuntimeLogContext BuildRuntimeLogContext(
            IDynamicContext context,
            string phase,
            string detail = null,
            string variables = null,
            string refKey = null)
        {
            return new RichTextRuntimeLogContext
            {
                Phase = phase,
                Template = _sourceMode == RichTextSourceMode.Template ? _template : null,
                RefKey = refKey,
                RefKeyDiagnostics = _sourceMode == RichTextSourceMode.RefService ? BuildRefKeyDiagnostics(context) : null,
                Detail = detail,
                Variables = variables,
                Settings = BuildSourceSettingsSummary(),
                AllowImplicitKeys = _sourceMode == RichTextSourceMode.Template ? _allowImplicitKeys : null,
                IncludeActorStores = _sourceMode == RichTextSourceMode.RefService && _logActorStoresOnWarn,
                MaxActorStoreEntries = _maxActorStoreEntries,
                DynamicContext = context,
            };
        }

        string BuildSourceSettingsSummary()
        {
            var localCount = _variables?.Count ?? 0;
            var externalCount = _externalVariables?.Count ?? 0;
            return
                $"sourceMode={_sourceMode}, allowImplicitKeys={_allowImplicitKeys}, " +
                $"localVariables={localCount}, externalVariables={externalCount}, includeLocalWithExternal={_includeLocalVariablesWithExternal}, " +
                $"storeDumpOnWarn=always(full)";
        }

        string BuildRefKeyDiagnostics(IDynamicContext context)
        {
            if (!_refKey.HasSource)
                return "hasSource=false";

            var sb = new StringBuilder(192);
            sb.Append("hasSource=true");
            sb.Append(", sourceType=");
            sb.Append(_refKey.SourceTypeName);
            sb.Append(", source=");
            sb.Append(string.IsNullOrEmpty(_refKey.SourceDebugData) ? "(empty)" : _refKey.SourceDebugData);

            try
            {
                var variant = _refKey.Evaluate(context ?? DummyDynamicContext.Instance);
                sb.Append(", evaluatedKind=");
                sb.Append(variant.Kind);
                sb.Append(", evaluatedValue=");
                sb.Append(variant.Kind == ValueKind.Null ? "<null>" : variant.ToString());
            }
            catch (Exception ex)
            {
                sb.Append(", evaluateError=");
                sb.Append(ex.Message);
            }

            return sb.ToString();
        }

        string BuildVariableDefinitionSummary()
        {
            var sb = new StringBuilder(128);
            var count = 0;

            void AppendVariables(IReadOnlyList<ExpressionVariable> vars, string label)
            {
                if (vars == null)
                    return;

                for (var i = 0; i < vars.Count; i++)
                {
                    var entry = vars[i];
                    if (entry == null)
                        continue;

                    if (count > 0)
                        sb.Append(", ");
                    if (count >= 10)
                    {
                        sb.Append("...");
                        return;
                    }

                    sb.Append(label);
                    sb.Append(':');
                    sb.Append(entry.ExpressionKey);
                    sb.Append('(');
                    sb.Append(entry.ExpectedKind);
                    sb.Append(')');
                    count++;
                }
            }

            if (_externalVariables != null)
                AppendVariables(_externalVariables, "external");

            if (_variables != null && (_includeLocalVariablesWithExternal || _externalVariables == null))
                AppendVariables(_variables, "local");

            if (count == 0)
                return "(none)";

            return sb.ToString();
        }

        string TryResolveRefKeyPreview(IDynamicContext context)
        {
            if (!_refKey.HasSource)
                return string.Empty;

            try
            {
                var variant = _refKey.Evaluate(context ?? DummyDynamicContext.Instance);
                if (variant.Kind == ValueKind.Null)
                    return "<null>";

                var text = variant.AsString ?? variant.ToString();
                return string.IsNullOrEmpty(text) ? "<empty>" : text;
            }
            catch (Exception ex)
            {
                return $"<error:{ex.Message}>";
            }
        }

        void MarkDirty()
        {
            _configurationRevision++;
            _dirty = true;
            _compiledNodes = null;
            _usedIdentifiers = null;
            _evalScope = null;
        }

        void Validate()
        {
            _dirty = true;
            _compiledNodes = null;
            TryCompile(out _validationMessage);
        }

        Color GetValidationColor()
        {
            return _validationIsError ? Color.red : Color.green;
        }

        public string GetExpressionVariablesDebugData()
        {
            if (_sourceMode == RichTextSourceMode.RefService)
                return "Variables: (ref service)";

            var local = _variables;
            var external = _externalVariables;

            var localCount = local?.Count ?? 0;
            var externalCount = external?.Count ?? 0;
            if (localCount == 0 && externalCount == 0)
                return "Variables: (none)";

            var texts = new List<string>(localCount + externalCount);

            if (externalCount > 0)
            {
                foreach (var v in external)
                {
                    if (v == null)
                        continue;
                    texts.Add(v.ExpressionKey);
                }
            }

            if (localCount > 0 && (_includeLocalVariablesWithExternal || externalCount == 0))
            {
                foreach (var v in local)
                {
                    if (v == null)
                        continue;
                    texts.Add(v.ExpressionKey);
                }
            }

            return "Variables: " + string.Join(", ", texts);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            MarkDirty();
        }
#endif

        public void SetExternalVariables(IReadOnlyList<ExpressionVariable> variables, bool includeLocalVariables = false)
        {
            _externalVariables = variables;
            _includeLocalVariablesWithExternal = includeLocalVariables;
            MarkDirty();
        }

        public void ClearExternalVariables()
        {
            if (_externalVariables == null)
                return;

            _externalVariables = null;
            _includeLocalVariablesWithExternal = false;
            MarkDirty();
        }
    }
}
