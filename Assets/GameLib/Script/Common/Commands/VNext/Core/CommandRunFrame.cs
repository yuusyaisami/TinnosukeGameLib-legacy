#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Game;

namespace Game.Commands.VNext
{
    public enum CommandExecutionDomain
    {
        Kernel = 10,
        Project = 20,
        Platform = 25,
        Global = 30,
        Scene = 40,
        Field = 50,
        Entity = 60,
        UI = 70,
        Test = 90,
    }

    public enum CommandFailureBoundary
    {
        FailCommand = 10,
        FailFrame = 20,
        FailSequence = 30,
        FailRunner = 40,
        FailScope = 50,
    }

    public enum CommandLocalScope
    {
        Frame = 10,
        Sequence = 20,
        AsyncWaitBoundary = 30,
        NestedBlock = 40,
        Detached = 50,
    }

    public enum CommandDetachedCancellationMode
    {
        FollowCaller = 10,
        DetachFromCaller = 20,
    }

    public readonly struct CommandFrameId : IEquatable<CommandFrameId>
    {
        public readonly int Value;

        public CommandFrameId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value > 0;
        public bool Equals(CommandFrameId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CommandFrameId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public readonly struct CommandLocalKey : IEquatable<CommandLocalKey>
    {
        public readonly int Value;
        public readonly string DebugName;

        public CommandLocalKey(int value, string debugName = "")
        {
            Value = value;
            DebugName = debugName ?? string.Empty;
        }

        public bool IsValid => Value > 0;
        public bool Equals(CommandLocalKey other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CommandLocalKey other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => string.IsNullOrEmpty(DebugName) ? Value.ToString() : $"{DebugName}({Value})";
    }

    public sealed class CommandLocal : IDisposable
    {
        readonly object _gate = new();
        readonly Dictionary<CommandLocalKey, object> _values = new();
        readonly List<string> _channelExecutionStack = new(4);
        readonly CancellationTokenSource? _cancellationSource;
        bool _breakRequested;
        bool _cancelRequested;
        bool _disposed;

        public CommandLocal(CommandLocalScope scope, CommandFrameId ownerFrameId, CommandLocal? parent = null, CancellationTokenSource? cancellationSource = null)
        {
            Scope = scope;
            OwnerFrameId = ownerFrameId;
            Parent = parent;
            _cancellationSource = cancellationSource;
        }

        public CommandLocalScope Scope { get; }
        public CommandFrameId OwnerFrameId { get; }
        public CommandLocal? Parent { get; }
        public int ValueCount
        {
            get
            {
                lock (_gate)
                    return _values.Count;
            }
        }

        public bool TryGet<T>(CommandLocalKey key, out T value)
        {
            if (!key.IsValid)
            {
                value = default!;
                return false;
            }

            lock (_gate)
            {
                if (_values.TryGetValue(key, out var stored) && stored is T typed)
                {
                    value = typed;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public bool Set<T>(CommandLocalKey key, T value)
        {
            if (!key.IsValid)
                return false;

            lock (_gate)
            {
                _values[key] = value!;
                return true;
            }
        }

        public bool Remove(CommandLocalKey key)
        {
            if (!key.IsValid)
                return false;

            lock (_gate)
                return _values.Remove(key);
        }

        public void RequestBreak()
        {
            lock (_gate)
                _breakRequested = true;
        }

        public void RequestCancel()
        {
            CancellationTokenSource? cancellationSource;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _cancelRequested = true;
                cancellationSource = _cancellationSource;
            }

            if (cancellationSource == null)
                return;

            try
            {
                cancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            CancellationTokenSource? cancellationSource;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                cancellationSource = _cancellationSource;
            }

            cancellationSource?.Dispose();
        }

        public bool TryConsumeBreakRequest()
        {
            lock (_gate)
            {
                if (!_breakRequested)
                    return false;

                _breakRequested = false;
                return true;
            }
        }

        public bool TryConsumeCancelRequest()
        {
            lock (_gate)
            {
                if (!_cancelRequested)
                    return false;

                _cancelRequested = false;
                return true;
            }
        }

        public bool TryEnterChannelExecution(string key, out string chain)
        {
            chain = "<empty>";
            if (string.IsNullOrEmpty(key))
                return false;

            if (Parent != null && Parent.ContainsChannelExecution(key, out chain))
                return false;

            lock (_gate)
            {
                if (_channelExecutionStack.Contains(key))
                {
                    chain = _channelExecutionStack.Count > 0
                        ? string.Join(" -> ", _channelExecutionStack)
                        : "<empty>";
                    return false;
                }

                _channelExecutionStack.Add(key);
                chain = string.Join(" -> ", _channelExecutionStack);
                return true;
            }
        }

        bool ContainsChannelExecution(string key, out string chain)
        {
            chain = "<empty>";
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_gate)
            {
                if (_channelExecutionStack.Contains(key))
                {
                    chain = _channelExecutionStack.Count > 0
                        ? string.Join(" -> ", _channelExecutionStack)
                        : "<empty>";
                    return true;
                }
            }

            if (Parent != null)
                return Parent.ContainsChannelExecution(key, out chain);

            return false;
        }

        public void ExitChannelExecution(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_gate)
            {
                for (var i = _channelExecutionStack.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(_channelExecutionStack[i], key, StringComparison.Ordinal))
                        continue;

                    _channelExecutionStack.RemoveAt(i);
                    break;
                }
            }
        }
    }

    public readonly struct CommandDetachedExecutionPolicy
    {
        public readonly bool IsAllowed;
        public readonly CommandFrameId OwnerFrameId;
        public readonly IScopeNode? OwnerScope;
        public readonly CommandDetachedCancellationMode CancellationMode;
        public readonly string DiagnosticDestination;
        public readonly string DebugName;

        public CommandDetachedExecutionPolicy(
            bool isAllowed,
            CommandFrameId ownerFrameId,
            IScopeNode? ownerScope,
            CommandDetachedCancellationMode cancellationMode,
            string diagnosticDestination,
            string debugName)
        {
            IsAllowed = isAllowed;
            OwnerFrameId = ownerFrameId;
            OwnerScope = ownerScope;
            CancellationMode = cancellationMode;
            DiagnosticDestination = diagnosticDestination ?? string.Empty;
            DebugName = debugName ?? string.Empty;
        }

        public static CommandDetachedExecutionPolicy Forbidden => default;
    }

    public sealed class CommandFrame
    {
        public CommandFrame(
            CommandFrameId id,
            CommandFrameId parentId,
            CommandExecutionDomain domain,
            int commandIndex,
            int commandId,
            int payloadSchemaId,
            string sourceType,
            string dataType,
            string debugData,
            IScopeNode? scope,
            IScopeNode? actor,
            IScopeNode? commandRootScope,
            IScopeNode? rootActor,
            IScopeNode? callerActor,
            CancellationToken cancellationToken,
            CommandLocal local)
        {
            Id = id;
            ParentId = parentId;
            Domain = domain;
            CommandIndex = commandIndex;
            CommandId = commandId;
            PayloadSchemaId = payloadSchemaId;
            SourceType = sourceType ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DebugData = debugData ?? string.Empty;
            Scope = scope;
            Actor = actor;
            CommandRootScope = commandRootScope;
            RootActor = rootActor;
            CallerActor = callerActor;
            CancellationToken = cancellationToken;
            Local = local ?? throw new ArgumentNullException(nameof(local));
        }

        public CommandFrameId Id { get; }
        public CommandFrameId ParentId { get; }
        public CommandExecutionDomain Domain { get; }
        public int CommandIndex { get; }
        public int CommandId { get; }
        public int PayloadSchemaId { get; }
        public string SourceType { get; }
        public string DataType { get; }
        public string DebugData { get; }
        public IScopeNode? Scope { get; }
        public IScopeNode? Actor { get; }
        public IScopeNode? CommandRootScope { get; }
        public IScopeNode? RootActor { get; }
        public IScopeNode? CallerActor { get; }
        public CancellationToken CancellationToken { get; }
        public CommandLocal Local { get; }

        public CommandFrameSnapshot ToSnapshot(CommandFailureBoundary failureBoundary, bool isDetached, bool isTimedOut)
        {
            return new CommandFrameSnapshot(
                Id,
                ParentId,
                Domain,
                CommandIndex,
                CommandId,
                PayloadSchemaId,
                SourceType,
                DataType,
                DebugData,
                failureBoundary,
                Local.Scope,
                isDetached,
                isTimedOut);
        }
    }

    public readonly struct CommandFrameSnapshot
    {
        public readonly CommandFrameId FrameId;
        public readonly CommandFrameId ParentFrameId;
        public readonly CommandExecutionDomain Domain;
        public readonly int CommandIndex;
        public readonly int CommandId;
        public readonly int PayloadSchemaId;
        public readonly string SourceType;
        public readonly string DataType;
        public readonly string DebugData;
        public readonly CommandFailureBoundary FailureBoundary;
        public readonly CommandLocalScope LocalScope;
        public readonly bool IsDetached;
        public readonly bool IsTimedOut;

        public CommandFrameSnapshot(
            CommandFrameId frameId,
            CommandFrameId parentFrameId,
            CommandExecutionDomain domain,
            int commandIndex,
            int commandId,
            int payloadSchemaId,
            string sourceType,
            string dataType,
            string debugData,
            CommandFailureBoundary failureBoundary,
            CommandLocalScope localScope,
            bool isDetached,
            bool isTimedOut)
        {
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            Domain = domain;
            CommandIndex = commandIndex;
            CommandId = commandId;
            PayloadSchemaId = payloadSchemaId;
            SourceType = sourceType ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DebugData = debugData ?? string.Empty;
            FailureBoundary = failureBoundary;
            LocalScope = localScope;
            IsDetached = isDetached;
            IsTimedOut = isTimedOut;
        }

        public static CommandFrameSnapshot Empty => default;
    }

    public readonly struct CommandRunFrame
    {
        public readonly int CommandIndex;
        public readonly int CommandId;
        public readonly string SourceType;
        public readonly string DataType;
        public readonly string DebugData;
        public readonly CommandFrameId FrameId;
        public readonly CommandFrameId ParentFrameId;
        public readonly CommandExecutionDomain Domain;
        public readonly CommandFailureBoundary FailureBoundary;
        public readonly CommandLocalScope LocalScope;

        public CommandRunFrame(int commandIndex, int commandId, string sourceType, string dataType, string debugData = "")
            : this(commandIndex, commandId, sourceType, dataType, debugData, default, default, CommandExecutionDomain.Project, CommandFailureBoundary.FailFrame, CommandLocalScope.Frame)
        {
        }

        public CommandRunFrame(CommandFrameSnapshot snapshot)
            : this(
                snapshot.CommandIndex,
                snapshot.CommandId,
                snapshot.SourceType,
                snapshot.DataType,
                snapshot.DebugData,
                snapshot.FrameId,
                snapshot.ParentFrameId,
                snapshot.Domain,
                snapshot.FailureBoundary,
                snapshot.LocalScope)
        {
        }

        public CommandRunFrame(
            int commandIndex,
            int commandId,
            string sourceType,
            string dataType,
            string debugData,
            CommandFrameId frameId,
            CommandFrameId parentFrameId,
            CommandExecutionDomain domain,
            CommandFailureBoundary failureBoundary,
            CommandLocalScope localScope)
        {
            CommandIndex = commandIndex;
            CommandId = commandId;
            SourceType = sourceType ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DebugData = debugData ?? string.Empty;
            FrameId = frameId;
            ParentFrameId = parentFrameId;
            Domain = domain;
            FailureBoundary = failureBoundary;
            LocalScope = localScope;
        }
    }
}
