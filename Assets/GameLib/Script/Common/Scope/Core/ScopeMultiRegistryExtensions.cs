using System;

namespace Game
{
    // ================================================================
    // ScopeMultiRegistryExtensions - VContainer 諡｡蠑ｵ繝｡繧ｽ繝・ラ
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // 隍・焚逋ｻ骭ｲ繧ｵ繝ｼ繝薙せ繧・IScopeMultiRegistry 縺ｫ閾ｪ蜍慕匳骭ｲ縺吶ｋ縺溘ａ縺ｮ
    // VContainer 諡｡蠑ｵ繝｡繧ｽ繝・ラ縲・
    //
    // ## 菴ｿ逕ｨ譁ｹ豕・
    //
    // ### RegisterAsScopeMulti - DI逋ｻ骭ｲ縺ｨ繝ｬ繧ｸ繧ｹ繝医Μ逋ｻ骭ｲ繧貞酔譎ゅ↓陦後≧
    //
    // ```csharp
    // builder.RegisterAsScopeMulti<IChannelHubService, AnimationSpriteHubService>(RuntimeLifetime.Singleton)
    //        .WithParameter(channels);
    // ```
    //
    // (removed) AddToScopeMulti 縺ｯ蟒・ｭ｢
    //
    // ### AddComponentToScopeMulti - 繧ｳ繝ｳ繝昴・繝阪Φ繝医ｒ繝ｬ繧ｸ繧ｹ繝医Μ縺ｫ霑ｽ蜉
    //
    // ```csharp
    // builder.AddComponentToScopeMulti<IMyService>(myComponent);
    // ```
    //
    // ================================================================

    /// <summary>
    /// IScopeMultiRegistry 逕ｨ縺ｮ VContainer 諡｡蠑ｵ繝｡繧ｽ繝・ラ縲・
    /// </summary>
    public static class ScopeMultiRegistryExtensions
    {
        // ----------------------------------------------------------------
        // RegisterAsScopeMulti - DI逋ｻ骭ｲ + 繝ｬ繧ｸ繧ｹ繝医Μ逋ｻ骭ｲ
        // ----------------------------------------------------------------

        /// <summary>
        /// TImpl 繧・DI 縺ｫ逋ｻ骭ｲ縺励▽縺､縲√ン繝ｫ繝牙ｾ後↓ IScopeMultiRegistry 縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// ## 驥崎ｦ・
        /// 
        /// 蜀・Κ縺ｧ AsSelf() 繧貞他縺ｳ蜃ｺ縺吶◆繧√ゝImpl 縺ｧ逶ｴ謗･ Resolve 蜿ｯ閭ｽ縺ｫ縺ｪ繧九・
        /// 縺薙ｌ縺ｫ繧医ｊ BuildCallback 縺ｧ遒ｺ螳溘↓繝ｭ繝ｼ繧ｫ繝ｫ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ繧貞叙蠕励〒縺阪ｋ縲・
        /// </summary>
        /// <typeparam name="TService">繧ｵ繝ｼ繝薙せ縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ蝙・/typeparam>
        /// <typeparam name="TImpl">螳溯｣・け繝ｩ繧ｹ蝙・/typeparam>
        /// <param name="builder">IRuntimeContainerBuilder</param>
        /// <param name="RuntimeLifetime">繝ｩ繧､繝輔ち繧､繝</param>
        /// <returns>繝√ぉ繝ｼ繝ｳ逕ｨ縺ｮ IRuntimeRegistrationBuilder</returns>
        public static IRuntimeRegistrationBuilder RegisterAsScopeMulti<TService, TImpl>(
            this IRuntimeContainerBuilder builder,
            RuntimeLifetime RuntimeLifetime)
            where TService : class
            where TImpl : class, TService
        {
            var registration = builder.Register<TImpl>(RuntimeLifetime)
                .As<TService>()
                .AsSelf(); // 雜・㍾隕・ｼ壼・菴灘梛縺ｧ繝ｭ繝ｼ繧ｫ繝ｫ縺ｫ蠑輔￠繧九ｈ縺・↓縺吶ｋ

            builder.RegisterBuildCallback(resolver =>
            {
                var registry = resolver.Resolve<IScopeMultiRegistry>();
                var instance = resolver.Resolve<TImpl>(); // 繝ｭ繝ｼ繧ｫ繝ｫ繧堤｢ｺ螳溘↓蜿悶ｌ繧・
                registry.Add<TService>(instance);
            });

            return registration;
        }

        /// <summary>
        /// TImpl 繧・DI 縺ｫ逋ｻ骭ｲ縺励▽縺､縲∬､・焚縺ｮ繧ｵ繝ｼ繝薙せ蝙九→縺励※ IScopeMultiRegistry 縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// </summary>
        /// <typeparam name="TService1">繧ｵ繝ｼ繝薙せ縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ蝙・</typeparam>
        /// <typeparam name="TService2">繧ｵ繝ｼ繝薙せ縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ蝙・</typeparam>
        /// <typeparam name="TImpl">螳溯｣・け繝ｩ繧ｹ蝙・/typeparam>
        public static IRuntimeRegistrationBuilder RegisterAsScopeMulti<TService1, TService2, TImpl>(
            this IRuntimeContainerBuilder builder,
            RuntimeLifetime RuntimeLifetime)
            where TService1 : class
            where TService2 : class
            where TImpl : class, TService1, TService2
        {
            var registration = builder.Register<TImpl>(RuntimeLifetime)
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
        // AddComponentToScopeMulti - 繧ｳ繝ｳ繝昴・繝阪Φ繝育畑
        // ----------------------------------------------------------------

        /// <summary>
        /// 譌｢縺ｫ蟄伜惠縺吶ｋ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ・医さ繝ｳ繝昴・繝阪Φ繝育ｭ会ｼ峨ｒ繝ｬ繧ｸ繧ｹ繝医Μ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// </summary>
        /// <typeparam name="TService">繧ｵ繝ｼ繝薙せ縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ蝙・/typeparam>
        /// <param name="builder">IRuntimeContainerBuilder</param>
        /// <param name="instance">逋ｻ骭ｲ縺吶ｋ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ</param>
        public static void AddComponentToScopeMulti<TService>(
            this IRuntimeContainerBuilder builder,
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
        /// 譌｢縺ｫ蟄伜惠縺吶ｋ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ繧定､・焚縺ｮ繧ｵ繝ｼ繝薙せ蝙九→縺励※繝ｬ繧ｸ繧ｹ繝医Μ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// </summary>
        public static void AddComponentToScopeMulti<TService1, TService2>(
            this IRuntimeContainerBuilder builder,
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
