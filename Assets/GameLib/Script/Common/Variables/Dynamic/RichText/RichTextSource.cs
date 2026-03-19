using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum RichTextSourceMode
    {
        Template = 0,
        RefService = 1,
    }

    [Serializable]
    public sealed class RichTextSource : IDynamicSource, IExpressionSource, IExternalExpressionVariablesReceiver
    {
        [LabelText("Source Mode")]
        [SerializeField]
        [OnValueChanged(nameof(MarkDirty), true)]
        RichTextSourceMode _sourceMode = RichTextSourceMode.Template;

        [LabelText("Ref Key")]
        [SerializeField]
        [ShowIf(nameof(IsRefServiceMode))]
        DynamicValue<string> _refKey;

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

        bool IsTemplateMode => _sourceMode == RichTextSourceMode.Template;
        bool IsRefServiceMode => _sourceMode == RichTextSourceMode.RefService;
        string ExpressionFunctionTooltip => ExpressionFunctionRegistry.GetInspectorFunctionTooltip();

        public string SourceTypeName => _sourceMode == RichTextSourceMode.RefService ? "RichTextRef" : "RichText";
        public string GetDebugData => _sourceMode == RichTextSourceMode.RefService
            ? (_refKey.HasSource ? "(ref key set)" : "(ref key empty)")
            : (string.IsNullOrEmpty(_template) ? "(empty)" : _template);

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

            if (_dirty || _compiledNodes == null)
            {
                if (!TryCompile(out _validationMessage))
                {
                    _validationIsError = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    RichTextRuntimeLogger.Log($"RichText compile failed: {_validationMessage} template='{_template}'");
#endif
                    return DynamicVariant.FromString(string.Empty);
                }
            }

            if (_compiledNodes == null || _compiledNodes.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(_template))
                    RichTextRuntimeLogger.Log($"RichText has no compiled nodes. template='{_template}'");
#endif
                return DynamicVariant.FromString(string.Empty);
            }

            var ctx = context ?? DummyDynamicContext.Instance;
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
                RichTextRuntimeLogger.Log(
                    $"RichText evaluated empty. template='{_template}' nodes={_compiledNodes.Count} vars=[{BuildVariableEvaluationDebug(ctx)}]");
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
                return DynamicVariant.FromString(string.Empty);

            if (!TryResolveRefKey(context, out var key))
                return DynamicVariant.FromString(string.Empty);

            var resolver = context.Scope?.Resolver;
            if (resolver == null || !resolver.TryResolve<IRichTextRefService>(out var refService) || refService == null)
                return DynamicVariant.FromString(string.Empty);

            if (!refService.TryEvaluate(key, context, out var text))
                return DynamicVariant.FromString(string.Empty);

            return DynamicVariant.FromString(text ?? string.Empty);
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

        void MarkDirty()
        {
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
