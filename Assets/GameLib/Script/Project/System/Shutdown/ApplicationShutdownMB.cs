// Assets/Game/Script/Systems/ApplicationShutdown/ApplicationShutdownMB.cs
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Project
{
        /// <summary>
        /// ApplicationShutdownService の設定・登録を行う軽量 MB。
        /// 実際のロジックは Service 側に集約する。
        /// </summary>
        public sealed class ApplicationShutdownMB : MonoBehaviour, IFeatureInstaller, IApplicationShutdownOptions
        {
                [Header("Automatic Triggers")]
                [Tooltip("Application.quitting をフックして ShutdownReason.ApplicationQuitting を発行するか。")]
                [SerializeField] bool listenToApplicationQuitting = true;

                [Tooltip("AppDomain.ProcessExit をフックして ShutdownReason.ForcedTermination を発行するか。")]
                [SerializeField] bool listenToProcessExit = true;

                [Tooltip("UnhandledException をフックして ShutdownReason.UnhandledException を発行するか。")]
                [SerializeField] bool listenToUnhandledException = true;

#if UNITY_EDITOR
                [Tooltip("Editor の終了(エディタ自体の終了)をフックするか。")]
                [SerializeField] bool listenToEditorQuitting = true;
#endif

                [Header("Behavior")]
                [Tooltip("Shutdown がリクエストされたときに Application.Quit / ExitPlaymode を呼ぶか。")]
                [SerializeField] bool exitApplicationOnShutdown = true;

                [Tooltip("シャットダウン要求が発生したときにログを出すか。")]
                [SerializeField] bool logShutdownRequests = true;

                // New properties required by ApplicationShutdownService
                public bool ListenToApplicationQuitting => listenToApplicationQuitting;
                public bool ListenToProcessExit => listenToProcessExit;
                public bool ListenToUnhandledException => listenToUnhandledException;
#if UNITY_EDITOR
                public bool ListenToEditorQuitting => listenToEditorQuitting;
#endif
                public bool ExitApplicationOnShutdown => exitApplicationOnShutdown;
                public bool LogShutdownRequests => logShutdownRequests;

                [Inject]
                IApplicationShutdownService _shutdownService = null;


                // あなたの IFeatureInstaller が (builder, owner) シグネチャだったのでそれに合わせている
                public void InstallFeature(IContainerBuilder builder, IScopeNode lts)
                {
                        // この MB 自身をオプションとしてコンテナに登録
                        builder.RegisterInstance(this).As<IApplicationShutdownOptions>();

                        // Service 本体を Singleton として登録
                        builder.Register<IApplicationShutdownService, ApplicationShutdownService>(Lifetime.Singleton);
                }

                void OnDestroy()
                {
                        // コンテナのライフサイクルと二重になっても害はないよう実装してあるが、
                        // 気になるならここは消してコンテナ側の Dispose に任せてもいい。
                        _shutdownService?.Dispose();
                }
        }

        public interface IApplicationShutdownOptions
        {
                public bool ListenToApplicationQuitting { get; }
                public bool ListenToProcessExit { get; }
                public bool ListenToUnhandledException { get; }
#if UNITY_EDITOR
                public bool ListenToEditorQuitting { get; }
#endif

                public bool ExitApplicationOnShutdown { get; }
                public bool LogShutdownRequests { get; }
        }
}
