#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game.Common.Editor
{
    public enum ExpressionPreviewSourceKind
    {
        Float = 0,
        Int = 1,
    }

    public sealed class ExpressionPreviewVariableMeta
    {
        public string Key = string.Empty;
        public ValueKind Kind = ValueKind.Auto;
    }

    public sealed class ExpressionGraphSourceModel
    {
        public ExpressionPreviewSourceKind SourceKind;
        public string Expression = string.Empty;
        public readonly List<ExpressionPreviewVariableMeta> Variables = new();
    }

    public readonly struct ExpressionSamplePoint
    {
        public readonly float X;
        public readonly float Y;
        public readonly bool IsValid;

        public ExpressionSamplePoint(float x, float y, bool isValid)
        {
            X = x;
            Y = y;
            IsValid = isValid;
        }
    }

    public sealed class ExpressionGraphSamplingResult
    {
        public bool CanPlot;
        public string CannotPlotReason = string.Empty;
        public readonly List<string> Identifiers = new();
        public readonly Dictionary<string, ValueKind> IdentifierKinds = new(StringComparer.Ordinal);
        public readonly List<ExpressionSamplePoint> Samples = new();
        public float YMin;
        public float YMax;
    }

    public sealed class ExpressionGraphSamplingRequest
    {
        public ExpressionGraphSourceModel Source = new();
        public string XAxisKey = string.Empty;
        public float XMin = -10f;
        public float XMax = 10f;
        public int SampleCount = 201;
        public readonly Dictionary<string, float> NumericFixedValues = new(StringComparer.Ordinal);
        public readonly Dictionary<string, bool> BoolFixedValues = new(StringComparer.Ordinal);
    }

    public static class ExpressionGraphSamplingService
    {
        static readonly BindingFlags SourceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static bool TryCreateSourceModel(IDynamicSource source, out ExpressionGraphSourceModel model, out string error)
        {
            model = new ExpressionGraphSourceModel();
            error = string.Empty;

            if (source == null)
            {
                error = "Source is null.";
                return false;
            }

            var sourceType = source.GetType();
            var fallbackKind = ValueKind.Float;

            if (source is FloatExpressionSource)
            {
                model.SourceKind = ExpressionPreviewSourceKind.Float;
                fallbackKind = ValueKind.Float;
            }
            else if (source is IntExpressionSource)
            {
                model.SourceKind = ExpressionPreviewSourceKind.Int;
                fallbackKind = ValueKind.Int;
            }
            else
            {
                error = "Only FloatExpressionSource / IntExpressionSource are supported.";
                return false;
            }

            var expressionField = sourceType.GetField("_expression", SourceFlags);
            if (expressionField == null)
            {
                error = $"Expression field was not found on {sourceType.Name}.";
                return false;
            }

            model.Expression = expressionField.GetValue(source) as string ?? string.Empty;

            var variablesField = sourceType.GetField("_variables", SourceFlags);
            if (variablesField == null)
                return true;

            if (variablesField.GetValue(source) is not IEnumerable variableEnumerable)
                return true;

            var existingKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in variableEnumerable)
            {
                if (item is not ExpressionVariable variable)
                    continue;

                var key = variable.ExpressionKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!existingKeys.Add(key))
                    continue;

                model.Variables.Add(new ExpressionPreviewVariableMeta
                {
                    Key = key,
                    Kind = ResolveExpectedKind(variable, fallbackKind),
                });
            }

            return true;
        }

        public static ExpressionGraphSamplingResult Sample(ExpressionGraphSamplingRequest request, ExpressionPlotDiagnostics diagnostics)
        {
            var result = new ExpressionGraphSamplingResult();
            diagnostics.Clear();

            if (request == null || request.Source == null)
            {
                SetCannotPlot(result, diagnostics, "Sampling request is empty.");
                return result;
            }

            var expression = request.Source.Expression ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expression))
            {
                SetCannotPlot(result, diagnostics, "Expression is empty.");
                return result;
            }

            var tokenizer = new ExpressionTokenizer(expression);
            var tokens = tokenizer.Tokenize(out var lexError);
            if (!string.IsNullOrEmpty(lexError))
            {
                SetCannotPlot(result, diagnostics, $"Tokenizer error: {lexError}");
                return result;
            }

            if (ContainsStringToken(tokens))
            {
                SetCannotPlot(result, diagnostics, "Expression contains a string literal, so this graph cannot be plotted.");
                return result;
            }

            var typeMap = BuildTypeMap(request.Source.Variables);
            var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
            var parser = new ExpressionParser(tokens, typeMap, usedIdentifiers);
            var root = parser.ParseExpression(out var parseError);
            if (!string.IsNullOrEmpty(parseError) || root == null)
            {
                SetCannotPlot(result, diagnostics, $"Parser error: {parseError}");
                return result;
            }

            foreach (var identifier in usedIdentifiers)
                result.Identifiers.Add(identifier);
            result.Identifiers.Sort(StringComparer.Ordinal);

            if (result.Identifiers.Count == 0)
            {
                SetCannotPlot(result, diagnostics, "No variables were found in the expression. Select an expression that contains x-axis candidates.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(request.XAxisKey))
            {
                SetCannotPlot(result, diagnostics, "Select x-axis variable.");
                return result;
            }

            if (!usedIdentifiers.Contains(request.XAxisKey))
            {
                SetCannotPlot(result, diagnostics, $"x-axis variable '{request.XAxisKey}' is not used by this expression.");
                return result;
            }

            var unknownKeys = new List<string>(4);
            for (int i = 0; i < result.Identifiers.Count; i++)
            {
                var id = result.Identifiers[i];
                var kind = typeMap.TryGetValue(id, out var known) ? known : ValueKind.Auto;
                result.IdentifierKinds[id] = kind;

                if (kind == ValueKind.String)
                {
                    SetCannotPlot(result, diagnostics, $"Variable '{id}' is String. String expressions cannot be graphed.");
                    return result;
                }

                if (kind == ValueKind.ManagedRef || kind == ValueKind.UnityObject)
                {
                    SetCannotPlot(result, diagnostics, $"Variable '{id}' is not numeric and cannot be sampled.");
                    return result;
                }

                if (kind == ValueKind.Bool)
                    diagnostics.AddWarning($"Variable '{id}' is Bool. v0.1 preview uses fixed bool values only.");
                else if (kind == ValueKind.Auto || kind == ValueKind.Null)
                    unknownKeys.Add(id);
            }

            for (int i = 0; i < unknownKeys.Count; i++)
                diagnostics.AddWarning($"Variable '{unknownKeys[i]}' type is Unknown. Numeric fixed value mode is used.");

            var xMin = request.XMin;
            var xMax = request.XMax;
            if (xMax <= xMin)
                xMax = xMin + 0.0001f;

            var sampleCount = Mathf.Clamp(request.SampleCount, 2, 2001);
            var varMap = new Dictionary<string, DynamicValue>(result.Identifiers.Count, StringComparer.Ordinal);
            var evalScope = new ExprEvalScope(PreviewDynamicContext.Instance, varMap);

            var hasFiniteY = false;
            var yMin = 0f;
            var yMax = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                var t = sampleCount <= 1 ? 0f : (float)i / (sampleCount - 1);
                var x = Mathf.Lerp(xMin, xMax, t);

                FillVariableMap(varMap, result.Identifiers, result.IdentifierKinds, request, x);

                DynamicVariant value;
                try
                {
                    value = root.Evaluate(evalScope);
                }
                catch (Exception ex)
                {
                    SetCannotPlot(result, diagnostics, $"Evaluation failed at x={x}: {ex.Message}");
                    result.Samples.Clear();
                    return result;
                }

                if (!TryConvertToPlotValue(value, request.Source.SourceKind, out var y))
                {
                    SetCannotPlot(result, diagnostics, $"Expression did not produce numeric output at x={x}. Kind={value.Kind}");
                    result.Samples.Clear();
                    return result;
                }

                var finite = !(float.IsNaN(y) || float.IsInfinity(y));
                result.Samples.Add(new ExpressionSamplePoint(x, y, finite));

                if (!finite)
                    continue;

                if (!hasFiniteY)
                {
                    hasFiniteY = true;
                    yMin = y;
                    yMax = y;
                }
                else
                {
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }

            if (!hasFiniteY)
            {
                SetCannotPlot(result, diagnostics, "All sampled values were NaN/Infinity.");
                return result;
            }

            if (Mathf.Abs(yMax - yMin) < 0.0001f)
            {
                yMin -= 0.5f;
                yMax += 0.5f;
            }

            result.YMin = yMin;
            result.YMax = yMax;
            result.CanPlot = true;

            return result;
        }

        static bool TryConvertToPlotValue(DynamicVariant value, ExpressionPreviewSourceKind sourceKind, out float y)
        {
            if (value.Kind == ValueKind.Int || value.Kind == ValueKind.Float || value.Kind == ValueKind.Bool)
            {
                y = ExpressionHelper.AsNumber(value);
                if (sourceKind == ExpressionPreviewSourceKind.Int)
                    y = Mathf.RoundToInt(y);
                return true;
            }

            y = 0f;
            return false;
        }

        static void FillVariableMap(
            Dictionary<string, DynamicValue> map,
            List<string> identifiers,
            Dictionary<string, ValueKind> kinds,
            ExpressionGraphSamplingRequest request,
            float x)
        {
            map.Clear();

            for (int i = 0; i < identifiers.Count; i++)
            {
                var key = identifiers[i];
                if (string.Equals(key, request.XAxisKey, StringComparison.Ordinal))
                {
                    map[key] = DynamicValue.FromLiteral(x);
                    continue;
                }

                var kind = kinds.TryGetValue(key, out var k) ? k : ValueKind.Auto;
                if (kind == ValueKind.Bool)
                {
                    request.BoolFixedValues.TryGetValue(key, out var boolValue);
                    map[key] = DynamicValue.FromLiteral(boolValue);
                    continue;
                }

                request.NumericFixedValues.TryGetValue(key, out var fixedValue);
                if (kind == ValueKind.Int)
                    map[key] = DynamicValue.FromLiteral(Mathf.RoundToInt(fixedValue));
                else
                    map[key] = DynamicValue.FromLiteral(fixedValue);
            }
        }

        static Dictionary<string, ValueKind> BuildTypeMap(List<ExpressionPreviewVariableMeta> variables)
        {
            var map = new Dictionary<string, ValueKind>(StringComparer.Ordinal);
            if (variables == null)
                return map;

            for (int i = 0; i < variables.Count; i++)
            {
                var entry = variables[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                map[entry.Key] = entry.Kind;
            }

            return map;
        }

        static bool ContainsStringToken(List<ExprToken> tokens)
        {
            if (tokens == null)
                return false;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == ExprTokenKind.String)
                    return true;
            }

            return false;
        }

        static void SetCannotPlot(ExpressionGraphSamplingResult result, ExpressionPlotDiagnostics diagnostics, string reason)
        {
            result.CanPlot = false;
            result.CannotPlotReason = reason ?? string.Empty;
            diagnostics.AddError(result.CannotPlotReason);
        }

        static ValueKind ResolveExpectedKind(ExpressionVariable variable, ValueKind fallback)
        {
            if (variable == null)
                return fallback;

            var expected = variable.ExpectedKind;
            if (expected != ValueKind.Auto)
                return expected;

            try
            {
                var inferred = variable.Value.Evaluate(PreviewDynamicContext.Instance).Kind;
                return inferred == ValueKind.Null || inferred == ValueKind.ManagedRef || inferred == ValueKind.Auto
                    ? fallback
                    : inferred;
            }
            catch
            {
                return fallback;
            }
        }

        sealed class PreviewDynamicContext : IDynamicContext
        {
            public static readonly PreviewDynamicContext Instance = new();

            readonly IVarStore _vars = new VarStore();
            readonly IScopeNode _scope = new PreviewScopeNode();

            public IVarStore Vars => _vars;
            public IScopeNode Scope => _scope;
            public IScopeNode CommandRootScope => _scope;

            public IScopeNode ResolveOtherScope(Game.Commands.CommandTargetIdentityFilter filter)
            {
                _ = filter;
                return _scope;
            }
        }

        sealed class PreviewScopeNode : IScopeNode
        {
            public IScopeNode Parent => null;
            public ILTSIdentityService Identity => null;
            public LifetimeScopeKind Kind => LifetimeScopeKind.None;
            public IRuntimeResolver Resolver => null;
            public bool IsVisible => false;
            public bool IsActive => false;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return false;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return false;
            }

            public Cysharp.Threading.Tasks.UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return Cysharp.Threading.Tasks.UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode> GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }
}
#endif
