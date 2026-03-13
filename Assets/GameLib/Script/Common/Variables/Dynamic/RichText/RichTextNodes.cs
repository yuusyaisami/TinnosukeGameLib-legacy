using System;
using System.Collections.Generic;
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
                RichTextRuntimeLogger.Log($"RichText value resolve failed ({_identifier}): resolved value is Null");
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
                    RichTextRuntimeLogger.Log($"RichText cond error ({_identifier}): {ex.Message}");
                    return empty;
                }

                if (_useConditionForVisibility && condResult == false)
                    return empty;
            }

            if (!RichTextOptions.TryFormat(value, _formatOptions, out var formatted))
            {
                RichTextRuntimeLogger.Log($"RichText format error ({_identifier})");
                return empty;
            }

            if (!RichTextEffects.TryApply(formatted, _decoratorOptions, condResult, out var decorated))
            {
                RichTextRuntimeLogger.Log($"RichText effect error ({_identifier})");
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
                    RichTextRuntimeLogger.Log($"RichText value error ({_identifier}): {ex.Message}");
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
                    RichTextRuntimeLogger.Log($"RichText value error ({_identifier}): {ex.Message}");
                    return false;
                }
            }

            if (!_allowImplicitKeys || context?.Vars == null)
            {
                RichTextRuntimeLogger.Log($"RichText value resolve failed ({_identifier}): implicit key lookup is disabled or vars are null");
                return false;
            }

            var varId = _varId;
            if (varId == 0)
            {
                if (!VarIdResolver.TryResolve(_identifier, out var resolved) || resolved == 0)
                {
                    RichTextRuntimeLogger.Log($"RichText value resolve failed ({_identifier}): var id not found");
                    return false;
                }
                varId = resolved;
            }

            if (!context.Vars.TryGetVariant(varId, out value))
            {
                RichTextRuntimeLogger.Log($"RichText value resolve failed ({_identifier}): vars missing varId={varId}");
                return false;
            }

            return true;
        }
    }

    internal static class RichTextRuntimeLogger
    {
        public static void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }
    }
}
