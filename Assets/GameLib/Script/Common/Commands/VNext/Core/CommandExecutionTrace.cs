#nullable enable
using System;
using System.Threading;
using Game;
using UnityEngine;

namespace Game.Commands.VNext
{
    public readonly struct CommandExecutionTraceSnapshot
    {
        public readonly int CommandIndex;
        public readonly int CommandId;
        public readonly string SourceName;
        public readonly string CommandName;
        public readonly string DataType;
        public readonly string DebugData;
        public readonly string ListLabel;
        public readonly string ListFunctionName;
        public readonly IScopeNode? Scope;
        public readonly IScopeNode? Actor;
        public readonly IScopeNode? CommandRootScope;
        public readonly IScopeNode? RootActor;
        public readonly IScopeNode? CallerActor;

        public CommandExecutionTraceSnapshot(
            int commandIndex,
            int commandId,
            string sourceName,
            string commandName,
            string dataType,
            string debugData,
            string listLabel,
            string listFunctionName,
            IScopeNode? scope,
            IScopeNode? actor,
            IScopeNode? commandRootScope,
            IScopeNode? rootActor,
            IScopeNode? callerActor)
        {
            CommandIndex = commandIndex;
            CommandId = commandId;
            SourceName = sourceName ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DebugData = debugData ?? string.Empty;
            ListLabel = listLabel ?? string.Empty;
            ListFunctionName = listFunctionName ?? string.Empty;
            Scope = scope;
            Actor = actor;
            CommandRootScope = commandRootScope;
            RootActor = rootActor;
            CallerActor = callerActor;
        }
    }

    public static class CommandExecutionTrace
    {
        static readonly AsyncLocal<TraceEntry?> CurrentEntry = new();

        public static bool TryGetCurrent(out CommandExecutionTraceSnapshot snapshot)
        {
            var entry = CurrentEntry.Value;
            if (entry == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = entry.Snapshot;
            return true;
        }

        public static ScopeToken Push(CommandExecutionTraceSnapshot snapshot)
        {
            var entry = new TraceEntry(snapshot, CurrentEntry.Value);
            CurrentEntry.Value = entry;
            return new ScopeToken(entry);
        }

        public static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            if (scope is UnityEngine.Object unityObject && !unityObject)
                return "<destroyed>";

            if (scope is Component component && component.gameObject != null)
                return component.gameObject.name + " (" + component.GetType().Name + ")";

            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return id + " (" + scope.Kind + ")";

            return scope.GetType().Name;
        }

        internal sealed class TraceEntry
        {
            public readonly CommandExecutionTraceSnapshot Snapshot;
            public readonly TraceEntry? Previous;

            public TraceEntry(CommandExecutionTraceSnapshot snapshot, TraceEntry? previous)
            {
                Snapshot = snapshot;
                Previous = previous;
            }
        }

        public readonly struct ScopeToken : IDisposable
        {
            readonly TraceEntry? _entry;

            internal ScopeToken(TraceEntry? entry)
            {
                _entry = entry;
            }

            public void Dispose()
            {
                if (_entry == null)
                    return;

                var current = CurrentEntry.Value;
                if (ReferenceEquals(current, _entry))
                {
                    CurrentEntry.Value = _entry.Previous;
                    return;
                }

                CurrentEntry.Value = _entry.Previous;
            }
        }
    }
}
