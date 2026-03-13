// Game.Common.ExpressionAST.cs
//
// 式評価用の AST ノード定義。
// BoolExpressionSource / FloatExpressionSource で使用。

using System;
using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>式の型</summary>
    public enum ExprType
    {
        Unknown,
        Number,
        Bool,
        String,
    }

    /// <summary>
    /// 式評価用のスコープ。
    /// コンテキストと変数マップを保持。
    /// </summary>
    public sealed class ExprEvalScope
    {
        public IDynamicContext Context { get; set; }
        public Dictionary<string, DynamicValue> Variables { get; set; }

        public ExprEvalScope(IDynamicContext ctx, Dictionary<string, DynamicValue> vars)
        {
            Context = ctx;
            Variables = vars;
        }
    }

    /// <summary>
    /// 式ノードの基底クラス。
    /// </summary>
    public abstract class ExpressionNode
    {
        public ExprType Type { get; protected set; }
        public abstract DynamicVariant Evaluate(ExprEvalScope scope);
    }

    /// <summary>リテラルノード</summary>
    public sealed class LiteralExprNode : ExpressionNode
    {
        readonly DynamicVariant _value;

        public LiteralExprNode(DynamicVariant value, ExprType type)
        {
            _value = value;
            Type = type;
        }

        public override DynamicVariant Evaluate(ExprEvalScope scope) => _value;
    }

    /// <summary>変数ノード</summary>
    public sealed class VariableExprNode : ExpressionNode
    {
        readonly string _key;

        public string Key => _key;

        public VariableExprNode(string key, ValueKind hint)
        {
            _key = key;
            Type = hint switch
            {
                ValueKind.Bool => ExprType.Bool,
                ValueKind.Int or ValueKind.Float => ExprType.Number,
                ValueKind.String => ExprType.String,
                _ => ExprType.Unknown,
            };
        }

        public VariableExprNode(string key)
        {
            _key = key;
            Type = ExprType.Unknown;
        }

        public override DynamicVariant Evaluate(ExprEvalScope scope)
        {
            if (scope.Variables != null && scope.Variables.TryGetValue(_key, out var v))
                return v.Evaluate(scope.Context ?? DummyDynamicContext.Instance);

            var ctx = scope.Context;
            if (ctx?.Vars == null)
                throw new InvalidOperationException($"Var '{_key}' is not available (no VarStore in context).");

            if (!VarIdResolver.TryResolve(_key, out var varId) || varId == 0)
                throw new InvalidOperationException($"Var '{_key}' is not registered.");

            if (!ctx.Vars.TryGetVariant(varId, out var variant))
            {
                var registry = VarKeyRegistryLocator.GetOrCreate();
                if (registry != null &&
                    registry.TryResolve(_key, out var registryId) &&
                    registryId > 0 &&
                    ctx.Vars.TryGetVariant(registryId, out var resolvedVariant))
                {
                    return resolvedVariant;
                }

                throw new InvalidOperationException($"Var '{_key}' is not set in VarStore.");
            }

            return variant;
        }
    }

    /// <summary>関数呼び出しノード</summary>
    public sealed class CallExprNode : ExpressionNode
    {
        readonly string _name;
        readonly List<ExpressionNode> _args;

        public CallExprNode(string name, List<ExpressionNode> args)
        {
            _name = name;
            _args = args ?? new List<ExpressionNode>(0);
            Type = ExprType.Unknown;
        }

        public override DynamicVariant Evaluate(ExprEvalScope scope)
        {
            var argCount = _args.Count;
            var evaluated = new DynamicVariant[argCount];
            for (int i = 0; i < argCount; i++)
                evaluated[i] = _args[i].Evaluate(scope);

            return ExpressionFunctionRegistry.Invoke(_name, evaluated);
        }
    }

    /// <summary>単項演算ノード</summary>
    public sealed class UnaryExprNode : ExpressionNode
    {
        readonly ExprTokenKind _op;
        readonly ExpressionNode _rhs;

        public UnaryExprNode(ExprTokenKind op, ExpressionNode rhs)
        {
            _op = op;
            _rhs = rhs;
            Type = rhs?.Type ?? ExprType.Unknown;
        }

        public override DynamicVariant Evaluate(ExprEvalScope scope)
        {
            var v = _rhs.Evaluate(scope);
            return _op switch
            {
                ExprTokenKind.Not => DynamicVariant.FromBool(!ExpressionHelper.AsBool(v)),
                ExprTokenKind.Minus => DynamicVariant.FromFloat(-ExpressionHelper.AsNumber(v)),
                _ => v
            };
        }
    }

    /// <summary>二項演算ノード</summary>
    public sealed class BinaryExprNode : ExpressionNode
    {
        readonly ExprTokenKind _op;
        readonly ExpressionNode _lhs;
        readonly ExpressionNode _rhs;

        public BinaryExprNode(ExprTokenKind op, ExpressionNode lhs, ExpressionNode rhs, ExprType type)
        {
            _op = op;
            _lhs = lhs;
            _rhs = rhs;
            Type = type;
        }

        public override DynamicVariant Evaluate(ExprEvalScope scope)
        {
            var a = _lhs.Evaluate(scope);
            var b = _rhs.Evaluate(scope);

            return _op switch
            {
                ExprTokenKind.Plus => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(a) + ExpressionHelper.AsNumber(b)),
                ExprTokenKind.Minus => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(a) - ExpressionHelper.AsNumber(b)),
                ExprTokenKind.Star => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(a) * ExpressionHelper.AsNumber(b)),
                ExprTokenKind.Slash => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(a) / ExpressionHelper.AsNumber(b)),
                ExprTokenKind.Percent => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(a) % ExpressionHelper.AsNumber(b)),

                ExprTokenKind.Equal => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) == 0),
                ExprTokenKind.NotEqual => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) != 0),
                ExprTokenKind.Less => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) < 0),
                ExprTokenKind.LessEqual => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) <= 0),
                ExprTokenKind.Greater => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) > 0),
                ExprTokenKind.GreaterEqual => DynamicVariant.FromBool(ExpressionHelper.Compare(a, b) >= 0),

                ExprTokenKind.And => DynamicVariant.FromBool(ExpressionHelper.AsBool(a) && ExpressionHelper.AsBool(b)),
                ExprTokenKind.Or => DynamicVariant.FromBool(ExpressionHelper.AsBool(a) || ExpressionHelper.AsBool(b)),
                _ => DynamicVariant.Null
            };
        }
    }

    /// <summary>
    /// 式評価用ヘルパメソッド。
    /// </summary>
    public static class ExpressionHelper
    {
        public static float AsNumber(DynamicVariant v)
        {
            return v.Kind switch
            {
                ValueKind.Bool => v.AsBool ? 1f : 0f,
                ValueKind.Int => v.AsInt,
                ValueKind.Float => v.AsFloat,
                _ => 0f
            };
        }

        public static bool AsBool(DynamicVariant v)
        {
            return v.Kind switch
            {
                ValueKind.Null => false,
                ValueKind.Bool => v.AsBool,
                ValueKind.Int => v.AsInt != 0,
                ValueKind.Float => Math.Abs(v.AsFloat) > float.Epsilon,
                ValueKind.String => !string.IsNullOrEmpty(v.AsString),
                _ => false
            };
        }

        public static int Compare(DynamicVariant a, DynamicVariant b)
        {
            if (a.Kind == ValueKind.String || b.Kind == ValueKind.String)
            {
                return string.Compare(a.AsString, b.AsString, StringComparison.Ordinal);
            }

            return AsNumber(a).CompareTo(AsNumber(b));
        }
    }

    /// <summary>
    /// 最小限の IDynamicContext 実装（フォールバック用）。
    /// </summary>
    internal sealed class DummyDynamicContext : IDynamicContext
    {
        public static readonly DummyDynamicContext Instance = new();
        public IVarStore Vars => NullVarStore.Instance;
        public IScopeNode Scope => null;
        public IScopeNode CommandRootScope => null;
        public IScopeNode ResolveOtherScope(Game.Commands.CommandTargetIdentityFilter filter) => null;
    }
}
