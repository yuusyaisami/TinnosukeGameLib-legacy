// Game.Common.ExpressionTokenizer.cs
//
// 式文字列をトークン化する共通トークナイザ。
// BoolExpressionSource / FloatExpressionSource で使用。

using System.Collections.Generic;
using System.Text;

namespace Game.Common
{
    /// <summary>トークン種別</summary>
    public enum ExprTokenKind
    {
        Identifier,
        Number,
        String,
        Bool,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
        Comma,
        LParen,
        RParen,
        And,
        Or,
        Not,
        Equal,
        NotEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        End,
    }

    /// <summary>トークン構造体</summary>
    public readonly struct ExprToken
    {
        public readonly ExprTokenKind Kind;
        public readonly string Text;

        public ExprToken(ExprTokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    /// <summary>
    /// 式文字列をトークン化するクラス。
    /// </summary>
    public sealed class ExpressionTokenizer
    {
        readonly string _src;
        int _pos;

        public ExpressionTokenizer(string src)
        {
            _src = src ?? string.Empty;
            _pos = 0;
        }

        /// <summary>
        /// 式文字列をトークンリストに変換。
        /// </summary>
        /// <param name="error">エラー発生時にメッセージが入る</param>
        /// <returns>トークンリスト</returns>
        public List<ExprToken> Tokenize(out string error)
        {
            error = null;
            var list = new List<ExprToken>();

            while (true)
            {
                SkipWhitespace();
                if (Eof)
                {
                    list.Add(new ExprToken(ExprTokenKind.End, string.Empty));
                    break;
                }

                var c = Peek();

                // 識別子
                if (char.IsLetter(c) || c == '_')
                {
                    var ident = ReadIdentifier();
                    if (ident == "true" || ident == "false")
                        list.Add(new ExprToken(ExprTokenKind.Bool, ident));
                    else
                        list.Add(new ExprToken(ExprTokenKind.Identifier, ident));
                    continue;
                }

                // 数値
                if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek(1))))
                {
                    var number = ReadNumber();
                    list.Add(new ExprToken(ExprTokenKind.Number, number));
                    continue;
                }

                // 文字列
                if (c == '"')
                {
                    var str = ReadString(out var strErr);
                    if (strErr != null)
                    {
                        error = strErr;
                        return list;
                    }
                    list.Add(new ExprToken(ExprTokenKind.String, str));
                    continue;
                }

                // 演算子・括弧
                switch (c)
                {
                    case '+':
                        list.Add(new ExprToken(ExprTokenKind.Plus, "+"));
                        _pos++;
                        break;
                    case '-':
                        list.Add(new ExprToken(ExprTokenKind.Minus, "-"));
                        _pos++;
                        break;
                    case '*':
                        list.Add(new ExprToken(ExprTokenKind.Star, "*"));
                        _pos++;
                        break;
                    case '/':
                        list.Add(new ExprToken(ExprTokenKind.Slash, "/"));
                        _pos++;
                        break;
                    case '%':
                        list.Add(new ExprToken(ExprTokenKind.Percent, "%"));
                        _pos++;
                        break;
                    case ',':
                        list.Add(new ExprToken(ExprTokenKind.Comma, ","));
                        _pos++;
                        break;
                    case '(':
                        list.Add(new ExprToken(ExprTokenKind.LParen, "("));
                        _pos++;
                        break;
                    case ')':
                        list.Add(new ExprToken(ExprTokenKind.RParen, ")"));
                        _pos++;
                        break;
                    case '!':
                        if (Peek(1) == '=')
                        {
                            list.Add(new ExprToken(ExprTokenKind.NotEqual, "!="));
                            _pos += 2;
                        }
                        else
                        {
                            list.Add(new ExprToken(ExprTokenKind.Not, "!"));
                            _pos++;
                        }
                        break;
                    case '&':
                        if (Peek(1) == '&')
                        {
                            list.Add(new ExprToken(ExprTokenKind.And, "&&"));
                            _pos += 2;
                        }
                        else
                        {
                            error = "Expected &&";
                            return list;
                        }
                        break;
                    case '|':
                        if (Peek(1) == '|')
                        {
                            list.Add(new ExprToken(ExprTokenKind.Or, "||"));
                            _pos += 2;
                        }
                        else
                        {
                            error = "Expected ||";
                            return list;
                        }
                        break;
                    case '=':
                        if (Peek(1) == '=')
                        {
                            list.Add(new ExprToken(ExprTokenKind.Equal, "=="));
                            _pos += 2;
                        }
                        else
                        {
                            error = "Expected ==";
                            return list;
                        }
                        break;
                    case '<':
                        if (Peek(1) == '=')
                        {
                            list.Add(new ExprToken(ExprTokenKind.LessEqual, "<="));
                            _pos += 2;
                        }
                        else
                        {
                            list.Add(new ExprToken(ExprTokenKind.Less, "<"));
                            _pos++;
                        }
                        break;
                    case '>':
                        if (Peek(1) == '=')
                        {
                            list.Add(new ExprToken(ExprTokenKind.GreaterEqual, ">="));
                            _pos += 2;
                        }
                        else
                        {
                            list.Add(new ExprToken(ExprTokenKind.Greater, ">"));
                            _pos++;
                        }
                        break;
                    case '{':
                    case '}':
                        // 波括弧は無視（オプション）
                        _pos++;
                        break;
                    default:
                        error = $"Unexpected character '{c}'";
                        return list;
                }
            }

            return list;
        }

        void SkipWhitespace()
        {
            while (!Eof && char.IsWhiteSpace(Peek()))
                _pos++;
        }

        char Peek(int offset = 0)
        {
            var idx = _pos + offset;
            return idx >= 0 && idx < _src.Length ? _src[idx] : '\0';
        }

        bool Eof => _pos >= _src.Length;

        string ReadIdentifier()
        {
            var start = _pos;
            while (!Eof && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
                _pos++;
            return _src.Substring(start, _pos - start);
        }

        string ReadNumber()
        {
            var start = _pos;
            while (!Eof && (char.IsDigit(Peek()) || Peek() == '.'))
                _pos++;
            return _src.Substring(start, _pos - start);
        }

        string ReadString(out string err)
        {
            err = null;
            _pos++; // skip opening quote
            var sb = new StringBuilder();
            while (!Eof)
            {
                var c = Peek();
                if (c == '"')
                {
                    _pos++;
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    _pos++;
                    if (Eof)
                    {
                        err = "Unterminated string";
                        return null;
                    }
                    var esc = Peek();
                    _pos++;
                    sb.Append(esc switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => esc
                    });
                    continue;
                }
                sb.Append(c);
                _pos++;
            }

            err = "Unterminated string";
            return null;
        }
    }
}
