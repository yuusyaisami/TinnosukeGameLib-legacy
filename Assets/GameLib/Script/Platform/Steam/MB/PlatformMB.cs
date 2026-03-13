// スチームのプラットフォーム固有の実装
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
        /// 開発者が指定したプラットフォームでの起動に失敗した場合にアプリケーションを終了するかどうか。
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
    public class PlatformMB : MonoBehaviour, IFeatureInstaller, IPlatformOptions
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

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance<IPlatformOptions>(this);

            builder.Register<ISteamPlatformService, SteamPlatformService>(Lifetime.Singleton);

            // nullは確定で入れておく
            builder.Register<INullPlatformService, NullPlatformService>(Lifetime.Singleton);

            // 外部がIPlatformServiceを解決したときにPlatformServiceProxyを返すようにする - 最後の登録が有効になる
            builder.Register<PlatformServiceProxy>(Lifetime.Singleton)
                .As<IPlatformService>()
                .As<IAchievementPlatform>()
                .As<IRichPresencePlatform>()
                .As<ICloudSavePlatform>()      // 追加するなら
                .As<IDisposable>();

        }
    }
}
