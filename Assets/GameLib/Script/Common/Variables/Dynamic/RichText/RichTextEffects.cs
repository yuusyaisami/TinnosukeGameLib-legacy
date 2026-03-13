namespace Game.Common
{
    public enum RichTextEffectKind
    {
        None,
        ColorIf,
    }

    public struct RichTextDecoratorOptions
    {
        public string Wrap;
        public string WrapEnd;
        public string Color;
        public RichTextEffectKind EffectKind;
        public string TrueColor;
        public string FalseColor;
    }

    public static class RichTextEffects
    {
        public static bool TryApply(string text, in RichTextDecoratorOptions options, bool? condResult, out string result)
        {
            result = text ?? string.Empty;

            if (!string.IsNullOrEmpty(options.Color))
                result = WrapColor(result, options.Color);

            if (options.EffectKind == RichTextEffectKind.ColorIf)
            {
                if (!condResult.HasValue)
                    return false;

                var color = condResult.Value ? options.TrueColor : options.FalseColor;
                if (!string.IsNullOrEmpty(color))
                    result = WrapColor(result, color);
            }

            if (!string.IsNullOrEmpty(options.Wrap))
                result = options.Wrap + result + (options.WrapEnd ?? string.Empty);

            return true;
        }

        static string WrapColor(string text, string color)
        {
            return string.IsNullOrEmpty(color) ? text : $"<color={color}>{text}</color>";
        }
    }
}
