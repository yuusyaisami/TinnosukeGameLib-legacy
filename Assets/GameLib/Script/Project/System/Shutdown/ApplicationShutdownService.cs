using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

namespace Game.Project
{
    public enum ShutdownReason
    {
        Unknown = 0,
        ApplicationQuitting = 1,
        ForcedTermination = 2,
        UnhandledException = 3,
        SteamUnavailable = 4,
        PreferredPlatformUnavailable = 5,
        Manual = 6,
        FreezeDetected = 7, // 将来のために残しておくが今は使わない
    }

    public interface IApplicationShutdownService : IDisposable
    {
        /// <summary>初回のシャットダウン要求が飛んだときに一度だけ発火。</summary>
        event Action<ShutdownReason> OnShutdownRequested;

        /// <summary>すでにシャットダウン要求が発生したか。</summary>
        bool HasShutdownBeenRequested { get; }

        /// <summary>最後に記録されたシャットダウン理由。</summary>
        ShutdownReason LastReason { get; }

        /// <summary>UnhandledException 由来の場合に保持される例外。ない場合は null。</summary>
        Exception LastException { get; }

        /// <summary>
        /// 明示的にシャットダウンを要求する。
        /// exitApplication == null の場合はオプション(MB)の設定に従う。
        /// </summary>
        void RequestShutdown(ShutdownReason reason = ShutdownReason.Unknown, bool? exitApplication = null);

        /// <summary>
        /// Report a per-frame heartbeat so the service can detect freezes.
        /// </summary>
        void ReportHeartbeat();
    }
    /// <summary>
    /// アプリのシャットダウンを一元管理するサービス。
    /// - 終了理由を ShutdownReason として集約
    /// - Application.quitting / ProcessExit / UnhandledException をフック
    /// - 手動の RequestShutdown も受け付ける
    /// </summary>
    public sealed class ApplicationShutdownService : IApplicationShutdownService
    {
        public event Action<ShutdownReason> OnShutdownRequested;

        public bool HasShutdownBeenRequested { get; private set; }
        public ShutdownReason LastReason { get; private set; } = ShutdownReason.Unknown;
        public Exception LastException { get; private set; }

        readonly IApplicationShutdownOptions _options;
        readonly object _lock = new object();
        bool _disposed;

        // Cancellation token source used by monitoring loop (kept but not used when disabled)
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private float _lastHeartbeatRealtime;


        public ApplicationShutdownService(IApplicationShutdownOptions options)
        {
            _options = options;

            // イベント登録
            if (_options.ListenToApplicationQuitting)
            {
                Application.quitting += OnApplicationQuitting;
            }

            if (_options.ListenToProcessExit)
            {
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            }

            if (_options.ListenToUnhandledException)
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            }

#if UNITY_EDITOR
            if (_options.ListenToEditorQuitting)
            {
                UnityEditor.EditorApplication.quitting += OnEditorQuitting;
            }
#endif

            // Initialize heartbeat timestamp. Freeze detection is intentionally disabled for now.
            _lastHeartbeatRealtime = Time.realtimeSinceStartup;
        }

        public void RequestShutdown(ShutdownReason reason = ShutdownReason.Unknown, bool? exitApplication = null)
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                if (HasShutdownBeenRequested)
                    return;

                HasShutdownBeenRequested = true;
                LastReason = reason;

                if (_options.LogShutdownRequests)
                {
                    Debug.Log($"[ApplicationShutdown] Shutdown requested: {reason}");
                }

                try
                {
                    OnShutdownRequested?.Invoke(reason);
                }
                catch (Exception ex)
                {
                    // シャットダウン中にハンドラで例外が出ても、ここで握りつぶす
                    Debug.LogException(ex);
                }

                var shouldExit = exitApplication ?? _options.ExitApplicationOnShutdown;
                if (!shouldExit)
                    return;

#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    UnityEditor.EditorApplication.ExitPlaymode();
                }
#else
                Application.Quit();
#endif
            }
        }

        // ---- イベントハンドラ ----

        void OnApplicationQuitting()
        {
            // 既に Unity が終了処理に入っているので exitApplication: false
            RequestShutdown(ShutdownReason.ApplicationQuitting, exitApplication: false);
        }

        void OnProcessExit(object sender, EventArgs args)
        {
            // プロセス強制終了側なのでここで Quit はしない
            RequestShutdown(ShutdownReason.ForcedTermination, exitApplication: false);
        }

#if UNITY_EDITOR
        void OnEditorQuitting()
        {
            RequestShutdown(ShutdownReason.ApplicationQuitting, exitApplication: false);
        }
#endif

        void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception ex)
            {
                LastException = ex;
                if (_options.LogShutdownRequests)
                {
                    Debug.LogException(ex);
                }
            }

            // ここは状況により別スレッドの可能性があることは理解しておくこと。
            // 「とりあえず理由を記録して終了フラグを立てる」役割に限定している。
            RequestShutdown(ShutdownReason.UnhandledException);
        }

        /// <summary>
        /// Called by MB every frame to mark the service as still alive. NB: this avoids coroutines/Update in the service.
        /// </summary>
        public void ReportHeartbeat()
        {
            // No-op: freeze detection removed
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;

                if (_options.ListenToApplicationQuitting)
                {
                    Application.quitting -= OnApplicationQuitting;
                }

                if (_options.ListenToProcessExit)
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                }

                if (_options.ListenToUnhandledException)
                {
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                }

#if UNITY_EDITOR
                if (_options.ListenToEditorQuitting)
                {
                    UnityEditor.EditorApplication.quitting -= OnEditorQuitting;
                }
#endif

                // Cancel monitor
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                _cts.Dispose();
            }
        }
    }
}
