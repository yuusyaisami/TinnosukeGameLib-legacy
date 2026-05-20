// Game.Common.Vector3ExpressionSources.cs
//
// DynamicValue<Vector3> expression sources.
// - Vector3ExpressionSource: single expression expected to evaluate to Vector3.
// - Vector3XYZExpressionSource: separate expressions for X/Y/Z with shared variables.

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    [Serializable]
    public sealed class Vector3ExpressionSource : IDynamicSource, IExpressionSource, IExternalExpressionVariablesReceiver, IDynamicTrackedEvaluationPolicyProvider, IDynamicSourceConfigurationRevisionProvider, IDynamicSourceDependencyRevisionProvider
    {
        [LabelText("Allow Implicit Keys")]
        [SerializeField]
        bool _allowImplicitVariablesFromContext = true;

        [LabelText("@GetExpressionVariablesDebugData()")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        [OnValueChanged(nameof(MarkDirty), true)]
        List<ExpressionVariable> _variables = new();

        [NonSerialized]
        IReadOnlyList<ExpressionVariable> _externalVariables;

        [NonSerialized]
        bool _includeLocalVariablesWithExternal;

        [LabelText("Expression")]
        [PropertyTooltip("$ExpressionFunctionTooltip")]
        [SerializeField]
        [OnValueChanged(nameof(Validate))]
        [MultiLineProperty(3)]
        string _expression = "Vec3(0, 0, 0)";

        [ShowInInspector]
        [ReadOnly]
        [LabelText("Validation")]
        [GUIColor(nameof(GetValidationColor))]
        string _validationMessage;

        Dictionary<string, DynamicValue> _scopeMap;
        Dictionary<string, ValueKind> _typeMap;
        ExpressionNode _compiled;
        ExprEvalScope _evalScope;
        HashSet<string> _usedIdentifiers;
        List<string> _usedIdentifiersList;

        bool _dirty = true;
        bool _validationIsError;
        int _configurationRevision;
        bool _allowTrackedEvaluation;

        public string SourceTypeName => "Vector3Expression";
        public string GetDebugData => string.IsNullOrEmpty(_expression) ? "(empty)" : _expression;
        public int GetSourceConfigurationRevision() => _configurationRevision;
        public bool AllowTrackedEvaluation => _allowTrackedEvaluation;

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = 0;
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

        string ExpressionFunctionTooltip => ExpressionFunctionRegistry.GetInspectorFunctionTooltip();

        public string GetExpressionVariablesDebugData()
        {
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

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_dirty || _compiled == null)
            {
                if (!TryCompile(out _validationMessage))
                {
                    _validationIsError = true;
                    ExpressionRuntimeLogger.Error(
                        "EXV3-COMPILE-FAILED",
                        "Compile failed at runtime.",
                        BuildRuntimeLogContext(context, "Compile", _validationMessage));
                    return DynamicVariant.FromVector3(Vector3.zero);
                }
            }

            if (_compiled == null)
                return DynamicVariant.FromVector3(Vector3.zero);

            try
            {
                if (_evalScope == null)
                    _evalScope = new ExprEvalScope(context, _scopeMap);
                else
                {
                    _evalScope.Context = context;
                    _evalScope.Variables = _scopeMap;
                }

                var result = _compiled.Evaluate(_evalScope);
                if (!result.TryGet<Vector3>(out var vec3))
                    throw new InvalidOperationException("Expression result is not Vector3-compatible.");

                return DynamicVariant.FromVector3(vec3);
            }
            catch (Exception ex)
            {
                _validationMessage = $"Runtime error: {ex.Message}";
                _validationIsError = true;
                ExpressionRuntimeLogger.Error(
                    "EXV3-EVAL-EXCEPTION",
                    _validationMessage,
                    BuildRuntimeLogContext(context, "Evaluate", ex.Message));
                return DynamicVariant.FromVector3(Vector3.zero);
            }
        }

        public IReadOnlyList<string> GetDependentKeys()
        {
            if (_dirty || _usedIdentifiers == null)
                TryCompile(out _);

            if (_usedIdentifiers == null || _usedIdentifiers.Count == 0)
                return null;

            if (_usedIdentifiersList == null)
                _usedIdentifiersList = new List<string>(_usedIdentifiers.Count);
            else
                _usedIdentifiersList.Clear();

            foreach (var id in _usedIdentifiers)
                _usedIdentifiersList.Add(id);

            return _usedIdentifiersList;
        }

        bool TryCompile(out string message)
        {
            message = null;
            _dirty = false;
            _validationIsError = false;
            _allowTrackedEvaluation = false;

            if (!BuildCaches(out message))
            {
                _validationIsError = true;
                _compiled = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(_expression))
            {
                message = "Expression is empty";
                _validationIsError = true;
                _compiled = null;
                return false;
            }

            try
            {
                var tokenizer = new ExpressionTokenizer(_expression);
                var tokens = tokenizer.Tokenize(out var lexError);
                if (lexError != null)
                {
                    _compiled = null;
                    message = lexError;
                    _validationIsError = true;
                    _allowTrackedEvaluation = false;
                    return false;
                }

                var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
                var usedFunctions = new HashSet<string>(StringComparer.Ordinal);
                var parser = new ExpressionParser(tokens, _typeMap, usedIdentifiers, usedFunctions);
                var node = parser.ParseExpression(out var parseError);
                if (parseError != null)
                {
                    _compiled = null;
                    message = parseError;
                    _validationIsError = true;
                    _allowTrackedEvaluation = false;
                    return false;
                }

                if (!_allowImplicitVariablesFromContext)
                {
                    if (!ValidateIdentifiers(usedIdentifiers, out message))
                    {
                        _compiled = null;
                        _validationIsError = true;
                        _allowTrackedEvaluation = false;
                        return false;
                    }
                }

                _usedIdentifiers = usedIdentifiers;
                _compiled = node;
                _allowTrackedEvaluation = !ContainsNondeterministicFunctions(usedFunctions);
                message = "OK";
                _validationIsError = false;
                return true;
            }
            catch (Exception ex)
            {
                _compiled = null;
                message = $"Compile error: {ex.Message}";
                _validationIsError = true;
                _allowTrackedEvaluation = false;
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

                    if (!keys.Add(key))
                    {
                        if (!allowOverrideExisting)
                        {
                            localError = $"Duplicate variable key: {key}";
                            return false;
                        }
                    }

                    var expectedKind = ResolveExpectedKind(v, fallback: ValueKind.Vector3);
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

        bool ValidateIdentifiers(HashSet<string> used, out string message)
        {
            message = null;
            foreach (var key in used)
            {
                if (!_typeMap.ContainsKey(key))
                {
                    message = $"Identifier '{key}' is not defined in Variables";
                    return false;
                }
            }

            return true;
        }

        ExpressionRuntimeLogContext BuildRuntimeLogContext(IDynamicContext context, string phase, string detail)
        {
            return new ExpressionRuntimeLogContext
            {
                SourceType = SourceTypeName,
                Phase = phase,
                Expression = _expression,
                Variables = GetExpressionVariablesDebugData(),
                Detail = detail,
                AllowImplicitKeys = _allowImplicitVariablesFromContext,
                DynamicContext = context,
            };
        }

        void MarkDirty()
        {
            _allowTrackedEvaluation = false;
            _configurationRevision++;
            _dirty = true;
            _compiled = null;
        }

        void Validate()
        {
            MarkDirty();
            TryCompile(out _validationMessage);
        }

        Color GetValidationColor()
        {
            if (string.IsNullOrEmpty(_validationMessage))
                return Color.white;

            return _validationIsError ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
        }

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

        static bool ContainsNondeterministicFunctions(HashSet<string> usedFunctions)
        {
            if (usedFunctions == null || usedFunctions.Count == 0)
                return false;

            foreach (var functionName in usedFunctions)
            {
                if (ExpressionFunctionRegistry.IsNondeterministicFunction(functionName))
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class Vector3XYZExpressionSource : IDynamicSource, IExpressionSource, IExternalExpressionVariablesReceiver, IDynamicTrackedEvaluationPolicyProvider, IDynamicSourceConfigurationRevisionProvider, IDynamicSourceDependencyRevisionProvider
    {
        [LabelText("Allow Implicit Keys")]
        [SerializeField]
        bool _allowImplicitVariablesFromContext = true;

        [LabelText("@GetExpressionVariablesDebugData()")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        [OnValueChanged(nameof(MarkDirty), true)]
        List<ExpressionVariable> _variables = new();

        [NonSerialized]
        IReadOnlyList<ExpressionVariable> _externalVariables;

        [NonSerialized]
        bool _includeLocalVariablesWithExternal;

        [LabelText("Expression X")]
        [PropertyTooltip("$ExpressionFunctionTooltip")]
        [SerializeField]
        [OnValueChanged(nameof(Validate))]
        [MultiLineProperty(2)]
        string _expressionX = "0";

        [LabelText("Expression Y")]
        [PropertyTooltip("$ExpressionFunctionTooltip")]
        [SerializeField]
        [OnValueChanged(nameof(Validate))]
        [MultiLineProperty(2)]
        string _expressionY = "0";

        [LabelText("Expression Z")]
        [PropertyTooltip("$ExpressionFunctionTooltip")]
        [SerializeField]
        [OnValueChanged(nameof(Validate))]
        [MultiLineProperty(2)]
        string _expressionZ = "0";

        [ShowInInspector]
        [ReadOnly]
        [LabelText("Validation")]
        [GUIColor(nameof(GetValidationColor))]
        string _validationMessage;

        Dictionary<string, DynamicValue> _scopeMap;
        Dictionary<string, ValueKind> _typeMap;
        ExpressionNode _compiledX;
        ExpressionNode _compiledY;
        ExpressionNode _compiledZ;
        ExprEvalScope _evalScope;
        HashSet<string> _usedIdentifiers;
        List<string> _usedIdentifiersList;

        bool _dirty = true;
        bool _validationIsError;
        int _configurationRevision;
        bool _allowTrackedEvaluation;

        public string SourceTypeName => "Vector3XYZExpression";
        public string GetDebugData => $"x={_expressionX}; y={_expressionY}; z={_expressionZ}";
        public int GetSourceConfigurationRevision() => _configurationRevision;
        public bool AllowTrackedEvaluation => _allowTrackedEvaluation;

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            var revision = 0;
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

        string ExpressionFunctionTooltip => ExpressionFunctionRegistry.GetInspectorFunctionTooltip();

        public string GetExpressionVariablesDebugData()
        {
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

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_dirty || _compiledX == null || _compiledY == null || _compiledZ == null)
            {
                if (!TryCompile(out _validationMessage))
                {
                    _validationIsError = true;
                    ExpressionRuntimeLogger.Error(
                        "EXV3XYZ-COMPILE-FAILED",
                        "Compile failed at runtime.",
                        BuildRuntimeLogContext(context, "Compile", _validationMessage));
                    return DynamicVariant.FromVector3(Vector3.zero);
                }
            }

            if (_compiledX == null || _compiledY == null || _compiledZ == null)
                return DynamicVariant.FromVector3(Vector3.zero);

            try
            {
                if (_evalScope == null)
                    _evalScope = new ExprEvalScope(context, _scopeMap);
                else
                {
                    _evalScope.Context = context;
                    _evalScope.Variables = _scopeMap;
                }

                var xResult = _compiledX.Evaluate(_evalScope);
                var yResult = _compiledY.Evaluate(_evalScope);
                var zResult = _compiledZ.Evaluate(_evalScope);
                var x = ExpressionHelper.AsNumber(xResult);
                var y = ExpressionHelper.AsNumber(yResult);
                var z = ExpressionHelper.AsNumber(zResult);
                return DynamicVariant.FromVector3(new Vector3(x, y, z));
            }
            catch (Exception ex)
            {
                _validationMessage = $"Runtime error: {ex.Message}";
                _validationIsError = true;
                ExpressionRuntimeLogger.Error(
                    "EXV3XYZ-EVAL-EXCEPTION",
                    _validationMessage,
                    BuildRuntimeLogContext(context, "Evaluate", ex.Message));
                return DynamicVariant.FromVector3(Vector3.zero);
            }
        }

        public IReadOnlyList<string> GetDependentKeys()
        {
            if (_dirty || _usedIdentifiers == null)
                TryCompile(out _);

            if (_usedIdentifiers == null || _usedIdentifiers.Count == 0)
                return null;

            if (_usedIdentifiersList == null)
                _usedIdentifiersList = new List<string>(_usedIdentifiers.Count);
            else
                _usedIdentifiersList.Clear();

            foreach (var id in _usedIdentifiers)
                _usedIdentifiersList.Add(id);

            return _usedIdentifiersList;
        }

        bool TryCompile(out string message)
        {
            message = null;
            _dirty = false;
            _validationIsError = false;
            _allowTrackedEvaluation = false;

            if (!BuildCaches(out message))
            {
                _validationIsError = true;
                _compiledX = null;
                _compiledY = null;
                _compiledZ = null;
                    _allowTrackedEvaluation = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(_expressionX) || string.IsNullOrWhiteSpace(_expressionY) || string.IsNullOrWhiteSpace(_expressionZ))
            {
                message = "Expression X, Y or Z is empty";
                _validationIsError = true;
                _compiledX = null;
                _compiledY = null;
                _compiledZ = null;
                    _allowTrackedEvaluation = false;
                return false;
            }

            try
            {
                var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
                var usedFunctions = new HashSet<string>(StringComparer.Ordinal);

                if (!TryParse(_expressionX, "X", usedIdentifiers, usedFunctions, out _compiledX, out message))
                {
                    _validationIsError = true;
                    _compiledY = null;
                    _compiledZ = null;
                    _allowTrackedEvaluation = false;
                    return false;
                }

                if (!TryParse(_expressionY, "Y", usedIdentifiers, usedFunctions, out _compiledY, out message))
                {
                    _validationIsError = true;
                    _compiledX = null;
                    _compiledZ = null;
                    _allowTrackedEvaluation = false;
                    return false;
                }

                if (!TryParse(_expressionZ, "Z", usedIdentifiers, usedFunctions, out _compiledZ, out message))
                {
                    _validationIsError = true;
                    _compiledX = null;
                    _compiledY = null;
                    _allowTrackedEvaluation = false;
                    return false;
                }

                if (!_allowImplicitVariablesFromContext)
                {
                    if (!ValidateIdentifiers(usedIdentifiers, out message))
                    {
                        _compiledX = null;
                        _compiledY = null;
                        _compiledZ = null;
                        _validationIsError = true;
                        _allowTrackedEvaluation = false;
                        return false;
                    }
                }

                _usedIdentifiers = usedIdentifiers;
                _allowTrackedEvaluation = !ContainsNondeterministicFunctions(usedFunctions);
                message = "OK";
                _validationIsError = false;
                return true;
            }
            catch (Exception ex)
            {
                _compiledX = null;
                _compiledY = null;
                _compiledZ = null;
                message = $"Compile error: {ex.Message}";
                _validationIsError = true;
                _allowTrackedEvaluation = false;
                return false;
            }
        }

        bool TryParse(string expression, string axisName, HashSet<string> usedIdentifiers, HashSet<string> usedFunctions, out ExpressionNode node, out string message)
        {
            node = null;
            message = null;

            var tokenizer = new ExpressionTokenizer(expression);
            var tokens = tokenizer.Tokenize(out var lexError);
            if (lexError != null)
            {
                message = $"{axisName}: {lexError}";
                return false;
            }

            var parser = new ExpressionParser(tokens, _typeMap, usedIdentifiers, usedFunctions);
            node = parser.ParseExpression(out var parseError);
            if (parseError != null)
            {
                message = $"{axisName}: {parseError}";
                return false;
            }

            return true;
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

                    if (!keys.Add(key))
                    {
                        if (!allowOverrideExisting)
                        {
                            localError = $"Duplicate variable key: {key}";
                            return false;
                        }
                    }

                    var expectedKind = ResolveExpectedKind(v, fallback: ValueKind.Float);
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

        bool ValidateIdentifiers(HashSet<string> used, out string message)
        {
            message = null;
            foreach (var key in used)
            {
                if (!_typeMap.ContainsKey(key))
                {
                    message = $"Identifier '{key}' is not defined in Variables";
                    return false;
                }
            }

            return true;
        }

        ExpressionRuntimeLogContext BuildRuntimeLogContext(IDynamicContext context, string phase, string detail)
        {
            return new ExpressionRuntimeLogContext
            {
                SourceType = SourceTypeName,
                Phase = phase,
                Expression = $"x={_expressionX}; y={_expressionY}; z={_expressionZ}",
                Variables = GetExpressionVariablesDebugData(),
                Detail = detail,
                AllowImplicitKeys = _allowImplicitVariablesFromContext,
                DynamicContext = context,
            };
        }

        void MarkDirty()
        {
            _allowTrackedEvaluation = false;
            _configurationRevision++;
            _dirty = true;
            _compiledX = null;
            _compiledY = null;
            _compiledZ = null;
        }

        void Validate()
        {
            MarkDirty();
            TryCompile(out _validationMessage);
        }

        Color GetValidationColor()
        {
            if (string.IsNullOrEmpty(_validationMessage))
                return Color.white;

            return _validationIsError ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
        }

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

        static bool ContainsNondeterministicFunctions(HashSet<string> usedFunctions)
        {
            if (usedFunctions == null || usedFunctions.Count == 0)
                return false;

            foreach (var functionName in usedFunctions)
            {
                if (ExpressionFunctionRegistry.IsNondeterministicFunction(functionName))
                    return true;
            }

            return false;
        }
    }
}