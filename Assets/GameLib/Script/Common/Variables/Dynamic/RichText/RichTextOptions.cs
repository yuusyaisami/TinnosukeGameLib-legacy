using System;
using System.Globalization;

namespace Game.Common
{
    public struct RichTextFormatOptions
    {
        public bool HasEmptyFallback;
        public string EmptyFallback;

        public bool UsePercent;
        public int? PercentDigits;
        public int? RoundDigits;
        public int? FixedDigits;
        public bool SignAlways;
        public string Prefix;
        public string Suffix;
    }

    public static class RichTextOptions
    {
        static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public static string GetEmptyFallback(in RichTextFormatOptions options)
        {
            if (!options.HasEmptyFallback)
                return string.Empty;
            return options.EmptyFallback ?? string.Empty;
        }

        public static bool TryFormat(in DynamicVariant value, in RichTextFormatOptions options, out string text)
        {
            text = string.Empty;

            if (value.Kind == ValueKind.Null)
            {
                text = GetEmptyFallback(options);
                return true;
            }

            if (HasNumericOptions(options))
            {
                if (!TryGetNumber(value, out var number))
                    return false;

                if (options.UsePercent)
                    number *= 100.0;

                var roundDigits = options.RoundDigits;
                var fixedDigits = options.FixedDigits;
                if (options.UsePercent && options.PercentDigits.HasValue && !roundDigits.HasValue && !fixedDigits.HasValue)
                    fixedDigits = options.PercentDigits;

                if (roundDigits.HasValue)
                    number = Math.Round(number, roundDigits.Value, MidpointRounding.AwayFromZero);
                else if (!fixedDigits.HasValue)
                    number = Math.Round(number, 6, MidpointRounding.AwayFromZero);

                string numberText = fixedDigits.HasValue
                    ? number.ToString("F" + fixedDigits.Value, InvariantCulture)
                    : number.ToString(BuildNumberFormat(roundDigits ?? 6), InvariantCulture);

                if (options.SignAlways && number >= 0)
                    numberText = "+" + numberText;

                if (options.UsePercent)
                    numberText += "%";

                text = ApplyAffixes(numberText, options);
                return true;
            }

            text = value.Kind == ValueKind.String ? value.AsString : value.ToString();
            text = ApplyAffixes(text, options);
            return true;
        }

        static string BuildNumberFormat(int digits)
        {
            if (digits <= 0)
                return "0";

            return "0." + new string('#', digits);
        }

        static bool HasNumericOptions(in RichTextFormatOptions options)
        {
            return options.UsePercent || options.RoundDigits.HasValue || options.FixedDigits.HasValue || options.SignAlways;
        }

        static bool TryGetNumber(in DynamicVariant value, out double number)
        {
            switch (value.Kind)
            {
                case ValueKind.Bool:
                    number = value.AsBool ? 1.0 : 0.0;
                    return true;
                case ValueKind.Int:
                    number = value.AsInt;
                    return true;
                case ValueKind.Float:
                    number = value.AsFloat;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        static string ApplyAffixes(string text, in RichTextFormatOptions options)
        {
            if (!string.IsNullOrEmpty(options.Prefix))
                text = options.Prefix + text;
            if (!string.IsNullOrEmpty(options.Suffix))
                text += options.Suffix;
            return text;
        }
    }
}
