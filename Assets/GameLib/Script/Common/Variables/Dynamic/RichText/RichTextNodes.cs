using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Common
{
    internal static class RichTextConstants
    {
        public const string ValueIdentifier = "value";
    }

    public sealed class RichTextValueProxySource : IDynamicSource
    {
        DynamicVariant _value;

        public void SetValue(in DynamicVariant value)
        {
            _value = value;
        }

        public DynamicVariant Evaluate(IDynamicContext context) => _value;
        public string SourceTypeName => "RichTextValueProxy";
        public string GetDebugData => _value.ToString();
    }

    public sealed class RichTextEvalScope
    {
        public IDynamicContext Context { get; private set; }
        public Dictionary<string, DynamicValue> Variables { get; }
        public RichTextValueProxySource ValueProxy { get; }
        public ExprEvalScope ExprScope { get; }

        public RichTextEvalScope(IDynamicContext context, Dictionary<string, DynamicValue> variables, RichTextValueProxySource valueProxy)
        {
            Context = context;
            Variables = variables;
            ValueProxy = valueProxy;
            ExprScope = new ExprEvalScope(context, variables);
        }

        public void UpdateContext(IDynamicContext context)
        {
            Context = context;
            ExprScope.Context = context;
            ExprScope.Variables = Variables;
        }
    }

    public abstract class RichTextNode
    {
        public abstract string Evaluate(RichTextEvalScope scope);
    }

    public sealed class TextNode : RichTextNode
    {
        readonly string _text;

        public TextNode(string text)
        {
            _text = text ?? string.Empty;
        }

        public override string Evaluate(RichTextEvalScope scope) => _text;
    }

    public sealed class PlaceholderNode : RichTextNode
    {
        readonly string _identifier;
        readonly DynamicValue _explicitValue;
        readonly bool _hasExplicitValue;
        readonly int _varId;
        readonly bool _allowImplicitKeys;
        readonly RichTextFormatOptions _formatOptions;
        readonly RichTextDecoratorOptions _decoratorOptions;
        readonly ExpressionNode _valueExpression;
        readonly ExpressionNode _condition;
        readonly bool _useConditionForVisibility;

        public PlaceholderNode(
            string identifier,
            DynamicValue explicitValue,
            bool hasExplicitValue,
            int varId,
            bool allowImplicitKeys,
            in RichTextFormatOptions formatOptions,
            in RichTextDecoratorOptions decoratorOptions,
            ExpressionNode valueExpression,
            ExpressionNode condition,
            bool useConditionForVisibility)
        {
            _identifier = identifier;
            _explicitValue = explicitValue;
            _hasExplicitValue = hasExplicitValue;
            _varId = varId;
            _allowImplicitKeys = allowImplicitKeys;
            _formatOptions = formatOptions;
            _decoratorOptions = decoratorOptions;
            _valueExpression = valueExpression;
            _condition = condition;
            _useConditionForVisibility = useConditionForVisibility;
        }

        public override string Evaluate(RichTextEvalScope scope)
        {
            var empty = RichTextOptions.GetEmptyFallback(_formatOptions);

            if (!TryResolveValue(scope, out var value))
                return empty;

            if (value.Kind == ValueKind.Null)
            {
                RichTextRuntimeLogger.Warn(
                    "RTN-RESOLVE-NULL",
                    "Resolved value is Null.",
                    BuildLogContext(scope.Context, "Evaluate", varId: ResolveKnownVarId(), value));
                return empty;
            }

            bool? condResult = null;
            if (_condition != null)
            {
                scope.ValueProxy.SetValue(value);
                try
                {
                    var condValue = _condition.Evaluate(scope.ExprScope);
                    condResult = ExpressionHelper.AsBool(condValue);
                }
                catch (Exception ex)
                {
                    RichTextRuntimeLogger.Error(
                        "RTN-COND-EXCEPTION",
                        $"Condition evaluation failed: {ex.Message}",
                        BuildLogContext(scope.Context, "Evaluate", varId: ResolveKnownVarId(), value));
                    return empty;
                }

                if (_useConditionForVisibility && condResult == false)
                    return empty;
            }

            if (!RichTextOptions.TryFormat(value, _formatOptions, out var formatted))
            {
                RichTextRuntimeLogger.Warn(
                    "RTN-FORMAT-FAILED",
                    "Value formatting failed for current options.",
                    BuildLogContext(scope.Context, "Format", varId: ResolveKnownVarId(), value));
                return empty;
            }

            if (!RichTextEffects.TryApply(formatted, _decoratorOptions, condResult, out var decorated))
            {
                RichTextRuntimeLogger.Warn(
                    "RTN-EFFECT-FAILED",
                    "Decorator/effect application failed.",
                    BuildLogContext(scope.Context, "Decorate", varId: ResolveKnownVarId(), value));
                return empty;
            }

            return decorated ?? string.Empty;
        }

        bool TryResolveValue(RichTextEvalScope scope, out DynamicVariant value)
        {
            value = DynamicVariant.Null;
            var context = scope.Context;

            if (_valueExpression != null)
            {
                try
                {
                    value = _valueExpression.Evaluate(scope.ExprScope);
                    return true;
                }
                catch (Exception ex)
                {
                    RichTextRuntimeLogger.Error(
                        "RTN-VALUE-EXPRESSION-EXCEPTION",
                        $"Value expression evaluation failed: {ex.Message}",
                        BuildLogContext(context, "ResolveValue"));
                    return false;
                }
            }

            if (_hasExplicitValue)
            {
                try
                {
                    value = _explicitValue.Evaluate(context);
                    return true;
                }
                catch (Exception ex)
                {
                    RichTextRuntimeLogger.Error(
                        "RTN-VALUE-EXPLICIT-EXCEPTION",
                        $"Explicit DynamicValue evaluation failed: {ex.Message}",
                        BuildLogContext(context, "ResolveValue"));
                    return false;
                }
            }

            if (!_allowImplicitKeys || context?.Vars == null)
            {
                RichTextRuntimeLogger.Warn(
                    "RTN-IMPLICIT-DISABLED",
                    "Implicit key lookup is disabled or VarStore is null.",
                    BuildLogContext(context, "ResolveValue"));
                return false;
            }

            var varId = _varId;
            if (varId == 0)
            {
                if (!VarIdResolver.TryResolve(_identifier, out var resolved) || resolved == 0)
                {
                    RichTextRuntimeLogger.Warn(
                        "RTN-IMPLICIT-VARID-NOT-FOUND",
                        "VarIdResolver could not resolve identifier.",
                        BuildLogContext(context, "ResolveValue"));
                    return false;
                }
                varId = resolved;
            }

            if (!context.Vars.TryGetVariant(varId, out value))
            {
                RichTextRuntimeLogger.Warn(
                    "RTN-IMPLICIT-VAR-MISSING",
                    "VarStore does not contain the resolved var id.",
                    BuildLogContext(context, "ResolveValue", varId));
                return false;
            }

            return true;
        }

        RichTextRuntimeLogContext BuildLogContext(
            IDynamicContext context,
            string phase,
            int? varId = null,
            DynamicVariant value = default)
        {
            var data = new RichTextRuntimeLogContext
            {
                Phase = phase,
                Identifier = _identifier,
                VarId = varId,
                AllowImplicitKeys = _allowImplicitKeys,
                Settings = BuildSettingsSummary(),
                DynamicContext = context,
            };

            if (value.Kind != ValueKind.Auto)
            {
                data.ValueKind = value.Kind;
                data.ValuePreview = value.ToString();
            }

            return data;
        }

        int? ResolveKnownVarId()
        {
            return _varId != 0 ? _varId : null;
        }

        string BuildSettingsSummary()
        {
            var resolver = _valueExpression != null ? "expression" : (_hasExplicitValue ? "explicit" : "implicit");
            var conditionMode = _condition == null
                ? "none"
                : (_useConditionForVisibility ? "condition+visibility" : "condition-only");

            var format =
                $"emptyFallback={_formatOptions.HasEmptyFallback}, percent={_formatOptions.UsePercent}, " +
                $"percentDigits={FormatNullable(_formatOptions.PercentDigits)}, round={FormatNullable(_formatOptions.RoundDigits)}, " +
                $"fixed={FormatNullable(_formatOptions.FixedDigits)}, signAlways={_formatOptions.SignAlways}, " +
                $"prefix='{_formatOptions.Prefix ?? string.Empty}', suffix='{_formatOptions.Suffix ?? string.Empty}'";

            var decorator =
                $"effect={_decoratorOptions.EffectKind}, color='{_decoratorOptions.Color ?? string.Empty}', " +
                $"trueColor='{_decoratorOptions.TrueColor ?? string.Empty}', falseColor='{_decoratorOptions.FalseColor ?? string.Empty}', " +
                $"wrap='{_decoratorOptions.Wrap ?? string.Empty}', wrapEnd='{_decoratorOptions.WrapEnd ?? string.Empty}'";

            return $"resolver={resolver}, condition={conditionMode}, format=({format}), decorator=({decorator})";
        }

        static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "<none>";
        }
    }

    internal struct RichTextRuntimeLogContext
    {
        public string Phase;
        public string Identifier;
        public string Template;
        public string RefKey;
        public string Detail;
        public string Variables;
        public string Settings;
        public int? VarId;
        public bool? AllowImplicitKeys;
        public ValueKind? ValueKind;
        public string ValuePreview;
        public IDynamicContext DynamicContext;
    }

    internal static class RichTextRuntimeLogger
    {
        public static void Warn(string code, string message, RichTextRuntimeLogContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(BuildMessage("WARN", code, message, context));
#endif
        }

        public static void Error(string code, string message, RichTextRuntimeLogContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(BuildMessage("ERROR", code, message, context));
#endif
        }

        public static void Log(string message)
        {
            Warn("RT-LEGACY", message, default);
        }

        static string BuildMessage(string level, string code, string message, RichTextRuntimeLogContext context)
        {
            var sb = new StringBuilder(768);
            DynamicRuntimeLogUtility.AppendLogHeader(sb, "RichText", level, string.IsNullOrEmpty(code) ? "RT-UNKNOWN" : code, message ?? string.Empty);

            DynamicRuntimeLogUtility.AppendSection(sb, "RichText");
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "phase", context.Phase);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "identifier", context.Identifier);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "refKey", context.RefKey);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "template", context.Template, allowMultiline: true);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "settings", context.Settings, allowMultiline: true);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "variables", context.Variables, allowMultiline: true);

            if (context.VarId.HasValue)
                DynamicRuntimeLogUtility.AppendFieldLine(sb, "varId", context.VarId.Value.ToString());
            if (context.AllowImplicitKeys.HasValue)
                DynamicRuntimeLogUtility.AppendFieldLine(sb, "allowImplicitKeys", context.AllowImplicitKeys.Value ? "true" : "false");
            if (context.ValueKind.HasValue)
                DynamicRuntimeLogUtility.AppendFieldLine(sb, "valueKind", context.ValueKind.Value.ToString());
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "value", context.ValuePreview);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "detail", context.Detail, allowMultiline: true);

            DynamicRuntimeLogUtility.AppendDynamicContextSection(sb, context.DynamicContext);
            DynamicRuntimeLogUtility.AppendCommandTraceSection(sb);
            return sb.ToString();
        }
    }
}
