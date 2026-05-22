// Assets/Game/Script/Systems/ApplicationShutdown/ApplicationShutdownMB.cs
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Project
{
        /// <summary>
        /// ApplicationShutdownService 縺ｮ險ｭ螳壹・逋ｻ骭ｲ繧定｡後≧霆ｽ驥・MB縲・
        /// 螳滄圀縺ｮ繝ｭ繧ｸ繝・け縺ｯ Service 蛛ｴ縺ｫ髮・ｴ・☆繧九・
        /// </summary>
        public sealed class ApplicationShutdownMB : MonoBehaviour, IApplicationShutdownOptions
        {
                [Header("Automatic Triggers")]
                [Tooltip("Inspector setting.")]
                [SerializeField] bool listenToApplicationQuitting = true;

                [Tooltip("Inspector setting.")]
                [SerializeField] bool listenToProcessExit = true;

                [Tooltip("Inspector setting.")]
                [SerializeField] bool listenToUnhandledException = true;

#if UNITY_EDITOR
                [Tooltip("Inspector setting.")]
                [SerializeField] bool listenToEditorQuitting = true;
#endif

                [Header("Behavior")]
                [Tooltip("Inspector setting.")]
                [SerializeField] bool exitApplicationOnShutdown = true;

                [Tooltip("Inspector setting.")]
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
                public void InstallApplicationShutdownRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
                {
                        _ = owner ?? throw new System.ArgumentNullException(nameof(owner));

                        // 縺薙・ MB 閾ｪ霄ｫ繧偵が繝励す繝ｧ繝ｳ縺ｨ縺励※繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ
                        builder.RegisterInstance(this).As<IApplicationShutdownOptions>();

                        // Service 譛ｬ菴薙ｒ Singleton 縺ｨ縺励※逋ｻ骭ｲ
                        builder.Register<IApplicationShutdownService, ApplicationShutdownService>(RuntimeLifetime.Singleton);
                }

                void OnDestroy()
                {
                        // 繧ｳ繝ｳ繝・リ縺ｮ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ縺ｨ莠碁㍾縺ｫ縺ｪ縺｣縺ｦ繧ょｮｳ縺ｯ縺ｪ縺・ｈ縺・ｮ溯｣・＠縺ｦ縺ゅｋ縺後・
                        // 豌励↓縺ｪ繧九↑繧峨％縺薙・豸医＠縺ｦ繧ｳ繝ｳ繝・リ蛛ｴ縺ｮ Dispose 縺ｫ莉ｻ縺帙※繧ゅ＞縺・・
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
