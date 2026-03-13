using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Common
{
    public static class RichTextTemplateCompiler
    {
        public static bool TryCompile(
            string template,
            Dictionary<string, DynamicValue> scopeMap,
            Dictionary<string, ValueKind> typeMap,
            bool allowImplicitKeys,
            out List<RichTextNode> nodes,
            out HashSet<string> usedIdentifiers,
            out string error)
        {
            nodes = new List<RichTextNode>();
            usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
            error = null;

            if (string.IsNullOrEmpty(template))
                return true;

            var sb = new StringBuilder(template.Length);

            for (int i = 0; i < template.Length; i++)
            {
                var c = template[i];

                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append('{');
                        i++;
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        nodes.Add(new TextNode(sb.ToString()));
                        sb.Clear();
                    }

                    if (!TryReadPlaceholder(template, ref i, out var content, out error))
                        return false;

                    if (!TryParsePlaceholder(content, scopeMap, typeMap, allowImplicitKeys, usedIdentifiers, out var node, out error))
                        return false;

                    nodes.Add(node);
                    continue;
                }

                if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}')
                    {
                        sb.Append('}');
                        i++;
                        continue;
                    }

                    error = $"Unexpected '}}' at index {i}";
                    return false;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                nodes.Add(new TextNode(sb.ToString()));

            return true;
        }

        static bool TryReadPlaceholder(string template, ref int index, out string content, out string error)
        {
            error = null;
            content = null;
            int start = index + 1;
            bool inQuote = false;

            for (int i = start; i < template.Length; i++)
            {
                var c = template[i];
                if (c == '"' && !IsEscaped(template, i))
                    inQuote = !inQuote;

                if (!inQuote && c == '}')
                {
                    content = template.Substring(start, i - start);
                    index = i;
                    return true;
                }
            }

            error = "Missing '}' in placeholder";
            return false;
        }

        static bool TryParsePlaceholder(
            string content,
            Dictionary<string, DynamicValue> scopeMap,
            Dictionary<string, ValueKind> typeMap,
            bool allowImplicitKeys,
            HashSet<string> usedIdentifiers,
            out RichTextNode node,
            out string error)
        {
            node = null;
            error = null;

            if (!TrySplitSegments(content, out var segments, out error))
                return false;

            if (segments.Count == 0)
            {
                error = "Empty placeholder";
                return false;
            }

            var identifier = segments[0].Trim();
            if (string.IsNullOrEmpty(identifier))
            {
                error = "Placeholder identifier is empty";
                return false;
            }

            bool hasExplicitValue = false;
            var explicitValue = default(DynamicValue);
            int varId = 0;
            ExpressionNode valueExpression = null;
            ValueKind valueKindForCondition = ValueKind.Null;

            if (LooksLikeExpression(identifier))
            {
                if (!TryCompileValueExpression(identifier, typeMap, allowImplicitKeys, out valueExpression, out var exprUsed, out error))
                    return false;

                if (exprUsed != null && exprUsed.Count > 0)
                {
                    foreach (var key in exprUsed)
                        usedIdentifiers.Add(key);
                }

                valueKindForCondition = MapExprTypeToValueKind(valueExpression?.Type ?? ExprType.Unknown);
            }
            else
            {
                if (string.Equals(identifier, RichTextConstants.ValueIdentifier, StringComparison.Ordinal))
                {
                    error = "Identifier 'value' is reserved";
                    return false;
                }

                hasExplicitValue = typeMap.TryGetValue(identifier, out var expectedKind);
                if (hasExplicitValue)
                {
                    if (!scopeMap.TryGetValue(identifier, out explicitValue))
                    {
                        error = $"Variable '{identifier}' is not available";
                        return false;
                    }
                    valueKindForCondition = expectedKind;
                }
                else if (allowImplicitKeys)
                {
                    if (VarIdResolver.TryResolve(identifier, out var resolved) && resolved != 0)
                        varId = resolved;
                }
                else
                {
                    error = $"Identifier '{identifier}' is not defined in Variables";
                    return false;
                }

                usedIdentifiers.Add(identifier);
            }

            var formatOptions = new RichTextFormatOptions();
            var decoratorOptions = new RichTextDecoratorOptions();
            string condExpression = null;
            bool hasCond = false;

            for (int i = 1; i < segments.Count; i++)
            {
                var segment = segments[i].Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    error = "Empty option in placeholder";
                    return false;
                }

                if (!TrySplitNameValue(segment, out var name, out var rawValue, out var hasValue, out error))
                    return false;

                var optionKey = name.Trim().ToLowerInvariant();
                string value = null;

                if (hasValue)
                {
                    if (!TryParseOptionValue(rawValue, out value, out error))
                        return false;
                }

                switch (optionKey)
                {
                    case "empty":
                        if (!hasValue)
                        {
                            error = "empty expects a value";
                            return false;
                        }
                        formatOptions.HasEmptyFallback = true;
                        formatOptions.EmptyFallback = value ?? string.Empty;
                        break;
                    case "sign":
                        if (!hasValue)
                        {
                            error = "sign expects a value";
                            return false;
                        }
                        if (string.Equals(value, "always", StringComparison.OrdinalIgnoreCase))
                            formatOptions.SignAlways = true;
                        else if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
                            formatOptions.SignAlways = false;
                        else
                        {
                            error = "sign expects 'auto' or 'always'";
                            return false;
                        }
                        break;
                    case "round":
                        if (!TryParseNonNegativeInt(value, hasValue, "round", out var roundDigits, out error))
                            return false;
                        formatOptions.RoundDigits = roundDigits;
                        break;
                    case "fixed":
                        if (!TryParseNonNegativeInt(value, hasValue, "fixed", out var fixedDigits, out error))
                            return false;
                        formatOptions.FixedDigits = fixedDigits;
                        break;
                    case "percent":
                        formatOptions.UsePercent = true;
                        if (hasValue)
                        {
                            if (!TryParseNonNegativeInt(value, true, "percent", out var percentDigits, out error))
                                return false;
                            formatOptions.PercentDigits = percentDigits;
                        }
                        break;
                    case "prefix":
                        if (!hasValue)
                        {
                            error = "prefix expects a value";
                            return false;
                        }
                        formatOptions.Prefix = value ?? string.Empty;
                        break;
                    case "suffix":
                        if (!hasValue)
                        {
                            error = "suffix expects a value";
                            return false;
                        }
                        formatOptions.Suffix = value ?? string.Empty;
                        break;
                    case "cond":
                        if (!hasValue)
                        {
                            error = "cond expects a value";
                            return false;
                        }
                        if (hasCond)
                        {
                            error = "cond is already set";
                            return false;
                        }
                        hasCond = true;
                        condExpression = value ?? string.Empty;
                        break;
                    case "effect":
                        if (!hasValue)
                        {
                            error = "effect expects a value";
                            return false;
                        }
                        if (decoratorOptions.EffectKind != RichTextEffectKind.None)
                        {
                            error = "effect is already set";
                            return false;
                        }
                        if (string.Equals(value, "color_if", StringComparison.OrdinalIgnoreCase))
                        {
                            decoratorOptions.EffectKind = RichTextEffectKind.ColorIf;
                        }
                        else
                        {
                            error = $"Unknown effect: {value}";
                            return false;
                        }
                        break;
                    case "truecolor":
                        if (!hasValue)
                        {
                            error = "trueColor expects a value";
                            return false;
                        }
                        decoratorOptions.TrueColor = value ?? string.Empty;
                        break;
                    case "falsecolor":
                        if (!hasValue)
                        {
                            error = "falseColor expects a value";
                            return false;
                        }
                        decoratorOptions.FalseColor = value ?? string.Empty;
                        break;
                    case "color":
                        if (!hasValue)
                        {
                            error = "color expects a value";
                            return false;
                        }
                        decoratorOptions.Color = value ?? string.Empty;
                        break;
                    case "wrap":
                        if (!hasValue)
                        {
                            error = "wrap expects a value";
                            return false;
                        }
                        decoratorOptions.Wrap = value ?? string.Empty;
                        break;
                    case "wrapend":
                        if (!hasValue)
                        {
                            error = "wrapEnd expects a value";
                            return false;
                        }
                        decoratorOptions.WrapEnd = value ?? string.Empty;
                        break;
                    default:
                        error = $"Unknown option: {name}";
                        return false;
                }
            }

            if (decoratorOptions.EffectKind == RichTextEffectKind.ColorIf)
            {
                if (!hasCond)
                {
                    error = "effect=color_if requires cond";
                    return false;
                }

                if (!string.IsNullOrEmpty(decoratorOptions.Color))
                {
                    error = "color cannot be combined with effect=color_if";
                    return false;
                }

                if (string.IsNullOrEmpty(decoratorOptions.TrueColor) || string.IsNullOrEmpty(decoratorOptions.FalseColor))
                {
                    error = "color_if requires trueColor and falseColor";
                    return false;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(decoratorOptions.TrueColor) || !string.IsNullOrEmpty(decoratorOptions.FalseColor))
                {
                    error = "trueColor/falseColor require effect=color_if";
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(decoratorOptions.Color) && !IsValidColorCode(decoratorOptions.Color))
            {
                error = $"Invalid color: {decoratorOptions.Color}";
                return false;
            }

            if (!string.IsNullOrEmpty(decoratorOptions.TrueColor) && !IsValidColorCode(decoratorOptions.TrueColor))
            {
                error = $"Invalid trueColor: {decoratorOptions.TrueColor}";
                return false;
            }

            if (!string.IsNullOrEmpty(decoratorOptions.FalseColor) && !IsValidColorCode(decoratorOptions.FalseColor))
            {
                error = $"Invalid falseColor: {decoratorOptions.FalseColor}";
                return false;
            }

            ExpressionNode conditionNode = null;
            bool useConditionForVisibility = false;
            if (hasCond)
            {
                if (!TryCompileCondition(condExpression, typeMap, valueKindForCondition, allowImplicitKeys, out conditionNode, out var condUsed, out error))
                    return false;

                if (condUsed != null && condUsed.Count > 0)
                {
                    foreach (var key in condUsed)
                        usedIdentifiers.Add(key);
                }

                useConditionForVisibility = decoratorOptions.EffectKind == RichTextEffectKind.None;
            }

            node = new PlaceholderNode(
                identifier,
                explicitValue,
                hasExplicitValue,
                varId,
                allowImplicitKeys,
                formatOptions,
                decoratorOptions,
                valueExpression,
                conditionNode,
                useConditionForVisibility);

            return true;
        }

        static bool TryCompileCondition(
            string expression,
            Dictionary<string, ValueKind> typeMap,
            ValueKind valueKind,
            bool allowImplicitKeys,
            out ExpressionNode node,
            out HashSet<string> usedIdentifiers,
            out string error)
        {
            node = null;
            usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "cond is empty";
                return false;
            }

            try
            {
                var tokenizer = new ExpressionTokenizer(expression);
                var tokens = tokenizer.Tokenize(out var lexError);
                if (lexError != null)
                {
                    error = $"cond: {lexError}";
                    return false;
                }

                var localTypes = new Dictionary<string, ValueKind>(typeMap, StringComparer.Ordinal)
                {
                    [RichTextConstants.ValueIdentifier] = valueKind
                };

                var parser = new ExpressionParser(tokens, localTypes, usedIdentifiers);
                node = parser.ParseExpression(out var parseError);
                if (parseError != null)
                {
                    error = $"cond: {parseError}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"cond: {ex.Message}";
                return false;
            }

            usedIdentifiers.Remove(RichTextConstants.ValueIdentifier);

            if (!allowImplicitKeys)
            {
                foreach (var key in usedIdentifiers)
                {
                    if (!typeMap.ContainsKey(key))
                    {
                        error = $"Identifier '{key}' is not defined in Variables";
                        return false;
                    }
                }
            }

            return true;
        }

        static bool TryCompileValueExpression(
            string expression,
            Dictionary<string, ValueKind> typeMap,
            bool allowImplicitKeys,
            out ExpressionNode node,
            out HashSet<string> usedIdentifiers,
            out string error)
        {
            node = null;
            usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "expression is empty";
                return false;
            }

            try
            {
                var tokenizer = new ExpressionTokenizer(expression);
                var tokens = tokenizer.Tokenize(out var lexError);
                if (lexError != null)
                {
                    error = lexError;
                    return false;
                }

                var parser = new ExpressionParser(tokens, typeMap, usedIdentifiers);
                node = parser.ParseExpression(out var parseError);
                if (parseError != null)
                {
                    error = parseError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!allowImplicitKeys)
            {
                foreach (var key in usedIdentifiers)
                {
                    if (!typeMap.ContainsKey(key))
                    {
                        error = $"Identifier '{key}' is not defined in Variables";
                        return false;
                    }
                }
            }

            return true;
        }

        static bool LooksLikeExpression(string identifier)
        {
            for (int i = 0; i < identifier.Length; i++)
            {
                var c = identifier[i];
                if (char.IsWhiteSpace(c))
                    return true;
                switch (c)
                {
                    case '(':
                    case ')':
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '%':
                    case '<':
                    case '>':
                    case '=':
                    case '!':
                    case '&':
                    case '|':
                    case ',':
                    case '"':
                        return true;
                }
            }
            return false;
        }

        static ValueKind MapExprTypeToValueKind(ExprType type)
        {
            return type switch
            {
                ExprType.Bool => ValueKind.Bool,
                ExprType.Number => ValueKind.Float,
                ExprType.String => ValueKind.String,
                _ => ValueKind.Null,
            };
        }

        static bool TryParseNonNegativeInt(string value, bool hasValue, string label, out int parsed, out string error)
        {
            parsed = 0;
            error = null;

            if (!hasValue)
            {
                error = $"{label} expects a value";
                return false;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < 0)
            {
                error = $"{label} expects a non-negative integer";
                return false;
            }

            return true;
        }

        static bool TrySplitSegments(string content, out List<string> segments, out string error)
        {
            segments = new List<string>();
            error = null;

            if (content == null)
            {
                segments.Add(string.Empty);
                return true;
            }

            int start = 0;
            bool inQuote = false;

            for (int i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (c == '"' && !IsEscaped(content, i))
                    inQuote = !inQuote;

                if (!inQuote && c == '|')
                {
                    segments.Add(content.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (inQuote)
            {
                error = "Missing closing quote";
                return false;
            }

            segments.Add(content.Substring(start));
            return true;
        }

        static bool TrySplitNameValue(
            string segment,
            out string name,
            out string value,
            out bool hasValue,
            out string error)
        {
            name = null;
            value = null;
            hasValue = false;
            error = null;

            bool inQuote = false;
            for (int i = 0; i < segment.Length; i++)
            {
                var c = segment[i];
                if (c == '"' && !IsEscaped(segment, i))
                    inQuote = !inQuote;

                if (!inQuote && c == '=')
                {
                    name = segment.Substring(0, i).Trim();
                    value = segment.Substring(i + 1).Trim();
                    hasValue = true;

                    if (string.IsNullOrEmpty(name))
                    {
                        error = "Option name is empty";
                        return false;
                    }
                    return true;
                }
            }

            name = segment.Trim();
            if (string.IsNullOrEmpty(name))
            {
                error = "Option name is empty";
                return false;
            }

            return true;
        }

        static bool TryParseOptionValue(string raw, out string value, out string error)
        {
            value = string.Empty;
            error = null;

            if (raw == null)
                return true;

            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                value = string.Empty;
                return true;
            }

            if (trimmed[0] == '"')
            {
                if (trimmed.Length < 2 || trimmed[trimmed.Length - 1] != '"')
                {
                    error = "Missing closing quote";
                    return false;
                }

                value = UnescapeQuoted(trimmed.Substring(1, trimmed.Length - 2));
                return true;
            }

            value = trimmed;
            return true;
        }

        static string UnescapeQuoted(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    if (next == '"' || next == '\\')
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static bool IsValidColorCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 7 || value[0] != '#')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                var c = value[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        static bool IsEscaped(string text, int index)
        {
            int backslashCount = 0;
            for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
                backslashCount++;
            return (backslashCount % 2) == 1;
        }
    }
}
