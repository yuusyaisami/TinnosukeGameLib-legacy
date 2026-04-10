#nullable enable

namespace Game.Dialogue
{
    public enum DialogueAdvanceReason
    {
        None = 0,
        Input = 10,
        Auto = 20,
        TypewriterSkipped = 30,
        Canceled = 40,
    }

    public enum DialogueTypewriterState
    {
        Idle = 0,
        Playing = 10,
        Completed = 20,
    }

    public enum DialogueChoiceState
    {
        None = 0,
        Waiting = 10,
        Completed = 20,
    }

    public readonly struct DialogueAdvanceResult
    {
        public DialogueAdvanceReason Reason { get; }
        public string Message { get; }

        public bool IsAdvanced => Reason == DialogueAdvanceReason.Input || Reason == DialogueAdvanceReason.Auto;

        DialogueAdvanceResult(DialogueAdvanceReason reason, string message)
        {
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public static DialogueAdvanceResult FromInput()
            => new(DialogueAdvanceReason.Input, string.Empty);

        public static DialogueAdvanceResult FromAuto(string message = "")
            => new(DialogueAdvanceReason.Auto, message);

        public static DialogueAdvanceResult FromTypewriterSkipped()
            => new(DialogueAdvanceReason.TypewriterSkipped, string.Empty);

        public static DialogueAdvanceResult FromCanceled(string message)
            => new(DialogueAdvanceReason.Canceled, message);
    }

    public readonly struct DialogueMessageResult
    {
        public bool Success { get; }
        public DialogueAdvanceResult Advance { get; }
        public string Message { get; }

        DialogueMessageResult(bool success, DialogueAdvanceResult advance, string message)
        {
            Success = success;
            Advance = advance;
            Message = message ?? string.Empty;
        }

        public static DialogueMessageResult Completed(DialogueAdvanceResult advance)
            => new(true, advance, string.Empty);

        public static DialogueMessageResult Failed(string message)
            => new(false, DialogueAdvanceResult.FromCanceled(message), message);
    }

    public readonly struct DialogueChoiceResult
    {
        public bool Success { get; }
        public Game.Channel.GridObjectChoiceSessionResult SourceResult { get; }
        public string Message { get; }

        DialogueChoiceResult(bool success, Game.Channel.GridObjectChoiceSessionResult sourceResult, string message)
        {
            Success = success;
            SourceResult = sourceResult;
            Message = message ?? string.Empty;
        }

        public static DialogueChoiceResult Completed(Game.Channel.GridObjectChoiceSessionResult sourceResult)
            => new(sourceResult.IsSuccess, sourceResult, sourceResult.Message);

        public static DialogueChoiceResult Failed(string message)
            => new(false, Game.Channel.GridObjectChoiceSessionResult.Failed(message), message);
    }
}
