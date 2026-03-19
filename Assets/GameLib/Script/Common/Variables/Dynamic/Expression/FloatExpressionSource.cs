// Game.Common.FloatExpressionSource.cs
//
// DynamicValue<float> 専用の式評価ソース。
// 数値式を評価して float 値を返す。

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// DynamicValue&lt;float&gt; 専用の式評価ソース。
    /// Variables に定義した変数を Expression で参照して float 値を返す。
    /// </summary>
    [Serializable]
    public sealed class FloatExpressionSource : IDynamicSource, IExpressionSource, IExternalExpressionVariablesReceiver
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
        string _expression = "0";

        [ShowInInspector]
        [ReadOnly]
        [LabelText("Validation")]
        [GUIColor(nameof(GetValidationColor))]
        string _validationMessage;

        // キャッシュ
        Dictionary<string, DynamicValue> _scopeMap;
        Dictionary<string, ValueKind> _typeMap;
        ExpressionNode _compiled;
        ExprEvalScope _evalScope;
        HashSet<string> _usedIdentifiers;
        List<string> _usedIdentifiersList;

        bool _dirty = true;
        bool _validationIsError;

        public string SourceTypeName => "FloatExpression";
        public string GetDebugData => string.IsNullOrEmpty(_expression) ? "(empty)" : _expression;

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

        string ExpressionFunctionTooltip => ExpressionFunctionRegistry.GetInspectorFunctionTooltip();

        // ================================================================
        // IDynamicSource
        // ================================================================

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_dirty || _compiled == null)
            {
                if (!TryCompile(out _validationMessage))
                {
                    _validationIsError = true;
                    Debug.LogError($"[FloatExpressionSource] Compile failed at runtime: {_validationMessage} (Expr: {_expression})");
                    return DynamicVariant.FromFloat(0f);
                }
            }

            if (_compiled == null)
                return DynamicVariant.FromFloat(0f);

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

                // 結果を float に変換（bool の場合は true=1, false=0）
                return DynamicVariant.FromFloat(ExpressionHelper.AsNumber(result));
            }
            catch (Exception ex)
            {
                _validationMessage = $"Runtime error: {ex.Message}";
                _validationIsError = true;
                Debug.LogError($"[FloatExpressionSource] {_validationMessage} (Expr: {_expression})");
                return DynamicVariant.FromFloat(0f);
            }
        }

        // ================================================================
        // IExpressionSource
        // ================================================================

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

        // ================================================================
        // コンパイル
        // ================================================================

        bool TryCompile(out string message)
        {
            message = null;
            _dirty = false;
            _validationIsError = false;

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
                    return false;
                }

                var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
                var parser = new ExpressionParser(tokens, _typeMap, usedIdentifiers);
                var node = parser.ParseExpression(out var parseError);
                if (parseError != null)
                {
                    _compiled = null;
                    message = parseError;
                    _validationIsError = true;
                    return false;
                }

                if (!_allowImplicitVariablesFromContext)
                {
                    if (!ValidateIdentifiers(usedIdentifiers, out message))
                    {
                        _compiled = null;
                        _validationIsError = true;
                        return false;
                    }
                }

                _usedIdentifiers = usedIdentifiers;
                _compiled = node;
                message = "OK";
                _validationIsError = false;
                return true;
            }
            catch (Exception ex)
            {
                _compiled = null;
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

        // ================================================================
        // Editor
        // ================================================================

        void MarkDirty()
        {
            _dirty = true;
            _compiled = null;
        }

        void Validate()
        {
            _dirty = true;
            _compiled = null;
            TryCompile(out _validationMessage);
        }

        Color GetValidationColor()
        {
            return _validationIsError ? Color.red : Color.green;
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
