#nullable enable
using Game.Commands.VNext;
using Game.Common;

namespace Game.Channel
{
    internal static class TextChannelTextEvaluationUtility
    {
        public static string EvaluateRichTextTemplate(CommandContext context, string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (context == null)
                return text;

            if (text.IndexOf('{') < 0 || text.IndexOf('}') < 0)
                return text;

            var source = new RichTextSource
            {
                AllowImplicitKeys = true,
                Template = text,
            };

            var evaluated = source.Evaluate(context).AsString ?? string.Empty;
            return string.IsNullOrEmpty(evaluated) ? text : evaluated;
        }
    }
}
