#nullable enable
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Game.BuildConsole
{
    public enum BuildConsoleEntrySource
    {
        BuildConsole = 10,
        UnityLog = 20,
    }

    public sealed class BuildConsoleOptions
    {
        public bool EnableInEditor { get; set; } = true;
        public bool CaptureUnityLogs { get; set; } = true;
        public bool CaptureStackTrace { get; set; } = true;
        public bool VisibleOnStart { get; set; }
        public int MaxEntries { get; set; } = 512;
        public int PreviewCharacterLimit { get; set; } = 220;
        public float WindowWidth { get; set; } = 1040f;
        public float WindowHeight { get; set; } = 680f;
        public float Margin { get; set; } = 16f;
        public int HeaderFontSize { get; set; } = 14;
        public int RowFontSize { get; set; } = 12;
        public int DetailFontSize { get; set; } = 11;
    }

    public sealed class BuildConsoleEntry
    {
        public BuildConsoleEntry(
            int sequence,
            float realtimeSeconds,
            string preview,
            string message,
            string stackTrace,
            LogType logType,
            BuildConsoleEntrySource source,
            LifetimeScopeKind scopeKind,
            string scopeId,
            bool hasScope)
        {
            Sequence = sequence;
            RealtimeSeconds = realtimeSeconds;
            Preview = preview ?? string.Empty;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            LogType = logType;
            Source = source;
            ScopeKind = scopeKind;
            ScopeId = scopeId ?? string.Empty;
            HasScope = hasScope;
        }

        public int Sequence { get; }
        public float RealtimeSeconds { get; }
        public string Preview { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogType LogType { get; }
        public BuildConsoleEntrySource Source { get; }
        public LifetimeScopeKind ScopeKind { get; }
        public string ScopeId { get; }
        public bool HasScope { get; }
        public string SourceLabel => Source switch
        {
            BuildConsoleEntrySource.BuildConsole => "BuildConsole",
            BuildConsoleEntrySource.UnityLog => "UnityLog",
            _ => "Unknown",
        };
        public string ScopePrefix => HasScope ? $"[{ScopeKind}:{ScopeId}]" : string.Empty;
    }

    public interface IBuildConsole
    {
        bool IsVisible { get; set; }
        BuildConsoleOptions Options { get; }
        IReadOnlyList<BuildConsoleEntry> Entries { get; }

        void Clear();
        void Log(string message, LogType logType = LogType.Log, string? stackTrace = null);
        void LogScope(IScopeNode? scope, string message, LogType logType = LogType.Log, string? stackTrace = null);
        void LogResolver(IRuntimeResolver? resolver, string message, LogType logType = LogType.Log, string? stackTrace = null);
    }
}
