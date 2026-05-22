// スチ�EムのプラチE��フォーム固有�E実裁E
using UnityEngine;
using Game.Platform;
using VContainer;
using System;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Game.Platform
{
    /// <summary>
    /// Shared configuration for how the Steam platform behaves during startup failures.
    /// </summary>
    public interface IPlatformOptions
    {
        /// <summary>
        /// 開発老E��持E��した�EラチE��フォームでの起動に失敗した場合にアプリケーションを終亁E��るかどぁE��、E
        /// </summary>
        public bool ExitIfPlatformUnavailable { get; set; }
        public PlatformKind PreferredPlatform { get; set; }
    }
    public enum PlatformKind
    {
        Null,
        Steam,
        // Other platforms...
    }
    [DisallowMultipleComponent]
    public class PlatformMB : MonoBehaviour, IPlatformOptions
    {
        [SerializeField, Tooltip("Quit the game if Steam fails to initialize at startup.")]
        private bool _exitIfSteamUnavailable = true;
        [SerializeField] private PlatformKind _platformKind = PlatformKind.Steam;

        public bool ExitIfPlatformUnavailable
        {
            get => _exitIfSteamUnavailable;
            set => _exitIfSteamUnavailable = value;
        }
        public PlatformKind PreferredPlatform
        {
            get => _platformKind;
            set => _platformKind = value;
        }

        public void InstallPlatformRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            _ = owner ?? throw new ArgumentNullException(nameof(owner));

            builder.RegisterInstance<IPlatformOptions>(this);

            builder.Register<ISteamPlatformService, SteamPlatformService>(RuntimeLifetime.Singleton);

            // nullは確定で入れておく
            builder.Register<INullPlatformService, NullPlatformService>(RuntimeLifetime.Singleton);

            // 外部がIPlatformServiceを解決したときにPlatformServiceProxyを返すようにする - 最後�E登録が有効になめE
            builder.Register<PlatformServiceProxy>(RuntimeLifetime.Singleton)
                .As<IPlatformService>()
                .As<IAchievementPlatform>()
                .As<IRichPresencePlatform>()
                .As<ICloudSavePlatform>()      // 追加するなめE
                .As<IDisposable>();

        }
    }
}
