#nullable enable
using UnityEngine;
using System;

namespace Game.Commands.VNext
{
    public sealed class UnityCommandResolveLogger : ICommandResolveLogger
    {
        public void LogResolveFailed(ICommandSource source, string message)
        {
            var label = source != null ? source.ToString() : "null";
            Debug.LogError(Format(
                "#FFA500",
                "Resolve failed",
                $"Source={label} {message}"));
        }

        public void LogExecutorMissing(int commandId, string message)
        {
            Debug.LogError(Format(
                "#FF66CC",
                "Executor missing",
                $"Cmd=<unknown>(Id={commandId}) {message}"));
        }

        public void LogPayloadInvalid(int commandId, string message)
        {
            Debug.LogError(Format(
                "#FF9966",
                "Payload invalid",
                $"Cmd=<unknown>(Id={commandId}) {message}"));
        }

        public void LogExecutionFailed(int commandId, string message)
        {
            Debug.LogError(Format(
                "#FF5555",
                "Execution failed",
                message));
        }

        public void LogExecutionCanceled(int commandId, string message)
        {
            Debug.LogWarning(Format(
                "#FFFF55",
                "Execution canceled",
                message));
        }


        static string Format(string accentColor, string title, string message)
        {
            if (string.IsNullOrEmpty(message))
                message = string.Empty;

            // ConsoleがRichText表示のときに重要な情報が一目で分かるように。
            var normalized = message.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized.Split('\n');

            if (lines.Length == 0)
                return $"<color={accentColor}>[CommandResolve]</color> <b>{title}</b>";

            var header = HighlightLine(lines[0], accentColor);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color={accentColor}>[CommandResolve]</color> <b>{title}</b> ").Append(header);

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;
                var bodyLine = HighlightLine(lines[i], accentColor);
                sb.Append("\n<color=#888888>|</color> ").Append(bodyLine);
            }

            return sb.ToString();
        }

        static string Highlight(string text, string token, string color)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return text;

            // 置換を複雑にしない（ログは壊さない方針）。
            return text.Replace(token, $"<color={color}><b>{token}</b></color>");
        }

        static string HighlightLine(string text, string accentColor)
        {
            var line = text ?? string.Empty;
            line = Highlight(line, "Actor=", "#66CCFF");
            line = Highlight(line, "Scope=", "#66CCFF");
            line = Highlight(line, "CommandRoot=", "#66CCFF");
            line = Highlight(line, "RootActor=", "#66CCFF");
            line = Highlight(line, "CallerActor=", "#66CCFF");
            line = Highlight(line, "Cmd=", "#66CCFF");
            line = Highlight(line, "CmdData=", "#66CCFF");
            line = Highlight(line, "Source=", "#AAAAAA");
            line = Highlight(line, "Resolved=", "#AAAAAA");
            line = Highlight(line, "ContextSlots:", "#66CC66");
            line = Highlight(line, "ContextA=", "#66CC66");
            line = Highlight(line, "ContextB=", "#66CC66");
            line = Highlight(line, "ContextC=", "#66CC66");
            line = Highlight(line, "ContextD=", "#66CC66");
            line = Highlight(line, "ReadScope=", "#66CC66");
            line = Highlight(line, "Target=", "#66CC66");
            line = Highlight(line, "Exception:", accentColor);
            line = Highlight(line, "Detail:", "#66CC66");
            line = Highlight(line, "ExceptionType:", accentColor);
            line = Highlight(line, "ExceptionMessage:", accentColor);
            line = Highlight(line, "ExceptionStack:", accentColor);
            line = Highlight(line, "Trace:", "#66CC66");
            line = Highlight(line, "FN=", "#66CCFF");
            return line;
        }
    }
}
