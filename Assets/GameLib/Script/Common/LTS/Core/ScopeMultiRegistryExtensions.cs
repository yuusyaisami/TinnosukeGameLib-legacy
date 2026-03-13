using System;
using VContainer;

namespace Game
{
    // ================================================================
    // ScopeMultiRegistryExtensions - VContainer 拡張メソッド
    // ================================================================
    //
    // ## 概要
    //
    // 複数登録サービスを IScopeMultiRegistry に自動登録するための
    // VContainer 拡張メソッド。
    //
    // ## 使用方法
    //
    // ### RegisterAsScopeMulti - DI登録とレジストリ登録を同時に行う
    //
    // ```csharp
    // builder.RegisterAsScopeMulti<IChannelHubService, AnimationSpriteHubService>(Lifetime.Singleton)
    //        .WithParameter(channels);
    // ```
    //
    // (removed) AddToScopeMulti は廃止
    //
    // ### AddComponentToScopeMulti - コンポーネントをレジストリに追加
    //
    // ```csharp
    // builder.AddComponentToScopeMulti<IMyService>(myComponent);
    // ```
    //
    // ================================================================

    /// <summary>
    /// IScopeMultiRegistry 用の VContainer 拡張メソッド。
    /// </summary>
    public static class ScopeMultiRegistryExtensions
    {
        // ----------------------------------------------------------------
        // RegisterAsScopeMulti - DI登録 + レジストリ登録
        // ----------------------------------------------------------------

        /// <summary>
        /// TImpl を DI に登録しつつ、ビルド後に IScopeMultiRegistry に登録する。
        /// 
        /// ## 重要
        /// 
        /// 内部で AsSelf() を呼び出すため、TImpl で直接 Resolve 可能になる。
        /// これにより BuildCallback で確実にローカルインスタンスを取得できる。
        /// </summary>
        /// <typeparam name="TService">サービスのインターフェース型</typeparam>
        /// <typeparam name="TImpl">実装クラス型</typeparam>
        /// <param name="builder">IContainerBuilder</param>
        /// <param name="lifetime">ライフタイム</param>
        /// <returns>チェーン用の RegistrationBuilder</returns>
        public static RegistrationBuilder RegisterAsScopeMulti<TService, TImpl>(
            this IContainerBuilder builder,
            Lifetime lifetime)
            where TService : class
            where TImpl : class, TService
        {
            var registration = builder.Register<TImpl>(lifetime)
                .As<TService>()
                .AsSelf(); // 超重要：具体型でローカルに引けるようにする

            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<IScopeMultiRegistry>();
                var instance = resolver.Resolve<TImpl>(); // ローカルを確実に取れる
                registry.Add<TService>(instance);
            });

            return registration;
        }

        /// <summary>
        /// TImpl を DI に登録しつつ、複数のサービス型として IScopeMultiRegistry に登録する。
        /// </summary>
        /// <typeparam name="TService1">サービスのインターフェース型1</typeparam>
        /// <typeparam name="TService2">サービスのインターフェース型2</typeparam>
        /// <typeparam name="TImpl">実装クラス型</typeparam>
        public static RegistrationBuilder RegisterAsScopeMulti<TService1, TService2, TImpl>(
            this IContainerBuilder builder,
            Lifetime lifetime)
            where TService1 : class
            where TService2 : class
            where TImpl : class, TService1, TService2
        {
            var registration = builder.Register<TImpl>(lifetime)
                .As<TService1>()
                .As<TService2>()
                .AsSelf();

            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<IScopeMultiRegistry>();
                var instance = resolver.Resolve<TImpl>();
                registry.Add<TService1>(instance);
                registry.Add<TService2>(instance);
            });

            return registration;
        }

        // ----------------------------------------------------------------
        // AddComponentToScopeMulti - コンポーネント用
        // ----------------------------------------------------------------

        /// <summary>
        /// 既に存在するインスタンス（コンポーネント等）をレジストリに登録する。
        /// </summary>
        /// <typeparam name="TService">サービスのインターフェース型</typeparam>
        /// <param name="builder">IContainerBuilder</param>
        /// <param name="instance">登録するインスタンス</param>
        public static void AddComponentToScopeMulti<TService>(
            this IContainerBuilder builder,
            TService instance)
            where TService : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<IScopeMultiRegistry>();
                registry.Add<TService>(instance);
            });
        }

        /// <summary>
        /// 既に存在するインスタンスを複数のサービス型としてレジストリに登録する。
        /// </summary>
        public static void AddComponentToScopeMulti<TService1, TService2>(
            this IContainerBuilder builder,
            object instance)
            where TService1 : class
            where TService2 : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<IScopeMultiRegistry>();
                registry.Add(typeof(TService1), instance);
                registry.Add(typeof(TService2), instance);
            });
        }
    }
}
