// Game.Common.ExpressionParser.cs
//
// 式文字列をパースして AST を構築するパーサ。
// BoolExpressionSource / FloatExpressionSource で使用。

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// 式文字列をパースして AST を構築するクラス。
    /// </summary>
    public sealed class ExpressionParser
    {
        readonly List<ExprToken> _tokens;
        readonly Dictionary<string, ValueKind> _types;
        readonly HashSet<string> _usedIdentifiers;
        int _pos;

        public ExpressionParser(
            List<ExprToken> tokens,
            Dictionary<string, ValueKind> types,
            HashSet<string> usedIdentifiers)
        {
            _tokens = tokens;
            _types = types ?? new Dictionary<string, ValueKind>(System.StringComparer.Ordinal);
            _usedIdentifiers = usedIdentifiers ?? new HashSet<string>(System.StringComparer.Ordinal);
        }

        /// <summary>
        /// 式をパースして AST ノードを返す。
        /// </summary>
        /// <param name="error">エラー発生時にメッセージが入る</param>
        /// <returns>AST のルートノード</returns>
        public ExpressionNode ParseExpression(out string error)
        {
            error = null;
            var expr = ParseOr(ref error);
            if (error != null)
                return null;

            if (Current.Kind != ExprTokenKind.End)
            {
                error = $"Unexpected token '{Current.Text}'";
                return null;
            }
            return expr;
        }

        ExpressionNode ParseOr(ref string error)
        {
            var node = ParseAnd(ref error);
            while (error == null && Match(ExprTokenKind.Or))
            {
                var rhs = ParseAnd(ref error);
                node = MakeBinary(ExprTokenKind.Or, node, rhs, ExprType.Bool, ref error);
            }
            return node;
        }

        ExpressionNode ParseAnd(ref string error)
        {
            var node = ParseEquality(ref error);
            while (error == null && Match(ExprTokenKind.And))
            {
                var rhs = ParseEquality(ref error);
                node = MakeBinary(ExprTokenKind.And, node, rhs, ExprType.Bool, ref error);
            }
            return node;
        }

        ExpressionNode ParseEquality(ref string error)
        {
            var node = ParseRelational(ref error);
            while (error == null && (Match(ExprTokenKind.Equal) || Match(ExprTokenKind.NotEqual)))
            {
                var op = Previous;
                var rhs = ParseRelational(ref error);
                node = MakeComparison(op.Kind, node, rhs, ref error);
            }
            return node;
        }

        ExpressionNode ParseRelational(ref string error)
        {
            var node = ParseAdd(ref error);

            // チェーン比較対応: "a <= b < c" → "(a <= b) && (b < c)"
            // 前回の比較の右辺を記憶し、次の比較で左辺として再利用する
            ExpressionNode lastComparisonRhs = null;

            while (error == null && IsRelationalOperator(Current.Kind))
            {
                Match(Current.Kind);
                var op = Previous;
                var rhs = ParseAdd(ref error);
                if (error != null) return null;

                if (lastComparisonRhs != null)
                {
                    // チェーン比較: 前回は (a op1 b), 今回は op2 c
                    // "(a op1 b) && (b op2 c)" に書き換える
                    var chainedRight = MakeComparison(op.Kind, lastComparisonRhs, rhs, ref error);
                    if (error != null) return null;
                    node = MakeBinary(ExprTokenKind.And, node, chainedRight, ExprType.Bool, ref error);
                }
                else
                {
                    node = MakeComparison(op.Kind, node, rhs, ref error);
                }

                lastComparisonRhs = rhs;
            }
            return node;
        }

        static bool IsRelationalOperator(ExprTokenKind kind)
        {
            return kind == ExprTokenKind.Less || kind == ExprTokenKind.LessEqual ||
                   kind == ExprTokenKind.Greater || kind == ExprTokenKind.GreaterEqual;
        }

        ExpressionNode ParseAdd(ref string error)
        {
            var node = ParseMul(ref error);
            while (error == null && (Match(ExprTokenKind.Plus) || Match(ExprTokenKind.Minus)))
            {
                var op = Previous;
                var rhs = ParseMul(ref error);
                node = MakeNumeric(op.Kind, node, rhs, ref error);
            }
            return node;
        }

        ExpressionNode ParseMul(ref string error)
        {
            var node = ParseUnary(ref error);
            while (error == null && (Match(ExprTokenKind.Star) || Match(ExprTokenKind.Slash) || Match(ExprTokenKind.Percent)))
            {
                var op = Previous;
                var rhs = ParseUnary(ref error);
                node = MakeNumeric(op.Kind, node, rhs, ref error);
            }
            return node;
        }

        ExpressionNode ParseUnary(ref string error)
        {
            if (Match(ExprTokenKind.Not) || Match(ExprTokenKind.Minus))
            {
                var op = Previous;
                var rhs = ParseUnary(ref error);
                if (error != null) return null;
                // Allow dynamic types for Unary operators
                return new UnaryExprNode(op.Kind, rhs);
            }
            return ParsePrimary(ref error);
        }

        ExpressionNode ParsePrimary(ref string error)
        {
            if (Match(ExprTokenKind.Number))
            {
                if (!float.TryParse(Previous.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    error = "Invalid number";
                    return null;
                }
                return new LiteralExprNode(DynamicVariant.FromFloat(f), ExprType.Number);
            }

            if (Match(ExprTokenKind.String))
            {
                return new LiteralExprNode(DynamicVariant.FromString(Previous.Text), ExprType.String);
            }

            if (Match(ExprTokenKind.Bool))
            {
                return new LiteralExprNode(DynamicVariant.FromBool(Previous.Text == "true"), ExprType.Bool);
            }

            if (Match(ExprTokenKind.Identifier))
            {
                var key = Previous.Text;

                // Function call: Name(...)
                if (Match(ExprTokenKind.LParen))
                {
                    var args = new List<ExpressionNode>(4);

                    // Zero-arg
                    if (!Match(ExprTokenKind.RParen))
                    {
                        while (true)
                        {
                            var arg = ParseOr(ref error);
                            if (error != null)
                                return null;

                            args.Add(arg);

                            if (Match(ExprTokenKind.Comma))
                                continue;

                            if (!Match(ExprTokenKind.RParen))
                            {
                                error = "Missing ')'";
                                return null;
                            }
                            break;
                        }
                    }

                    return new CallExprNode(key, args);
                }

                // Variable
                if (_types.TryGetValue(key, out var hint))
                {
                    _usedIdentifiers.Add(key);
                    return new VariableExprNode(key, hint);
                }

                // Implicit key
                _usedIdentifiers.Add(key);
                return new VariableExprNode(key);
            }

            if (Match(ExprTokenKind.LParen))
            {
                var expr = ParseOr(ref error);
                if (!Match(ExprTokenKind.RParen))
                    error = "Missing ')'";
                return expr;
            }

            error = $"Unexpected token '{Current.Text}'";
            return null;
        }

        ExpressionNode MakeNumeric(ExprTokenKind op, ExpressionNode a, ExpressionNode b, ref string error)
        {
            if (a == null || b == null)
            {
                error = "Incomplete arithmetic expression";
                return null;
            }
            // Allow dynamic types (runtime conversion)
            return new BinaryExprNode(op, a, b, ExprType.Number);
        }

        ExpressionNode MakeComparison(ExprTokenKind op, ExpressionNode a, ExpressionNode b, ref string error)
        {
            if (a == null || b == null)
            {
                error = "Incomplete comparison expression";
                return null;
            }

            // Allow dynamic types (runtime conversion)
            return new BinaryExprNode(op, a, b, ExprType.Bool);
        }

        ExpressionNode MakeBinary(ExprTokenKind op, ExpressionNode a, ExpressionNode b, ExprType type, ref string error)
        {
            if (a == null || b == null)
            {
                error = "Incomplete binary expression";
                return null;
            }
            // Allow dynamic types (runtime conversion)
            return new BinaryExprNode(op, a, b, type);
        }

        static bool IsNumeric(ExpressionNode n) => n != null && (n.Type == ExprType.Number || n.Type == ExprType.Unknown);
        static bool IsString(ExpressionNode n) => n != null && (n.Type == ExprType.String || n.Type == ExprType.Unknown);
        static bool IsBool(ExpressionNode n) => n != null && (n.Type == ExprType.Bool || n.Type == ExprType.Unknown);
        static bool IsBoolLike(ExpressionNode n) => IsBool(n);

        bool Match(ExprTokenKind kind)
        {
            if (Current.Kind == kind)
            {
                _pos++;
                return true;
            }
            return false;
        }

        ExprToken Current => _tokens[Mathf.Clamp(_pos, 0, _tokens.Count - 1)];
        ExprToken Previous => _tokens[Mathf.Clamp(_pos - 1, 0, _tokens.Count - 1)];
    }
}
