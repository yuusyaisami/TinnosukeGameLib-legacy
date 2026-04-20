#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using VContainer;
using VContainer.Unity;

namespace Game.BuildConsole
{
    public sealed class BuildConsoleService : IBuildConsole, IScopeAcquireHandler, IScopeReleaseHandler, IScopeTickHandler, IDisposable
    {
        readonly BuildConsoleOptions _options;
        readonly List<BuildConsoleEntry> _entries;

        bool _disposed;
        bool _unityLogSubscribed;
        int _nextSequence = 1;

        public BuildConsoleService(BuildConsoleOptions options)
        {
            _options = options ?? new BuildConsoleOptions();
            _entries = new List<BuildConsoleEntry>(Mathf.Max(32, _options.MaxEntries));
            IsVisible = _options.VisibleOnStart;
        }

        public bool IsVisible { get; set; }
        public BuildConsoleOptions Options => _options;
        public IReadOnlyList<BuildConsoleEntry> Entries => _entries;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            if (_disposed)
            {
                _disposed = false;
            }

            BuildConsoleLog.Bind(this);
            if (ShouldRunInCurrentEnvironment() && _options.CaptureUnityLogs)
            {
                SubscribeUnityLogs();
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            UnsubscribeUnityLogs();
            BuildConsoleLog.Unbind(this);
        }

        public void Tick()
        {
            if (_disposed || !ShouldRunInCurrentEnvironment())
            {
                return;
            }

            if (IsTogglePressed())
            {
                IsVisible = !IsVisible;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnsubscribeUnityLogs();
            BuildConsoleLog.Unbind(this);
            _entries.Clear();
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public void Log(string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            AppendEntry(scope: null, message, logType, stackTrace, BuildConsoleEntrySource.BuildConsole);
        }

        public void LogScope(IScopeNode? scope, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            AppendEntry(scope, message, logType, stackTrace, BuildConsoleEntrySource.BuildConsole);
        }

        public void LogResolver(IRuntimeResolver? resolver, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            if (resolver != null &&
                resolver.TryResolve<IScopeNode>(out var scope) &&
                scope != null)
            {
                AppendEntry(scope, message, logType, stackTrace, BuildConsoleEntrySource.BuildConsole);
                return;
            }

            AppendEntry(scope: null, message, logType, stackTrace, BuildConsoleEntrySource.BuildConsole);
        }

        void SubscribeUnityLogs()
        {
            if (_unityLogSubscribed)
            {
                return;
            }

            Application.logMessageReceived += HandleUnityLog;
            _unityLogSubscribed = true;
        }

        void UnsubscribeUnityLogs()
        {
            if (!_unityLogSubscribed)
            {
                return;
            }

            Application.logMessageReceived -= HandleUnityLog;
            _unityLogSubscribed = false;
        }

        void HandleUnityLog(string condition, string stackTrace, LogType logType)
        {
            if (_disposed || !_options.CaptureUnityLogs)
            {
                return;
            }

            AppendEntry(scope: null, condition, logType, stackTrace, BuildConsoleEntrySource.UnityLog);
        }

        void AppendEntry(IScopeNode? scope, string message, LogType logType, string? stackTrace, BuildConsoleEntrySource source)
        {
            if (_disposed || !ShouldRunInCurrentEnvironment())
            {
                return;
            }

            var normalizedMessage = Normalize(message);
            var normalizedStack = _options.CaptureStackTrace ? Normalize(stackTrace) : string.Empty;
            var preview = BuildPreview(normalizedMessage, _options.PreviewCharacterLimit);

            var scopeKind = LifetimeScopeKind.None;
            var scopeId = string.Empty;
            var hasScope = false;
            var identity = scope?.Identity;
            if (identity != null)
            {
                scopeKind = identity.Kind;
                scopeId = string.IsNullOrEmpty(identity.Id) ? "(no-id)" : identity.Id;
                hasScope = true;
            }
            else if (scope != null)
            {
                scopeKind = scope.Kind;
                scopeId = "(no-identity)";
                hasScope = true;
            }

            _entries.Add(new BuildConsoleEntry(
                sequence: _nextSequence++,
                realtimeSeconds: Time.realtimeSinceStartup,
                preview: preview,
                message: normalizedMessage,
                stackTrace: normalizedStack,
                logType: logType,
                source: source,
                scopeKind: scopeKind,
                scopeId: scopeId,
                hasScope: hasScope));

            var overflow = _entries.Count - Mathf.Max(16, _options.MaxEntries);
            if (overflow > 0)
            {
                _entries.RemoveRange(0, overflow);
            }
        }

        bool ShouldRunInCurrentEnvironment()
        {
            return !Application.isEditor || _options.EnableInEditor;
        }

        static string Normalize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static string BuildPreview(string message, int previewCharacterLimit)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "(empty)";
            }

            var newlineIndex = message.IndexOf('\n');
            var firstLine = newlineIndex >= 0 ? message[..newlineIndex] : message;
            if (firstLine.Length <= previewCharacterLimit)
            {
                return firstLine;
            }

            return firstLine[..previewCharacterLimit] + "...";
        }

        static bool IsTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F2))
            {
                return true;
            }
#endif
            return false;
        }
    }
}
